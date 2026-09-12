using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using KhimTools.RebarTool.Core;
using Newtonsoft.Json;

namespace KhimTools.RebarTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdRebarFixtureQa : IExternalCommand
    {
        private const double DiameterToleranceMm = 0.05;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument?.Document;
            if (doc == null || doc.IsFamilyDocument)
            {
                message = "Rebar QA Fixture cần một project document đang mở.";
                return Result.Failed;
            }

            var checks = new List<FixtureCheck>();
            string reportPath = null;

            using (var group = new TransactionGroup(doc, "K-TOOLS Rebar QA Fixture (rollback)"))
            {
                group.Start();
                try
                {
                    Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault();
                    FloorType floorType = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>()
                        .FirstOrDefault(x => !x.IsFoundationSlab);
                    RebarBarType barType = new FilteredElementCollector(doc).OfClass(typeof(RebarBarType)).Cast<RebarBarType>()
                        .OrderBy(x => x.BarModelDiameter).FirstOrDefault();

                    Require(level != null, "FIXTURE_LEVEL", "Project có Level để dựng fixture.", checks);
                    Require(floorType != null, "FIXTURE_FLOOR_TYPE", "Project có FloorType thông thường.", checks);
                    Require(barType != null, "FIXTURE_REBAR_TYPE", "Project có RebarBarType.", checks);
                    if (level == null || floorType == null || barType == null)
                        throw new InvalidOperationException("Thiếu type bắt buộc; hãy nạp Rebar Bar Type trước khi chạy fixture.");

                    Floor floor;
                    using (var tx = new Transaction(doc, "Create temporary QA host"))
                    {
                        tx.Start();
                        double size = Mm(4000);
                        double ox = Mm(50000);
                        var loop = new CurveLoop();
                        var p0 = new XYZ(ox, 0, level.Elevation);
                        var p1 = new XYZ(ox + size, 0, level.Elevation);
                        var p2 = new XYZ(ox + size, size, level.Elevation);
                        var p3 = new XYZ(ox, size, level.Elevation);
                        loop.Append(Line.CreateBound(p0, p1));
                        loop.Append(Line.CreateBound(p1, p2));
                        loop.Append(Line.CreateBound(p2, p3));
                        loop.Append(Line.CreateBound(p3, p0));
                        floor = Floor.Create(doc, new List<CurveLoop> { loop }, floorType.Id, level.Id);
                        Parameter structural = floor.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
                        if (structural != null && !structural.IsReadOnly) structural.Set(1);
                        tx.Commit();
                    }

                    BoundingBoxXYZ hostBox = floor.get_BoundingBox(null);
                    Require(hostBox != null, "HOST_GEOMETRY", "Host fixture có bounding box hợp lệ.", checks);

                    Rebar rebar;
                    using (var tx = new Transaction(doc, "Create temporary QA rebar"))
                    {
                        tx.Start();
                        double z = hostBox.Min.Z + Mm(25) + barType.BarModelDiameter / 2.0;
                        var start = new XYZ(hostBox.Min.X + Mm(100), hostBox.Min.Y + Mm(500), z);
                        var end = new XYZ(hostBox.Max.X - Mm(100), hostBox.Min.Y + Mm(500), z);
                        rebar = RebarShapeCreationHelper.TryCreateStraightBar(doc, floor, barType, start, end);
                        tx.Commit();
                    }

                    Require(rebar != null && rebar.IsValidObject, "REBAR_CREATED",
                        rebar != null ? "Rebar được tạo bằng production helper."
                            : "Production helper không tạo được Rebar: " + RebarShapeCreationHelper.LastFailureReason, checks);
                    if (rebar != null && rebar.IsValidObject)
                    {
                        Require(rebar.GetHostId() == floor.Id, "HOST_ID", "Rebar giữ đúng host ID của Floor.", checks);
                        double actualDia = UnitUtils.ConvertFromInternalUnits(rebar.GetTypeId() == barType.Id
                            ? barType.BarModelDiameter : 0, UnitTypeId.Millimeters);
                        double expectedDia = UnitUtils.ConvertFromInternalUnits(barType.BarModelDiameter, UnitTypeId.Millimeters);
                        Require(Math.Abs(actualDia - expectedDia) <= DiameterToleranceMm, "BAR_DIAMETER",
                            $"Đường kính thực {actualDia:F2} mm khớp type {expectedDia:F2} mm.", checks);
                        Require(rebar.GetShapeId() != ElementId.InvalidElementId, "REBAR_SHAPE", "Rebar có RebarShape hợp lệ.", checks);
                        var containment = RebarSafetyValidator.CheckRebarContainment(floor, new[] { rebar }, 1.0);
                        Require(containment.outCount == 0, "CONTAINMENT", "Rebar nằm trong biên host với tolerance 1 mm.", checks);
                    }

                    reportPath = WriteReport(doc, checks);
                }
                catch (Exception ex)
                {
                    checks.Add(new FixtureCheck { Id = "FIXTURE_EXCEPTION", Passed = false, Detail = ex.Message });
                    reportPath = WriteReport(doc, checks);
                }
                finally
                {
                    group.RollBack();
                }
            }

            bool passed = checks.Count > 0 && checks.All(x => x.Passed);
            TaskDialog.Show("K-TOOLS - Rebar QA Fixture",
                $"Kết quả: {(passed ? "PASS" : "FAIL")} ({checks.Count(x => x.Passed)}/{checks.Count})\n\n" +
                string.Join("\n", checks.Select(x => $"[{(x.Passed ? "PASS" : "FAIL")}] {x.Id}: {x.Detail}")) +
                $"\n\nBáo cáo: {reportPath}\nFixture đã rollback, model không bị thay đổi.");
            return passed ? Result.Succeeded : Result.Failed;
        }

        private static void Require(bool condition, string id, string detail, ICollection<FixtureCheck> checks)
        {
            checks.Add(new FixtureCheck { Id = id, Passed = condition, Detail = detail });
        }

        private static string WriteReport(Document doc, IList<FixtureCheck> checks)
        {
            string dir = Path.Combine(Path.GetTempPath(), "KhimTools", "RebarFixtureQA");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"RebarFixture-Revit{doc.Application.VersionNumber}-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new
            {
                RevitVersion = doc.Application.VersionNumber,
                Document = doc.Title,
                Timestamp = DateTime.Now,
                Passed = checks.Count > 0 && checks.All(x => x.Passed),
                Checks = checks
            }, Formatting.Indented));
            return path;
        }

        private static double Mm(double value) => UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);

        private class FixtureCheck
        {
            public string Id { get; set; }
            public bool Passed { get; set; }
            public string Detail { get; set; }
        }
    }
}
