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
            bool rollbackConfirmed = false;

            using (var group = new TransactionGroup(doc, "K-TOOLS Rebar QA Fixture (rollback)"))
            {
                try
                {
                    if (group.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("Could not start the Rebar QA fixture transaction group.");

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

                    RunRectangularColumnStationQa(commandData.Application.ActiveUIDocument, checks);

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
                        TransactionStatus commitStatus = tx.Commit();
                        if (commitStatus != TransactionStatus.Committed)
                        {
                            throw new InvalidOperationException(
                                $"Không thể commit temporary QA host. Trạng thái transaction: {commitStatus}.");
                        }
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
                        TransactionStatus commitStatus = tx.Commit();
                        if (commitStatus != TransactionStatus.Committed)
                        {
                            throw new InvalidOperationException(
                                $"Không thể commit temporary QA rebar. Trạng thái transaction: {commitStatus}.");
                        }
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
                    try
                    {
                        TransactionStatus status = group.GetStatus();
                        if (status == TransactionStatus.Started) status = group.RollBack();
                        rollbackConfirmed = status == TransactionStatus.RolledBack;
                    }
                    catch (Exception rollbackException)
                    {
                        checks.Add(new FixtureCheck
                        {
                            Id = "FIXTURE_ROLLBACK",
                            Passed = false,
                            Detail = "TransactionGroup rollback failed: " + rollbackException.Message
                        });
                    }
                    if (!rollbackConfirmed && !checks.Any(x => x.Id == "FIXTURE_ROLLBACK"))
                    {
                        checks.Add(new FixtureCheck
                        {
                            Id = "FIXTURE_ROLLBACK",
                            Passed = false,
                            Detail = "TransactionGroup rollback was not confirmed; inspect the QA model for fixture changes."
                        });
                    }
                }
            }

            reportPath = WriteReport(doc, checks);
            bool passed = checks.Count > 0 && checks.All(x => x.Passed);
            TaskDialog.Show("K-TOOLS - Rebar QA Fixture",
                $"Kết quả: {(passed ? "PASS" : "FAIL")} ({checks.Count(x => x.Passed)}/{checks.Count})\n\n" +
                string.Join("\n", checks.Select(x => $"[{(x.Passed ? "PASS" : "FAIL")}] {x.Id}: {x.Detail}")) +
                $"\n\nBáo cáo: {reportPath}\n" +
                (rollbackConfirmed
                    ? "Fixture rollback đã được xác nhận."
                    : "CẢNH BÁO: Không xác nhận được rollback. Hãy kiểm tra model QA trước khi tiếp tục."));
            return passed ? Result.Succeeded : Result.Failed;
        }

        private static void Require(bool condition, string id, string detail, ICollection<FixtureCheck> checks)
        {
            checks.Add(new FixtureCheck { Id = id, Passed = condition, Detail = detail });
        }

        /// <summary>
        /// Revit 2025 acceptance fixture for the rectangular-column tie topology.
        /// The selected structural column is used as the known C1 host, three ties
        /// are created at base + 500 mm, validated after regeneration, and then
        /// removed with the enclosing fixture TransactionGroup rollback.
        /// </summary>
        private static void RunRectangularColumnStationQa(UIDocument uidoc, ICollection<FixtureCheck> checks)
        {
            Document doc = uidoc?.Document;
            FamilyInstance column = uidoc?.Selection?.GetElementIds()
                .Select(id => doc.GetElement(id) as FamilyInstance)
                .FirstOrDefault(x => x != null && x.Category != null &&
                    x.Category.Id.Value == (long)BuiltInCategory.OST_StructuralColumns);
            if (column == null)
            {
                Require(false, "COLUMN_FIXTURE_SELECTION", "Chọn một cột chữ nhật C1 trước khi chạy fixture để kiểm tra 3 đai đa ô.", checks);
                return;
            }
            if (CmdColumnRebar.IsCircular(column))
            {
                Require(false, "COLUMN_FIXTURE_SELECTION", "Cột đã chọn là cột tròn; chọn cột chữ nhật C1 để chạy fixture.", checks);
                return;
            }

            RectangularColumnGeometryHelper.ColumnProfile profile;
            try
            {
                profile = RectangularColumnGeometryHelper.GetRectangularProfile(column);
            }
            catch (Exception ex)
            {
                Require(false, "COLUMN_FIXTURE_PROFILE", "Không đọc được tiết diện cột đã chọn: " + ex.Message, checks);
                return;
            }

            bool profileOk = profile.B > 0 && profile.H > 0 && profile.Height > 0;
            Require(profileOk, "COLUMN_FIXTURE_PROFILE", "Cột đã chọn có tiết diện chữ nhật và chiều cao hợp lệ.", checks);
            if (!profileOk) return;
            double profileBMm = UnitUtils.ConvertFromInternalUnits(profile.B, UnitTypeId.Millimeters);
            double profileHMm = UnitUtils.ConvertFromInternalUnits(profile.H, UnitTypeId.Millimeters);
            string mark = column.LookupParameter("Mark")?.AsString();
            bool knownC1 = string.Equals(mark, "C1", StringComparison.OrdinalIgnoreCase) ||
                (Math.Abs(profileBMm - 1000.0) <= 2.0 && Math.Abs(profileHMm - 350.0) <= 2.0);
            Require(knownC1, "COLUMN_FIXTURE_HOST", "Fixture dùng cột C1 (1000 x 350 mm) hoặc cột đang được đánh dấu C1.", checks);
            if (!knownC1) return;

            RebarBarType mainBar = FindBarType(doc, 32.0);
            RebarBarType stirrupBar = FindBarType(doc, 12.0);
            Require(mainBar != null, "COLUMN_FIXTURE_MAIN_BAR", "Đã nạp RebarBarType gần N32 cho fixture C1.", checks);
            Require(stirrupBar != null, "COLUMN_FIXTURE_STIRRUP_BAR", "Đã nạp RebarBarType gần N12 cho fixture C1.", checks);
            if (mainBar == null || stirrupBar == null) return;

            var input = new RectangularColumnRebarInput
            {
                Column = column,
                MainBarType = mainBar,
                StirrupBarType = stirrupBar,
                BarsAlongB = 7,
                BarsAlongH = 3,
                TieLayout = ColumnTieLayoutType.MultiCellClosed,
                HasInnerDiamondStirrup = false,
                HasCrossLinks = false
            };
            double stationZ = profile.BaseCenter.Z + Mm(500);
            var report = new RebarGenerationReport();
            var failurePreprocessor = new RebarGenerationFailurePreprocessor();
            var transactionChecks = new List<FixtureCheck>();
            using (var tx = new Transaction(doc, "K-TOOLS rectangular column tie QA"))
            {
                tx.Start();
                FailureHandlingOptions options = tx.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(failurePreprocessor);
                options.SetClearAfterRollback(true);
                tx.SetFailureHandlingOptions(options);
                try
                {
                    List<Rebar> ties = new RectangularColumnRebarGenerator(doc)
                        .CreateSingleTieStation(input, stationZ, report);
                    doc.Regenerate();
                    bool countOk = ties != null && ties.Count == 3;
                    Require(countOk, "COLUMN_FIXTURE_TIE_COUNT",
                        countOk ? "Đã tạo đúng 3 đai: outer + inner trái + inner phải tại base + 500 mm."
                            : "Fixture tạo " + (ties == null ? 0 : ties.Count) + " đai thay vì 3.", transactionChecks);

                    bool valid = countOk;
                    if (valid)
                    {
                        foreach (Rebar tie in ties)
                        {
                            string failure;
                            if (!RebarShapeCreationHelper.TryValidateCreatedRebar(
                                doc, tie, column, RebarStyle.StirrupTie, true,
                                "Column fixture tie", out failure))
                            {
                                valid = false;
                                report.AddError(column, "Column fixture tie", new InvalidOperationException(failure));
                                break;
                            }
                        }
                    }
                    Require(valid, "COLUMN_FIXTURE_TIE_VALIDATION",
                        valid ? "Tất cả 3 đai có host, RebarShape StirrupTie, accessor và containment hợp lệ."
                            : "Một hoặc nhiều đai không vượt qua kiểm tra shape/host/containment.", transactionChecks);

                    TransactionStatus status = tx.Commit();
                    bool committed = status == TransactionStatus.Committed && !failurePreprocessor.HasUnrecoverableFailure;
                    Require(committed, "COLUMN_FIXTURE_TRANSACTION",
                        committed ? "Transaction QA đã commit trước khi TransactionGroup rollback."
                            : "Transaction QA bị rollback: " + failurePreprocessor.Summary, checks);
                    if (committed)
                    {
                        foreach (FixtureCheck transactionCheck in transactionChecks)
                        {
                            checks.Add(transactionCheck);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Require(false, "COLUMN_FIXTURE_EXCEPTION", ex.Message, checks);
                    if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
                }
            }
        }

        private static RebarBarType FindBarType(Document doc, double diameterMm)
        {
            RebarBarType candidate = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .OrderBy(x => Math.Abs(UnitUtils.ConvertFromInternalUnits(x.BarModelDiameter, UnitTypeId.Millimeters) - diameterMm))
                .FirstOrDefault();
            if (candidate == null) return null;
            double actual = UnitUtils.ConvertFromInternalUnits(candidate.BarModelDiameter, UnitTypeId.Millimeters);
            return Math.Abs(actual - diameterMm) <= 0.5 ? candidate : null;
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
