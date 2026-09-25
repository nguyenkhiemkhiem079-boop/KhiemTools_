using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// 3. Thép hỗ trợ gối dạng thanh thẳng (Support bars theo L/4, L/3, có xét Skip Edge; hook chưa hỗ trợ)
    /// 5. Thép Chân Chó / Con Kê (High Chairs / Spacers với Hook Length)
    /// 6. Thép Gia Cường Lỗ Mở (Opening Trim Bars)
    /// </summary>
    public class SlabRebarGenerator
    {
        private readonly Document _doc;
        private readonly List<RebarBarType> _barTypes;

        public SlabRebarGenerator(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _barTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType)).Cast<RebarBarType>().ToList();
        }

        public string GetPanelInputFingerprint(SlabPanel panel) => RebarPreviewService.Fingerprint(panel, _barTypes);
        public IList<RebarBarType> BarTypes => _barTypes.AsReadOnly();

        public List<Rebar> GeneratePanel(SlabPanel panel, RebarGenerationReport report = null,
            IDictionary<string, string> roleByBarId = null)
        {
            if (panel == null || panel.HostFloor == null)
                throw new ArgumentException("A slab panel with a Floor host is required.", nameof(panel));
            if (panel.Boundary == null)
                throw new InvalidOperationException("Slab reinforcement requires the actual analyzed host boundary; a bounding-box fallback is not supported.");
            if (panel.Config == null) throw new InvalidOperationException("Slab reinforcement configuration is missing.");

            var createdRebars = new List<Rebar>();
            var barTypes = _barTypes;
            var cfg = panel.Config;
            ValidatePanelInputs(panel, cfg, barTypes);
            BoundingBoxXYZ bb = panel.HostFloor.get_BoundingBox(null);
            if (bb == null) throw new InvalidOperationException("Slab host bounds are unavailable; reinforcement cannot be generated safely.");

            double coverTop = panel.CoverTopFeet > 0 ? panel.CoverTopFeet : ToFeet(25);
            double coverBot = panel.CoverBottomFeet > 0 ? panel.CoverBottomFeet : ToFeet(25);

            RebarBarType botXType = cfg.BottomLayer.Enabled ? RequireBarType(barTypes, cfg.BottomLayer.DiaXLabel, "bottom X") : null;
            RebarBarType botYType = cfg.BottomLayer.Enabled ? RequireBarType(barTypes, cfg.BottomLayer.DiaYLabel, "bottom Y") : null;
            RebarBarType topMeshXType = cfg.TopLayer.Enabled ? RequireBarType(barTypes, cfg.TopLayer.DiaXLabel, "top X") : null;
            RebarBarType topMeshYType = cfg.TopLayer.Enabled ? RequireBarType(barTypes, cfg.TopLayer.DiaYLabel, "top Y") : null;
            RebarBarType hatXType = cfg.HatReinforce.Enabled ? RequireBarType(barTypes, cfg.HatReinforce.DiaXLabel, "support X") : null;
            RebarBarType hatYType = cfg.HatReinforce.Enabled ? RequireBarType(barTypes, cfg.HatReinforce.DiaYLabel, "support Y") : null;
            RebarBarType chairType = cfg.Spacer.Enabled ? RequireBarType(barTypes, cfg.Spacer.DiaLabel, "spacer") : null;
            RebarBarType openingType = panel.Openings != null && panel.Openings.Count > 0
                ? botXType ?? topMeshXType ?? hatXType ?? chairType ?? throw new InvalidOperationException("Opening reinforcement needs an enabled mesh/support/spacer bar type; no silent default type is used.")
                : null;

            // Cao độ Z các lớp
            double bottomDiaX = botXType?.BarModelDiameter ?? 0;
            double bottomDiaY = botYType?.BarModelDiameter ?? 0;
            double topDiaX = topMeshXType?.BarModelDiameter ?? 0;
            double topDiaY = topMeshYType?.BarModelDiameter ?? 0;
            double hatDiaX = hatXType?.BarModelDiameter ?? 0;
            double hatDiaY = hatYType?.BarModelDiameter ?? 0;
            double bottomOuterDia = cfg.BottomLayer.InvertLayer ? bottomDiaY : bottomDiaX;
            double bottomInnerDia = cfg.BottomLayer.InvertLayer ? bottomDiaX : bottomDiaY;
            double bottomOuterZ = bb.Min.Z + coverBot + bottomOuterDia / 2.0;
            double bottomInnerZ = bottomOuterZ + (bottomOuterDia + bottomInnerDia) / 2.0;
            double zBotX = cfg.BottomLayer.InvertLayer ? bottomInnerZ : bottomOuterZ;
            double zBotY = cfg.BottomLayer.InvertLayer ? bottomOuterZ : bottomInnerZ;

            double topOuterDia = cfg.TopLayer.InvertLayer ? topDiaY : topDiaX;
            double topInnerDia = cfg.TopLayer.InvertLayer ? topDiaX : topDiaY;
            double topOuterZ = bb.Max.Z - coverTop - topOuterDia / 2.0;
            double topInnerZ = topOuterZ - (topOuterDia + topInnerDia) / 2.0;
            double zTopX = cfg.TopLayer.InvertLayer ? topInnerZ : topOuterZ;
            double zTopY = cfg.TopLayer.InvertLayer ? topOuterZ : topInnerZ;
            double zHatX = cfg.HatReinforce.Enabled ? bb.Max.Z - coverTop - hatDiaX / 2.0 : zTopX;
            double zHatY = cfg.HatReinforce.Enabled ? zHatX - (hatDiaX + hatDiaY) / 2.0 : zTopY;

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

            double coverOffset = Math.Max(coverTop, coverBot);

            // ── 1. BOTTOM LAYER (LƯỚI ĐÁY) ──────────────────────────────────
            if (cfg.BottomLayer.Enabled)
            {
                // Bottom X (Thanh ngang rải theo Y)
                var botX = CreateBoundaryConstrainedRebars(panel.HostFloor, botXType,
                    bMinY + coverOffset, bMaxY - coverOffset, zBotX,
                    isXDirection: true, cfg.BottomLayer.SpacingXMm,
                    panel.Boundary, panel.Openings,
                    report, $"{panel.PanelId} - Thép đáy phương X");
                createdRebars.AddRange(botX);
                RecordRoles(botX, "bottom-x", roleByBarId);

                // Bottom Y (Thanh dọc rải theo X)
                var botY = CreateBoundaryConstrainedRebars(panel.HostFloor, botYType,
                    bMinX + coverOffset, bMaxX - coverOffset, zBotY,
                    isXDirection: false, cfg.BottomLayer.SpacingYMm,
                    panel.Boundary, panel.Openings,
                    report, $"{panel.PanelId} - Thép đáy phương Y");
                createdRebars.AddRange(botY);
                RecordRoles(botY, "bottom-y", roleByBarId);
            }

            // ── 2. TOP LAYER FULL MESH (LƯỚI TRÊN TOÀN DIỆN NẾU BẬT) ──────────
            if (cfg.TopLayer.Enabled)
            {
                var topX = CreateBoundaryConstrainedRebars(panel.HostFloor, topMeshXType,
                    bMinY + coverOffset, bMaxY - coverOffset, zTopX,
                    isXDirection: true, cfg.TopLayer.SpacingXMm,
                    panel.Boundary, panel.Openings,
                    report, $"{panel.PanelId} - Lưới trên full X");
                createdRebars.AddRange(topX);
                RecordRoles(topX, "top-x", roleByBarId);

                var topY = CreateBoundaryConstrainedRebars(panel.HostFloor, topMeshYType,
                    bMinX + coverOffset, bMaxX - coverOffset, zTopY,
                    isXDirection: false, cfg.TopLayer.SpacingYMm,
                    panel.Boundary, panel.Openings,
                    report, $"{panel.PanelId} - Lưới trên full Y");
                createdRebars.AddRange(topY);
                RecordRoles(topY, "top-y", roleByBarId);
            }

            // ── 3. SUPPORT REINFORCEMENT (STRAIGHT BARS; NO HOOKS) ─────────
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

                int supportXStart = createdRebars.Count;
                // Mũ gối gối trái phương X (X-min vươn sang phải)
                if (!skipEdge3)
                {
                    for (double y = bMinY + coverOffset; y <= bMaxY - coverOffset; y += stepY)
                    {
                        var segs = SlabGeometryHelper.GetSlabIntervalsAtCoord(y, isXDirection: true, panel.Boundary, panel.Openings, coverOffset);
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
                        var segs = SlabGeometryHelper.GetSlabIntervalsAtCoord(y, isXDirection: true, panel.Boundary, panel.Openings, coverOffset);
                        foreach (var seg in segs)
                        {
                            double hx1 = Math.Max(seg.Start, seg.End - hatLenX);
                            double hx2 = seg.End;
                            CreateSingleStraightBar(panel.HostFloor, hatXType, new XYZ(hx1, y, zHatX), new XYZ(hx2, y, zHatX), createdRebars, report, "Mũ gối X phải");
                        }
                    }
                }

                RecordRoles(createdRebars.Skip(supportXStart), "support-x", roleByBarId);
                int supportYStart = createdRebars.Count;
                // Mũ gối gối dưới phương Y (Y-min vươn lên trên)
                if (!skipEdge0)
                {
                    for (double x = bMinX + coverOffset; x <= bMaxX - coverOffset; x += stepX)
                    {
                        var segs = SlabGeometryHelper.GetSlabIntervalsAtCoord(x, isXDirection: false, panel.Boundary, panel.Openings, coverOffset);
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
                        var segs = SlabGeometryHelper.GetSlabIntervalsAtCoord(x, isXDirection: false, panel.Boundary, panel.Openings, coverOffset);
                        foreach (var seg in segs)
                        {
                            double hy1 = Math.Max(seg.Start, seg.End - hatLenY);
                            double hy2 = seg.End;
                            CreateSingleStraightBar(panel.HostFloor, hatYType, new XYZ(x, hy1, zHatY), new XYZ(x, hy2, zHatY), createdRebars, report, "Mũ gối Y trên");
                        }
                    }
                }
                RecordRoles(createdRebars.Skip(supportYStart), "support-y", roleByBarId);
            }

            // ── 4. SPACERS (THÉP CHÂN CHÓ KÊ SÀN) ───────────────────────────
            if (cfg.Spacer.Enabled)
            {
                var chairs = CreateSpacers(panel.HostFloor, chairType, bMinX, bMaxX, bMinY, bMaxY, zBotX, zHatX, panel.Boundary, panel.Openings, cfg.Spacer, report, panel.PanelId);
                createdRebars.AddRange(chairs);
                RecordRoles(chairs, "spacer", roleByBarId);
            }

            // ── 5. THÉP GIA CƯỜNG BO VIỀN LỖ MỞ (OPENING TRIM REBARS) ──────────
            if (panel.Openings != null && panel.Openings.Any())
            {
                double zOpeningBottom = bb.Min.Z + coverBot + openingType.BarModelDiameter / 2.0;
                double zOpeningTop = bb.Max.Z - coverTop - openingType.BarModelDiameter / 2.0;
                var trimmers = CreateOpeningTrimmerBars(panel.HostFloor, openingType, panel.Boundary, panel.Openings, coverOffset, zOpeningBottom, zOpeningTop, report);
                createdRebars.AddRange(trimmers);
                RecordRoles(trimmers, "opening", roleByBarId);
            }

            return createdRebars;
        }

        private static void RecordRoles(IEnumerable<Rebar> bars, string role, IDictionary<string, string> roleByBarId)
        {
            if (roleByBarId == null) return;
            foreach (Rebar bar in bars ?? Enumerable.Empty<Rebar>())
                if (bar != null) roleByBarId[bar.Id.Value.ToString(CultureInfo.InvariantCulture)] = role;
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
                Openings = profile.InnerOpenings ?? new List<CurveLoop>(),
                Edges = profile.OuterBoundary == null
                    ? new List<SlabPanelEdge>()
                    : profile.OuterBoundary.Select((curve, index) => new SlabPanelEdge
                    {
                        EdgeIndex = index,
                        EdgeCurve = curve,
                        EdgeType = SlabPanelEdgeType.BeamSupport
                    }).ToList()
            };

            panel.Config.BottomLayer.Enabled = settings.BottomMeshEnabled;
            panel.Config.BottomLayer.InvertLayer = settings.BottomInvertLayer;
            panel.Config.BottomLayer.DiaXLabel = settings.BotXDiaLabel;
            panel.Config.BottomLayer.SpacingXMm = settings.BotXSpacingMm;
            panel.Config.BottomLayer.DiaYLabel = settings.BotYDiaLabel;
            panel.Config.BottomLayer.SpacingYMm = settings.BotYSpacingMm;

            panel.Config.TopLayer.Enabled = settings.TopMeshEnabled;
            panel.Config.TopLayer.InvertLayer = settings.TopInvertLayer;
            panel.Config.TopLayer.DiaXLabel = settings.TopMeshXDiaLabel;
            panel.Config.TopLayer.SpacingXMm = settings.TopMeshXSpacingMm;
            panel.Config.TopLayer.DiaYLabel = settings.TopMeshYDiaLabel;
            panel.Config.TopLayer.SpacingYMm = settings.TopMeshYSpacingMm;

            panel.Config.HatReinforce.Enabled = settings.SupportEnabled;
            panel.Config.HatReinforce.DiaXLabel = settings.TopXDiaLabel;
            panel.Config.HatReinforce.SpacingXMm = settings.TopXSpacingMm;
            panel.Config.HatReinforce.DiaYLabel = settings.TopYDiaLabel;
            panel.Config.HatReinforce.SpacingYMm = settings.TopYSpacingMm;
            panel.Config.HatReinforce.HatFactor = settings.TopExtensionRatio;
            panel.Config.HatReinforce.IsFullSpan = settings.SupportFullSpan;
            panel.Config.HatReinforce.HookDownEdge = false;

            panel.Config.Spacer.Enabled = settings.EnableChairRebar;
            panel.Config.Spacer.DiaLabel = settings.ChairDiaLabel;
            panel.Config.Spacer.StepXMm = settings.ChairSpacingXmm;
            panel.Config.Spacer.StepYMm = settings.ChairSpacingYmm;
            panel.Config.Spacer.HookLenMm = settings.ChairHookLenMm;
            panel.Config.TopDistribution.Enabled = false;

            return GeneratePanel(panel, report);
        }

        private List<Rebar> CreateBoundaryConstrainedRebars(Floor floor, RebarBarType barType,
            double startPerp, double endPerp, double zLevel,
            bool isXDirection, double spacingMm,
            CurveLoop boundary, List<CurveLoop> openings,
            RebarGenerationReport report = null, string groupName = "Thép sàn")
        {
            var list = new List<Rebar>();
            if (barType == null) throw new InvalidOperationException("A required slab reinforcement bar type was not resolved.");
            if (!IsFinitePositive(spacingMm) || endPerp <= startPerp)
                throw new InvalidOperationException("Slab mesh spacing must be finite and positive, and the distribution interval must be non-empty.");

            try
            {
                double spacingFeet = UnitUtils.ConvertToInternalUnits(spacingMm, UnitTypeId.Millimeters);
                double coverFeet = RebarCoverHelper.GetFloorCover(floor, RebarFace.Exterior);
                XYZ normal = isXDirection ? XYZ.BasisY : XYZ.BasisX;

                for (double perp = startPerp; perp <= endPerp; perp += spacingFeet)
                {
                    var intervals = SlabGeometryHelper.GetSlabIntervalsAtCoord(
                        perp, isXDirection, boundary, openings, coverFeet);

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
                        else
                        {
                            ReportCreationFailure(report, floor, groupName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RecordFailure(report, floor, groupName, ex);
            }

            return list;
        }

        private List<Rebar> CreateOpeningTrimmerBars(Floor floor, RebarBarType barType,
            CurveLoop boundary, List<CurveLoop> openings, double coverFeet, double zBot, double zTop, RebarGenerationReport report = null)
        {
            var list = new List<Rebar>();
            if (openings == null || !openings.Any()) return list;
            if (barType == null) throw new InvalidOperationException("Opening reinforcement has no explicitly resolved bar type.");

            double barDia = barType.BarModelDiameter;
            double anchLen = barDia * 40; // Lb = 40d theo Eurocode 2
            foreach (var op in openings)
            {
                if (!TryGetAxisAlignedRectangularOpeningBounds(op, out double minX, out double maxX,
                    out double minY, out double maxY))
                {
                    var unsupported = new InvalidOperationException(
                        "Automatic opening trim bars currently support axis-aligned rectangular openings only. Other opening shapes require an explicit reinforcement detail.");
                    if (report == null) throw unsupported;
                    report.AddError(floor, "Opening trim geometry", unsupported);
                    continue;
                }

                double width = maxX - minX;
                double height = maxY - minY;
                if (width < 0.5 || height < 0.5) continue; // Bỏ qua lỗ quá nhỏ < 150mm

                bool fullAnchorageFits =
                    ContainsFullInterval(boundary, openings, minY - coverFeet, true, minX - anchLen, maxX + anchLen) &&
                    ContainsFullInterval(boundary, openings, maxY + coverFeet, true, minX - anchLen, maxX + anchLen) &&
                    ContainsFullInterval(boundary, openings, minX - coverFeet, false, minY - anchLen, maxY + anchLen) &&
                    ContainsFullInterval(boundary, openings, maxX + coverFeet, false, minY - anchLen, maxY + anchLen);
                if (!fullAnchorageFits)
                {
                    var unsupported = new InvalidOperationException(
                        "Opening trim bars require full 40d anchorage inside the concrete boundary on all four sides; this opening is too close to a slab edge or another void.");
                    if (report == null) throw unsupported;
                    report.AddError(floor, "Opening trim anchorage", unsupported);
                    continue;
                }

                // Thép gia cường 4 cạnh lỗ mở (Bottom & Top)
                double[] zLevels = new double[] { zBot, zTop };
                foreach (double z in zLevels)
                {
                    // 2 thanh song song cạnh dưới
                    XYZ b1 = new XYZ(minX - anchLen, minY - coverFeet, z);
                    XYZ b2 = new XYZ(maxX + anchLen, minY - coverFeet, z);
                    CreateSingleStraightBar(floor, barType, b1, b2, list, report, "Gia cường lỗ mở cạnh dưới");

                    // 2 thanh song song cạnh trên
                    XYZ t1 = new XYZ(minX - anchLen, maxY + coverFeet, z);
                    XYZ t2 = new XYZ(maxX + anchLen, maxY + coverFeet, z);
                    CreateSingleStraightBar(floor, barType, t1, t2, list, report, "Gia cường lỗ mở cạnh trên");

                    // 2 thanh song song cạnh trái
                    XYZ l1 = new XYZ(minX - coverFeet, minY - anchLen, z);
                    XYZ l2 = new XYZ(minX - coverFeet, maxY + anchLen, z);
                    CreateSingleStraightBar(floor, barType, l1, l2, list, report, "Gia cường lỗ mở cạnh trái");

                    // 2 thanh song song cạnh phải
                    XYZ r1 = new XYZ(maxX + coverFeet, minY - anchLen, z);
                    XYZ r2 = new XYZ(maxX + coverFeet, maxY + anchLen, z);
                    CreateSingleStraightBar(floor, barType, r1, r2, list, report, "Gia cường lỗ mở cạnh phải");
                }
            }

            return list;
        }

        private static bool ContainsFullInterval(CurveLoop boundary, List<CurveLoop> openings,
            double fixedCoord, bool isXDirection, double start, double end)
        {
            double tolerance = UnitUtils.ConvertToInternalUnits(0.1, UnitTypeId.Millimeters);
            return SlabGeometryHelper.GetSlabIntervalsAtCoord(fixedCoord, isXDirection, boundary, openings, 0)
                .Any(interval => interval.Start <= start + tolerance && interval.End >= end - tolerance);
        }

        private static bool TryGetAxisAlignedRectangularOpeningBounds(CurveLoop opening,
            out double minX, out double maxX, out double minY, out double maxY)
        {
            minX = maxX = minY = maxY = 0;
            if (opening == null) return false;
            Curve[] edges = opening.ToArray();
            if (edges.Length != 4 || edges.Any(edge => !(edge is Line))) return false;

            XYZ[] endpoints = edges.SelectMany(edge => new[] { edge.GetEndPoint(0), edge.GetEndPoint(1) }).ToArray();
            double tolerance = UnitUtils.ConvertToInternalUnits(0.1, UnitTypeId.Millimeters);
            if (endpoints.Max(point => point.Z) - endpoints.Min(point => point.Z) > tolerance) return false;

            double[] xValues = DistinctCoordinates(endpoints.Select(point => point.X), tolerance);
            double[] yValues = DistinctCoordinates(endpoints.Select(point => point.Y), tolerance);
            if (xValues.Length != 2 || yValues.Length != 2) return false;
            minX = xValues[0]; maxX = xValues[1]; minY = yValues[0]; maxY = yValues[1];
            if (maxX - minX <= tolerance || maxY - minY <= tolerance) return false;

            foreach (XYZ point in endpoints)
            {
                bool isCorner = (Math.Abs(point.X - minX) <= tolerance || Math.Abs(point.X - maxX) <= tolerance) &&
                    (Math.Abs(point.Y - minY) <= tolerance || Math.Abs(point.Y - maxY) <= tolerance);
                if (!isCorner) return false;
            }

            foreach (Curve edge in edges)
            {
                XYZ start = edge.GetEndPoint(0);
                XYZ end = edge.GetEndPoint(1);
                bool horizontal = Math.Abs(start.Y - end.Y) <= tolerance && Math.Abs(start.X - end.X) > tolerance;
                bool vertical = Math.Abs(start.X - end.X) <= tolerance && Math.Abs(start.Y - end.Y) > tolerance;
                if (!horizontal && !vertical) return false;
            }
            return true;
        }

        private static double[] DistinctCoordinates(IEnumerable<double> coordinates, double tolerance)
        {
            var distinct = new List<double>();
            foreach (double coordinate in coordinates.OrderBy(value => value))
            {
                if (distinct.Count == 0 || Math.Abs(coordinate - distinct[distinct.Count - 1]) > tolerance)
                    distinct.Add(coordinate);
            }
            return distinct.ToArray();
        }

        private void CreateSingleStraightBar(Floor floor, RebarBarType barType, XYZ p1, XYZ p2,
            List<Rebar> list, RebarGenerationReport report, string desc)
        {
            try
            {
                if (barType == null) throw new InvalidOperationException("A required slab bar type was not resolved.");
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
                else
                {
                    ReportCreationFailure(report, floor, desc);
                }
            }
            catch (Exception ex)
            {
                RecordFailure(report, floor, desc, ex);
            }
        }

        private List<Rebar> CreateSpacers(Floor floor, RebarBarType barType,
            double minX, double maxX, double minY, double maxY,
            double zBot, double zTop, CurveLoop boundary, List<CurveLoop> openings,
            SlabSpacerSettings settings, RebarGenerationReport report = null, string panelId = "P")
        {
            var list = new List<Rebar>();
            if (barType == null) throw new InvalidOperationException("Spacer reinforcement has no explicitly resolved bar type.");

            double stepXFeet = UnitUtils.ConvertToInternalUnits(settings.StepXMm, UnitTypeId.Millimeters);
            double stepYFeet = UnitUtils.ConvertToInternalUnits(settings.StepYMm, UnitTypeId.Millimeters);
            double footLenFeet = UnitUtils.ConvertToInternalUnits(settings.HookLenMm, UnitTypeId.Millimeters); // Chân A/E
            double bridgeWidthFeet = ToFeet(150); // Cầu trên C: 150mm
            double barDia = barType.BarModelDiameter;

            double hChair = zTop - zBot;
            if (hChair <= 0.1)
                throw new InvalidOperationException("Spacer height is non-positive; enable compatible bottom and upper reinforcement layers.");

            int supportedChairStations = 0;

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

                        if (!IsChairGeometryInsideSlab(p1, p2, p3, p4, p5, p6, boundary, openings))
                            continue;
                        supportedChairStations++;

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
                        else
                        {
                            ReportCreationFailure(report, floor, $"{panelId} - Con kê");
                        }
                    }
                    catch (Exception ex)
                    {
                        RecordFailure(report, floor, $"{panelId} - Con kê / Thép chân chó (Spacer Shape 31)", ex);
                    }
                }
            }

            if (supportedChairStations == 0)
                RecordFailure(report, floor, $"{panelId} - Spacer", new InvalidOperationException("No complete spacer shape fits inside the slab concrete boundary and outside its openings."));

            return list;
        }

        private static bool IsChairGeometryInsideSlab(XYZ p1, XYZ p2, XYZ p3, XYZ p4, XYZ p5, XYZ p6,
            CurveLoop boundary, List<CurveLoop> openings)
        {
            if (!SlabGeometryHelper.IsPointInsideSlab(p2, boundary, openings) ||
                !SlabGeometryHelper.IsPointInsideSlab(p3, boundary, openings) ||
                !SlabGeometryHelper.IsPointInsideSlab(p4, boundary, openings) ||
                !SlabGeometryHelper.IsPointInsideSlab(p5, boundary, openings)) return false;
            return IsPlanIntervalInsideSlab(p1.X, p1.Y, p2.Y, false, boundary, openings) &&
                IsPlanIntervalInsideSlab(p3.Y, p3.X, p4.X, true, boundary, openings) &&
                IsPlanIntervalInsideSlab(p5.X, p5.Y, p6.Y, false, boundary, openings);
        }

        private static bool IsPlanIntervalInsideSlab(double fixedCoordinate, double start, double end,
            bool isXDirection, CurveLoop boundary, List<CurveLoop> openings)
        {
            double minimum = Math.Min(start, end);
            double maximum = Math.Max(start, end);
            var cuts = new List<double> { minimum, maximum };
            AddPlanBoundaryCuts(boundary, fixedCoordinate, isXDirection, minimum, maximum, cuts);
            foreach (CurveLoop opening in openings ?? new List<CurveLoop>())
                AddPlanBoundaryCuts(opening, fixedCoordinate, isXDirection, minimum, maximum, cuts);
            cuts = cuts.OrderBy(value => value).Aggregate(new List<double>(), (unique, value) =>
            {
                if (unique.Count == 0 || Math.Abs(value - unique[unique.Count - 1]) > 1e-8) unique.Add(value);
                return unique;
            });
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                double coordinate = (cuts[i] + cuts[i + 1]) / 2.0;
                XYZ sample = isXDirection ? new XYZ(coordinate, fixedCoordinate, 0) : new XYZ(fixedCoordinate, coordinate, 0);
                if (!SlabGeometryHelper.IsPointInsideSlab(sample, boundary, openings)) return false;
            }
            return true;
        }

        private static void AddPlanBoundaryCuts(CurveLoop loop, double fixedCoordinate, bool isXDirection,
            double minimum, double maximum, ICollection<double> cuts)
        {
            if (loop == null) return;
            const double tolerance = 1e-8;
            foreach (Curve curve in loop)
            {
                XYZ p0 = curve.GetEndPoint(0);
                XYZ p1 = curve.GetEndPoint(1);
                if (isXDirection)
                {
                    if (Math.Abs(p0.Y - fixedCoordinate) <= tolerance && Math.Abs(p1.Y - fixedCoordinate) <= tolerance)
                    {
                        if (p0.X > minimum && p0.X < maximum) cuts.Add(p0.X);
                        if (p1.X > minimum && p1.X < maximum) cuts.Add(p1.X);
                    }
                    else if (Math.Abs(p0.X - p1.X) <= tolerance && fixedCoordinate >= Math.Min(p0.Y, p1.Y) - tolerance && fixedCoordinate <= Math.Max(p0.Y, p1.Y) + tolerance && p0.X > minimum && p0.X < maximum)
                        cuts.Add(p0.X);
                }
                else
                {
                    if (Math.Abs(p0.X - fixedCoordinate) <= tolerance && Math.Abs(p1.X - fixedCoordinate) <= tolerance)
                    {
                        if (p0.Y > minimum && p0.Y < maximum) cuts.Add(p0.Y);
                        if (p1.Y > minimum && p1.Y < maximum) cuts.Add(p1.Y);
                    }
                    else if (Math.Abs(p0.Y - p1.Y) <= tolerance && fixedCoordinate >= Math.Min(p0.X, p1.X) - tolerance && fixedCoordinate <= Math.Max(p0.X, p1.X) + tolerance && p0.Y > minimum && p0.Y < maximum)
                        cuts.Add(p0.Y);
                }
            }
        }

        private double ParseHatFactor(string factorStr)
        {
            switch ((factorStr ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "L/3": return 1.0 / 3.0;
                case "L/4": return 1.0 / 4.0;
                case "L/5": return 1.0 / 5.0;
                default: throw new InvalidOperationException("Support reinforcement factor must be one of L/3, L/4 or L/5.");
            }
        }

        internal static RebarBarType FindBarType(IList<RebarBarType> list, string diaLabel)
        {
            if (list == null || string.IsNullOrWhiteSpace(diaLabel)) return null;
            string search = diaLabel.Replace("d", "").Replace("Φ", "").Replace("ϕ", "").Trim();

            RebarBarType exactName = list.FirstOrDefault(bt =>
                string.Equals(bt.Name, diaLabel.Trim(), StringComparison.OrdinalIgnoreCase));
            if (exactName != null) return exactName;

            double target;
            bool hasTarget = double.TryParse(search, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out target)
                || double.TryParse(search, out target);

            foreach (var bt in list)
            {
                double diaMm = UnitUtils.ConvertFromInternalUnits(bt.BarModelDiameter, UnitTypeId.Millimeters);
                if (hasTarget && Math.Abs(diaMm - target) < 0.5) return bt;
            }
            return null;
        }

        private static void ValidatePanelInputs(SlabPanel panel, SlabPanelRebarConfig config, IList<RebarBarType> barTypes)
        {
            if (config.BottomLayer == null || config.TopLayer == null || config.HatReinforce == null ||
                config.TopDistribution == null || config.Spacer == null || config.Anchors == null || config.Tolerances == null)
                throw new InvalidOperationException("Slab configuration is incomplete; refresh the panel settings before preview/create.");
            if (config.TopDistribution.Enabled)
                throw new InvalidOperationException("Top distribution bars are not supported by the production slab generator; disable this legacy option.");
            if (config.HatReinforce.HookDownEdge)
                throw new InvalidOperationException("Support-hat hook-down is not supported by the production slab generator; disable this legacy option.");
            if (config.Anchors.BeamAnchorAMm != 250 || config.Anchors.SlabAnchorBMm != 300)
                throw new InvalidOperationException("Beam/slab edge anchorage settings are not applied by the production slab generator.");
            if (config.Tolerances.RoundingMm != 10)
                throw new InvalidOperationException("Slab bar-length rounding is not implemented; use the supported default value.");
            if (!string.IsNullOrEmpty(config.BottomLayer.ExtraParam) || !string.IsNullOrEmpty(config.TopLayer.ExtraParam))
                throw new InvalidOperationException("Legacy slab layer parameters are not applied by the production slab generator.");
            if (panel.Edges == null || panel.Edges.Any(edge => edge == null)) throw new InvalidOperationException("Slab panel edge settings are missing or invalid.");
            if (panel.Edges.Any(edge => edge.SkipBottomMesh || edge.EdgeType != SlabPanelEdgeType.BeamSupport || edge.SupportingBeamId != null))
                throw new InvalidOperationException("Only the default beam-support edge classification and enabled bottom mesh are currently supported; edge type, beam mapping and bottom-edge skips are not applied.");
            if (config.BottomLayer.Enabled)
            {
                RequireBarType(barTypes, config.BottomLayer.DiaXLabel, "bottom X");
                RequireBarType(barTypes, config.BottomLayer.DiaYLabel, "bottom Y");
                ValidateSpacing(config.BottomLayer.SpacingXMm, 50, 500, "bottom X");
                ValidateSpacing(config.BottomLayer.SpacingYMm, 50, 500, "bottom Y");
            }
            if (config.TopLayer.Enabled)
            {
                RequireBarType(barTypes, config.TopLayer.DiaXLabel, "top X");
                RequireBarType(barTypes, config.TopLayer.DiaYLabel, "top Y");
                ValidateSpacing(config.TopLayer.SpacingXMm, 50, 500, "top X");
                ValidateSpacing(config.TopLayer.SpacingYMm, 50, 500, "top Y");
            }
            if (config.HatReinforce.Enabled)
            {
                if (panel.Edges.Count != 4)
                    throw new InvalidOperationException("Per-edge support-hat skips are supported only for four-edge slab panels.");
                RequireBarType(barTypes, config.HatReinforce.DiaXLabel, "support X");
                RequireBarType(barTypes, config.HatReinforce.DiaYLabel, "support Y");
                ValidateSpacing(config.HatReinforce.SpacingXMm, 50, 500, "support X");
                ValidateSpacing(config.HatReinforce.SpacingYMm, 50, 500, "support Y");
                switch ((config.HatReinforce.HatFactor ?? string.Empty).Trim().ToUpperInvariant())
                {
                    case "L/3": case "L/4": case "L/5": break;
                    default: throw new InvalidOperationException("Support reinforcement factor must be one of L/3, L/4 or L/5.");
                }
            }
            if (config.Spacer.Enabled)
            {
                if (!config.BottomLayer.Enabled || (!config.TopLayer.Enabled && !config.HatReinforce.Enabled))
                    throw new InvalidOperationException("Spacers require an enabled bottom mat and an enabled upper mesh or support layer.");
                RequireBarType(barTypes, config.Spacer.DiaLabel, "spacer");
                ValidateSpacing(config.Spacer.StepXMm, 300, 2000, "spacer X step");
                ValidateSpacing(config.Spacer.StepYMm, 300, 2000, "spacer Y step");
                ValidateSpacing(config.Spacer.HookLenMm, 50, 300, "spacer foot length");
            }
            if (panel.Openings != null && panel.Openings.Count > 0 &&
                !config.BottomLayer.Enabled && !config.TopLayer.Enabled && !config.HatReinforce.Enabled && !config.Spacer.Enabled)
                throw new InvalidOperationException("Opening trim bars require an enabled mesh/support bar type to define their diameter.");
            ValidateSpacing(config.Tolerances.MinSpanMm, 500, 3000, "minimum span");
        }

        private static RebarBarType RequireBarType(IList<RebarBarType> barTypes, string label, string role)
        {
            RebarBarType type = FindBarType(barTypes, label);
            if (type == null)
                throw new InvalidOperationException($"Could not resolve the selected bar type '{label}' for {role}; no substitute type was selected.");
            return type;
        }

        private static void ValidateSpacing(double value, double minimum, double maximum, string role)
        {
            if (!IsFinitePositive(value) || value < minimum || value > maximum)
                throw new InvalidOperationException($"{role} must be between {minimum:0.###} and {maximum:0.###} mm.");
        }

        private static bool IsFinitePositive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

        private static void RecordFailure(RebarGenerationReport report, Element host, string category, Exception exception)
        {
            if (report == null) throw new InvalidOperationException(category + " failed: " + exception.Message, exception);
            report.AddError(host, category, exception);
        }

        private static double ToFeet(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);

        private static void ReportCreationFailure(RebarGenerationReport report, Element host, string groupName)
        {
            var exception = new InvalidOperationException(RebarShapeCreationHelper.LastFailureReason ?? "Revit không tạo được thanh thép.");
            if (report == null) throw exception;
            report.AddError(host, groupName, exception);
        }
    }
}
