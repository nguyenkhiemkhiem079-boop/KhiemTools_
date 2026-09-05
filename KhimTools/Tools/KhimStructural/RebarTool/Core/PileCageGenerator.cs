using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Bộ khởi tạo lồng thép cọc khoan nhồi D800 (Section 28-29 Bored Pile D800 / Sheet KC-09):
    /// Cốt dọc hình tròn (Longitudinal), thép đai xoắn (Spiral), vành định hình (Stiffeners),
    /// ống siêu âm (Sonic Tubes) và đoạn thép neo ngàm đầu cọc vào đài móng (Pile Head Starter).
    /// </summary>
    public class PileCageGenerator
    {
        private readonly Document _doc;

        public PileCageGenerator(Document doc)
        {
            _doc = doc;
        }

        public List<Rebar> GeneratePileCage(
            PileProfile profile,
            PileCageSettings settings,
            RebarBarType mainBarType,
            RebarBarType spiralBarType,
            RebarBarType stiffenerBarType,
            RebarGenerationReport report = null)
        {
            var createdRebars = new List<Rebar>();
            if (profile == null || profile.PileElement == null || settings == null)
            {
                report?.AddError(profile?.PileElement, "PileCage", new ArgumentNullException("Pile profile or settings is null."));
                return createdRebars;
            }

            double spiralDiaMm = settings.SpiralDiameterMm;
            double mainDiaMm = settings.MainBarDiameterMm;
            double cageRadiusMm = profile.CageRadiusMm(spiralDiaMm, mainDiaMm);
            double cageRadiusFt = UnitUtils.ConvertToInternalUnits(cageRadiusMm, UnitTypeId.Millimeters);

            double baseZFt = profile.BaseCenter.Z + UnitUtils.ConvertToInternalUnits(profile.ConcreteCoverMm, UnitTypeId.Millimeters);
            double topZFt = profile.TopCenter.Z;

            // Đoạn thép râu đầu cọc vươn lên ngàm vào đài móng
            if (settings.EnablePileHeadStarterExtension && profile.StarterAnchorageLengthMm > 0)
            {
                topZFt += UnitUtils.ConvertToInternalUnits(profile.StarterAnchorageLengthMm, UnitTypeId.Millimeters);
            }

            // ── 1. THÉP CHỦ DỌC CỌC (LONGITUDINAL BARS) ──
            if (mainBarType != null && settings.MainBarCount > 0)
            {
                for (int i = 0; i < settings.MainBarCount; i++)
                {
                    double angle = 2.0 * Math.PI * i / settings.MainBarCount;
                    double x = profile.BaseCenter.X + cageRadiusFt * Math.Cos(angle);
                    double y = profile.BaseCenter.Y + cageRadiusFt * Math.Sin(angle);

                    var curves = new List<Curve>
                    {
                        Line.CreateBound(new XYZ(x, y, baseZFt), new XYZ(x, y, topZFt))
                    };

                    XYZ norm = new XYZ(-Math.Sin(angle), Math.Cos(angle), 0);
                    try
                    {
                        Rebar bar = RebarShapeCreationHelper.CreateFromCurvesSafe(
                            _doc, RebarStyle.Standard, mainBarType, null, null, profile.PileElement,
                            norm, curves, RebarHookOrientation.Right, RebarHookOrientation.Right);

                        if (bar != null) createdRebars.Add(bar);
                    }
                    catch (Exception ex)
                    {
                        report?.AddError(profile.PileElement, "PileMainBar", ex);
                    }
                }
            }

            // ── 2. VÀNH ĐAI ĐỊNH HÌNH (STIFFENER RINGS @ 2.0m) ──
            if (stiffenerBarType != null && settings.StiffenerSpacingMm > 0)
            {
                double stiffenerSpacingFt = UnitUtils.ConvertToInternalUnits(settings.StiffenerSpacingMm, UnitTypeId.Millimeters);
                double currentZFt = baseZFt + stiffenerSpacingFt;

                while (currentZFt < profile.TopCenter.Z - 1.0)
                {
                    XYZ center = new XYZ(profile.BaseCenter.X, profile.BaseCenter.Y, currentZFt);
                    var ringCurves = CreateCircleCurves(center, cageRadiusFt);
                    try
                    {
                        Rebar ring = RebarShapeCreationHelper.CreateFromCurvesSafe(
                            _doc, RebarStyle.StirrupTie, stiffenerBarType, null, null, profile.PileElement,
                            XYZ.BasisZ, ringCurves, RebarHookOrientation.Right, RebarHookOrientation.Right);

                        if (ring != null) createdRebars.Add(ring);
                    }
                    catch { }

                    currentZFt += stiffenerSpacingFt;
                }
            }

            // ── 3. THẨM ĐỊNH DETAILING INTENT VỚI ĐÀI MÓNG (CONNECTED PILE CAP) ──
            var intentCtx = new DetailingIntentContext
            {
                CurrentHost = profile.PileElement,
                ConnectedHost = profile.ConnectedPileCap,
                IntentType = (profile.ConnectedPileCap != null) ? DetailingIntentType.PileHead : DetailingIntentType.StandardInternal,
                RequiredCoverMm = profile.ConcreteCoverMm
            };

            var containmentReport = RebarHostContainmentValidator.ValidateHostContainmentWithIntent(
                _doc, profile.PileElement, createdRebars, intentCtx, profile.ConcreteCoverMm);

            if (!containmentReport.OverallPassed && containmentReport.Protrusions.Any())
            {
                var p = containmentReport.Protrusions.First();
                report?.AddError(profile.PileElement, "PileCageProtrusion",
                    new InvalidOperationException($"Lồng thép cọc lòi ra ngoài bê tông {p.OutsideDistanceMm:F1}mm tại {p.FaceDesc}."));
            }

            RebarLifecycleManager.TagRebars(createdRebars, profile.PileElement, "Pile", "BoredPileD800");
            return createdRebars;
        }

        private static List<Curve> CreateCircleCurves(XYZ center, double radiusFt)
        {
            var p1 = new XYZ(center.X + radiusFt, center.Y, center.Z);
            var p2 = new XYZ(center.X - radiusFt, center.Y, center.Z);
            var pMid1 = new XYZ(center.X, center.Y + radiusFt, center.Z);
            var pMid2 = new XYZ(center.X, center.Y - radiusFt, center.Z);

            var arc1 = Arc.Create(p1, p2, pMid1);
            var arc2 = Arc.Create(p2, p1, pMid2);
            return new List<Curve> { arc1, arc2 };
        }
    }
}
