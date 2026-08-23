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

            // Tính BoundingBox thực tế từ ranh giới ô sàn (Boundary Polygon)
            double bMinX = double.MaxValue, bMaxX = double.MinValue;
            double bMinY = double.MaxValue, bMaxY = double.MinValue;
            if (panel.Boundary != null && panel.Boundary.Any())
            {
                foreach (Curve c in panel.Boundary)
                {
                    XYZ p0 = c.GetEndPoint(0);
                    XYZ p1 = c.GetEndPoint(1);
                    bMinX = Math.Min(bMinX, Math.Min(p0.X, p1.X));
                    bMaxX = Math.Max(bMaxX, Math.Max(p0.X, p1.X));
                    bMinY = Math.Min(bMinY, Math.Min(p0.Y, p1.Y));
                    bMaxY = Math.Max(bMaxY, Math.Max(p0.Y, p1.Y));
                }
            }
            else
            {
                bMinX = bb.Min.X; bMaxX = bb.Max.X;
                bMinY = bb.Min.Y; bMaxY = bb.Max.Y;
            }

            double beamAnchorFeet = ToFeet(cfg.Anchors.BeamAnchorAMm);
            double slabAnchorFeet = ToFeet(cfg.Anchors.SlabAnchorBMm);
            double coverOffset = ToFeet(25);

            // ── 1. BOTTOM LAYER (LƯỚI ĐÁY) ──────────────────────────────────
            if (cfg.BottomLayer.Enabled)
            {
                // Bottom X (Thanh ngang rải theo Y)
                var botX = CreateBoundaryConstrainedRebars(panel.HostFloor, botXType,
                    bMinY + coverOffset, bMaxY - coverOffset, zBotX,
                    isXDirection: true, cfg.BottomLayer.SpacingXMm,
                    panel.Boundary, panel.Openings, beamAnchorFeet,
                    report, $"{panel.PanelId} - Thép đáy phương X");
                createdRebars.AddRange(botX);

                // Bottom Y (Thanh dọc rải theo X)
                var botY = CreateBoundaryConstrainedRebars(panel.HostFloor, botYType,
                    bMinX + coverOffset, bMaxX - coverOffset, zBotY,
                    isXDirection: false, cfg.BottomLayer.SpacingYMm,
                    panel.Boundary, panel.Openings, beamAnchorFeet,
                    report, $"{panel.PanelId} - Thép đáy phương Y");
                createdRebars.AddRange(botY);
            }

            // ── 2. TOP LAYER FULL MESH (LƯỚI TRÊN TOÀN DIỆN NẾU BẬT) ──────────
            if (cfg.TopLayer.Enabled)
            {
                var topX = CreateBoundaryConstrainedRebars(panel.HostFloor, topMeshXType,
                    bMinY + coverOffset, bMaxY - coverOffset, zTop1,
                    isXDirection: true, cfg.TopLayer.SpacingXMm,
                    panel.Boundary, panel.Openings, beamAnchorFeet,
                    report, $"{panel.PanelId} - Lưới trên full X");
                createdRebars.AddRange(topX);

                var topY = CreateBoundaryConstrainedRebars(panel.HostFloor, topMeshYType,
                    bMinX + coverOffset, bMaxX - coverOffset, zTop2,
                    isXDirection: false, cfg.TopLayer.SpacingYMm,
                    panel.Boundary, panel.Openings, beamAnchorFeet,
                    report, $"{panel.PanelId} - Lưới trên full Y");
                createdRebars.AddRange(topY);
            }

            // ── 3. HAT REINFORCE (MŨ GỐI) & TOP DISTRIBUTION ────────────────
            if (cfg.HatReinforce.Enabled)
            {
                double spanX = Math.Abs(bMaxX - bMinX);
                double spanY = Math.Abs(bMaxY - bMinY);

                double facRatio = ParseHatFactor(cfg.HatReinforce.HatFactor); // 0.25 cho L/4
                bool fullX = cfg.HatReinforce.IsFullSpan || (panel.WidthMm < cfg.Tolerances.MinSpanMm);
                bool fullY = cfg.HatReinforce.IsFullSpan || (panel.LengthMm < cfg.Tolerances.MinSpanMm);

                double hatLenX = fullX ? spanX : spanX * facRatio;
                double hatLenY = fullY ? spanY : spanY * facRatio;

                bool skipEdge0 = panel.Edges.Count > 0 && panel.Edges[0].SkipTopHat;
                bool skipEdge1 = panel.Edges.Count > 1 && panel.Edges[1].SkipTopHat;
                bool skipEdge2 = panel.Edges.Count > 2 && panel.Edges[2].SkipTopHat;
                bool skipEdge3 = panel.Edges.Count > 3 && panel.Edges[3].SkipTopHat;

                double stepY = UnitUtils.ConvertToInternalUnits(cfg.HatReinforce.SpacingXMm, UnitTypeId.Millimeters);
                double stepX = UnitUtils.ConvertToInternalUnits(cfg.HatReinforce.SpacingYMm, UnitTypeId.Millimeters);

                // Mũ gối gối trái phương X (X-min vươn sang phải)
                if (!skipEdge3)
                {
                    for (double y = bMinY + coverOffset; y <= bMaxY - coverOffset; y += stepY)
                    {
                        var segs = SlabGeometryHelper.GetSlabIntervalsAtCoord(y, isXDirection: true, panel.Boundary, panel.Openings, beamAnchorFeet, coverOffset);
                        foreach (var seg in segs)
                        {
                            double hx1 = seg.Start;
                            double hx2 = Math.Min(seg.End, seg.Start + hatLenX);
                            CreateSingleStraightBar(panel.HostFloor, hatXType, new XYZ(hx1, y, zHatX), new XYZ(hx2, y, zHatX), createdRebars, report, "Mũ gối X trái");
                        }
                    }
                }

                // Mũ gối gối phải phương X (X-max vươn sang trái)
                if (!skipEdge1 && !fullX)
                {
                    for (double y = bMinY + coverOffset; y <= bMaxY - coverOffset; y += stepY)
                    {
                        var segs = SlabGeometryHelper.GetSlabIntervalsAtCoord(y, isXDirection: true, panel.Boundary, panel.Openings, beamAnchorFeet, coverOffset);
                        foreach (var seg in segs)
                        {
                            double hx1 = Math.Max(seg.Start, seg.End - hatLenX);
                            double hx2 = seg.End;
                            CreateSingleStraightBar(panel.HostFloor, hatXType, new XYZ(hx1, y, zHatX), new XYZ(hx2, y, zHatX), createdRebars, report, "Mũ gối X phải");
                        }
                    }
                }

                // Mũ gối gối dưới phương Y (Y-min vươn lên trên)
                if (!skipEdge0)
                {
                    for (double x = bMinX + coverOffset; x <= bMaxX - coverOffset; x += stepX)
                    {
                        var segs = SlabGeometryHelper.GetSlabIntervalsAtCoord(x, isXDirection: false, panel.Boundary, panel.Openings, beamAnchorFeet, coverOffset);
                        foreach (var seg in segs)
                        {
                            double hy1 = seg.Start;
                            double hy2 = Math.Min(seg.End, seg.Start + hatLenY);
                            CreateSingleStraightBar(panel.HostFloor, hatYType, new XYZ(x, hy1, zHatY), new XYZ(x, hy2, zHatY), createdRebars, report, "Mũ gối Y dưới");
                        }
                    }
                }

                // Mũ gối gối trên phương Y (Y-max vươn xuống dưới)
                if (!skipEdge2 && !fullY)
                {
                    for (double x = bMinX + coverOffset; x <= bMaxX - coverOffset; x += stepX)
                    {
                        var segs = SlabGeometryHelper.GetSlabIntervalsAtCoord(x, isXDirection: false, panel.Boundary, panel.Openings, beamAnchorFeet, coverOffset);
                        foreach (var seg in segs)
                        {
                            double hy1 = Math.Max(seg.Start, seg.End - hatLenY);
                            double hy2 = seg.End;
                            CreateSingleStraightBar(panel.HostFloor, hatYType, new XYZ(x, hy1, zHatY), new XYZ(x, hy2, zHatY), createdRebars, report, "Mũ gối Y trên");
                        }
                    }
                }
            }

            // ── 4. SPACERS (THÉP CHÂN CHÓ KÊ SÀN) ───────────────────────────
            if (cfg.Spacer.Enabled)
            {
                var chairs = CreateSpacers(panel.HostFloor, chairType, bMinX, bMaxX, bMinY, bMaxY, zBotX, zHatX, panel.Boundary, panel.Openings, cfg.Spacer, report, panel.PanelId);
                createdRebars.AddRange(chairs);
            }

            // ── 5. THÉP GIA CƯỜNG BO VIỀN LỖ MỞ (OPENING TRIM REBARS) ──────────
            if (panel.Openings != null && panel.Openings.Any())
            {
                RebarBarType trimType = botXType ?? topMeshXType ?? barTypes.FirstOrDefault();
                var trimmers = CreateOpeningTrimmerBars(panel.HostFloor, trimType, panel.Openings, zBot1, zTop1, report);
                createdRebars.AddRange(trimmers);
            }

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
            bool isXDirection, double spacingMm,
            CurveLoop boundary, List<CurveLoop> openings, double anchorFeet,
            RebarGenerationReport report = null, string groupName = "Thép sàn")
        {
            var list = new List<Rebar>();
            if (barType == null || spacingMm <= 0 || endPerp <= startPerp) return list;

            try
            {
                double spacingFeet = UnitUtils.ConvertToInternalUnits(spacingMm, UnitTypeId.Millimeters);
                double coverFeet = ToFeet(25);
                XYZ normal = isXDirection ? XYZ.BasisY : XYZ.BasisX;

                for (double perp = startPerp; perp <= endPerp; perp += spacingFeet)
                {
                    var intervals = SlabGeometryHelper.GetSlabIntervalsAtCoord(
                        perp, isXDirection, boundary, openings, anchorFeet, coverFeet);

                    foreach (var seg in intervals)
                    {
                        if (seg.End - seg.Start < 0.5) continue; // Bỏ qua đoạn quá ngắn < 150mm

                        XYZ p1 = isXDirection ? new XYZ(seg.Start, perp, zLevel) : new XYZ(perp, seg.Start, zLevel);
                        XYZ p2 = isXDirection ? new XYZ(seg.End, perp, zLevel) : new XYZ(perp, seg.End, zLevel);

                        var curves = new List<Curve> { Line.CreateBound(p1, p2) };
                        Rebar rebar = RebarShapeCreationHelper.CreateFromCurvesSafe(
                            _doc, RebarStyle.Standard, barType, null, null, floor, normal, curves,
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
            List<CurveLoop> openings, double zBot, double zTop, RebarGenerationReport report = null)
        {
            var list = new List<Rebar>();
            if (barType == null || openings == null || !openings.Any()) return list;

            double barDia = barType.BarModelDiameter;
            double anchLen = barDia * 40; // Lb = 40d theo Eurocode 2
            double cover = ToFeet(25);

            foreach (var op in openings)
            {
                double minX = double.MaxValue, maxX = double.MinValue;
                double minY = double.MaxValue, maxY = double.MinValue;

                foreach (Curve c in op)
                {
                    XYZ p0 = c.GetEndPoint(0);
                    XYZ p1 = c.GetEndPoint(1);
                    minX = Math.Min(minX, Math.Min(p0.X, p1.X));
                    maxX = Math.Max(maxX, Math.Max(p0.X, p1.X));
                    minY = Math.Min(minY, Math.Min(p0.Y, p1.Y));
                    maxY = Math.Max(maxY, Math.Max(p0.Y, p1.Y));
                }

                double width = maxX - minX;
                double height = maxY - minY;
                if (width < 0.5 || height < 0.5) continue; // Bỏ qua lỗ quá nhỏ < 150mm

                // Thép gia cường 4 cạnh lỗ mở (Bottom & Top)
                double[] zLevels = new double[] { zBot, zTop };
                foreach (double z in zLevels)
                {
                    // 2 thanh song song cạnh dưới
                    XYZ b1 = new XYZ(minX - anchLen, minY - cover, z);
                    XYZ b2 = new XYZ(maxX + anchLen, minY - cover, z);
                    CreateSingleStraightBar(floor, barType, b1, b2, list, report, "Gia cường lỗ mở cạnh dưới");

                    // 2 thanh song song cạnh trên
                    XYZ t1 = new XYZ(minX - anchLen, maxY + cover, z);
                    XYZ t2 = new XYZ(maxX + anchLen, maxY + cover, z);
                    CreateSingleStraightBar(floor, barType, t1, t2, list, report, "Gia cường lỗ mở cạnh trên");

                    // 2 thanh song song cạnh trái
                    XYZ l1 = new XYZ(minX - cover, minY - anchLen, z);
                    XYZ l2 = new XYZ(minX - cover, maxY + anchLen, z);
                    CreateSingleStraightBar(floor, barType, l1, l2, list, report, "Gia cường lỗ mở cạnh trái");

                    // 2 thanh song song cạnh phải
                    XYZ r1 = new XYZ(maxX + cover, minY - anchLen, z);
                    XYZ r2 = new XYZ(maxX + cover, maxY + anchLen, z);
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
            double minX, double maxX, double minY, double maxY,
            double zBot, double zTop, CurveLoop boundary, List<CurveLoop> openings,
            SlabSpacerSettings settings, RebarGenerationReport report = null, string panelId = "P")
        {
            var list = new List<Rebar>();
            if (barType == null) return list;

            double stepXFeet = UnitUtils.ConvertToInternalUnits(settings.StepXMm, UnitTypeId.Millimeters);
            double stepYFeet = UnitUtils.ConvertToInternalUnits(settings.StepYMm, UnitTypeId.Millimeters);
            double footLenFeet = UnitUtils.ConvertToInternalUnits(settings.HookLenMm > 0 ? settings.HookLenMm : 150, UnitTypeId.Millimeters); // Chân A/E: 150mm
            double bridgeWidthFeet = ToFeet(150); // Cầu trên C: 150mm
            double barDia = barType.BarModelDiameter;

            double hChair = zTop - zBot;
            if (hChair <= 0.1) return list;

            for (double x = minX + stepXFeet; x < maxX - stepXFeet / 2.0; x += stepXFeet)
            {
                for (double y = minY + stepYFeet; y < maxY - stepYFeet / 2.0; y += stepYFeet)
                {
                    // Chỉ đặt con kê CHÂN CHÓ NẾU ĐIỂM (x, y) NẰM TRONG BÊ TÔNG SÀN VÀ NGOÀI LỖ MỞ
                    if (!SlabGeometryHelper.IsPointInsideSlab(new XYZ(x, y, 0), boundary, openings))
                        continue;

                    try
                    {
                        // Hình dạng Con Kê Chân Chó Chuẩn Shape 31 (JP_T31 / BS 8666 / Eurocode 2):
                        // P1: Chân trái dưới (zBot) -> P2: Gốc chân trái (zBot)
                        // P2: Gốc chân trái -> P3: Đỉnh trái (zTop)
                        // P3: Đỉnh trái -> P4: Đỉnh phải (zTop) (Cầu đỡ thép mặt trên)
                        // P4: Đỉnh phải -> P5: Gốc chân phải (zBot)
                        // P5: Gốc chân phải -> P6: Chân phải dưới (zBot)
                        double halfBridge = bridgeWidthFeet / 2.0;

                        XYZ p1 = new XYZ(x - halfBridge, y - footLenFeet, zBot);
                        XYZ p2 = new XYZ(x - halfBridge, y, zBot);
                        XYZ p3 = new XYZ(x - halfBridge, y, zTop);
                        XYZ p4 = new XYZ(x + halfBridge, y, zTop);
                        XYZ p5 = new XYZ(x + halfBridge, y, zBot);
                        XYZ p6 = new XYZ(x + halfBridge, y + footLenFeet, zBot);

                        var curves = new List<Curve>
                        {
                            Line.CreateBound(p1, p2),
                            Line.CreateBound(p2, p3),
                            Line.CreateBound(p3, p4),
                            Line.CreateBound(p4, p5),
                            Line.CreateBound(p5, p6)
                        };

                        Rebar chair = RebarShapeCreationHelper.CreateFromCurvesSafe(
                            _doc, RebarStyle.Standard, barType, null, null, floor, XYZ.BasisZ, curves,
                            RebarHookOrientation.Left, RebarHookOrientation.Right);

                        if (chair != null)
                        {
                            // Gán tham số kích thước phân đoạn theo Rebar Shape 31 / Shared Parameter VNDC:
                            // L1 = A - d/2, L2 = B - d, L3 = C - d, L4 = D - d, L5 = E - d/2
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
