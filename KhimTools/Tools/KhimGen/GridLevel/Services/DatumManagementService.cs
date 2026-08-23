using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace KhimTools.GridLevel.Services
{
    /// <summary>
    /// Service xử lý các thao tác chuyên sâu với Hệ Lưới Trục (Grid) và Cao Độ Tầng (Level):
    /// Cắt 2D/3D, Trim theo mô hình, Bật/Tắt Bubble đầu bóng, Chuyển đổi 2D/3D Extents.
    /// </summary>
    public static class DatumManagementService
    {
        // ════════════════════════════════════════════════════════════════════════════════
        // 1. CHUYỂN ĐỔI EXTENTS (2D / 3D)
        // ════════════════════════════════════════════════════════════════════════════════

        public static int SetDatumExtent(Document doc, View view, BuiltInCategory category, bool is2D, ICollection<ElementId> selIds = null)
        {
            if (doc == null || view == null) return 0;

            var collector = new FilteredElementCollector(doc, view.Id)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .Cast<DatumPlane>();

            var targetItems = selIds != null && selIds.Any()
                ? collector.Where(d => selIds.Contains(d.Id)).ToList()
                : collector.ToList();

            if (!targetItems.Any()) return 0;

            var targetExt = is2D ? DatumExtentType.ViewSpecific : DatumExtentType.Model;
            int count = 0;

            using (var tx = new Transaction(doc, $"K-TOOLS - Chuyển {(category == BuiltInCategory.OST_Grids ? "Grid" : "Level")} {(is2D ? "2D" : "3D")}"))
            {
                tx.Start();
                foreach (var datum in targetItems)
                {
                    try
                    {
                        datum.SetDatumExtentType(DatumEnds.End0, view, targetExt);
                        datum.SetDatumExtentType(DatumEnds.End1, view, targetExt);
                        count++;
                    }
                    catch { }
                }
                tx.Commit();
            }

            return count;
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 2. TOGGLE ĐẦU BÓNG (BUBBLE)
        // ════════════════════════════════════════════════════════════════════════════════

        public static int ToggleDatumBubble(Document doc, View view, BuiltInCategory category, ICollection<ElementId> selIds = null)
        {
            if (doc == null || view == null) return 0;

            var collector = new FilteredElementCollector(doc, view.Id)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .Cast<DatumPlane>();

            var targetItems = selIds != null && selIds.Any()
                ? collector.Where(d => selIds.Contains(d.Id)).ToList()
                : collector.ToList();

            if (!targetItems.Any()) return 0;

            int count = 0;

            using (var tx = new Transaction(doc, $"K-TOOLS - Bật/Tắt {(category == BuiltInCategory.OST_Grids ? "Grid" : "Level")} Bubble"))
            {
                tx.Start();
                foreach (var datum in targetItems)
                {
                    try
                    {
                        bool isBubble0 = datum.IsBubbleVisibleInView(DatumEnds.End0, view);
                        bool isBubble1 = datum.IsBubbleVisibleInView(DatumEnds.End1, view);

                        if (isBubble0 && isBubble1)
                        {
                            // Đang hiện 2 đầu -> chuyển sang chỉ hiện End0
                            datum.ShowBubbleInView(DatumEnds.End0, view);
                            datum.HideBubbleInView(DatumEnds.End1, view);
                        }
                        else if (isBubble0 && !isBubble1)
                        {
                            // Đang hiện End0 -> chuyển sang hiện End1
                            datum.HideBubbleInView(DatumEnds.End0, view);
                            datum.ShowBubbleInView(DatumEnds.End1, view);
                        }
                        else if (!isBubble0 && isBubble1)
                        {
                            // Đang hiện End1 -> chuyển sang hiện cả 2 đầu
                            datum.ShowBubbleInView(DatumEnds.End0, view);
                            datum.ShowBubbleInView(DatumEnds.End1, view);
                        }
                        else
                        {
                            // Đang tắt cả 2 -> bật End0
                            datum.ShowBubbleInView(DatumEnds.End0, view);
                        }
                        count++;
                    }
                    catch { }
                }
                tx.Commit();
            }

            return count;
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 3. CẮT & TRIM GRID 2D THEO PHẠM VI VIEW / MODEL
        // ════════════════════════════════════════════════════════════════════════════════

        public static int TrimGrid2D(Document doc, View view, ICollection<ElementId> selIds = null)
        {
            if (doc == null || view == null) return 0;

            var grids = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Grids)
                .WhereElementIsNotElementType()
                .Cast<Grid>();

            var targetGrids = selIds != null && selIds.Any()
                ? grids.Where(g => selIds.Contains(g.Id)).ToList()
                : grids.ToList();

            if (!targetGrids.Any()) return 0;

            // Tính BoundingBox bao quanh các đối tượng hình học trong View
            BoundingBoxXYZ modelBounds = GetViewModelBoundingBox(doc, view);
            if (modelBounds == null && view.CropBoxActive)
            {
                modelBounds = view.CropBox;
            }

            if (modelBounds == null)
            {
                // Fallback: Chuyển 2D cho toàn bộ Grid
                return SetDatumExtent(doc, view, BuiltInCategory.OST_Grids, true, selIds);
            }

            double offsetFeet = 1500.0 / 304.8; // 1500mm offset
            double minX = modelBounds.Min.X - offsetFeet;
            double maxX = modelBounds.Max.X + offsetFeet;
            double minY = modelBounds.Min.Y - offsetFeet;
            double maxY = modelBounds.Max.Y + offsetFeet;

            int count = 0;

            using (var tx = new Transaction(doc, "K-TOOLS - Trim Grid 2D"))
            {
                tx.Start();
                foreach (var grid in targetGrids)
                {
                    try
                    {
                        // Chuyển sang 2D
                        grid.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.ViewSpecific);
                        grid.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.ViewSpecific);

                        var curves = grid.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                        if (curves != null && curves.Count > 0 && curves[0] is Line line)
                        {
                            XYZ p0 = line.GetEndPoint(0);
                            XYZ p1 = line.GetEndPoint(1);
                            XYZ dir = (p1 - p0).Normalize();

                            // Xác định hướng trục: Dọc (theo Y) hay Ngang (theo X)
                            if (Math.Abs(dir.Y) > Math.Abs(dir.X))
                            {
                                // Trục dọc: cố định X, co giãn Y
                                double fixedX = (p0.X + p1.X) * 0.5;
                                double startY = Math.Min(p0.Y, p1.Y) < (minY + maxY) * 0.5 ? minY : maxY;
                                double endY = Math.Max(p0.Y, p1.Y) > (minY + maxY) * 0.5 ? maxY : minY;

                                XYZ newP0 = new XYZ(fixedX, startY, p0.Z);
                                XYZ newP1 = new XYZ(fixedX, endY, p1.Z);
                                if (newP0.DistanceTo(newP1) > 0.1)
                                {
                                    Line newLine = Line.CreateBound(newP0, newP1);
                                    grid.SetCurveInView(DatumExtentType.ViewSpecific, view, newLine);
                                }
                            }
                            else
                            {
                                // Trục ngang: cố định Y, co giãn X
                                double fixedY = (p0.Y + p1.Y) * 0.5;
                                double startX = Math.Min(p0.X, p1.X) < (minX + maxX) * 0.5 ? minX : maxX;
                                double endX = Math.Max(p0.X, p1.X) > (minX + maxX) * 0.5 ? maxX : minX;

                                XYZ newP0 = new XYZ(startX, fixedY, p0.Z);
                                XYZ newP1 = new XYZ(endX, fixedY, p1.Z);
                                if (newP0.DistanceTo(newP1) > 0.1)
                                {
                                    Line newLine = Line.CreateBound(newP0, newP1);
                                    grid.SetCurveInView(DatumExtentType.ViewSpecific, view, newLine);
                                }
                            }
                        }
                        count++;
                    }
                    catch { }
                }
                tx.Commit();
            }

            return count;
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 4. CẮT GRID 3D
        // ════════════════════════════════════════════════════════════════════════════════

        public static int CutGrid3D(Document doc, View view)
        {
            if (doc == null) return 0;

            var grids = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Grids)
                .WhereElementIsNotElementType()
                .Cast<Grid>()
                .ToList();

            if (!grids.Any()) return 0;

            int count = 0;
            using (var tx = new Transaction(doc, "K-TOOLS - Cắt Grid 3D Extents"))
            {
                tx.Start();
                foreach (var g in grids)
                {
                    try
                    {
                        if (view != null)
                        {
                            g.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.Model);
                            g.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.Model);
                        }
                        count++;
                    }
                    catch { }
                }
                tx.Commit();
            }

            return count;
        }

        // ════════════════════════════════════════════════════════════════════════════════
        // 5. CẮT LEVEL (RESIZE LEVEL EXTENTS)
        // ════════════════════════════════════════════════════════════════════════════════

        public static int CutLevel(Document doc, View view, ICollection<ElementId> selIds = null)
        {
            if (doc == null || view == null) return 0;

            var levels = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Levels)
                .WhereElementIsNotElementType()
                .Cast<Level>();

            var targetLevels = selIds != null && selIds.Any()
                ? levels.Where(l => selIds.Contains(l.Id)).ToList()
                : levels.ToList();

            if (!targetLevels.Any()) return 0;

            BoundingBoxXYZ modelBounds = GetViewModelBoundingBox(doc, view);
            if (modelBounds == null && view.CropBoxActive)
            {
                modelBounds = view.CropBox;
            }

            double offsetFeet = 2000.0 / 304.8; // 2000mm offset
            double minX = (modelBounds != null) ? modelBounds.Min.X - offsetFeet : -50.0;
            double maxX = (modelBounds != null) ? modelBounds.Max.X + offsetFeet : 50.0;

            int count = 0;
            using (var tx = new Transaction(doc, "K-TOOLS - Cắt Level Extents"))
            {
                tx.Start();
                foreach (var lvl in targetLevels)
                {
                    try
                    {
                        lvl.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.ViewSpecific);
                        lvl.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.ViewSpecific);

                        var curves = lvl.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                        if (curves != null && curves.Count > 0 && curves[0] is Line line)
                        {
                            XYZ p0 = line.GetEndPoint(0);
                            XYZ p1 = line.GetEndPoint(1);
                            double y = (p0.Y + p1.Y) * 0.5;
                            double z = (p0.Z + p1.Z) * 0.5;

                            Line newLine = Line.CreateBound(new XYZ(minX, y, z), new XYZ(maxX, y, z));
                            lvl.SetCurveInView(DatumExtentType.ViewSpecific, view, newLine);
                        }
                        count++;
                    }
                    catch { }
                }
                tx.Commit();
            }

            return count;
        }

        private static BoundingBoxXYZ GetViewModelBoundingBox(Document doc, View view)
        {
            try
            {
                var cats = new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_Walls,
                    BuiltInCategory.OST_Floors,
                    BuiltInCategory.OST_StructuralColumns,
                    BuiltInCategory.OST_Columns,
                    BuiltInCategory.OST_StructuralFraming,
                    BuiltInCategory.OST_StructuralFoundation
                };

                var multiclassFilter = new ElementMulticategoryFilter(cats);
                var elems = new FilteredElementCollector(doc, view.Id)
                    .WherePasses(multiclassFilter)
                    .WhereElementIsNotElementType()
                    .ToElements();

                if (!elems.Any()) return null;

                double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
                bool hasBox = false;

                foreach (var el in elems)
                {
                    var bb = el.get_BoundingBox(view);
                    if (bb != null)
                    {
                        hasBox = true;
                        minX = Math.Min(minX, bb.Min.X);
                        minY = Math.Min(minY, bb.Min.Y);
                        minZ = Math.Min(minZ, bb.Min.Z);
                        maxX = Math.Max(maxX, bb.Max.X);
                        maxY = Math.Max(maxY, bb.Max.Y);
                        maxZ = Math.Max(maxZ, bb.Max.Z);
                    }
                }

                if (!hasBox) return null;

                var result = new BoundingBoxXYZ();
                result.Min = new XYZ(minX, minY, minZ);
                result.Max = new XYZ(maxX, maxY, maxZ);
                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}