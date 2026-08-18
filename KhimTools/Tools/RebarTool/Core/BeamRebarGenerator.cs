using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    public enum BeamEndCondition
    {
        Column,
        BeamIntersection,
        Cantilever,
        Unsupported
    }

    public class BeamRebarInput
    {
        public FamilyInstance Beam { get; set; }
        public RebarBarType MainTopBarType { get; set; }
        public RebarBarType MainBottomBarType { get; set; }
        public RebarBarType StirrupBarType { get; set; }
        public RebarBarType SideBarType { get; set; }

        public int TopContinuousQty { get; set; } = 2;
        public int BottomContinuousQty { get; set; } = 2;

        public int TopLeftExtraQty { get; set; } = 1;
        public RebarBarType TopLeftExtraBarType { get; set; }
        public int TopRightExtraQty { get; set; } = 1;
        public RebarBarType TopRightExtraBarType { get; set; }
        public int BottomMidExtraQty { get; set; } = 1;
        public RebarBarType BottomMidExtraBarType { get; set; }

        public bool AutoSideBars { get; set; } = true;
        public int SideBarQty { get; set; } = 2;
        /// <summary>
        /// TCVN 5574:2018 Điều 10.3.5.4: Ngưỡng chiều cao dầm tự động bật thép sườn (mặc định 700mm).
        /// </summary>
        public double SideBarThresholdMm { get; set; } = 700.0;

        public int HangerStirrupQty { get; set; } = 3;
        public double HangerStirrupSpacingMm { get; set; } = 50.0;

        public double StirrupSpacingA1 { get; set; } = ToFeet(100);
        public double StirrupSpacingA2 { get; set; } = ToFeet(200);
        public double ZoneA1Length { get; set; } = 0; // If 0, defaults to L/4

        public double? CustomCoverFeet { get; set; }

        public DesignCode DesignStandard { get; set; } = DesignCode.TCVN5574_2018;
        public ConcreteGrade ConcreteGrade { get; set; } = ConcreteGrade.Auto;
        public SteelGrade SteelGrade { get; set; } = SteelGrade.Auto;

        public double LdMultiplier { get; set; } = 35;
        public double HookTailMultiplier { get; set; } = 12;

        private static double ToFeet(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }

    /// <summary>
    /// Sinh thép chủ (Top/Bottom/Side), thép tăng cường (Gối/Bụng) và thép đai A1/A2/A1 cho Dầm.
    /// </summary>
    public class BeamRebarGenerator
    {
        private readonly Document _doc;
        public BeamRebarGenerator(Document doc) => _doc = doc;

        public List<Rebar> Generate(BeamRebarInput input, RebarGenerationReport report = null)
        {
            if (input?.Beam == null) return new List<Rebar>();

            var profile = BeamGeometryHelper.GetBeamProfile(input.Beam);
            if (profile == null) return new List<Rebar>();

            var created = new List<Rebar>();

            double cover = input.CustomCoverFeet ?? RebarCoverHelper.GetFloorCover(input.Beam, RebarFace.Other);

            double stirrupDia = input.StirrupBarType.BarModelDiameter;
            double topMainDia = input.MainTopBarType.BarModelDiameter;
            double botMainDia = input.MainBottomBarType.BarModelDiameter;

            double halfB = profile.B / 2.0 - cover - stirrupDia / 2.0;
            double halfH = profile.H / 2.0 - cover - stirrupDia / 2.0;

            if (halfB <= 0 || halfH <= 0)
                throw new InvalidOperationException("Tiết diện dầm quá nhỏ so với lớp bảo vệ đã chọn.");

            // Đã loại bỏ kiểm tra cảnh báo hàm lượng thép an toàn kết cấu theo yêu cầu
            // 1. Thép chủ trên chạy suốt
            created.AddRange(CreateTopContinuousBars(input, profile, cover, stirrupDia, topMainDia));

            // 2. Thép chủ dưới chạy suốt
            created.AddRange(CreateBottomContinuousBars(input, profile, cover, stirrupDia, botMainDia));

            // 3. Thép tăng cường gối trái & gối phải (Top Extra)
            if (input.TopLeftExtraQty > 0)
                created.AddRange(CreateTopLeftExtraBars(input, profile, cover, stirrupDia, topMainDia));
            if (input.TopRightExtraQty > 0)
                created.AddRange(CreateTopRightExtraBars(input, profile, cover, stirrupDia, topMainDia));

            // 4. Thép tăng cường bụng (Bottom Mid Extra)
            if (input.BottomMidExtraQty > 0)
                created.AddRange(CreateBottomMidExtraBars(input, profile, cover, stirrupDia, botMainDia));

            // 5. Thép sườn dầm (Side/Skin Bars)
            double hMm = UnitUtils.ConvertFromInternalUnits(profile.H, UnitTypeId.Millimeters);
            if ((input.AutoSideBars && hMm >= input.SideBarThresholdMm) || input.SideBarQty > 0)
            {
                RebarBarType sideType = input.SideBarType ?? input.StirrupBarType;
                created.AddRange(CreateSideBars(input, profile, cover, stirrupDia, sideType));
            }

            // 6. Thép đai phân vùng A1 / A2 / A1
            created.AddRange(CreateBeamStirrups(input, profile, halfB, halfH));

            // 7. Thép đai treo chống giật dầm phụ giao dầm chính (Gap 7b)
            created.AddRange(CreateHangerStirrups(input, profile, halfB, halfH, report));

            report?.AddSuccess(created.Count);
            return created;
        }

        // ─── TOP CONTINUOUS ──────────────────────────────────────────────

        private List<Rebar> CreateTopContinuousBars(BeamRebarInput input,
            BeamGeometryHelper.BeamProfile profile, double cover, double stirrupDia, double mainDia)
        {
            var bars = new List<Rebar>();
            int qty = Math.Max(input.TopContinuousQty, 2);

            double yTop = profile.H / 2.0 - cover - stirrupDia - mainDia / 2.0;
            double halfB = profile.B / 2.0 - cover - stirrupDia - mainDia / 2.0;
            EndAnchorage startAnch = CalculateEndAnchorage(input, profile.StartPoint, profile.Direction, mainDia);
            EndAnchorage endAnch = CalculateEndAnchorage(input, profile.EndPoint, profile.Direction, mainDia);
            for (int i = 0; i < qty; i++)
            {
                double t = (double)i / (qty - 1);
                double x = -halfB + t * 2 * halfB;

                List<Curve> curves = BuildMainBarCurves(profile, x, yTop, startAnch, endAnch, true);

                try
                {
                    Rebar bar = Rebar.CreateFromCurves(
                        _doc,
                        RebarStyle.Standard,
                        input.MainTopBarType,
                        null,
                        null,
                        input.Beam,
                        profile.RightVector, // Normal of the vertical bending plane
                        curves,
                        RebarHookOrientation.Right,
                        RebarHookOrientation.Right,
                        true,
                        true);

                    if (bar != null)
                    {
                        bars.Add(bar);
                    }
                }
                catch
                {
                    // Fallback to straight bar if bend fails
                    XYZ start = BeamGeometryHelper.TransformLocalToWorld(profile, x, yTop, -startAnch.Extension);
                    XYZ end = BeamGeometryHelper.TransformLocalToWorld(profile, x, yTop, profile.Length + endAnch.Extension);
                    Rebar fallbackBar = RebarShapeCreationHelper.TryCreateStraightBar(_doc, input.Beam, input.MainTopBarType, start, end);
                    if (fallbackBar != null) bars.Add(fallbackBar);
                }
            }

            return bars;
        }

        // ─── BOTTOM CONTINUOUS ───────────────────────────────────────────

        private List<Rebar> CreateBottomContinuousBars(BeamRebarInput input,
            BeamGeometryHelper.BeamProfile profile, double cover, double stirrupDia, double mainDia)
        {
            var bars = new List<Rebar>();
            int qty = Math.Max(input.BottomContinuousQty, 2);

            double yBot = -profile.H / 2.0 + cover + stirrupDia + mainDia / 2.0;
            double halfB = profile.B / 2.0 - cover - stirrupDia - mainDia / 2.0;
            EndAnchorage startAnch = CalculateEndAnchorage(input, profile.StartPoint, profile.Direction, mainDia);
            EndAnchorage endAnch = CalculateEndAnchorage(input, profile.EndPoint, profile.Direction, mainDia);
            for (int i = 0; i < qty; i++)
            {
                double t = (double)i / (qty - 1);
                double x = -halfB + t * 2 * halfB;

                List<Curve> curves = BuildMainBarCurves(profile, x, yBot, startAnch, endAnch, false);

                try
                {
                    Rebar bar = Rebar.CreateFromCurves(
                        _doc,
                        RebarStyle.Standard,
                        input.MainBottomBarType,
                        null,
                        null,
                        input.Beam,
                        profile.RightVector,
                        curves,
                        RebarHookOrientation.Right,
                        RebarHookOrientation.Right,
                        true,
                        true);

                    if (bar != null)
                    {
                        bars.Add(bar);
                    }
                }
                catch
                {
                    XYZ start = BeamGeometryHelper.TransformLocalToWorld(profile, x, yBot, -startAnch.Extension);
                    XYZ end = BeamGeometryHelper.TransformLocalToWorld(profile, x, yBot, profile.Length + endAnch.Extension);
                    Rebar fallbackBar = RebarShapeCreationHelper.TryCreateStraightBar(_doc, input.Beam, input.MainBottomBarType, start, end);
                    if (fallbackBar != null) bars.Add(fallbackBar);
                }
            }

            return bars;
        }

        // ─── TOP EXTRA (LEFT & RIGHT) ───────────────────────────────────

        private List<Rebar> CreateTopLeftExtraBars(BeamRebarInput input,
            BeamGeometryHelper.BeamProfile profile, double cover, double stirrupDia, double mainDia)
        {
            var bars = new List<Rebar>();
            int qty = input.TopLeftExtraQty;
            double yTop = profile.H / 2.0 - cover - stirrupDia - mainDia / 2.0 - mainDia; // lớp 2

            double zStart = -ToFeet(300);
            double zEnd = profile.Length / 3.0; // cắt ở L/3

            double halfB = profile.B / 2.0 - cover - stirrupDia - mainDia / 2.0;
            double stepX = (qty > 1) ? (2 * halfB * 0.6) / (qty - 1) : 0;
            double startX = (qty > 1) ? -halfB * 0.6 : 0;

            RebarBarType barType = input.TopLeftExtraBarType ?? input.MainTopBarType;

            for (int i = 0; i < qty; i++)
            {
                double x = startX + i * stepX;
                XYZ start = BeamGeometryHelper.TransformLocalToWorld(profile, x, yTop, zStart);
                XYZ end = BeamGeometryHelper.TransformLocalToWorld(profile, x, yTop, zEnd);

                Rebar bar = RebarShapeCreationHelper.TryCreateStraightBar(_doc, input.Beam, barType, start, end);
                if (bar != null) bars.Add(bar);
            }

            return bars;
        }

        private List<Rebar> CreateTopRightExtraBars(BeamRebarInput input,
            BeamGeometryHelper.BeamProfile profile, double cover, double stirrupDia, double mainDia)
        {
            var bars = new List<Rebar>();
            int qty = input.TopRightExtraQty;
            double yTop = profile.H / 2.0 - cover - stirrupDia - mainDia / 2.0 - mainDia;

            double zStart = profile.Length * 2.0 / 3.0; // bắt đầu từ 2L/3
            double zEnd = profile.Length + ToFeet(300);

            double halfB = profile.B / 2.0 - cover - stirrupDia - mainDia / 2.0;
            double stepX = (qty > 1) ? (2 * halfB * 0.6) / (qty - 1) : 0;
            double startX = (qty > 1) ? -halfB * 0.6 : 0;

            RebarBarType barType = input.TopRightExtraBarType ?? input.MainTopBarType;

            for (int i = 0; i < qty; i++)
            {
                double x = startX + i * stepX;
                XYZ start = BeamGeometryHelper.TransformLocalToWorld(profile, x, yTop, zStart);
                XYZ end = BeamGeometryHelper.TransformLocalToWorld(profile, x, yTop, zEnd);

                Rebar bar = RebarShapeCreationHelper.TryCreateStraightBar(_doc, input.Beam, barType, start, end);
                if (bar != null) bars.Add(bar);
            }

            return bars;
        }

        // ─── BOTTOM MID EXTRA ───────────────────────────────────────────

        private List<Rebar> CreateBottomMidExtraBars(BeamRebarInput input,
            BeamGeometryHelper.BeamProfile profile, double cover, double stirrupDia, double mainDia)
        {
            var bars = new List<Rebar>();
            int qty = input.BottomMidExtraQty;
            double yBot = -profile.H / 2.0 + cover + stirrupDia + mainDia / 2.0 + mainDia;

            double zStart = profile.Length * 0.15; // cắt cách gối L/6
            double zEnd = profile.Length * 0.85;

            double halfB = profile.B / 2.0 - cover - stirrupDia - mainDia / 2.0;
            double stepX = (qty > 1) ? (2 * halfB * 0.6) / (qty - 1) : 0;
            double startX = (qty > 1) ? -halfB * 0.6 : 0;

            RebarBarType barType = input.BottomMidExtraBarType ?? input.MainBottomBarType;

            for (int i = 0; i < qty; i++)
            {
                double x = startX + i * stepX;
                XYZ start = BeamGeometryHelper.TransformLocalToWorld(profile, x, yBot, zStart);
                XYZ end = BeamGeometryHelper.TransformLocalToWorld(profile, x, yBot, zEnd);

                Rebar bar = RebarShapeCreationHelper.TryCreateStraightBar(_doc, input.Beam, barType, start, end);
                if (bar != null) bars.Add(bar);
            }

            return bars;
        }

        // ─── SIDE BARS ──────────────────────────────────────────────────

        private List<Rebar> CreateSideBars(BeamRebarInput input,
            BeamGeometryHelper.BeamProfile profile, double cover, double stirrupDia, RebarBarType sideType)
        {
            var bars = new List<Rebar>();
            int sidePairs = Math.Max(input.SideBarQty / 2, 1);
            double sideDia = sideType.BarModelDiameter;

            double halfB = profile.B / 2.0 - cover - stirrupDia - sideDia / 2.0;
            double usableH = profile.H - 2 * (cover + stirrupDia + sideDia);

            double zStart = 0;
            double zEnd = profile.Length;

            for (int i = 1; i <= sidePairs; i++)
            {
                double y = -profile.H / 2.0 + cover + stirrupDia + sideDia + i * (usableH / (sidePairs + 1));

                // Thanh trái
                XYZ startL = BeamGeometryHelper.TransformLocalToWorld(profile, -halfB, y, zStart);
                XYZ endL = BeamGeometryHelper.TransformLocalToWorld(profile, -halfB, y, zEnd);
                Rebar barL = RebarShapeCreationHelper.TryCreateStraightBar(_doc, input.Beam, sideType, startL, endL);
                if (barL != null) bars.Add(barL);

                // Thanh phải
                XYZ startR = BeamGeometryHelper.TransformLocalToWorld(profile, halfB, y, zStart);
                XYZ endR = BeamGeometryHelper.TransformLocalToWorld(profile, halfB, y, zEnd);
                Rebar barR = RebarShapeCreationHelper.TryCreateStraightBar(_doc, input.Beam, sideType, startR, endR);
                if (barR != null) bars.Add(barR);
            }

            return bars;
        }

        // ─── STIRRUPS ───────────────────────────────────────────────────

        private List<Rebar> CreateBeamStirrups(BeamRebarInput input,
            BeamGeometryHelper.BeamProfile profile, double halfB, double halfH)
        {
            var hoops = new List<Rebar>();

            double l1 = (input.ZoneA1Length > 0) ? input.ZoneA1Length : profile.Length / 4.0;
            double s1 = (input.StirrupSpacingA1 > 0) ? input.StirrupSpacingA1 : ToFeet(100);
            double s2 = (input.StirrupSpacingA2 > 0) ? input.StirrupSpacingA2 : ToFeet(200);

            // 1. Z positions for normal stirrups
            List<double> zList = CalculateBeamStirrupZ(profile.Length, l1, s1, s2);

            foreach (double z in zList)
            {
                Rebar hoop = CreateStirrupAtZ(input, profile, halfB, halfH, z);
                if (hoop != null) hoops.Add(hoop);
            }

            return hoops;
        }

        private List<Rebar> CreateHangerStirrups(BeamRebarInput input,
            BeamGeometryHelper.BeamProfile profile, double halfB, double halfH, RebarGenerationReport report = null)
        {
            var hoops = new List<Rebar>();

            // 2. Generate Hanger Stirrups (Thép Treo) at secondary beam intersections (Gap 7b)
            try
            {
                var interPts = FindIntersectingSecondaryBeams(input.Beam);
                int qty = Math.Max(input.HangerStirrupQty, 1);
                double spacingFeet = ToFeet(input.HangerStirrupSpacingMm > 0 ? input.HangerStirrupSpacingMm : 50.0);

                foreach (var pt in interPts)
                {
                    double zCenter = (pt - profile.StartPoint).DotProduct(profile.Direction);
                    if (zCenter > 0 && zCenter < profile.Length)
                    {
                        for (int i = 0; i < qty; i++)
                        {
                            double offset = (i - (qty - 1) / 2.0) * spacingFeet;
                            double hz = zCenter + offset;
                            if (hz > ToFeet(50) && hz < profile.Length - ToFeet(50))
                            {
                                Rebar hoop = CreateStirrupAtZ(input, profile, halfB, halfH, hz);
                                if (hoop != null) hoops.Add(hoop);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                report?.AddError(input.Beam, "Thép đai treo dầm (Hanger Stirrups)", ex);
            }

            return hoops;
        }

        private Rebar CreateStirrupAtZ(BeamRebarInput input, BeamGeometryHelper.BeamProfile profile, double halfB, double halfH, double z)
        {
            XYZ p1 = BeamGeometryHelper.TransformLocalToWorld(profile, halfB, halfH, z);
            XYZ p2 = BeamGeometryHelper.TransformLocalToWorld(profile, -halfB, halfH, z);
            XYZ p3 = BeamGeometryHelper.TransformLocalToWorld(profile, -halfB, -halfH, z);
            XYZ p4 = BeamGeometryHelper.TransformLocalToWorld(profile, halfB, -halfH, z);

            var loop = new List<Curve>
            {
                Line.CreateBound(p1, p2),
                Line.CreateBound(p2, p3),
                Line.CreateBound(p3, p4),
                Line.CreateBound(p4, p1)
            };

            RebarHookType hook135 = RebarHookHelper.GetHookType(_doc, 135, RebarStyle.StirrupTie);
            Rebar hoop = null;

            try
            {
                hoop = Rebar.CreateFromCurves(
                    _doc, RebarStyle.StirrupTie, input.StirrupBarType, hook135, hook135, input.Beam,
                    profile.Direction, loop, RebarHookOrientation.Right, RebarHookOrientation.Right, true, true);
            }
            catch
            {
                try
                {
                    hoop = Rebar.CreateFromCurves(
                        _doc, RebarStyle.StirrupTie, input.StirrupBarType, null, null, input.Beam,
                        profile.Direction, loop, RebarHookOrientation.Right, RebarHookOrientation.Right, true, true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BeamRebarGenerator] CreateStirrupAtZ failed: {ex.Message}");
                }
            }

            return hoop;
        }

        private static List<double> CalculateBeamStirrupZ(double totalLength, double l1, double s1, double s2)
        {
            var zList = new List<double>();
            double zLeftEnd = Math.Min(l1, totalLength / 2.0);
            double zRightStart = Math.Max(totalLength - l1, totalLength / 2.0);

            // Vùng gối trái A1
            for (double z = ToFeet(50); z <= zLeftEnd + 0.001; z += s1)
                zList.Add(z);

            // Vùng giữa A2
            double lastZ = zList.LastOrDefault();
            if (lastZ <= 0) lastZ = 0;

            for (double z = lastZ + s2; z < zRightStart - 0.001; z += s2)
                zList.Add(z);

            // Vùng gối phải A1
            for (double z = zRightStart; z <= totalLength - ToFeet(50); z += s1)
            {
                if (!zList.Any(existingZ => Math.Abs(existingZ - z) < 0.01))
                    zList.Add(z);
            }

            zList.Sort();
            return zList;
        }

        private FamilyInstance FindSupportingColumn(XYZ point)
        {
            var cols = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            foreach (var col in cols)
            {
                BoundingBoxXYZ bb = col.get_BoundingBox(null);
                if (bb == null) continue;

                // Tăng dung sai tìm kiếm cột lên 300mm (~1 foot) để bắt đúng cột lệch tâm nhẹ
                if (point.X >= bb.Min.X - ToFeet(300) && point.X <= bb.Max.X + ToFeet(300) &&
                    point.Y >= bb.Min.Y - ToFeet(300) && point.Y <= bb.Max.Y + ToFeet(300) &&
                    point.Z >= bb.Min.Z - ToFeet(500) && point.Z <= bb.Max.Z + ToFeet(500))
                {
                    return col;
                }
            }
            return null;
        }

        private static double GetColumnWidthAlongDirection(FamilyInstance col, XYZ direction)
        {
            BoundingBoxXYZ bb = col.get_BoundingBox(null);
            if (bb == null) return ToFeet(400); // fallback 400mm

            double dx = bb.Max.X - bb.Min.X;
            double dy = bb.Max.Y - bb.Min.Y;

            double absCos = Math.Abs(direction.X);
            double absSin = Math.Abs(direction.Y);

            return dx * absCos + dy * absSin;
        }

        private class EndAnchorage
        {
            public double Extension { get; set; } = ToFeet(300);
            public bool NeedsHook { get; set; } = false;
            public double HookLength { get; set; } = 0;
        }

        private EndAnchorage CalculateEndAnchorage(BeamRebarInput input, XYZ beamEndPt, XYZ direction, double barDia)
        {
            var anchorage = new EndAnchorage();

            FamilyInstance col;
            FamilyInstance supportingBeam;
            BeamEndCondition cond = DetermineEndCondition(input.Beam, beamEndPt, out col, out supportingBeam);

            double availLength = ToFeet(300); // Default straight extension
            double reqLd = RebarAnchorageCalculator.CalculateAnchorageLength(
                UnitUtils.ConvertFromInternalUnits(barDia, UnitTypeId.Millimeters),
                input.ConcreteGrade,
                input.SteelGrade,
                AnchorageType.TensionStraight,
                input.DesignStandard,
                input.LdMultiplier);
            reqLd = UnitUtils.ConvertToInternalUnits(reqLd, UnitTypeId.Millimeters);

            double reqHookLd = RebarAnchorageCalculator.CalculateAnchorageLength(
                UnitUtils.ConvertFromInternalUnits(barDia, UnitTypeId.Millimeters),
                input.ConcreteGrade,
                input.SteelGrade,
                AnchorageType.TensionHooked,
                input.DesignStandard,
                input.LdMultiplier);
            reqHookLd = UnitUtils.ConvertToInternalUnits(reqHookLd, UnitTypeId.Millimeters);

            if (cond == BeamEndCondition.Column && col != null)
            {
                double colDepth = GetColumnWidthAlongDirection(col, direction);
                double colCover = RebarCoverHelper.GetColumnCover(col, RebarFace.Exterior);
                availLength = Math.Max((colDepth / 2.0) - colCover, ToFeet(100));

                if (availLength >= reqLd)
                {
                    anchorage.Extension = availLength;
                    anchorage.NeedsHook = false;
                }
                else
                {
                    anchorage.Extension = availLength;
                    anchorage.NeedsHook = true;
                    anchorage.HookLength = Math.Max(reqHookLd - availLength, barDia * input.HookTailMultiplier);
                }
            }
            else if (cond == BeamEndCondition.BeamIntersection && supportingBeam != null)
            {
                // Secondary beam framing into primary beam
                // We obtain the primary beam section width
                var primProfile = BeamGeometryHelper.GetBeamProfile(supportingBeam);
                double primWidth = primProfile?.B ?? ToFeet(300);
                double primCover = RebarCoverHelper.GetColumnCover(supportingBeam, RebarFace.Exterior);
                availLength = Math.Max((primWidth / 2.0) - primCover, ToFeet(100));

                if (availLength >= reqLd)
                {
                    anchorage.Extension = availLength;
                    anchorage.NeedsHook = false;
                }
                else
                {
                    anchorage.Extension = availLength;
                    anchorage.NeedsHook = true;
                    anchorage.HookLength = Math.Max(reqHookLd - availLength, barDia * input.HookTailMultiplier);
                }
            }
            else
            {
                // Cantilever or unsupported: straight extension with cover clearance
                anchorage.Extension = ToFeet(100);
                anchorage.NeedsHook = false;
            }

            return anchorage;
        }

        private List<Curve> BuildMainBarCurves(BeamGeometryHelper.BeamProfile profile, double x, double y,
            EndAnchorage startAnch, EndAnchorage endAnch, bool isTop)
        {
            var curves = new List<Curve>();

            double zStart = -Math.Max(startAnch.Extension, 0);
            double zEnd = profile.Length + Math.Max(endAnch.Extension, 0);

            XYZ pStart = BeamGeometryHelper.TransformLocalToWorld(profile, x, y, zStart);
            XYZ pEnd = BeamGeometryHelper.TransformLocalToWorld(profile, x, y, zEnd);

            XYZ startCorner = pStart;
            XYZ endCorner = pEnd;

            // Start Hook (Left support)
            if (startAnch.NeedsHook && startAnch.HookLength > 0.01)
            {
                XYZ hookDir = isTop ? -profile.UpVector : profile.UpVector;
                XYZ hookStart = startCorner + startAnch.HookLength * hookDir;
                if (hookStart.DistanceTo(startCorner) > 0.01)
                {
                    curves.Add(Line.CreateBound(hookStart, startCorner));
                }
            }

            // Main Straight Segment
            curves.Add(Line.CreateBound(startCorner, endCorner));

            // End Hook (Right support)
            if (endAnch.NeedsHook && endAnch.HookLength > 0.01)
            {
                XYZ hookDir = isTop ? -profile.UpVector : profile.UpVector;
                XYZ hookEnd = endCorner + endAnch.HookLength * hookDir;
                if (endCorner.DistanceTo(hookEnd) > 0.01)
                {
                    curves.Add(Line.CreateBound(endCorner, hookEnd));
                }
            }

            return curves;
        }

        private FamilyInstance FindSupportingBeam(XYZ point, FamilyInstance currentBeam)
        {
            var beams = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            foreach (var bm in beams)
            {
                if (bm.Id == currentBeam.Id) continue;

                BoundingBoxXYZ bb = bm.get_BoundingBox(null);
                if (bb == null) continue;

                // Expand bounding box slightly for tolerance (approx. 350mm = 1.1 feet)
                XYZ min = bb.Min - new XYZ(1.1, 1.1, 1.1);
                XYZ max = bb.Max + new XYZ(1.1, 1.1, 1.1);

                if (point.X >= min.X && point.X <= max.X &&
                    point.Y >= min.Y && point.Y <= max.Y &&
                    point.Z >= min.Z && point.Z <= max.Z)
                {
                    return bm;
                }
            }
            return null;
        }

        private BeamEndCondition DetermineEndCondition(FamilyInstance beam, XYZ endPt, out FamilyInstance supportingCol, out FamilyInstance supportingBeam)
        {
            supportingCol = FindSupportingColumn(endPt);
            if (supportingCol != null)
            {
                supportingBeam = null;
                return BeamEndCondition.Column;
            }

            supportingBeam = FindSupportingBeam(endPt, beam);
            if (supportingBeam != null)
            {
                return BeamEndCondition.BeamIntersection;
            }

            supportingBeam = null;
            return BeamEndCondition.Unsupported;
        }

        private List<XYZ> FindIntersectingSecondaryBeams(FamilyInstance primaryBeam)
        {
            var intersectionPoints = new List<XYZ>();
            var beams = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            LocationCurve primLoc = primaryBeam.Location as LocationCurve;
            if (primLoc == null || primLoc.Curve == null) return intersectionPoints;

            Curve primCurve = primLoc.Curve;

            foreach (var bm in beams)
            {
                if (bm.Id == primaryBeam.Id) continue;

                LocationCurve secLoc = bm.Location as LocationCurve;
                if (secLoc == null || secLoc.Curve == null) continue;

                Curve secCurve = secLoc.Curve;

                IntersectionResultArray results;
                SetComparisonResult intersect = primCurve.Intersect(secCurve, out results);

                if (intersect == SetComparisonResult.Overlap && results != null)
                {
                    foreach (IntersectionResult r in results)
                    {
                        intersectionPoints.Add(r.XYZPoint);
                    }
                }
            }
            return intersectionPoints;
        }

        private static double ToFeet(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }
}
