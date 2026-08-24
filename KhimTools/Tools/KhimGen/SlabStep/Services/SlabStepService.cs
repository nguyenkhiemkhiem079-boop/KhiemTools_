using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.SlabStep.Models;

namespace KhimTools.SlabStep.Services
{
    public static class SlabStepService
    {
        /// <summary>
        /// Nạp Family từ file RFA bên ngoài vào dự án
        /// </summary>
        public static Family LoadStepFamily(Document doc, string rfaPath)
        {
            if (doc == null || string.IsNullOrEmpty(rfaPath) || !File.Exists(rfaPath))
                return null;

            Family family = null;
            using (var tx = new Transaction(doc, "K-TOOLS - Load Step Family"))
            {
                tx.Start();
                doc.LoadFamily(rfaPath, out family);
                tx.Commit();
            }
            return family;
        }

        /// <summary>
        /// Quét và lấy toàn bộ các Family Symbol liên quan đến nách sàn/giật cấp
        /// </summary>
        public static List<FamilySymbol> GetLoadedStepSymbols(Document doc)
        {
            var list = new List<FamilySymbol>();
            if (doc == null) return list;

            var symbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_GenericModel) // các family giật cấp thường thuộc Generic Model
                .Cast<FamilySymbol>()
                .OrderBy(s => s.Family.Name)
                .ThenBy(s => s.Name)
                .ToList();

            list.AddRange(symbols);
            return list;
        }

        /// <summary>
        /// Lấy toàn bộ tham số có kiểu là Double/Length để người dùng chọn map
        /// </summary>
        public static List<string> GetDoubleParameters(FamilySymbol symbol)
        {
            var list = new List<string>();
            if (symbol == null) return list;

            foreach (Parameter p in symbol.Parameters)
            {
                if (p.StorageType == StorageType.Double)
                {
                    list.Add(p.Definition.Name);
                }
            }

            // Quét thêm tham số của Instance mặc định từ family
            if (symbol.Family != null)
            {
                Document familyDoc = symbol.Document.EditFamily(symbol.Family);
                if (familyDoc != null)
                {
                    var familyManager = familyDoc.FamilyManager;
                    foreach (FamilyParameter fp in familyManager.Parameters)
                    {
                        if (fp.StorageType == StorageType.Double)
                        {
                            list.Add(fp.Definition.Name);
                        }
                    }
                    familyDoc.Close(false);
                }
            }

            return list.Distinct().OrderBy(s => s).ToList();
        }

        /// <summary>
        /// Lấy toàn bộ biên dạng đường lưới 2D của Sàn dựa vào Sketch hoặc Geometry
        /// </summary>
        public static List<Curve> GetFloorBoundaryCurves(Document doc, Floor floor)
        {
            var list = new List<Curve>();
            if (floor == null) return list;

            // 1. Thử lấy qua SketchId (Revit 2022+)
            try
            {
                var sketchId = floor.SketchId;
                if (sketchId != ElementId.InvalidElementId && doc.GetElement(sketchId) is Sketch sketch)
                {
                    foreach (CurveLoop loop in sketch.Profile)
                    {
                        foreach (Curve c in loop)
                        {
                            list.Add(c);
                        }
                    }
                    if (list.Any()) return list;
                }
            }
            catch { }

            // 2. Fallback: Lấy qua Geometry Solid
            var opt = new Options { DetailLevel = ViewDetailLevel.Fine };
            var geomElem = floor.get_Geometry(opt);
            if (geomElem != null)
            {
                foreach (var geomObj in geomElem)
                {
                    if (geomObj is Solid solid && solid.Volume > 0)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            // Tìm mặt trên cùng (Top Face) có normal hướng lên Z
                            if (face.ComputeNormal(new UV(0.5, 0.5)).IsAlmostEqualTo(XYZ.BasisZ))
                            {
                                foreach (CurveLoop loop in face.GetEdgesAsCurveLoops())
                                {
                                    foreach (Curve c in loop)
                                    {
                                        list.Add(c);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Tự động tìm kiếm các đoạn cạnh tiếp xúc gần nhau giữa Sàn Cao và Sàn Thấp
        /// </summary>
        public static List<Curve> AutoDetectBoundary(Document doc, Floor floorHigh, Floor floorLow, double toleranceMm)
        {
            var result = new List<Curve>();
            if (floorHigh == null || floorLow == null) return result;

            var curvesHigh = GetFloorBoundaryCurves(doc, floorHigh);
            var curvesLow = GetFloorBoundaryCurves(doc, floorLow);

            double toleranceFeet = toleranceMm / 304.8;

            foreach (var ch in curvesHigh)
            {
                // Chiếu phẳng ch xuống Z=0 để so sánh 2D
                XYZ startH = new XYZ(ch.GetEndPoint(0).X, ch.GetEndPoint(0).Y, 0);
                XYZ endH = new XYZ(ch.GetEndPoint(1).X, ch.GetEndPoint(1).Y, 0);
                Line lineH = Line.CreateBound(startH, endH);

                foreach (var cl in curvesLow)
                {
                    XYZ startL = new XYZ(cl.GetEndPoint(0).X, cl.GetEndPoint(0).Y, 0);
                    XYZ endL = new XYZ(cl.GetEndPoint(1).X, cl.GetEndPoint(1).Y, 0);
                    Line lineL = Line.CreateBound(startL, endL);

                    // Tính khoảng cách giữa 2 đoạn đường thẳng phẳng 2D
                    double dist = GetDistanceBetweenSegments2D(lineH, lineL);
                    if (dist <= toleranceFeet)
                    {
                        // Kiểm tra xem đoạn ch đã được thêm chưa để tránh trùng lặp
                        if (!result.Any(r => IsDuplicateCurve(r, ch)))
                        {
                            result.Add(ch);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Thực thi chèn nách sàn giật cấp dọc theo đường dẫn và gán tham số thủ công
        /// </summary>
        public static FamilyInstance GenerateSlabStep(Document doc, Curve boundaryCurve, FamilySymbol symbol, SlabStepSettings settings, double heightMm, double highThickMm, double lowThickMm)
        {
            if (doc == null || boundaryCurve == null || symbol == null || settings == null)
                return null;

            // Lấy Level của View hiện hành để làm Host chính
            Level level = doc.ActiveView.GenLevel;
            if (level == null)
            {
                level = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .FirstOrDefault();
            }
            if (level == null) return null;

            // Kích hoạt Symbol trước khi tạo
            if (!symbol.IsActive)
            {
                using (var t = new Transaction(doc, "K-TOOLS - Activate Symbol"))
                {
                    t.Start();
                    symbol.Activate();
                    t.Commit();
                }
            }

            // Quy đổi đơn vị mm sang feet (internal Revit units)
            double heightDiff = heightMm / 304.8;
            double thickHigh = highThickMm / 304.8;
            double thickLow = lowThickMm / 304.8;

            // Lấy điểm đầu cuối của cạnh ranh giới
            XYZ p1 = boundaryCurve.GetEndPoint(0);
            XYZ p2 = boundaryCurve.GetEndPoint(1);

            // Đưa cao độ điểm chèn về đúng cao độ của Level
            p1 = new XYZ(p1.X, p1.Y, level.Elevation);
            p2 = new XYZ(p2.X, p2.Y, level.Elevation);

            // Xác định hướng xoay dựa vào checkbox Reverse
            bool shouldSwap = settings.ReverseOrientation;
            XYZ startPt = shouldSwap ? p2 : p1;
            XYZ endPt = shouldSwap ? p1 : p2;

            FamilyInstance instance = null;

            using (var tx = new Transaction(doc, "K-TOOLS - Create Slab Step"))
            {
                tx.Start();

                // Tạo đối tượng chèn dạng Line-Based
                instance = doc.Create.NewFamilyInstance(startPt, symbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                
                if (instance.Location is LocationCurve locCurve)
                {
                    locCurve.Curve = Line.CreateBound(startPt, endPt);
                }

                // Gán tham số chiều cao giật cấp h
                if (!string.IsNullOrEmpty(settings.HeightParameterName))
                {
                    var pHeight = instance.LookupParameter(settings.HeightParameterName);
                    if (pHeight != null && !pHeight.IsReadOnly)
                    {
                        pHeight.Set(heightDiff);
                    }
                }

                // Gán tham số dày sàn cao (nếu có)
                if (!string.IsNullOrEmpty(settings.HighSlabThicknessParameter) && highThickMm > 0)
                {
                    var pThickHigh = instance.LookupParameter(settings.HighSlabThicknessParameter);
                    if (pThickHigh != null && !pThickHigh.IsReadOnly)
                    {
                        pThickHigh.Set(thickHigh);
                    }
                }

                // Gán tham số dày sàn thấp (nếu có)
                if (!string.IsNullOrEmpty(settings.LowSlabThicknessParameter) && lowThickMm > 0)
                {
                    var pThickLow = instance.LookupParameter(settings.LowSlabThicknessParameter);
                    if (pThickLow != null && !pThickLow.IsReadOnly)
                    {
                        pThickLow.Set(thickLow);
                    }
                }

                tx.Commit();
            }

            return instance;
        }

        #region PRIVATE GEOMETRIC HELPERS

        private static double GetFloorTopElevation(Floor floor)
        {
            var pOffset = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            double offset = (pOffset != null && pOffset.HasValue) ? pOffset.AsDouble() : 0.0;

            var level = floor.Document.GetElement(floor.LevelId) as Level;
            double levelElevation = (level != null) ? level.Elevation : 0.0;

            return levelElevation + offset;
        }

        private static bool DetermineIfNeedsSwap(Floor floorHigh, XYZ p1, XYZ p2)
        {
            // Vector chỉ hướng đoạn ranh giới phẳng 2D
            XYZ dir = new XYZ(p2.X - p1.X, p2.Y - p1.Y, 0).Normalize();
            XYZ normal = XYZ.BasisZ;
            
            // Hướng chỉ sang bên trái của đường đi (Cross Product)
            XYZ sideVec = dir.CrossProduct(normal).Normalize();

            // Lấy điểm test cách ranh giới 1 foot về phía bên trái
            XYZ mid = (p1 + p2) / 2.0;
            XYZ testPt = mid + sideVec * 1.0;

            // Kiểm tra xem testPt có nằm trong sàn cao không
            bool isInsideHigh = IsPointInsideFloor2D(floorHigh, testPt);

            // Mặc định nách sàn được thiết kế có mặt cao bên tay trái
            // Nếu sàn cao nằm bên tay phải (không nằm bên tay trái), ta cần đảo chiều
            return !isInsideHigh;
        }

        private static bool IsPointInsideFloor2D(Floor floor, XYZ pt)
        {
            var doc = floor.Document;
            var curves = GetFloorBoundaryCurves(doc, floor);
            if (!curves.Any()) return false;

            // Giải thuật Ray-Casting kiểm tra điểm trong đa giác phẳng Z=0
            int intersections = 0;
            XYZ rayEnd = new XYZ(pt.X + 10000.0, pt.Y + 1.234, 0); // Ray ngẫu nhiên nằm ngang dài 10000 ft

            foreach (var c in curves)
            {
                XYZ p1 = new XYZ(c.GetEndPoint(0).X, c.GetEndPoint(0).Y, 0);
                XYZ p2 = new XYZ(c.GetEndPoint(1).X, c.GetEndPoint(1).Y, 0);

                if (IsLineSegmentIntersection2D(pt, rayEnd, p1, p2))
                {
                    intersections++;
                }
            }

            return (intersections % 2 != 0);
        }

        private static bool IsLineSegmentIntersection2D(XYZ a1, XYZ a2, XYZ b1, XYZ b2)
        {
            double d = (a2.X - a1.X) * (b2.Y - b1.Y) - (a2.Y - a1.Y) * (b2.X - b1.X);
            if (Math.Abs(d) < 1e-9) return false; // Song song

            double u = ((b1.X - a1.X) * (b2.Y - b1.Y) - (b1.Y - a1.Y) * (b2.X - b1.X)) / d;
            double v = ((b1.X - a1.X) * (a2.Y - a1.Y) - (b1.Y - a1.Y) * (a2.X - a1.X)) / d;

            return (u >= 0 && u <= 1 && v >= 0 && v <= 1);
        }

        private static double GetDistanceBetweenSegments2D(Line l1, Line l2)
        {
            // Tính toán khoảng cách gần nhất giữa 2 phân đoạn thẳng phẳng 2D
            double d1 = l1.Distance(l2.GetEndPoint(0));
            double d2 = l1.Distance(l2.GetEndPoint(1));
            double d3 = l2.Distance(l1.GetEndPoint(0));
            double d4 = l2.Distance(l1.GetEndPoint(1));

            return Math.Min(Math.Min(d1, d2), Math.Min(d3, d4));
        }

        private static bool IsDuplicateCurve(Curve c1, Curve c2)
        {
            XYZ s1 = c1.GetEndPoint(0);
            XYZ e1 = c1.GetEndPoint(1);
            XYZ s2 = c2.GetEndPoint(0);
            XYZ e2 = c2.GetEndPoint(1);

            return (s1.IsAlmostEqualTo(s2) && e1.IsAlmostEqualTo(e2)) ||
                   (s1.IsAlmostEqualTo(e2) && e1.IsAlmostEqualTo(s2));
        }

        #endregion
    }
}
