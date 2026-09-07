using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Structural.QuickStructure.Models;

namespace KhimTools.Structural.QuickStructure.Services
{
    /// <summary>
    /// Service thực thi tạo tự động các cấu kiện kết cấu (Cột, Dầm, Móng) trong Revit.
    /// </summary>
    public static class QuickStructureService
    {
        public static List<Grid> GetAllGrids(Document doc)
        {
            if (doc == null) return new List<Grid>();
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .ToList();
        }

        public static List<XYZ> CalculateGridIntersections(List<Grid> grids)
        {
            var points2D = new List<Point2D>();
            if (grids == null || grids.Count < 2) return new List<XYZ>();

            for (int i = 0; i < grids.Count; i++)
            {
                Curve c1 = grids[i].Curve;
                if (c1 == null) continue;

                Point2D p1 = new Point2D(c1.GetEndPoint(0).X, c1.GetEndPoint(0).Y);
                Point2D p2 = new Point2D(c1.GetEndPoint(1).X, c1.GetEndPoint(1).Y);

                for (int j = i + 1; j < grids.Count; j++)
                {
                    Curve c2 = grids[j].Curve;
                    if (c2 == null) continue;

                    Point2D p3 = new Point2D(c2.GetEndPoint(0).X, c2.GetEndPoint(0).Y);
                    Point2D p4 = new Point2D(c2.GetEndPoint(1).X, c2.GetEndPoint(1).Y);

                    Point2D inter = GridIntersectionHelper.FindIntersection(p1, p2, p3, p4, true);
                    if (inter != null)
                    {
                        points2D.Add(inter);
                    }
                }
            }

            var dedup = GridIntersectionHelper.DeduplicatePoints(points2D, 1e-4);
            var result = new List<XYZ>();
            foreach (var pt in dedup)
            {
                result.Add(new XYZ(pt.X, pt.Y, 0));
            }

            return result;
        }

        public static List<FamilyInstance> CreateColumnsAtPoints(
            Document doc, 
            List<XYZ> points, 
            FamilySymbol columnSymbol, 
            Level baseLevel, 
            Level topLevel, 
            double baseOffsetMm, 
            double topOffsetMm)
        {
            var createdColumns = new List<FamilyInstance>();
            if (doc == null || points == null || columnSymbol == null || baseLevel == null)
                return createdColumns;

            // Kích hoạt Symbol trước khi tạo
            if (!columnSymbol.IsActive)
            {
                using (var t = new Transaction(doc, "K-TOOLS — Activate Column Symbol"))
                {
                    t.Start();
                    columnSymbol.Activate();
                    t.Commit();
                }
            }

            double baseOffsetFt = baseOffsetMm / 304.8;
            double topOffsetFt = topOffsetMm / 304.8;

            using (var tx = new Transaction(doc, "K-TOOLS — Quick Structure Columns"))
            {
                tx.Start();

                foreach (var pt in points)
                {
                    XYZ location = new XYZ(pt.X, pt.Y, baseLevel.Elevation);
                    FamilyInstance col = doc.Create.NewFamilyInstance(location, columnSymbol, baseLevel, StructuralType.Column);

                    if (col != null)
                    {
                        if (topLevel != null && topLevel.Id != baseLevel.Id)
                        {
                            var pTopLevel = col.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                            if (pTopLevel != null && !pTopLevel.IsReadOnly)
                            {
                                pTopLevel.Set(topLevel.Id);
                            }
                        }

                        if (Math.Abs(baseOffsetFt) > 1e-5)
                        {
                            var pBaseOffset = col.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
                            if (pBaseOffset != null && !pBaseOffset.IsReadOnly)
                            {
                                pBaseOffset.Set(baseOffsetFt);
                            }
                        }

                        if (Math.Abs(topOffsetFt) > 1e-5)
                        {
                            var pTopOffset = col.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                            if (pTopOffset != null && !pTopOffset.IsReadOnly)
                            {
                                pTopOffset.Set(topOffsetFt);
                            }
                        }

                        createdColumns.Add(col);
                    }
                }

                tx.Commit();
            }

            return createdColumns;
        }

        public static List<FamilyInstance> CreateBeamsAlongGridSpans(
            Document doc,
            List<Grid> grids,
            List<XYZ> nodePoints,
            FamilySymbol beamSymbol,
            Level level,
            double zOffsetMm)
        {
            var createdBeams = new List<FamilyInstance>();
            if (doc == null || grids == null || nodePoints == null || beamSymbol == null || level == null)
                return createdBeams;

            if (!beamSymbol.IsActive)
            {
                using (var t = new Transaction(doc, "K-TOOLS — Activate Beam Symbol"))
                {
                    t.Start();
                    beamSymbol.Activate();
                    t.Commit();
                }
            }

            double zOffsetFt = zOffsetMm / 304.8;

            using (var tx = new Transaction(doc, "K-TOOLS — Quick Structure Beams"))
            {
                tx.Start();

                foreach (var grid in grids)
                {
                    Curve gc = grid.Curve;
                    if (gc == null) continue;

                    // Lọc các điểm giao nằm trên đường lưới này
                    var ptsOnGrid = new List<XYZ>();
                    foreach (var pt in nodePoints)
                    {
                        XYZ flatPt = new XYZ(pt.X, pt.Y, gc.GetEndPoint(0).Z);
                        double dist = gc.Distance(flatPt);
                        if (dist < 0.05) // Sai số khoảng 15mm
                        {
                            ptsOnGrid.Add(flatPt);
                        }
                    }

                    // Sắp xếp các điểm theo chiều dọc theo đường curve
                    ptsOnGrid = ptsOnGrid
                        .OrderBy(p => gc.Project(p).Parameter)
                        .ToList();

                    // Nối dầm giữa các điểm liền kề
                    for (int k = 0; k < ptsOnGrid.Count - 1; k++)
                    {
                        XYZ start = ptsOnGrid[k];
                        XYZ end = ptsOnGrid[k + 1];

                        if (start.DistanceTo(end) < 0.5) continue; // Bỏ qua đoạn quá ngắn < 150mm

                        XYZ p1 = new XYZ(start.X, start.Y, level.Elevation + zOffsetFt);
                        XYZ p2 = new XYZ(end.X, end.Y, level.Elevation + zOffsetFt);

                        Line beamLine = Line.CreateBound(p1, p2);
                        FamilyInstance beam = doc.Create.NewFamilyInstance(beamLine, beamSymbol, level, StructuralType.Beam);
                        if (beam != null)
                        {
                            createdBeams.Add(beam);
                        }
                    }
                }

                tx.Commit();
            }

            return createdBeams;
        }

        public static List<FamilyInstance> CreateFootingsUnderColumns(
            Document doc,
            List<FamilyInstance> columns,
            FamilySymbol footingSymbol,
            Level level,
            double offsetMm)
        {
            var createdFootings = new List<FamilyInstance>();
            if (doc == null || columns == null || footingSymbol == null || level == null)
                return createdFootings;

            if (!footingSymbol.IsActive)
            {
                using (var t = new Transaction(doc, "K-TOOLS — Activate Footing Symbol"))
                {
                    t.Start();
                    footingSymbol.Activate();
                    t.Commit();
                }
            }

            double offsetFt = offsetMm / 304.8;

            using (var tx = new Transaction(doc, "K-TOOLS — Quick Structure Footings"))
            {
                tx.Start();

                foreach (var col in columns)
                {
                    LocationPoint loc = col.Location as LocationPoint;
                    if (loc == null) continue;

                    XYZ pt = new XYZ(loc.Point.X, loc.Point.Y, level.Elevation + offsetFt);
                    FamilyInstance fdn = doc.Create.NewFamilyInstance(pt, footingSymbol, level, StructuralType.Footing);
                    if (fdn != null)
                    {
                        createdFootings.Add(fdn);
                    }
                }

                tx.Commit();
            }

            return createdFootings;
        }
    }
}
