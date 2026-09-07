using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

namespace KhimTools.Architectural.QuickArchi.Services
{
    /// <summary>
    /// Service thực thi tạo nhanh các phần tử kiến trúc (Tường, Phòng) trong Revit.
    /// </summary>
    public static class QuickArchiService
    {
        public static List<WallType> GetWallTypes(Document doc)
        {
            if (doc == null) return new List<WallType>();

            return new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .Where(wt => wt.Kind != WallKind.Curtain) // Bỏ qua vách kính Curtain Wall
                .OrderBy(wt => wt.Name)
                .ToList();
        }

        public static List<Level> GetLevels(Document doc)
        {
            if (doc == null) return new List<Level>();

            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();
        }

        public static List<Curve> GetSelectedOrModelCurves(UIDocument uidoc)
        {
            var curves = new List<Curve>();
            if (uidoc == null) return curves;

            var doc = uidoc.Document;
            var selIds = uidoc.Selection.GetElementIds();

            foreach (var id in selIds)
            {
                var elem = doc.GetElement(id);
                if (elem is ModelCurve mc)
                {
                    curves.Add(mc.GeometryCurve);
                }
                else if (elem is DetailCurve dc)
                {
                    curves.Add(dc.GeometryCurve);
                }
            }

            return curves;
        }

        public static List<Wall> CreateWallsFromCurves(
            Document doc,
            List<Curve> curves,
            WallType wallType,
            Level level,
            double heightMm,
            double offsetMm,
            bool isStructural)
        {
            var createdWalls = new List<Wall>();
            if (doc == null || curves == null || wallType == null || level == null)
                return createdWalls;

            double heightFt = heightMm / 304.8;
            double offsetFt = offsetMm / 304.8;

            using (var tx = new Transaction(doc, "K-TOOLS — Quick Archi Walls"))
            {
                tx.Start();

                foreach (var curve in curves)
                {
                    try
                    {
                        Wall wall = Wall.Create(doc, curve, wallType.Id, level.Id, heightFt, offsetFt, false, isStructural);
                        if (wall != null)
                        {
                            createdWalls.Add(wall);
                        }
                    }
                    catch
                    {
                        // Tiếp tục với các đoạn tường tiếp theo
                    }
                }

                tx.Commit();
            }

            return createdWalls;
        }

        public static int CreateRoomsAndTags(Document doc, ViewPlan planView)
        {
            if (doc == null || planView == null || planView.GenLevel == null) return 0;

            int count = 0;
            using (var tx = new Transaction(doc, "K-TOOLS — Quick Archi Rooms"))
            {
                tx.Start();

                var topo = doc.Create.NewRooms2(planView.GenLevel);
                if (topo != null)
                {
                    count = topo.Count;
                }

                tx.Commit();
            }

            return count;
        }
    }
}
