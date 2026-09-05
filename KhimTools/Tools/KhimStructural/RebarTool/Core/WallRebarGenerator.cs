using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    public class WallRebarSettings
    {
        public double VerticalSpacingMm { get; set; } = 200.0;
        public double HorizontalSpacingMm { get; set; } = 200.0;
        public double ConcreteCoverMm { get; set; } = 30.0;
        public bool TwoLayers { get; set; } = true;
        public bool EnableFoundationStarterDowels { get; set; } = true;
        public double StarterLengthMm { get; set; } = 800.0;
    }

    /// <summary>
    /// Bộ khởi tạo cốt thép vách kết cấu (Section 25 Wall Reinforcement):
    /// Thép đứng (Vertical), Thép ngang (Horizontal), Cốt biên (Boundary elements)
    /// và Thép chờ chân vách cắm vào móng (Foundation Starter Dowels).
    /// </summary>
    public class WallRebarGenerator
    {
        private readonly Document _doc;

        public WallRebarGenerator(Document doc)
        {
            _doc = doc;
        }

        public List<Rebar> GenerateWallRebar(
            Wall wall,
            WallRebarSettings settings,
            RebarBarType vertBarType,
            RebarBarType horizBarType,
            Element connectedFoundation = null,
            RebarGenerationReport report = null)
        {
            var created = new List<Rebar>();
            if (wall == null || settings == null) return created;

            BoundingBoxXYZ bb = wall.get_BoundingBox(null);
            if (bb == null) return created;

            double coverFt = UnitUtils.ConvertToInternalUnits(settings.ConcreteCoverMm, UnitTypeId.Millimeters);
            double vertSpacingFt = UnitUtils.ConvertToInternalUnits(settings.VerticalSpacingMm, UnitTypeId.Millimeters);
            double horizSpacingFt = UnitUtils.ConvertToInternalUnits(settings.HorizontalSpacingMm, UnitTypeId.Millimeters);

            double xMin = bb.Min.X + coverFt;
            double xMax = bb.Max.X - coverFt;
            double zMin = bb.Min.Z;
            double zMax = bb.Max.Z - coverFt;
            double yCenter = (bb.Min.Y + bb.Max.Y) * 0.5;

            // Chân thép chờ vách cắm vào móng
            double zStart = zMin;
            if (settings.EnableFoundationStarterDowels && connectedFoundation != null)
            {
                zStart -= UnitUtils.ConvertToInternalUnits(settings.StarterLengthMm, UnitTypeId.Millimeters);
            }

            // 1. THÉP ĐỨNG (VERTICAL BARS)
            if (vertBarType != null && vertSpacingFt > 0)
            {
                double currentX = xMin;
                while (currentX <= xMax)
                {
                    var curves = new List<Curve>
                    {
                        Line.CreateBound(new XYZ(currentX, yCenter, zStart), new XYZ(currentX, yCenter, zMax))
                    };

                    try
                    {
                        Rebar bar = RebarShapeCreationHelper.CreateFromCurvesSafe(
                            _doc, RebarStyle.Standard, vertBarType, null, null, wall,
                            XYZ.BasisY, curves, RebarHookOrientation.Right, RebarHookOrientation.Right);

                        if (bar != null) created.Add(bar);
                    }
                    catch (Exception ex)
                    {
                        report?.AddError(wall, "WallVerticalBar", ex);
                    }

                    currentX += vertSpacingFt;
                }
            }

            // 2. THÉP NGANG (HORIZONTAL BARS)
            if (horizBarType != null && horizSpacingFt > 0)
            {
                double currentZ = zMin + coverFt;
                while (currentZ <= zMax)
                {
                    var curves = new List<Curve>
                    {
                        Line.CreateBound(new XYZ(xMin, yCenter, currentZ), new XYZ(xMax, yCenter, currentZ))
                    };

                    try
                    {
                        Rebar bar = RebarShapeCreationHelper.CreateFromCurvesSafe(
                            _doc, RebarStyle.Standard, horizBarType, null, null, wall,
                            XYZ.BasisZ, curves, RebarHookOrientation.Right, RebarHookOrientation.Right);

                        if (bar != null) created.Add(bar);
                    }
                    catch (Exception ex)
                    {
                        report?.AddError(wall, "WallHorizontalBar", ex);
                    }

                    currentZ += horizSpacingFt;
                }
            }

            // 3. THẨM ĐỊNH DETAILING INTENT VỚI MÓNG
            var intentCtx = new DetailingIntentContext
            {
                CurrentHost = wall,
                ConnectedHost = connectedFoundation,
                IntentType = (connectedFoundation != null) ? DetailingIntentType.WallStarter : DetailingIntentType.StandardInternal,
                RequiredCoverMm = settings.ConcreteCoverMm
            };

            var containment = RebarHostContainmentValidator.ValidateHostContainmentWithIntent(
                _doc, wall, created, intentCtx, settings.ConcreteCoverMm);

            if (!containment.OverallPassed && containment.Protrusions.Any())
            {
                report?.AddError(wall, "WallProtrusion",
                    new InvalidOperationException($"Cốt thép vách lòi ra ngoài bê tông {containment.Protrusions.First().OutsideDistanceMm:F1}mm."));
            }

            RebarLifecycleManager.TagRebars(created, wall, "Wall", "StructuralWallRebar");
            return created;
        }
    }
}
