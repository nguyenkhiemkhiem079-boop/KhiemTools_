using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using KhimTools.GridLevel.Models;

namespace KhimTools.GridLevel.Services
{
    public static class GridGeneratorService
    {
        /// <summary>
        /// Phân tích chuỗi khoảng cách (VD: "6000, 7200, 3x6000, 4500") thành danh sách các giá trị double (mm).
        /// </summary>
        public static List<double> ParseSpacings(string input)
        {
            var result = new List<double>();
            if (string.IsNullOrWhiteSpace(input)) return result;

            string[] tokens = input.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string token in tokens)
            {
                string t = token.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(t)) continue;

                // Match patterns like "3x6000" or "4*5000"
                var matchMultiplier = Regex.Match(t, @"^(\d+)\s*[xX\*]\s*([\d\.]+)$");
                if (matchMultiplier.Success)
                {
                    if (int.TryParse(matchMultiplier.Groups[1].Value, out int count) &&
                        double.TryParse(matchMultiplier.Groups[2].Value, out double dist))
                    {
                        for (int i = 0; i < count; i++) result.Add(dist);
                        continue;
                    }
                }

                if (double.TryParse(t, out double val) && val > 0)
                {
                    result.Add(val);
                }
            }

            return result;
        }

        /// <summary>
        /// Tạo hệ lưới trục (Grids) trong Revit.
        /// </summary>
        public static List<Grid> CreateGrids(Document doc, GridSettings settings, View activeView = null)
        {
            var createdGrids = new List<Grid>();
            if (doc == null || settings == null) return createdGrids;

            var xSpacings = ParseSpacings(settings.XSpacingsString);
            var ySpacings = ParseSpacings(settings.YSpacingsString);

            // Tính tổng kích thước lưới theo mm
            double totalWidthMm = xSpacings.Sum();
            double totalHeightMm = ySpacings.Sum();

            double totalWidthFt = UnitUtils.ConvertToInternalUnits(totalWidthMm, UnitTypeId.Millimeters);
            double totalHeightFt = UnitUtils.ConvertToInternalUnits(totalHeightMm, UnitTypeId.Millimeters);
            double xExtFt = UnitUtils.ConvertToInternalUnits(settings.XExtensionMm, UnitTypeId.Millimeters);
            double yExtFt = UnitUtils.ConvertToInternalUnits(settings.YExtensionMm, UnitTypeId.Millimeters);

            double angleRad = settings.RotationDegrees * Math.PI / 180.0;
            Transform rotTrans = Transform.CreateRotationAtPoint(XYZ.BasisZ, angleRad, settings.Origin);

            // ══════════════════════════════════════════════════════════════════
            // 1. TẠO CÁC TRỤC PHƯƠNG DỌC (Vertical Grids - Vị trí X thay đổi)
            // ══════════════════════════════════════════════════════════════════
            double currentXFt = 0;
            string currentXName = settings.XStartName;

            var xPositions = new List<double> { 0 };
            foreach (double sp in xSpacings)
            {
                currentXFt += UnitUtils.ConvertToInternalUnits(sp, UnitTypeId.Millimeters);
                xPositions.Add(currentXFt);
            }

            var xGridElements = new List<Grid>();

            for (int i = 0; i < xPositions.Count; i++)
            {
                double xPos = xPositions[i];
                XYZ p1 = settings.Origin + new XYZ(xPos, -yExtFt, 0);
                XYZ p2 = settings.Origin + new XYZ(xPos, totalHeightFt + yExtFt, 0);

                if (Math.Abs(angleRad) > 0.0001)
                {
                    p1 = rotTrans.OfPoint(p1);
                    p2 = rotTrans.OfPoint(p2);
                }

                Line gridLine = Line.CreateBound(p1, p2);
                Grid grid = Grid.Create(doc, gridLine);
                if (grid != null)
                {
                    SetGridNameSafely(doc, grid, currentXName);
                    ApplyBubbleVisibility(grid, settings.XShowBubbleEnd0, settings.XShowBubbleEnd1, activeView);
                    createdGrids.Add(grid);
                    xGridElements.Add(grid);
                }

                currentXName = GetNextName(currentXName);
            }

            // ══════════════════════════════════════════════════════════════════
            // 2. TẠO CÁC TRỤC PHƯƠNG NGANG (Horizontal Grids - Vị trí Y thay đổi)
            // ══════════════════════════════════════════════════════════════════
            double currentYFt = 0;
            string currentYName = settings.YStartName;

            var yPositions = new List<double> { 0 };
            foreach (double sp in ySpacings)
            {
                currentYFt += UnitUtils.ConvertToInternalUnits(sp, UnitTypeId.Millimeters);
                yPositions.Add(currentYFt);
            }

            var yGridElements = new List<Grid>();

            for (int j = 0; j < yPositions.Count; j++)
            {
                double yPos = yPositions[j];
                XYZ p1 = settings.Origin + new XYZ(-xExtFt, yPos, 0);
                XYZ p2 = settings.Origin + new XYZ(totalWidthFt + xExtFt, yPos, 0);

                if (Math.Abs(angleRad) > 0.0001)
                {
                    p1 = rotTrans.OfPoint(p1);
                    p2 = rotTrans.OfPoint(p2);
                }

                Line gridLine = Line.CreateBound(p1, p2);
                Grid grid = Grid.Create(doc, gridLine);
                if (grid != null)
                {
                    SetGridNameSafely(doc, grid, currentYName);
                    ApplyBubbleVisibility(grid, settings.YShowBubbleEnd0, settings.YShowBubbleEnd1, activeView);
                    createdGrids.Add(grid);
                    yGridElements.Add(grid);
                }

                currentYName = GetNextName(currentYName);
            }

            // ══════════════════════════════════════════════════════════════════
            // 3. TẠO DIMENSIONS LIÊN HOÀN (NẾU ĐƯỢC CHỌN VÀ CÓ ACTIVE VIEW)
            // ══════════════════════════════════════════════════════════════════
            if (settings.CreateDimensions && activeView != null && activeView.ViewType == ViewType.FloorPlan)
            {
                CreateGridDimensions(doc, activeView, xGridElements, yGridElements, settings, totalWidthFt, totalHeightFt, xExtFt, yExtFt);
            }

            return createdGrids;
        }

        private static void ApplyBubbleVisibility(Grid grid, bool showEnd0, bool showEnd1, View view)
        {
            if (grid == null) return;
            try
            {
                if (view != null)
                {
                    if (showEnd0) grid.ShowBubbleInView(DatumEnds.End0, view);
                    else grid.HideBubbleInView(DatumEnds.End0, view);

                    if (showEnd1) grid.ShowBubbleInView(DatumEnds.End1, view);
                    else grid.HideBubbleInView(DatumEnds.End1, view);
                }
            }
            catch { }
        }

        private static void CreateGridDimensions(Document doc, View view, List<Grid> xGrids, List<Grid> yGrids, GridSettings settings, double totalW, double totalH, double xExt, double yExt)
        {
            try
            {
                double angleRad = settings.RotationDegrees * Math.PI / 180.0;
                Transform rotTrans = Transform.CreateRotationAtPoint(XYZ.BasisZ, angleRad, settings.Origin);

                // 1. Dimension phương X (Đo các trục dọc X)
                if (xGrids.Count >= 2)
                {
                    var refArray = new ReferenceArray();
                    foreach (var g in xGrids)
                    {
                        refArray.Append(new Reference(g));
                    }

                    double dimY = -yExt * 0.5;
                    XYZ p1 = settings.Origin + new XYZ(0, dimY, 0);
                    XYZ p2 = settings.Origin + new XYZ(totalW, dimY, 0);

                    if (Math.Abs(angleRad) > 0.0001)
                    {
                        p1 = rotTrans.OfPoint(p1);
                        p2 = rotTrans.OfPoint(p2);
                    }

                    Line dimLine = Line.CreateBound(p1, p2);
                    doc.Create.NewDimension(view, dimLine, refArray);
                }

                // 2. Dimension phương Y (Đo các trục ngang Y)
                if (yGrids.Count >= 2)
                {
                    var refArray = new ReferenceArray();
                    foreach (var g in yGrids)
                    {
                        refArray.Append(new Reference(g));
                    }

                    double dimX = -xExt * 0.5;
                    XYZ p1 = settings.Origin + new XYZ(dimX, 0, 0);
                    XYZ p2 = settings.Origin + new XYZ(dimX, totalH, 0);

                    if (Math.Abs(angleRad) > 0.0001)
                    {
                        p1 = rotTrans.OfPoint(p1);
                        p2 = rotTrans.OfPoint(p2);
                    }

                    Line dimLine = Line.CreateBound(p1, p2);
                    doc.Create.NewDimension(view, dimLine, refArray);
                }
            }
            catch { }
        }

        public static string GetNextName(string currentName)
        {
            if (string.IsNullOrEmpty(currentName)) return "1";

            // Nếu là số: 1 -> 2 -> 3
            if (int.TryParse(currentName, out int num))
            {
                return (num + 1).ToString();
            }

            // Nếu là chữ cái: A -> B ... Z -> AA -> AB
            string upper = currentName.Trim().ToUpperInvariant();
            if (Regex.IsMatch(upper, @"^[A-Z]+$"))
            {
                char[] chars = upper.ToCharArray();
                int i = chars.Length - 1;
                while (i >= 0)
                {
                    if (chars[i] < 'Z')
                    {
                        chars[i]++;
                        return new string(chars);
                    }
                    chars[i] = 'A';
                    i--;
                }
                return "A" + new string(chars);
            }

            return currentName + "_1";
        }

        private static void SetGridNameSafely(Document doc, Grid grid, string desiredName)
        {
            if (grid == null) return;
            string finalName = desiredName;
            int counter = 1;

            while (IsGridNameExists(doc, finalName, grid.Id))
            {
                finalName = $"{desiredName}_{counter++}";
            }

            try
            {
                grid.Name = finalName;
            }
            catch { }
        }

        private static bool IsGridNameExists(Document doc, string name, ElementId excludeId)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .Any(g => g.Id != excludeId && g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
