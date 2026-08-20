using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.GridLevel.Models
{
    public class GridSettings
    {
        public bool CreateGrids { get; set; } = true;

        // Trục Phương X (Các trục dọc, đặt tên 1, 2, 3... hoặc A, B, C...)
        public string XStartName { get; set; } = "1";
        public string XSpacingsString { get; set; } = "6000, 7200, 6000, 7200, 6000";
        public double XExtensionMm { get; set; } = 2000;
        public bool XShowBubbleEnd0 { get; set; } = true;
        public bool XShowBubbleEnd1 { get; set; } = false;

        // Trục Phương Y (Các trục ngang, đặt tên A, B, C... hoặc 1, 2, 3...)
        public string YStartName { get; set; } = "A";
        public string YSpacingsString { get; set; } = "5000, 4500, 5000";
        public double YExtensionMm { get; set; } = 2000;
        public bool YShowBubbleEnd0 { get; set; } = true;
        public bool YShowBubbleEnd1 { get; set; } = false;

        // Tọa độ & Góc xoay
        public XYZ Origin { get; set; } = XYZ.Zero;
        public double RotationDegrees { get; set; } = 0;

        // Kích thước Dimension
        public bool CreateDimensions { get; set; } = true;
    }

    public class LevelItem
    {
        public string LevelName { get; set; }
        public double ElevationMm { get; set; }
        public double StoryHeightMm { get; set; }
        public bool CreateFloorPlan { get; set; } = true;
        public bool CreateStructuralPlan { get; set; } = true;
        public bool CreateCeilingPlan { get; set; } = false;
    }

    public class ProjectSetupSettings
    {
        public GridSettings Grids { get; set; } = new GridSettings();
        public List<LevelItem> Levels { get; set; } = new List<LevelItem>();
        public bool CreateLevelsAndPlans { get; set; } = true;
    }
}
