using System;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Describes the intended transverse reinforcement topology for a rectangular column.
    /// MultiCellClosed is the normal column-detail topology: one outer closed tie plus
    /// left and right inner closed ties.  The legacy alternatives remain opt-in so a
    /// saved project can request them explicitly without becoming the default.
    /// </summary>
    public enum ColumnTieLayoutType
    {
        MultiCellClosed,
        OuterOnly,
        CrossTie,
        DiamondLegacy
    }

    /// <summary>Named vertical reinforcement zones for a rectangular column.</summary>
    public enum ColumnTieZoneType
    {
        BottomA1,
        MiddleA2,
        TopA1,
        JointCore
    }

    /// <summary>
    /// A deterministic set of tie stations.  StartZ and EndZ are the first and last
    /// actual stations owned by this zone, so adjoining zones never duplicate a tie.
    /// </summary>
    public sealed class ColumnTieZone
    {
        public double StartZ { get; set; }
        public double EndZ { get; set; }
        public double Spacing { get; set; }
        public ColumnTieZoneType ZoneType { get; set; }
        public int StationCount { get; set; }

        public bool IsSingleStation => StationCount <= 1;
    }

    /// <summary>Identifies each closed loop in a rectangular-column tie station.</summary>
    public enum ColumnTieLoopRole
    {
        OuterTie,
        InnerTieLeft,
        InnerTieRight
    }

    public class ColumnRebarSettings
    {
        public string Name { get; set; }
        public string DesignStandard { get; set; } = "TCVN";
        public string ConcreteGrade { get; set; } = "Auto";
        public string SteelGrade { get; set; } = "Auto";
        public string MainBarType { get; set; }
        public string StirrupBarType { get; set; }
        public int BarsAlongB { get; set; } = 7;
        public int BarsAlongH { get; set; } = 3;
        public double StirrupSpacingA1 { get; set; } = 100;
        public double StirrupSpacingA2 { get; set; } = 200;
        public double ZoneA1Length { get; set; } = 0;
        public bool IsCustomCover { get; set; } = false;
        public double CustomCover { get; set; } = 25;
        public double LapLengthMultiplier { get; set; } = 40;
        public bool EnableCrankedSplice { get; set; } = true;
        public bool HasTopAnchor { get; set; } = true;
        public bool IsFoundationColumn { get; set; } = false;
        public bool HasDowel { get; set; } = false;
        public bool StaggeredSplice { get; set; } = true;
        public ColumnTieLayoutType TieLayout { get; set; } = ColumnTieLayoutType.MultiCellClosed;
        public bool HasInnerDiamondStirrup { get; set; } = false;
        public bool HasCrossLinks { get; set; } = false;
    }

    public class BeamRebarSettings
    {
        public string Name { get; set; }
        public string DesignStandard { get; set; } = "TCVN";
        public string ConcreteGrade { get; set; } = "Auto";
        public string SteelGrade { get; set; } = "Auto";
        public string MainTopBarType { get; set; }
        public string MainBottomBarType { get; set; }
        public string StirrupBarType { get; set; }
        public string SideBarType { get; set; }
        public int TopContinuousQty { get; set; } = 2;
        public int BottomContinuousQty { get; set; } = 2;
        public int TopLeftExtraQty { get; set; } = 1;
        public string TopLeftExtraBarType { get; set; }
        public int TopRightExtraQty { get; set; } = 1;
        public string TopRightExtraBarType { get; set; }
        public int BottomMidExtraQty { get; set; } = 1;
        public string BottomMidExtraBarType { get; set; }
        public bool AutoSideBars { get; set; } = true;
        public int SideBarQty { get; set; } = 2;
        public double StirrupSpacingA1 { get; set; } = 100;
        public double StirrupSpacingA2 { get; set; } = 200;
        public double ZoneA1Length { get; set; } = 0;
        public bool IsCustomCover { get; set; } = false;
        public double CustomCover { get; set; } = 25;
        public double LdMultiplier { get; set; } = 35;
        public double HookTailMultiplier { get; set; } = 12;
    }
}
