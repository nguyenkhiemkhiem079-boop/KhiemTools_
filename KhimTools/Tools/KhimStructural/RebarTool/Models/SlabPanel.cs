using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Models
{
    public enum SlabPanelEdgeType
    {
        BeamSupport,    // Tựa lên dầm (sử dụng Beam Anchor A)
        SlabAdjacent,   // Giáp ô sàn khác (sử dụng Slab Anchor B)
        FreeEdge        // Cạnh biên tự do (bẻ móc biên/free edge cover)
    }

    /// <summary>
    /// Thông tin 1 cạnh của Panel sàn
    /// </summary>
    public class SlabPanelEdge
    {
        public int EdgeIndex { get; set; }
        public Curve EdgeCurve { get; set; }
        public SlabPanelEdgeType EdgeType { get; set; } = SlabPanelEdgeType.BeamSupport;
        public ElementId SupportingBeamId { get; set; }

        public bool SkipTopHat { get; set; } = false;      // Không bố trí thép mũ gối ở cạnh này
        public bool SkipBottomMesh { get; set; } = false;   // Bỏ qua mép dưới ở cạnh này
    }

    /// <summary>
    /// Cấu hình Lớp Thép (Dùng cho Bottom Layer hoặc Top Layer)
    /// </summary>
    public class SlabLayerSettings
    {
        public bool Enabled { get; set; } = true;
        public bool InvertLayer { get; set; } = false; // Đảo thứ tự lớp X/Y nằm ngoài

        public string DiaXLabel { get; set; } = "d10";
        public double SpacingXMm { get; set; } = 150;

        public string DiaYLabel { get; set; } = "d10";
        public double SpacingYMm { get; set; } = 150;

        public string ExtraParam { get; set; } = "";
    }

    /// <summary>
    /// Cấu hình Thép Mũ Gối (Hat / Reinforce)
    /// </summary>
    public class SlabHatSettings
    {
        public bool Enabled { get; set; } = true;

        public string DiaXLabel { get; set; } = "d10";
        public double SpacingXMm { get; set; } = 150;

        public string DiaYLabel { get; set; } = "d10";
        public double SpacingYMm { get; set; } = 150;

        public bool IsFullSpan { get; set; } = false; // Chạy suốt nhịp (Full Hat)
        public string HatFactor { get; set; } = "L/4"; // L/4, L/3, L/5

        public bool HookDownEdge { get; set; } = true;
        public double HookDownLenMm { get; set; } = 100;
    }

    /// <summary>
    /// Cấu hình Thép Phân Bố / Nhiệt Độ (Top Distribution Rebar)
    /// </summary>
    public class SlabDistributionRebarSettings
    {
        public bool Enabled { get; set; } = true;
        public string DiaLabel { get; set; } = "d8";
        public double SpacingMm { get; set; } = 200;
    }

    /// <summary>
    /// Cấu hình Con Kê / Chân Chó (Spacer / High Chair)
    /// </summary>
    public class SlabSpacerSettings
    {
        public bool Enabled { get; set; } = true;
        public string DiaLabel { get; set; } = "d10";
        public double StepXMm { get; set; } = 800;
        public double StepYMm { get; set; } = 800;
        public double HookLenMm { get; set; } = 100; // Chiều dài móc bẻ chân tiếp xúc ván khuôn
    }

    /// <summary>
    /// Cấu hình Chiều dài Neo theo loại cạnh
    /// </summary>
    public class SlabAnchorSettings
    {
        public double BeamAnchorAMm { get; set; } = 250; // Neo vào Dầm (Beam Anchor A)
        public double SlabAnchorBMm { get; set; } = 300; // Neo giáp Sàn khác (Slab Anchor B)
    }

    /// <summary>
    /// Cấu hình Dung sai & Ngưỡng nhịp
    /// </summary>
    public class SlabToleranceSettings
    {
        public double RoundingMm { get; set; } = 10;   // Làm tròn chiều dài thanh thép
        public double MinSpanMm { get; set; } = 1200;  // Ngưỡng nhịp tối thiểu chạy suốt
    }

    /// <summary>
    /// Cấu hình Tổng hợp cho 1 Panel Sàn
    /// </summary>
    public class SlabPanelRebarConfig
    {
        public SlabLayerSettings BottomLayer { get; set; } = new SlabLayerSettings { Enabled = true };
        public SlabLayerSettings TopLayer { get; set; } = new SlabLayerSettings { Enabled = false }; // Mặc định Top Layer full tắt, dùng Hat
        public SlabHatSettings HatReinforce { get; set; } = new SlabHatSettings { Enabled = true };
        public SlabDistributionRebarSettings TopDistribution { get; set; } = new SlabDistributionRebarSettings { Enabled = true };
        public SlabSpacerSettings Spacer { get; set; } = new SlabSpacerSettings { Enabled = true };
        public SlabAnchorSettings Anchors { get; set; } = new SlabAnchorSettings();
        public SlabToleranceSettings Tolerances { get; set; } = new SlabToleranceSettings();

        public SlabPanelRebarConfig Clone()
        {
            return new SlabPanelRebarConfig
            {
                BottomLayer = new SlabLayerSettings
                {
                    Enabled = BottomLayer.Enabled,
                    InvertLayer = BottomLayer.InvertLayer,
                    DiaXLabel = BottomLayer.DiaXLabel,
                    SpacingXMm = BottomLayer.SpacingXMm,
                    DiaYLabel = BottomLayer.DiaYLabel,
                    SpacingYMm = BottomLayer.SpacingYMm,
                    ExtraParam = BottomLayer.ExtraParam
                },
                TopLayer = new SlabLayerSettings
                {
                    Enabled = TopLayer.Enabled,
                    InvertLayer = TopLayer.InvertLayer,
                    DiaXLabel = TopLayer.DiaXLabel,
                    SpacingXMm = TopLayer.SpacingXMm,
                    DiaYLabel = TopLayer.DiaYLabel,
                    SpacingYMm = TopLayer.SpacingYMm,
                    ExtraParam = TopLayer.ExtraParam
                },
                HatReinforce = new SlabHatSettings
                {
                    Enabled = HatReinforce.Enabled,
                    DiaXLabel = HatReinforce.DiaXLabel,
                    SpacingXMm = HatReinforce.SpacingXMm,
                    DiaYLabel = HatReinforce.DiaYLabel,
                    SpacingYMm = HatReinforce.SpacingYMm,
                    IsFullSpan = HatReinforce.IsFullSpan,
                    HatFactor = HatReinforce.HatFactor,
                    HookDownEdge = HatReinforce.HookDownEdge,
                    HookDownLenMm = HatReinforce.HookDownLenMm
                },
                TopDistribution = new SlabDistributionRebarSettings
                {
                    Enabled = TopDistribution.Enabled,
                    DiaLabel = TopDistribution.DiaLabel,
                    SpacingMm = TopDistribution.SpacingMm
                },
                Spacer = new SlabSpacerSettings
                {
                    Enabled = Spacer.Enabled,
                    DiaLabel = Spacer.DiaLabel,
                    StepXMm = Spacer.StepXMm,
                    StepYMm = Spacer.StepYMm,
                    HookLenMm = Spacer.HookLenMm
                },
                Anchors = new SlabAnchorSettings
                {
                    BeamAnchorAMm = Anchors.BeamAnchorAMm,
                    SlabAnchorBMm = Anchors.SlabAnchorBMm
                },
                Tolerances = new SlabToleranceSettings
                {
                    RoundingMm = Tolerances.RoundingMm,
                    MinSpanMm = Tolerances.MinSpanMm
                }
            };
        }
    }

    /// <summary>
    /// Đại diện cho 1 Panel / Ô sàn độc lập trong công trình
    /// </summary>
    public class SlabPanel
    {
        public string PanelId { get; set; } = "P1";
        public ElementId HostFloorId { get; set; }
        public Floor HostFloor { get; set; }
        public string FloorName { get; set; } = "";
        public string LevelName { get; set; } = "";

        public CurveLoop Boundary { get; set; }
        public List<CurveLoop> Openings { get; set; } = new List<CurveLoop>();
        public List<SlabPanelEdge> Edges { get; set; } = new List<SlabPanelEdge>();

        public double WidthMm { get; set; }
        public double LengthMm { get; set; }
        public double ThicknessMm { get; set; }
        public double ThicknessFeet { get; set; }
        public double CoverTopFeet { get; set; }
        public double CoverBottomFeet { get; set; }

        public bool IsMerged { get; set; } = false;
        public List<string> MergedChildrenIds { get; set; } = new List<string>();

        public XYZ Origin { get; set; } = XYZ.Zero;
        public XYZ AxisU { get; set; } = XYZ.BasisX;
        public XYZ AxisV { get; set; } = XYZ.BasisY;
        public double LocalMinU { get; set; }
        public double LocalMaxU { get; set; }
        public double LocalMinV { get; set; }
        public double LocalMaxV { get; set; }

        public SlabPanelRebarConfig Config { get; set; } = new SlabPanelRebarConfig();
        public bool IsSelected { get; set; } = true;
    }
}
