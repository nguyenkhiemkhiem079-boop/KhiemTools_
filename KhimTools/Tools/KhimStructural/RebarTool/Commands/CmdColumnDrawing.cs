using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using KhimTools.Core;
using KhimTools.Core.Revit;
using KhimTools.RebarTool.Core;

namespace KhimTools.RebarTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CmdColumnDrawing : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                List<FamilyInstance> columns = GetSelectedOrPickColumns(uidoc, doc);
                if (!columns.Any())
                {
                    TaskDialog.Show("Column Drawing", "Chưa chọn cột nào.");
                    return Result.Cancelled;
                }

                int created = 0, skipped = 0;
                var drawingGen = new ColumnRebarDrawingGenerator(doc);
                var sectionGen = new ColumnRebarSectionViewGenerator(doc);
                var view3DGen = new ColumnRebar3DViewGenerator(doc);

                var generatedViewIds = new List<ElementId>();
                created = TransactionBoundary.ExecuteGroup(doc, "Create Column Drawings", () =>
                {
                    int createdInTransaction = TransactionBoundary.Execute(doc, "Create Column Drawings", () =>
                    {
                        int generated = 0;
                        foreach (var col in columns)
                        {
                            var summary = ExistingRebarReader.ReadFromColumn(doc, col);
                            if (!summary.HasData)
                            {
                                skipped++;
                                continue;
                            }

                            string mark = col.LookupParameter("Mark")?.AsString() ?? col.Id.ToLongValue().ToString();
                            double coverFeet = RebarCoverHelper.GetColumnCover(col, RebarFace.Exterior);
                            double coverMm = UnitUtils.ConvertFromInternalUnits(coverFeet, UnitTypeId.Millimeters);

                            ColumnRebarDrawingInput input;

                            if (IsCircular(col))
                            {
                                var profile = CircularColumnGeometryHelper.GetCircularProfile(col);
                                input = new ColumnRebarDrawingInput
                                {
                                    Shape = ColumnShapeType.Circular,
                                    ColumnMark = mark,
                                    ColumnDiameterMm = UnitUtils.ConvertFromInternalUnits(profile.Diameter, UnitTypeId.Millimeters),
                                    MainBarQty = summary.MainBarQty,
                                    MainBarLabel = summary.MainBarLabel,
                                    StirrupLabel = summary.StirrupLabel,
                                    StirrupSpacingMm = summary.StirrupSpacingMm > 0 ? summary.StirrupSpacingMm : 150,
                                    CoverMm = coverMm
                                };
                            }
                            else
                            {
                                var profile = RectangularColumnGeometryHelper.GetRectangularProfile(col);
                                double bMm = UnitUtils.ConvertFromInternalUnits(profile.B, UnitTypeId.Millimeters);
                                double hMm = UnitUtils.ConvertFromInternalUnits(profile.H, UnitTypeId.Millimeters);

                                var (barsB, barsH) = EstimateBarsPerSide(summary.MainBarQty, bMm, hMm);

                                input = new ColumnRebarDrawingInput
                                {
                                    Shape = ColumnShapeType.Rectangular,
                                    ColumnMark = mark,
                                    ColumnWidthMm = bMm,
                                    ColumnHeightMm = hMm,
                                    BarsAlongB = barsB,
                                    BarsAlongH = barsH,
                                    MainBarLabel = summary.MainBarLabel,
                                    StirrupLabel = summary.StirrupLabel,
                                    StirrupSpacingMm = summary.StirrupSpacingMm > 0 ? summary.StirrupSpacingMm : 150,
                                    CoverMm = coverMm
                                };
                            }

                            ViewDrafting drawingView = drawingGen.CreateOrUpdate(input);
                            List<Rebar> hostedRebars = HostedRebarQuery.GetHostedRebar(doc, col);
                            ViewPlan sectionView = sectionGen.CreateOrUpdate(col, hostedRebars);
                            View3D inspectionView = view3DGen.CreateOrUpdate(col, hostedRebars);
                            Element[] views = { drawingView, sectionView, inspectionView };
                            if (views.Any(view => view == null || !view.IsValidObject || view.Document != doc))
                                throw new InvalidOperationException("Column drawing, section, and 3D view postconditions must all be satisfied.");
                            generatedViewIds.AddRange(views.Select(view => view.Id));
                            generated++;
                        }
                        return generated;
                    });

                    if (generatedViewIds.Count != createdInTransaction * 3)
                        throw new InvalidOperationException("Column drawing view count did not match processed columns.");
                    if (generatedViewIds.Any(id =>
                    {
                        Element view = doc.GetElement(id);
                        return view == null || !view.IsValidObject;
                    }))
                        throw new InvalidOperationException("A generated column drawing view was not present after transaction commit.");
                    return createdInTransaction;
                });

                TaskDialog.Show("Column Drawing",
                    $"Đã tạo/cập nhật {created} bản vẽ." + (skipped > 0 ? $" Bỏ qua {skipped} cột chưa có thép." : ""));

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                System.Diagnostics.Debug.WriteLine("[K-TOOLS][ColumnDrawing] " + ex);
                TaskDialog.Show("Column Drawing Error", "Column drawing creation failed. " + ex.Message);
                return Result.Failed;
            }
        }

        private static (int barsB, int barsH) EstimateBarsPerSide(int totalQty, double bMm, double hMm)
        {
            if (totalQty < 4 || bMm <= 0 || hMm <= 0) return (3, 3);

            double half = (totalQty + 4) / 2.0;
            double ratio = bMm / (bMm + hMm);
            int barsB = Math.Max(2, (int)Math.Round(half * ratio));
            int barsH = Math.Max(2, (int)Math.Round(half - barsB));
            return (barsB, barsH);
        }

        private static bool IsCircular(FamilyInstance col)
        {
            string typeName = col.Symbol?.Name?.ToLowerInvariant() ?? "";
            string famName = col.Symbol?.Family?.Name?.ToLowerInvariant() ?? "";
            return typeName.Contains("round") || typeName.Contains("circular") || typeName.Contains("tron")
                || famName.Contains("round") || famName.Contains("circular") || famName.Contains("tron");
        }

        private static List<FamilyInstance> GetSelectedOrPickColumns(UIDocument uidoc, Document doc)
        {
            var selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds.Any())
                return selectedIds.Select(id => doc.GetElement(id)).OfType<FamilyInstance>().ToList();

            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdUpdateColumnDrawing : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var inner = new CmdColumnDrawing();
                return inner.Execute(commandData, ref message, elements);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                System.Diagnostics.Debug.WriteLine("[K-TOOLS][UpdateColumnDrawing] " + ex);
                TaskDialog.Show("Update Column Drawing Error", "Column drawing update failed. " + ex.Message);
                return Result.Failed;
            }
        }
    }
}
