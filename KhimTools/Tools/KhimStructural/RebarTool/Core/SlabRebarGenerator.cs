using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;
using KhimTools.RebarTool.Models;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Engine cốt lõi chịu trách nhiệm dựng toàn bộ 3D Rebar cho Sàn (Floor) theo từng Panel:
    /// 1. Thép Lưới Đáy (Bottom Layer - X & Y, có xét Invert Layer & Anchor A/B)
    /// 2. Thép Lưới Mặt Trên (Top Layer Full Mesh nếu bật)
    /// 3. Thép Mũ Gối (Support Hats theo L/4, L/3, có xét Skip Edge và Hook Down)
    /// 4. Thép Phân Bố Vuông Góc Mũ Gối (Top Distribution Rebar)
    /// 5. Thép Chân Chó / Con Kê (High Chairs / Spacers với Hook Length)
    /// 6. Thép Gia Cường Lỗ Mở (Opening Trim Bars)
    /// </summary>
    public class SlabRebarGenerator
    {
        private readonly Document _doc;

        public SlabRebarGenerator(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public List<Rebar> GeneratePanel(SlabPanel panel, RebarGenerationReport report = null)
        {
            if (panel == null || panel.HostFloor == null)
                return new List<Rebar>();

            var createdRebars = new List<Rebar>();

            var barTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .ToList();

            if (!barTypes.Any()) return createdRebars;

            var cfg = panel.Config;
            BoundingBoxXYZ bb = panel.HostFloor.get_BoundingBox(null);
            if (bb == null) return createdRebars;

            double coverTop = panel.CoverTopFeet > 0 ? panel.CoverTopFeet : ToFeet(25);
            double coverBot = panel.CoverBottomFeet > 0 ? panel.CoverBottomFeet : ToFeet(25);

            RebarBarType botXType = FindBarType(barTypes, cfg.BottomLayer.DiaXLabel);
            RebarBarType botYType = FindBarType(barTypes, cfg.BottomLayer.DiaYLabel);
            RebarBarType topMeshXType = FindBarType(barTypes, cfg.TopLayer.DiaXLabel);
            RebarBarType topMeshYType = FindBarType(barTypes, cfg.TopLayer.DiaYLabel);
            RebarBarType hatXType = FindBarType(barTypes, cfg.HatReinforce.DiaXLabel);
            RebarBarType hatYType = FindBarType(barTypes, cfg.HatReinforce.DiaYLabel);
            RebarBarType distType = FindBarType(barTypes, cfg.TopDistribution.DiaLabel);
            RebarBarType chairType = FindBarType(barTypes, cfg.Spacer.DiaLabel);

            double diaBotX = botXType?.BarModelDiameter ?? ToFeet(10);
            double diaBotY = botYType?.BarModelDiameter ?? ToFeet(10);
            double diaHatX = hatXType?.BarModelDiameter ?? ToFeet(10);
            double diaHatY = hatYType?.BarModelDiameter ?? ToFeet(10);

            // Cao độ Z các lớp
            double zBot1 = bb.Min.Z + coverBot + diaBotX / 2.0;
            double zBot2 = zBot1 + diaBotX / 2.0 + diaBotY / 2.0;
            double zBotX = cfg.BottomLayer.InvertLayer ? zBot2 : zBot1;
            double zBotY = cfg.BottomLayer.InvertLayer ? zBot1 : zBot2;

            double zTop1 = bb.Max.Z - coverTop - diaHatX / 2.0;
            double zTop2 = zTop1 - diaHatX / 2.0 - diaHatY / 2.0;
            double zHatX = zTop1;
            double zHatY = zTop2;

            // Thiết lập hệ toạ độ phẳng Local 2D (Origin, AxisU, AxisV)
            XYZ origin = panel.Origin ?? XYZ.Zero;
            XYZ axisU = panel.AxisU != null && panel.AxisU.GetLength() > 0.5 ? panel.AxisU.Normalize() : XYZ.BasisX;
            XYZ axisV = panel.AxisV != null && panel.AxisV.GetLength() > 0.5 ? panel.AxisV.Normalize() : XYZ.BasisY;

            // Tính toán biên toạ độ phẳng Local (u, v)
            double minU = panel.LocalMinU, maxU = panel.LocalMaxU;
            double minV = panel.LocalMinV, maxV = panel.LocalMaxV;

            if (Math.Abs(maxU - minU) < 0.01 || Math.Abs(maxV - minV) < 0.01)
            {
                minU = double.MaxValue; maxU = double.MinValue;
                minV = double.MaxValue; maxV = double.MinValue;

                if (panel.Boundary != null && panel.Boundary.Any())
                {
                    foreach (Curve c in panel.Boundary)
                    {
                        XYZ p = c.GetEndPoint(0);
                        XYZ vec = p - origin;
                        double u = vec.DotProduct(axisU);
                        double v = vec.DotProduct(axisV);
                        minU = Math.Min(minU, u);
                        maxU = Math.Max(maxU, u);
                        minV = Math.Min(minV, v);
                        maxV = Math.Max(maxV, v);
                    }
                }
                else
                {
                    minU = 0; maxU = bb.Max.X - bb.Min.X;
                    minV = 0; maxV = bb.Max.Y - bb.Min.Y;
                }
            }

            double coverOffset = ToFeet(25);

            // ── 1. BOTTOM LAYER (LƯỚI ĐÁY) ──────────────────────────────────
            if (cfg.BottomLayer.Enabled)
            {
                // Bottom U (Thanh dọc theo trục U rải theo trục V)
                var botU = CreateBoundaryConstrainedRebars(panel.HostFloor, botXType,
                    minV + coverOffset, maxV - coverOffset, zBotX,
                    isUDirection: true, cfg.BottomLayer.SpacingXMm,
                    panel.Boundary, panel.Openings, origin, axisU, axisV, coverOffset,
                    report, $"{panel.PanelId} - Thép đáy phương U");
                createdRebars.AddRange(botU);

                // Bottom V (Thanh dọc theo trục V rải theo trục U)
                var botV = CreateBoundaryConstrainedRebars(panel.HostFloor, botYType,
                    minU + coverOffset, maxU - coverOffset, zBotY,
                    isUDirection: false, cfg.BottomLayer.SpacingYMm,
                    panel.Boundary, panel.Openings, origin, axisU, axisV, coverOffset,
                    report, $"{panel.PanelId} - Thép đáy phương V");
                createdRebars.AddRange(botV);
            }

            // ── 2. TOP LAYER FULL MESH (LƯỚI TRÊN TOÀN DIỆN NẾU BẬT) ──────────
            if (cfg.TopLayer.Enabled)
            {
                var topU = CreateBoundaryConstrainedRebars(panel.HostFloor, topMeshXType,
                    minV + coverOffset, maxV - coverOffset, zTop1,
                    isUDirection: true, cfg.TopLayer.SpacingXMm,
                    panel.Boundary, panel.Openings, origin, axisU, axisV, coverOffset,
                    report, $"{panel.PanelId} - Lưới trên full U");
                createdRebars.AddRange(topU);

                var topV = CreateBoundaryConstrainedRebars(panel.HostFloor, topMeshYType,
                    minU + coverOffset, maxU - coverOffset, zTop2,
                    isUDirection: false, cfg.TopLayer.SpacingYMm,
                    panel.Boundary, panel.Openings, origin, axisU, axisV, coverOffset,
                    report, $"{panel.PanelId} - Lưới trên full V");
                createdRebars.AddRange(topV);
            }

            // ── 3. HAT REINFORCE (MŨ GỐI) & TOP DISTRIBUTION ────────────────
            if (cfg.HatReinforce.Enabled)
            {
                double spanU = Math.Abs(maxU - minU);
                double spanV = Math.Abs(maxV - minV);

                double facRatio = ParseHatFactor(cfg.HatReinforce.HatFactor); // 0.25 cho L/4
                bool fullU = cfg.HatReinforce.IsFullSpan || (panel.WidthMm < cfg.Tolerances.MinSpanMm);
                bool fullV = cfg.HatReinforce.IsFullSpan || (panel.LengthMm < cfg.Tolerances.MinSpanMm);

                double hatLenU = fullU ? spanU : spanU * facRatio;
                double hatLenV = fullV ? spanV : spanV * facRatio;

                bool skipEdge0 = panel.Edges.Count > 0 && panel.Edges[0].SkipTopHat;
                bool skipEdge1 = panel.Edges.Count > 1 && panel.Edges[1].SkipTopHat;
                bool skipEdge2 = panel.Edges.Count > 2 && panel.Edges[2].SkipTopHat;
                bool skipEdge3 = panel.Edges.Count > 3 && panel.Edges[3].SkipTopHat;

                double stepV = UnitUtils.ConvertToInternalUnits(cfg.HatReinforce.SpacingXMm, UnitTypeId.Millimeters);
                double stepU = UnitUtils.ConvertToInternalUnits(cfg.HatReinforce.SpacingYMm, UnitTypeId.Millimeters);

                // Mũ gối gối trái phương U (U-min vươn sang phải)
                if (!skipEdge3)
                {
                    for (double v = minV + coverOffset; v <= maxV - coverOffset; v += stepV)
                    {
                        var segs = SlabGeometryHelper.GetSlabIntervalsLocal(v, isAlongU: true, panel.Boundary, panel.Openings, origin, axisU, axisV, coverOffset);
                        foreach (var seg in segs)
                        {
                            double u1 = seg.Start;
                            double u2 = Math.Min(seg.End, seg.Start + hatLenU);
                            XYZ p1 = origin + u1 * axisU + v * axisV + zHatX * XYZ.BasisZ;
                            XYZ p2 = origin + u2 * axisU + v * axisV + zHatX * XYZ.BasisZ;
                            CreateSingleStraightBar(panel.HostFloor, hatXType, p1, p2, createdRebars, report, "Mũ gối U trái");
                        }
                    }
                }

                // Mũ gối gối phải phương U (U-max vươn sang trái)
                if (!skipEdge1 && !fullU)
                {
                    for (double v = minV + coverOffset; v <= maxV - coverOffset; v += stepV)
                    {
                        var segs = SlabGeometryHelper.GetSlabIntervalsLocal(v, isAlongU: true, panel.Boundary, panel.Openings, origin, axisU, axisV, coverOffset);
                        foreach (var seg in segs)
                        {
                            double u1 = Math.Max(seg.Start, seg.End - hatLenU);
                            double u2 = seg.End;
                            XYZ p1 = origin + u1 * axisU + v * axisV + zHatX * XYZ.BasisZ;
                            XYZ p2 = origin + u2 * axisU + v * axisV + zHatX * XYZ.BasisZ;
                            CreateSingleStraightBar(panel.HostFloor, hatXType, p1, p2, createdRebars, report, "Mũ gối U phải");
                        }
                    }
                }

                // Mũ gối gối dưới phương V (V-min vươn lên trên)
                if (!skipEdge0)
                {
                    for (double u = minU + coverOffset; u <= maxU - coverOffset; u += stepU)
                    {
                        var segs = SlabGeometryHelper.GetSlabIntervalsLocal(u, isAlongU: false, panel.Boundary, panel.Openings, origin, axisU, axisV, coverOffset);
                        foreach (var seg in segs)
                        {
                            double v1 = seg.Start;
                            double v2 = Math.Min(seg.End, seg.Start + hatLenV);
                            XYZ p1 = origin + u * axisU + v1 * axisV + zHatY * XYZ.BasisZ;
                            XYZ p2 = origin + u * axisU + v2 * axisV + zHatY * XYZ.BasisZ;
                            CreateSingleStraightBar(panel.HostFloor, hatYType, p1, p2, createdRebars, report, "Mũ gối V dưới");
                        }
                    }
                }

                // Mũ gối gối trên phương V (V-max vươn xuống dưới)
                if (!skipEdge2 && !fullV)
                {
                    for (double u = minU + coverOffset; u <= maxU - coverOffset; u += stepU)
                    {
                        var segs = SlabGeometryHelper.GetSlabIntervalsLocal(u, isAlongU: false, panel.Boundary, panel.Openings, origin, axisU, axisV, coverOffset);
                        foreach (var seg in segs)
                        {
                            double v1 = Math.Max(seg.Start, seg.End - hatLenV);
                            double v2 = seg.End;
                            XYZ p1 = origin + u * axisU + v1 * axisV + zHatY * XYZ.BasisZ;
                            XYZ p2 = origin + u * axisU + v2 * axisV + zHatY * XYZ.BasisZ;
                            CreateSingleStraightBar(panel.HostFloor, hatYType, p1, p2, createdRebars, report, "Mũ gối V trên");
                        }
                    }
                }
            }

            // ── 4. SPACERS (THÉP CHÂN CHÓ KÊ SÀN) ───────────────────────────
            if (cfg.Spacer.Enabled)
            {
                var chairs = CreateSpacers(panel.HostFloor, chairType, minU, maxU, minV, maxV,
                    origin, axisU, axisV, zBotX, zHatX, panel.Boundary, panel.Openings, cfg.Spacer, report, panel.PanelId);
                createdRebars.AddRange(chairs);
            }

            // ── 5. THÉP GIA CƯỜNG BO VIỀN LỖ MỞ (OPENING TRIM REBARS) ──────────
            if (panel.Openings != null && panel.Openings.Any())
            {
                RebarBarType trimType = botXType ?? topMeshXType ?? barTypes.FirstOrDefault();
                var trimmers = CreateOpeningTrimmerBars(panel.HostFloor, trimType, panel.Openings, origin, axisU, axisV, zBot1, zTop1, report);
                createdRebars.AddRange(trimmers);
            }

            RebarLifecycleManager.TagRebars(createdRebars, panel.HostFloor, "Slab", "SlabPanelReinforcement");
            return createdRebars;
        }

        // Tương thích ngược với hàm Generate cũ
        public List<Rebar> Generate(SlabProfile profile, SlabRebarSettings settings, RebarGenerationReport report = null)
        {
            if (profile == null || profile.FloorElement == null) return new List<Rebar>();

            var panel = new SlabPanel
            {
                PanelId = "P1",
                HostFloorId = profile.FloorId,
                HostFloor = profile.FloorElement,
                WidthMm = profile.WidthMm,
                LengthMm = profile.LengthMm,
                ThicknessFeet = profile.ThicknessFeet,
                ThicknessMm = profile.ThicknessMm,
                CoverTopFeet = profile.CoverTopFeet,
                CoverBottomFeet = profile.CoverBottomFeet,
                Origin = profile.Origin,
                AxisU = profile.AxisU,
                AxisV = profile.AxisV,
                LocalMinU = profile.LocalMinU,
                LocalMaxU = profile.LocalMaxU,
                LocalMinV = profile.LocalMinV,
                LocalMaxV = profile.LocalMaxV,
                Boundary = profile.OuterBoundary,
                Openings = profile.InnerOpenings ?? new List<CurveLoop>()
            };

            panel.Config.BottomLayer.DiaXLabel = settings.BotXDiaLabel;
            panel.Config.BottomLayer.SpacingXMm = settings.BotXSpacingMm;
            panel.Config.BottomLayer.DiaYLabel = settings.BotYDiaLabel;
            panel.Config.BottomLayer.SpacingYMm = settings.BotYSpacingMm;

            panel.Config.HatReinforce.DiaXLabel = settings.TopXDiaLabel;
            panel.Config.HatReinforce.SpacingXMm = settings.TopXSpacingMm;
            panel.Config.HatReinforce.DiaYLabel = settings.TopYDiaLabel;
            panel.Config.HatReinforce.SpacingYMm = settings.TopYSpacingMm;
            panel.Config.HatReinforce.HatFactor = settings.TopExtensionRatio;

            panel.Config.Spacer.Enabled = settings.EnableChairRebar;
            panel.Config.Spacer.DiaLabel = settings.ChairDiaLabel;
            panel.Config.Spacer.StepXMm = settings.ChairSpacingXmm;
            panel.Config.Spacer.StepYMm = settings.ChairSpacingYmm;

            return GeneratePanel(panel, report);
        }

        private List<Rebar> CreateBoundaryConstrainedRebars(Floor floor, RebarBarType barType,
            double startPerp, double endPerp, double zLevel,
            bool isUDirection, double spacingMm,
            CurveLoop boundary, List<CurveLoop> openings,
            XYZ origin, XYZ axisU, XYZ axisV, double coverFeet,
            RebarGenerationReport report = null, string groupName = "Thép sàn")
        {
            var list = new List<Rebar>();
            if (barType == null || spacingMm <= 0 || endPerp <= startPerp) return list;

            try
            {
                double spacingFeet = UnitUtils.ConvertToInternalUnits(spacingMm, UnitTypeId.Millimeters);

                for (double perp = startPerp; perp <= endPerp; perp += spacingFeet)
                {
                    var intervals = SlabGeometryHelper.GetSlabIntervalsLocal(
                        perp, isUDirection, boundary, openings, origin, axisU, axisV, coverFeet);

                    foreach (var seg in intervals)
                    {
                        if (seg.End - seg.Start < 0.5) continue; // Bỏ qua đoạn quá ngắn < 150mm

                        XYZ p1 = origin + (isUDirection ? (seg.Start * axisU + perp * axisV) : (perp * axisU + seg.Start * axisV)) + zLevel * XYZ.BasisZ;
                        XYZ p2 = origin + (isUDirection ? (seg.End * axisU + perp * axisV) : (perp * axisU + seg.End * axisV)) + zLevel * XYZ.BasisZ;

                        var curves = new List<Curve> { Line.CreateBound(p1, p2) };
                        Rebar rebar = RebarShapeCreationHelper.CreateFromCurvesSafe(
                            _doc, RebarStyle.Standard, barType, null, null, floor, XYZ.BasisZ, curves,
                            RebarHookOrientation.Left, RebarHookOrientation.Right);
                        if (rebar != null)
                        {
                            list.Add(rebar);
                            report?.AddSuccess(1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                report?.AddError(floor, groupName, ex);
            }

            return list;
        }

        private List<Rebar> CreateOpeningTrimmerBars(Floor floor, RebarBarType barType,
            List<CurveLoop> openings, XYZ origin, XYZ axisU, XYZ axisV,
            double zBot, double zTop, RebarGenerationReport report = null)
        {
            var list = new List<Rebar>();
            if (barType == null || openings == null || !openings.Any()) return list;

            double barDia = barType.BarModelDiameter;
            double anchLen = barDia * 40; // Lb = 40d theo Eurocode 2
            double cover = ToFeet(25);

            foreach (var op in openings)
            {
                double minU = double.MaxValue, maxU = double.MinValue;
                double minV = double.MaxValue, maxV = double.MinValue;

                foreach (Curve c in op)
                {
                    XYZ p0 = c.GetEndPoint(0);
                    XYZ p1 = c.GetEndPoint(1);
                    double u0 = (p0 - origin).DotProduct(axisU);
                    double v0 = (p0 - origin).DotProduct(axisV);
                    double u1 = (p1 - origin).DotProduct(axisU);
                    double v1 = (p1 - origin).DotProduct(axisV);

                    minU = Math.Min(minU, Math.Min(u0, u1));
                    maxU = Math.Max(maxU, Math.Max(u0, u1));
                    minV = Math.Min(minV, Math.Min(v0, v1));
                    maxV = Math.Max(maxV, Math.Max(v0, v1));
                }

                double widthU = maxU - minU;
                double lengthV = maxV - minV;
                if (widthU < 0.5 || lengthV < 0.5) continue; // Bỏ qua lỗ quá nhỏ < 150mm

                // Thép gia cường 4 cạnh lỗ mở (Bottom & Top) theo hệ trục Local
                double[] zLevels = new double[] { zBot, zTop };
                foreach (double z in zLevels)
                {
                    // 2 thanh song song cạnh dưới (V-min, chạy theo U)
                    XYZ b1 = origin + (minU - anchLen) * axisU + (minV - cover) * axisV + z * XYZ.BasisZ;
                    XYZ b2 = origin + (maxU + anchLen) * axisU + (minV - cover) * axisV + z * XYZ.BasisZ;
                    CreateSingleStraightBar(floor, barType, b1, b2, list, report, "Gia cường lỗ mở cạnh dưới");

                    // 2 thanh song song cạnh trên (V-max, chạy theo U)
                    XYZ t1 = origin + (minU - anchLen) * axisU + (maxV + cover) * axisV + z * XYZ.BasisZ;
                    XYZ t2 = origin + (maxU + anchLen) * axisU + (maxV + cover) * axisV + z * XYZ.BasisZ;
                    CreateSingleStraightBar(floor, barType, t1, t2, list, report, "Gia cường lỗ mở cạnh trên");

                    // 2 thanh song song cạnh trái (U-min, chạy theo V)
                    XYZ l1 = origin + (minU - cover) * axisU + (minV - anchLen) * axisV + z * XYZ.BasisZ;
                    XYZ l2 = origin + (minU - cover) * axisU + (maxV + anchLen) * axisV + z * XYZ.BasisZ;
                    CreateSingleStraightBar(floor, barType, l1, l2, list, report, "Gia cường lỗ mở cạnh trái");

                    // 2 thanh song song cạnh phải (U-max, chạy theo V)
                    XYZ r1 = origin + (maxU + cover) * axisU + (minV - anchLen) * axisV + z * XYZ.BasisZ;
                    XYZ r2 = origin + (maxU + cover) * axisU + (maxV + anchLen) * axisV + z * XYZ.BasisZ;
                    CreateSingleStraightBar(floor, barType, r1, r2, list, report, "Gia cường lỗ mở cạnh phải");
                }
            }

            return list;
        }

        private void CreateSingleStraightBar(Floor floor, RebarBarType barType, XYZ p1, XYZ p2,
            List<Rebar> list, RebarGenerationReport report, string desc)
        {
            try
            {
                if (p1.DistanceTo(p2) < 0.5) return;
                var curves = new List<Curve> { Line.CreateBound(p1, p2) };
                Rebar rebar = RebarShapeCreationHelper.CreateFromCurvesSafe(
                    _doc, RebarStyle.Standard, barType, null, null, floor, XYZ.BasisZ, curves,
                    RebarHookOrientation.Left, RebarHookOrientation.Right);
                if (rebar != null)
                {
                    list.Add(rebar);
                    report?.AddSuccess(1);
                }
            }
            catch (Exception ex)
            {
                report?.AddError(floor, desc, ex);
            }
        }

        private List<Rebar> CreateSpacers(Floor floor, RebarBarType barType,
            double minU, double maxU, double minV, double maxV,
            XYZ origin, XYZ axisU, XYZ axisV,
            double zBot, double zTop, CurveLoop boundary, List<CurveLoop> openings,
            SlabSpacerSettings settings, RebarGenerationReport report = null, string panelId = "P")
        {
            var list = new List<Rebar>();
            if (barType == null) return list;

            double stepUFeet = UnitUtils.ConvertToInternalUnits(settings.StepXMm, UnitTypeId.Millimeters);
            double stepVFeet = UnitUtils.ConvertToInternalUnits(settings.StepYMm, UnitTypeId.Millimeters);
            double footLenFeet = UnitUtils.ConvertToInternalUnits(settings.HookLenMm > 0 ? settings.HookLenMm : 150, UnitTypeId.Millimeters); // Chân A/E: 150mm
            double bridgeWidthFeet = ToFeet(150); // Cầu trên C: 150mm
            double barDia = barType.BarModelDiameter;

            double hChair = zTop - zBot;
            if (hChair <= 0.1) return list;

            for (double u = minU + stepUFeet; u < maxU - stepUFeet / 2.0; u += stepUFeet)
            {
                for (double v = minV + stepVFeet; v < maxV - stepVFeet / 2.0; v += stepVFeet)
                {
                    XYZ ptWorld = origin + u * axisU + v * axisV;
                    // Chỉ đặt con kê CHÂN CHÓ NẾU ĐIỂM (u, v) NẰM TRONG BÊ TÔNG SÀN VÀ NGOÀI LỖ MỞ
                    if (!SlabGeometryHelper.IsPointInsideSlab(ptWorld, boundary, openings))
                        continue;

                    try
                    {
                        // Hình dạng Con Kê Chân Chó Chuẩn Shape 31 (JP_T31 / BS 8666 / Eurocode 2) định hướng theo hệ trục Local:
                        double halfBridge = bridgeWidthFeet / 2.0;

                        XYZ p1 = ptWorld - halfBridge * axisU - footLenFeet * axisV + zBot * XYZ.BasisZ;
                        XYZ p2 = ptWorld - halfBridge * axisU + zBot * XYZ.BasisZ;
                        XYZ p3 = ptWorld - halfBridge * axisU + zTop * XYZ.BasisZ;
                        XYZ p4 = ptWorld + halfBridge * axisU + zTop * XYZ.BasisZ;
                        XYZ p5 = ptWorld + halfBridge * axisU + zBot * XYZ.BasisZ;
                        XYZ p6 = ptWorld + halfBridge * axisU + footLenFeet * axisV + zBot * XYZ.BasisZ;

                        var curves = new List<Curve>
                        {
                            Line.CreateBound(p1, p2),
                            Line.CreateBound(p2, p3),
                            Line.CreateBound(p3, p4),
                            Line.CreateBound(p4, p5),
                            Line.CreateBound(p5, p6)
                        };

                        Rebar chair = RebarShapeCreationHelper.CreateFromCurvesSafe(
                            _doc, RebarStyle.Standard, barType, null, null, floor, axisV, curves,
                            RebarHookOrientation.Left, RebarHookOrientation.Right);

                        if (chair != null)
                        {
                            var shapeParams = new Dictionary<string, double>
                            {
                                { "A", footLenFeet },
                                { "B", hChair },
                                { "C", bridgeWidthFeet },
                                { "D", hChair },
                                { "E", footLenFeet },
                                { "VNDC_L1", footLenFeet - barDia / 2.0 },
                                { "VNDC_L2", hChair - barDia },
                                { "VNDC_L3", bridgeWidthFeet - barDia },
                                { "VNDC_L4", hChair - barDia },
                                { "VNDC_L5", footLenFeet - barDia / 2.0 }
                            };
                            RebarShapeLibrary.ApplyShapeParameters(chair, shapeParams);

                            list.Add(chair);
                            report?.AddSuccess(1);
                        }
                    }
                    catch (Exception ex)
                    {
                        report?.AddError(floor, $"{panelId} - Con kê / Thép chân chó (Spacer Shape 31)", ex);
                    }
                }
            }

            return list;
        }

        private double ParseHatFactor(string factorStr)
        {
            if (string.IsNullOrWhiteSpace(factorStr)) return 0.25;
            if (factorStr.Contains("3")) return 1.0 / 3.0;
            if (factorStr.Contains("5")) return 1.0 / 5.0;
            return 0.25; // Default L/4
        }

        private RebarBarType FindBarType(List<RebarBarType> list, string diaLabel)
        {
            if (string.IsNullOrWhiteSpace(diaLabel)) return list.FirstOrDefault();
            string search = diaLabel.Replace("d", "").Replace("Φ", "").Replace("ϕ", "").Trim();

            foreach (var bt in list)
            {
                if (bt.Name.Contains(search)) return bt;
                double diaMm = UnitUtils.ConvertFromInternalUnits(bt.BarModelDiameter, UnitTypeId.Millimeters);
                if (double.TryParse(search, out double target) && Math.Abs(diaMm - target) < 1.0) return bt;
            }
            return list.FirstOrDefault();
        }

        private static double ToFeet(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }
}
