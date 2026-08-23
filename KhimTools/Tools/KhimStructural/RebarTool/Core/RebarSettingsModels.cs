using System;

namespace KhimTools.RebarTool.Core
{
    public class ColumnRebarSettings
    {
        public string Name { get; set; }
        public string DesignStandard { get; set; } = "TCVN";
        public string ConcreteGrade { get; set; } = "Auto";
        public string SteelGrade { get; set; } = "Auto";
        public string MainBarType { get; set; }
        public string StirrupBarType { get; set; }
        public int BarsAlongB { get; set; } = 3;
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
        public bool HasInnerDiamondStirrup { get; set; } = true;
        public bool HasCrossLinks { get; set; } = true;
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
