using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.RebarTool.Models;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Engine cốt lõi chịu trách nhiệm dựng toàn bộ 3D Rebar cho Móng (Structural Foundations):
    /// 1. Thép Lưới Dưới (Bottom Mat - X & Y)
    /// 2. Thép Lưới Trên (Top Mat - X & Y nếu bật)
    /// 3. Thép Đai Mép Móng / Thép Chữ U Gia Cường (Side Ties)
    /// 4. Thép Chờ Cột (Column Dowels / Starter Bars)
    /// </summary>
    public class FoundationRebarGenerator
    {
        private readonly Document _doc;

        public FoundationRebarGenerator(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public List<Rebar> Generate(FoundationProfile profile, FoundationRebarSettings settings, RebarGenerationReport report = null)
        {
            if (profile == null || profile.FoundationElement == null || settings == null)
                return new List<Rebar>();

            var createdRebars = new List<Rebar>();

            var barTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .ToList();

            if (!barTypes.Any()) return createdRebars;

            RebarBarType botXType = FindBarType(barTypes, settings.BotXDiaLabel);
            RebarBarType botYType = FindBarType(barTypes, settings.BotYDiaLabel);
            RebarBarType topXType = FindBarType(barTypes, settings.TopXDiaLabel);
            RebarBarType topYType = FindBarType(barTypes, settings.TopYDiaLabel);
            RebarBarType sideType = FindBarType(barTypes, settings.SideTieDiaLabel);
            RebarBarType dowelType = FindBarType(barTypes, settings.DowelDiaLabel);

            BoundingBoxXYZ bb = profile.BoundingBox;
            if (bb == null) return createdRebars;

            double coverMm = settings.CustomCoverMm > 0 ? settings.CustomCoverMm : 50.0;
            double coverFeet = UnitUtils.ConvertToInternalUnits(coverMm, UnitTypeId.Millimeters);

            double zBotX = bb.Min.Z + coverFeet + (botXType?.BarModelDiameter ?? 0.04) / 2.0;
            double zBotY = zBotX + (botXType?.BarModelDiameter ?? 0.04);
            double zTopX = bb.Max.Z - coverFeet - (topXType?.BarModelDiameter ?? 0.04) / 2.0;
            double zTopY = zTopX - (topXType?.BarModelDiameter ?? 0.04);

            double footingH = Math.Abs(bb.Max.Z - bb.Min.Z);
            double vertHookH = settings.CustomHookHeightMm > 0
                ? UnitUtils.ConvertToInternalUnits(settings.CustomHookHeightMm, UnitTypeId.Millimeters)
                : Math.Max(0.2, footingH - 2.0 * coverFeet);

            // ── 1. BOTTOM MAT (X & Y) ─────────────────────────────────────────
            var botXRebars = CreateMatRebarSet(profile.FoundationElement, botXType, RebarHookOrientation.Left,
                bb.Min.X + coverFeet, bb.Max.X - coverFeet, bb.Min.Y + coverFeet, bb.Max.Y - coverFeet,
                zBotX, XYZ.BasisX, XYZ.BasisY, settings.BotXSpacingMm, vertHookH, settings.BotXHookUp, report, "Thép đáy móng X");
            createdRebars.AddRange(botXRebars);

            var botYRebars = CreateMatRebarSet(profile.FoundationElement, botYType, RebarHookOrientation.Right,
                bb.Min.Y + coverFeet, bb.Max.Y - coverFeet, bb.Min.X + coverFeet, bb.Max.X - coverFeet,
                zBotY, XYZ.BasisY, XYZ.BasisX, settings.BotYSpacingMm, vertHookH, settings.BotYHookUp, report, "Thép đáy móng Y");
            createdRebars.AddRange(botYRebars);

            // ── 2. TOP MAT (X & Y - Optional) ────────────────────────────────
            if (settings.EnableTopMesh)
            {
                var topXRebars = CreateMatRebarSet(profile.FoundationElement, topXType, RebarHookOrientation.Left,
                    bb.Min.X + coverFeet, bb.Max.X - coverFeet, bb.Min.Y + coverFeet, bb.Max.Y - coverFeet,
                    zTopX, XYZ.BasisX, XYZ.BasisY, settings.TopXSpacingMm, vertHookH, !settings.TopXHookDown, report, "Thép mặt móng X");
                createdRebars.AddRange(topXRebars);

                var topYRebars = CreateMatRebarSet(profile.FoundationElement, topYType, RebarHookOrientation.Right,
                    bb.Min.Y + coverFeet, bb.Max.Y - coverFeet, bb.Min.X + coverFeet, bb.Max.X - coverFeet,
                    zTopY, XYZ.BasisY, XYZ.BasisX, settings.TopYSpacingMm, vertHookH, !settings.TopYHookDown, report, "Thép mặt móng Y");
                createdRebars.AddRange(topYRebars);
            }

            // ── 3. COLUMN STARTER BARS & DOWEL STIRRUPS (Thép Chờ Cột & Đai Cổ Móng) ──────
            if (settings.EnableColumnDowels && dowelType != null)
            {
                var dowels = CreateColumnDowels(profile, dowelType, settings, coverFeet, zBotX, report);
                createdRebars.AddRange(dowels);

                if (settings.EnableDowelStirrups)
                {
                    RebarBarType dowelStirrupType = FindBarType(barTypes, settings.DowelStirrupDiaLabel);
                    var dowelStirrups = CreateDowelStirrups(profile, dowelStirrupType, settings, zBotX, bb.Max.Z, report);
                    createdRebars.AddRange(dowelStirrups);
                }
            }

            // ── 4. PERIMETER EDGE U-BARS / STIRRUPS (Thép Chữ U Gia Cường Mép Móng) ────────
            if (settings.EnablePerimeterUStirrups)
            {
                RebarBarType uBarType = FindBarType(barTypes, settings.PerimeterStirrupDiaLabel);
                var uBars = CreatePerimeterUBars(profile, uBarType, settings, coverFeet, zBotX, zTopX, report);
                createdRebars.AddRange(uBars);
            }

            RebarLifecycleManager.TagRebars(createdRebars, profile.FoundationElement, "Foundation", "FootingReinforcement");
            return createdRebars;
        }

        private List<Rebar> CreateMatRebarSet(FamilyInstance foundation, RebarBarType barType, RebarHookOrientation hookOrient,
            double startDir, double endDir, double startPerp, double endPerp, double zLevel,
            XYZ dirVector, XYZ arrayVector, double spacingMm, double hookH, bool isHookUp,
            RebarGenerationReport report = null, string barGroupName = "Thép lưới móng")
        {
            var list = new List<Rebar>();
            if (barType == null || spacingMm <= 0) return list;

            try
            {
                double spacingFeet = UnitUtils.ConvertToInternalUnits(spacingMm, UnitTypeId.Millimeters);

                double signZ = isHookUp ? 1.0 : -1.0;
                XYZ pStart = (dirVector == XYZ.BasisX)
                    ? new XYZ(startDir, (startPerp + endPerp) / 2.0, zLevel)
                    : new XYZ((startPerp + endPerp) / 2.0, startDir, zLevel);

                XYZ pEnd = (dirVector == XYZ.BasisX)
                    ? new XYZ(endDir, (startPerp + endPerp) / 2.0, zLevel)
                    : new XYZ((startPerp + endPerp) / 2.0, endDir, zLevel);

                XYZ pHookStart = pStart + XYZ.BasisZ * signZ * hookH;
                XYZ pHookEnd = pEnd + XYZ.BasisZ * signZ * hookH;

                var curves = new List<Curve>
                {
                    Line.CreateBound(pHookStart, pStart),
                    Line.CreateBound(pStart, pEnd),
                    Line.CreateBound(pEnd, pHookEnd)
                };

                Rebar rebar = RebarShapeCreationHelper.CreateFromCurvesSafe(_doc, RebarStyle.Standard, barType, null, null, foundation, arrayVector, curves, hookOrient, hookOrient);
                if (rebar != null)
                {
                    double arrayLen = Math.Abs(endPerp - startPerp);
                    int count = Math.Max(2, (int)Math.Floor(arrayLen / spacingFeet));
                    rebar.GetShapeDrivenAccessor().SetLayoutAsNumberWithSpacing(count, spacingFeet, true, true, true);
                    list.Add(rebar);
                    report?.AddSuccess(1);
                }
            }
            catch (Exception ex)
            {
                report?.AddError(foundation, barGroupName, ex);
            }

            return list;
        }

        private List<Rebar> CreateColumnDowels(FoundationProfile profile, RebarBarType barType, FoundationRebarSettings settings, double coverFeet, double zBot, RebarGenerationReport report = null)
        {
            var list = new List<Rebar>();
            if (barType == null) return list;

            try
            {
                BoundingBoxXYZ bb = profile.BoundingBox;
                double legFeet = UnitUtils.ConvertToInternalUnits(settings.DowelFootLegMm, UnitTypeId.Millimeters);
                double extFeet = UnitUtils.ConvertToInternalUnits(settings.DowelExtensionMm, UnitTypeId.Millimeters);

                // Ưu tiên 1: Kích thước cột thực tế trên móng
                // Ưu tiên 2: Kích thước người dùng nhập tay trong cài đặt
                double colSizeX;
                double colSizeY;
                XYZ center;
                double rot = 0.0;

                if (profile.SupportedColumn != null && profile.SupportedColumn.IsDetected)
                {
                    colSizeX = profile.SupportedColumn.SizeXFeet;
                    colSizeY = profile.SupportedColumn.SizeYFeet;
                    center = profile.SupportedColumn.Center;
                    rot = profile.SupportedColumn.RotationRad;
                }
                else
                {
                    colSizeX = UnitUtils.ConvertToInternalUnits(settings.ManualColumnSizeXMm, UnitTypeId.Millimeters);
                    colSizeY = UnitUtils.ConvertToInternalUnits(settings.ManualColumnSizeYMm, UnitTypeId.Millimeters);
                    center = profile.Center;
                    rot = 0.0;
                    report?.AddWarning($"Không tự động tìm thấy Cột trên móng; sử dụng kích thước cổ cột cấu hình: {settings.ManualColumnSizeXMm}x{settings.ManualColumnSizeYMm}mm");
                }

                double halfBx = colSizeX / 2.0;
                double halfBy = colSizeY / 2.0;

                int nx = Math.Max(2, settings.DowelQtyX);
                int ny = Math.Max(2, settings.DowelQtyY);

                double stepX = (nx > 1) ? (colSizeX) / (nx - 1) : 0;
                double stepY = (ny > 1) ? (colSizeY) / (ny - 1) : 0;

                int barIndex = 0;
                for (int i = 0; i < nx; i++)
                {
                    for (int j = 0; j < ny; j++)
                    {
                        if (i > 0 && i < nx - 1 && j > 0 && j < ny - 1) continue;

                        double lx = -halfBx + i * stepX;
                        double ly = -halfBy + j * stepY;

                        // So le 50%: thanh lẻ dâng cao thêm 1.3 * L0
                        double curExtFeet = (settings.StaggeredDowels && barIndex % 2 == 1) ? (extFeet * 1.3) : extFeet;
                        double zTop = bb.Max.Z + curExtFeet;
                        barIndex++;

                        // Hướng bẻ chân quỳ: Xòe ra ngoài (Outward) hoặc Úp vào trong (Inward)
                        double dirSign = settings.DowelLegInward ? -1.0 : 1.0;
                        double legDx = (lx >= 0) ? (legFeet * dirSign) : (-legFeet * dirSign);

                        XYZ pLegEnd = FoundationGeometryHelper.TransformLocalToWorld(center, rot, lx + legDx, ly, zBot);
                        XYZ pCorner = FoundationGeometryHelper.TransformLocalToWorld(center, rot, lx, ly, zBot);
                        XYZ pTop = FoundationGeometryHelper.TransformLocalToWorld(center, rot, lx, ly, zTop);

                        var curves = new List<Curve>
                        {
                            Line.CreateBound(pLegEnd, pCorner),
                            Line.CreateBound(pCorner, pTop)
                        };

                        XYZ norm = (pCorner - pLegEnd).CrossProduct(pTop - pCorner);
                        if (norm.GetLength() < 0.001) norm = XYZ.BasisY;
                        else norm = norm.Normalize();

                        Rebar dowel = RebarShapeCreationHelper.CreateFromCurvesSafe(
                            _doc, RebarStyle.Standard, barType, null, null, profile.FoundationElement,
                            norm, curves, RebarHookOrientation.Left, RebarHookOrientation.Right, report);

                        if (dowel != null)
                        {
                            list.Add(dowel);
                            report?.AddSuccess(1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                report?.AddError(profile.FoundationElement, "Thép chờ cổ cột (Column Dowels)", ex);
            }

            return list;
        }

        private List<Rebar> CreateDowelStirrups(FoundationProfile profile, RebarBarType barType, FoundationRebarSettings settings, double zBot, double zTopFdn, RebarGenerationReport report = null)
        {
            var list = new List<Rebar>();
            if (barType == null) return list;

            try
            {
                double colSizeX;
                double colSizeY;
                XYZ center;
                double rot = 0.0;

                if (profile.SupportedColumn != null && profile.SupportedColumn.IsDetected)
                {
                    colSizeX = profile.SupportedColumn.SizeXFeet;
                    colSizeY = profile.SupportedColumn.SizeYFeet;
                    center = profile.SupportedColumn.Center;
                    rot = profile.SupportedColumn.RotationRad;
                }
                else
                {
                    colSizeX = UnitUtils.ConvertToInternalUnits(settings.ManualColumnSizeXMm, UnitTypeId.Millimeters);
                    colSizeY = UnitUtils.ConvertToInternalUnits(settings.ManualColumnSizeYMm, UnitTypeId.Millimeters);
                    center = profile.Center;
                    rot = 0.0;
                }

                double halfBx = colSizeX / 2.0;
                double halfBy = colSizeY / 2.0;

                int qty = Math.Max(2, settings.DowelStirrupQty);
                double stepZ = (zTopFdn - zBot) / (qty + 1);

                for (int k = 1; k <= qty; k++)
                {
                    double z = zBot + k * stepZ;

                    XYZ p1 = FoundationGeometryHelper.TransformLocalToWorld(center, rot, -halfBx, -halfBy, z);
                    XYZ p2 = FoundationGeometryHelper.TransformLocalToWorld(center, rot, halfBx, -halfBy, z);
                    XYZ p3 = FoundationGeometryHelper.TransformLocalToWorld(center, rot, halfBx, halfBy, z);
                    XYZ p4 = FoundationGeometryHelper.TransformLocalToWorld(center, rot, -halfBx, halfBy, z);

                    var curves = new List<Curve>
                    {
                        Line.CreateBound(p1, p2),
                        Line.CreateBound(p2, p3),
                        Line.CreateBound(p3, p4),
                        Line.CreateBound(p4, p1)
                    };

                    Rebar stirrup = RebarShapeCreationHelper.CreateFromCurvesSafe(
                        _doc, RebarStyle.StirrupTie, barType, null, null, profile.FoundationElement,
                        XYZ.BasisZ, curves, RebarHookOrientation.Left, RebarHookOrientation.Right, report);

                    if (stirrup != null)
                    {
                        list.Add(stirrup);
                        report?.AddSuccess(1);
                    }
                }
            }
            catch (Exception ex)
            {
                report?.AddError(profile.FoundationElement, "Thép đai cổ cột (Dowel Stirrups)", ex);
            }

            return list;
        }

        private List<Rebar> CreatePerimeterUBars(FoundationProfile profile, RebarBarType barType, FoundationRebarSettings settings, double coverFeet, double zBot, double zTop, RebarGenerationReport report = null)
        {
            var list = new List<Rebar>();
            if (barType == null) return list;

            try
            {
                BoundingBoxXYZ bb = profile.BoundingBox;
                double spacingFeet = UnitUtils.ConvertToInternalUnits(settings.PerimeterStirrupSpacingMm, UnitTypeId.Millimeters);

                double uLegLen = Math.Min(profile.ThicknessFeet * 0.4, 1.2); // Chiều dài chân thép chữ U mép móng

                // Thép đai U mép móng 4 cạnh (Cạnh X_Min, X_Max, Y_Min, Y_Max)
                for (double x = bb.Min.X + coverFeet + spacingFeet; x < bb.Max.X - coverFeet - spacingFeet / 2.0; x += spacingFeet)
                {
                    // U-bar mép Y_Min
                    XYZ p1 = new XYZ(x, bb.Min.Y + coverFeet + uLegLen, zTop);
                    XYZ p2 = new XYZ(x, bb.Min.Y + coverFeet, zTop);
                    XYZ p3 = new XYZ(x, bb.Min.Y + coverFeet, zBot);
                    XYZ p4 = new XYZ(x, bb.Min.Y + coverFeet + uLegLen, zBot);

                    var curvesYMin = new List<Curve> { Line.CreateBound(p1, p2), Line.CreateBound(p2, p3), Line.CreateBound(p3, p4) };
                    Rebar uBarYMin = RebarShapeCreationHelper.CreateFromCurvesSafe(_doc, RebarStyle.Standard, barType, null, null, profile.FoundationElement, XYZ.BasisX, curvesYMin, RebarHookOrientation.Left, RebarHookOrientation.Right);
                    if (uBarYMin != null)
                    {
                        list.Add(uBarYMin);
                        report?.AddSuccess(1);
                    }
                }
            }
            catch (Exception ex)
            {
                report?.AddError(profile.FoundationElement, "Thép chữ U mép móng (Perimeter U-bars)", ex);
            }

            return list;
        }

        private RebarBarType FindBarType(List<RebarBarType> list, string diaLabel)
        {
            if (string.IsNullOrWhiteSpace(diaLabel)) return list.FirstOrDefault();
            string search = diaLabel.Replace("d", "").Replace("Φ", "").Replace("ϕ", "").Trim();

            foreach (var bt in list)
            {
                if (bt.Name.Contains(search)) return bt;
                double diaMm = UnitUtils.ConvertFromInternalUnits(bt.BarModelDiameter, UnitTypeId.Millimeters);
                if (Math.Abs(diaMm - double.Parse(search)) < 1.0) return bt;
            }
            return list.FirstOrDefault();
        }
    }
}
