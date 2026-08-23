using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Models
{
    /// <summary>
    /// Model chứa thông tin hình học & thuộc tính của Sàn (Floor) được trích xuất từ Revit.
    /// </summary>
    public class SlabProfile
    {
        public ElementId FloorId { get; set; }
        public Floor FloorElement { get; set; }
        public string FloorName { get; set; } = "";
        public string LevelName { get; set; } = "";
        public double ThicknessFeet { get; set; }
        public double ThicknessMm { get; set; }

        public double CoverTopFeet { get; set; }
        public double CoverBottomFeet { get; set; }

        public XYZ Normal { get; set; } = XYZ.BasisZ;
        public XYZ Origin { get; set; } = XYZ.Zero;

        public CurveLoop OuterBoundary { get; set; }
        public List<CurveLoop> InnerOpenings { get; set; } = new List<CurveLoop>();
        public List<FamilyInstance> SupportingBeams { get; set; } = new List<FamilyInstance>();
        public List<Element> SupportingWalls { get; set; } = new List<Element>();

        public BoundingBoxXYZ BoundingBox { get; set; }
        public double WidthMm { get; set; }
        public double LengthMm { get; set; }
    }
}
