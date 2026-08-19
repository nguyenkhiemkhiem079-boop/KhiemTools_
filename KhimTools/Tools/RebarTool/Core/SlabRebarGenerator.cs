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

            double beamAnchorFeet = ToFeet(cfg.Anchors.BeamAnchorAMm);
            double slabAnchorFeet = ToFeet(cfg.Anchors.SlabAnchorBMm);

            // ── 1. BOTTOM LAYER (LƯỚI ĐÁY) ──────────────────────────────────
            if (cfg.BottomLayer.Enabled)
            {
                double startX = bb.Min.X - beamAnchorFeet;
                double endX = bb.Max.X + beamAnchorFeet;
                double startY = bb.Min.Y - beamAnchorFeet;
                double endY = bb.Max.Y + beamAnchorFeet;

                // Bottom X
                var botX = CreateRebarLineArray(panel.HostFloor, botXType,
                    startX, endX, bb.Min.Y + ToFeet(50), bb.Max.Y - ToFeet(50), zBotX,
                    XYZ.BasisX, XYZ.BasisY, cfg.BottomLayer.SpacingXMm,
                    panel.Openings,
                    report, $"{panel.PanelId} - Thép đáy phương X");
                createdRebars.AddRange(botX);

                // Bottom Y
                var botY = CreateRebarLineArray(panel.HostFloor, botYType,
                    startY, endY, bb.Min.X + ToFeet(50), bb.Max.X - ToFeet(50), zBotY,
                    XYZ.BasisY, XYZ.BasisX, cfg.BottomLayer.SpacingYMm,
                    panel.Openings,
                    report, $"{panel.PanelId} - Thép đáy phương Y");
                createdRebars.AddRange(botY);
            }

            // ── 2. TOP LAYER FULL MESH (NẾU BẬT) ─────────────────────────────
            if (cfg.TopLayer.Enabled)
            {
                double startX = bb.Min.X - beamAnchorFeet;
                double endX = bb.Max.X + beamAnchorFeet;
                double startY = bb.Min.Y - beamAnchorFeet;
                double endY = bb.Max.Y + beamAnchorFeet;

                var topX = CreateRebarLineArray(panel.HostFloor, topMeshXType,
                    startX, endX, bb.Min.Y + ToFeet(50), bb.Max.Y - ToFeet(50), zTop1,
                    XYZ.BasisX, XYZ.BasisY, cfg.TopLayer.SpacingXMm,
                    panel.Openings,
                    report, $"{panel.PanelId} - Lưới trên full X");
                createdRebars.AddRange(topX);

                var topY = CreateRebarLineArray(panel.HostFloor, topMeshYType,
                    startY, endY, bb.Min.X + ToFeet(50), bb.Max.X - ToFeet(50), zTop2,
                    XYZ.BasisY, XYZ.BasisX, cfg.TopLayer.SpacingYMm,
                    panel.Openings,
                    report, $"{panel.PanelId} - Lưới trên full Y");
                createdRebars.AddRange(topY);
            }

            // ── 3. HAT REINFORCE (MŨ GỐI) & TOP DISTRIBUTION ────────────────
            if (cfg.HatReinforce.Enabled)
            {
                double spanX = Math.Abs(bb.Max.X - bb.Min.X);
                double spanY = Math.Abs(bb.Max.Y - bb.Min.Y);

                double facRatio = ParseHatFactor(cfg.HatReinforce.HatFactor); // 0.25 cho L/4
                bool fullX = cfg.HatReinforce.IsFullSpan || (panel.WidthMm < cfg.Tolerances.MinSpanMm);
                bool fullY = cfg.HatReinforce.IsFullSpan || (panel.LengthMm < cfg.Tolerances.MinSpanMm);

                double hatLenX = fullX ? spanX : spanX * facRatio;
                double hatLenY = fullY ? spanY : spanY * facRatio;

                // Kiểm tra 4 cạnh (Cạnh 0: Y-min, Cạnh 1: X-max, Cạnh 2: Y-max, Cạnh 3: X-min)
                bool skipEdge0 = panel.Edges.Count > 0 && panel.Edges[0].SkipTopHat;
                bool skipEdge1 = panel.Edges.Count > 1 && panel.Edges[1].SkipTopHat;
                bool skipEdge2 = panel.Edges.Count > 2 && panel.Edges[2].SkipTopHat;
                bool skipEdge3 = panel.Edges.Count > 3 && panel.Edges[3].SkipTopHat;

                // Mũ gối gối trái phương X (X-min vươn sang phải)
                if (!skipEdge3)
                {
                    double x1 = bb.Min.X - beamAnchorFeet;
                    double x2 = bb.Min.X + hatLenX;
                    var hatLeft = CreateRebarLineArray(panel.HostFloor, hatXType,
                        x1, x2, bb.Min.Y + ToFeet(50), bb.Max.Y - ToFeet(50), zHatX,
                        XYZ.BasisX, XYZ.BasisY, cfg.HatReinforce.SpacingXMm,
                        panel.Openings,
                        report, $"{panel.PanelId} - Mũ gối X bên trái");
                    createdRebars.AddRange(hatLeft);

                    // Thép phân bố vuông góc dưới mũ gối trái
                    if (cfg.TopDistribution.Enabled)
                    {
                        var distLeft = CreateRebarLineArray(panel.HostFloor, distType,
                            bb.Min.Y, bb.Max.Y, bb.Min.X + ToFeet(50), x2 - ToFeet(50), zHatX - ToFeet(15),
                            XYZ.BasisY, XYZ.BasisX, cfg.TopDistribution.SpacingMm,
                            panel.Openings,
                            report, $"{panel.PanelId} - Thép phân bố mũ X trái");
                        createdRebars.AddRange(distLeft);
                    }
                }

                // Mũ gối gối phải phương X (X-max vươn sang trái)
                if (!skipEdge1 && !fullX)
                {
                    double x1 = bb.Max.X - hatLenX;
                    double x2 = bb.Max.X + beamAnchorFeet;
                    var hatRight = CreateRebarLineArray(panel.HostFloor, hatXType,
                        x1, x2, bb.Min.Y + ToFeet(50), bb.Max.Y - ToFeet(50), zHatX,
                        XYZ.BasisX, XYZ.BasisY, cfg.HatReinforce.SpacingXMm,
                        panel.Openings,
                        report, $"{panel.PanelId} - Mũ gối X bên phải");
                    createdRebars.AddRange(hatRight);

                    if (cfg.TopDistribution.Enabled)
                    {
                        var distRight = CreateRebarLineArray(panel.HostFloor, distType,
                            bb.Min.Y, bb.Max.Y, x1 + ToFeet(50), bb.Max.X - ToFeet(50), zHatX - ToFeet(15),
                            XYZ.BasisY, XYZ.BasisX, cfg.TopDistribution.SpacingMm,
                            panel.Openings,
                            report, $"{panel.PanelId} - Thép phân bố mũ X phải");
                        createdRebars.AddRange(distRight);
                    }
                }

                // Mũ gối gối dưới phương Y (Y-min vươn lên trên)
                if (!skipEdge0)
                {
                    double y1 = bb.Min.Y - beamAnchorFeet;
                    double y2 = bb.Min.Y + hatLenY;
                    var hatBot = CreateRebarLineArray(panel.HostFloor, hatYType,
                        y1, y2, bb.Min.X + ToFeet(50), bb.Max.X - ToFeet(50), zHatY,
                        XYZ.BasisY, XYZ.BasisX, cfg.HatReinforce.SpacingYMm,
                        panel.Openings,
                        report, $"{panel.PanelId} - Mũ gối Y phía dưới");
                    createdRebars.AddRange(hatBot);

                    if (cfg.TopDistribution.Enabled)
                    {
                        var distBot = CreateRebarLineArray(panel.HostFloor, distType,
                            bb.Min.X, bb.Max.X, bb.Min.Y + ToFeet(50), y2 - ToFeet(50), zHatY - ToFeet(15),
                            XYZ.BasisX, XYZ.BasisY, cfg.TopDistribution.SpacingMm,
                            panel.Openings,
                            report, $"{panel.PanelId} - Thép phân bố mũ Y dưới");
                        createdRebars.AddRange(distBot);
                    }
                }

                // Mũ gối gối trên phương Y (Y-max vươn xuống dưới)
                if (!skipEdge2 && !fullY)
                {
                    double y1 = bb.Max.Y - hatLenY;
                    double y2 = bb.Max.Y + beamAnchorFeet;
                    var hatTop = CreateRebarLineArray(panel.HostFloor, hatYType,
                        y1, y2, bb.Min.X + ToFeet(50), bb.Max.X - ToFeet(50), zHatY,
                        XYZ.BasisY, XYZ.BasisX, cfg.HatReinforce.SpacingYMm,
                        panel.Openings,
                        report, $"{panel.PanelId} - Mũ gối Y phía trên");
                    createdRebars.AddRange(hatTop);

                    if (cfg.TopDistribution.Enabled)
                    {
                        var distTop = CreateRebarLineArray(panel.HostFloor, distType,
                            bb.Min.X, bb.Max.X, y1 + ToFeet(50), bb.Max.Y - ToFeet(50), zHatY - ToFeet(15),
                            XYZ.BasisX, XYZ.BasisY, cfg.TopDistribution.SpacingMm,
                            panel.Openings,
                            report, $"{panel.PanelId} - Thép phân bố mũ Y trên");
                        createdRebars.AddRange(distTop);
                    }
                }
            }

            // ── 4. SPACERS (THÉP CHÂN CHÓ KÊ SÀN) ───────────────────────────
            if (cfg.Spacer.Enabled)
            {
                var chairs = CreateSpacers(panel.HostFloor, chairType, bb, zBotX, zHatX, cfg.Spacer, report, panel.PanelId);
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

        private List<Rebar> CreateRebarLineArray(Floor floor, RebarBarType barType,
            double startDir, double endDir, double startPerp, double endPerp, double zLevel,
            XYZ dirVector, XYZ arrayVector, double spacingMm,
            List<CurveLoop> openings = null,
            RebarGenerationReport report = null, string groupName = "Thép sàn")
        {
            var list = new List<Rebar>();
            if (barType == null || spacingMm <= 0 || endDir <= startDir || endPerp <= startPerp) return list;

            try
            {
                double spacingFeet = UnitUtils.ConvertToInternalUnits(spacingMm, UnitTypeId.Millimeters);
                double coverFeet = ToFeet(25);
                bool isX = (dirVector == XYZ.BasisX);
                XYZ normal = (arrayVector != null && arrayVector.GetLength() > 0.01)
                    ? arrayVector.Normalize()
                    : (isX ? XYZ.BasisY : XYZ.BasisX);

                if (openings != null && openings.Any())
                {
                    // Rải từng thanh thép cắt ngắn hoặc tách đôi khi gặp lỗ mở
                    for (double perp = startPerp; perp <= endPerp; perp += spacingFeet)
                    {
                        var intervals = SlabGeometryHelper.ClipIntervalAgainstOpenings(startDir, endDir, perp, isX, openings, coverFeet);
                        foreach (var seg in intervals)
                        {
                            if (seg.End - seg.Start < 0.5) continue; // Bỏ qua đoạn quá ngắn < 150mm

                            XYZ p1 = isX ? new XYZ(seg.Start, perp, zLevel) : new XYZ(perp, seg.Start, zLevel);
                            XYZ p2 = isX ? new XYZ(seg.End, perp, zLevel) : new XYZ(perp, seg.End, zLevel);

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
                else
                {
                    // Không có lỗ mở: Rải Rebar Array tiêu chuẩn
                    XYZ p1 = isX ? new XYZ(startDir, startPerp, zLevel) : new XYZ(startPerp, startDir, zLevel);
                    XYZ p2 = isX ? new XYZ(endDir, startPerp, zLevel) : new XYZ(startPerp, endDir, zLevel);

                    var curves = new List<Curve> { Line.CreateBound(p1, p2) };
                    Rebar rebar = RebarShapeCreationHelper.CreateFromCurvesSafe(
                        _doc, RebarStyle.Standard, barType, null, null, floor, normal, curves,
                        RebarHookOrientation.Left, RebarHookOrientation.Right);
                    if (rebar != null)
                    {
                        double arrayLen = Math.Abs(endPerp - startPerp);
                        int count = Math.Max(2, (int)Math.Floor(arrayLen / spacingFeet));
                        rebar.GetShapeDrivenAccessor().SetLayoutAsNumberWithSpacing(count, spacingFeet, true, true, true);
                        list.Add(rebar);
                        report?.AddSuccess(1);
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

        private List<Rebar> CreateSpacers(Floor floor, RebarBarType barType, BoundingBoxXYZ bb,
            double zBot, double zTop, SlabSpacerSettings settings, RebarGenerationReport report = null, string panelId = "P")
        {
            var list = new List<Rebar>();
            if (barType == null) return list;

            double stepXFeet = UnitUtils.ConvertToInternalUnits(settings.StepXMm, UnitTypeId.Millimeters);
            double stepYFeet = UnitUtils.ConvertToInternalUnits(settings.StepYMm, UnitTypeId.Millimeters);
            double hookFeet = UnitUtils.ConvertToInternalUnits(settings.HookLenMm, UnitTypeId.Millimeters);

            double hChair = zTop - zBot;
            if (hChair <= 0.1) return list;

            for (double x = bb.Min.X + stepXFeet; x < bb.Max.X - stepXFeet / 2.0; x += stepXFeet)
            {
                for (double y = bb.Min.Y + stepYFeet; y < bb.Max.Y - stepYFeet / 2.0; y += stepYFeet)
                {
                    try
                    {
                        // Thanh Z Chân chó với chiều dài móc tùy chỉnh
                        XYZ p1 = new XYZ(x - hookFeet, y, zBot);
                        XYZ p2 = new XYZ(x, y, zBot);
                        XYZ p3 = new XYZ(x, y, zTop);
                        XYZ p4 = new XYZ(x + hookFeet, y, zTop);

                        var curves = new List<Curve>
                        {
                            Line.CreateBound(p1, p2),
                            Line.CreateBound(p2, p3),
                            Line.CreateBound(p3, p4)
                        };

                        Rebar chair = RebarShapeCreationHelper.CreateFromCurvesSafe(_doc, RebarStyle.Standard, barType, null, null, floor, XYZ.BasisY, curves, RebarHookOrientation.Left, RebarHookOrientation.Right);
                        if (chair != null)
                        {
                            list.Add(chair);
                            report?.AddSuccess(1);
                        }
                    }
                    catch (Exception ex)
                    {
                        report?.AddError(floor, $"{panelId} - Con kê / Thép chân chó (Spacer)", ex);
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
