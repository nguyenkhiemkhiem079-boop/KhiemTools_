using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core.Workflow;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Strict Revit adapter for the currently supported Manual v1 slice: explicitly
    /// positioned, straight longitudinal bars fully contained in one verified Beam.
    /// It deliberately does not route through the legacy BeamRebarInput defaults.
    /// </summary>
    public sealed class BeamManualRebarGeneratorV1
    {
        private readonly Document _document;

        public BeamManualRebarGeneratorV1(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public string GetDocumentKey()
        {
            if (_document.ProjectInformation == null || string.IsNullOrWhiteSpace(_document.ProjectInformation.UniqueId))
                throw new InvalidOperationException("A stable Revit project identity is unavailable; Manual v1 cannot safely bind saved plans.");
            string path = _document.PathName ?? string.Empty;
            string title = _document.Title ?? string.Empty;
            return WorkflowFingerprint.Compute(new[] { _document.ProjectInformation.UniqueId, path, title });
        }

        public BeamManualDetailingPlanV1 CreateBlankPlan(FamilyInstance beam)
        {
            BeamGeometryHelper.BeamProfile profile = RequireBeamProfile(beam);
            return new BeamManualDetailingPlanV1
            {
                DocumentKey = GetDocumentKey(),
                HostUniqueId = beam.UniqueId,
                Frame = SnapshotFrame(beam, profile),
                BarGroups = new List<BeamManualBarGroupV1>(),
                StirrupGroups = new List<BeamManualStirrupGroupV1>(),
                Covers = new List<BeamManualCoverV1>(),
                SupportEnds = new List<BeamManualSupportEndV1>()
            };
        }

        public BeamManualFrameV1 GetCurrentFrame(FamilyInstance beam)
        {
            return SnapshotFrame(beam, RequireBeamProfile(beam));
        }

        public string Fingerprint(BeamManualDetailingPlanV1 plan, FamilyInstance beam)
        {
            ValidateHostIdentity(plan, beam);
            BeamManualFrameV1 current = GetCurrentFrame(beam);
            if (!FrameMatches(plan.Frame, current))
                throw new InvalidOperationException("Beam geometry or local orientation changed since this Manual v1 plan was authored. Refresh and review the plan before solving.");
            BeamManualPlanValidatorV1.Validate(plan, requireResolvedReferences: true).ThrowIfInvalid();
            string[] typeState = ResolvePlanReferences(plan).Select(type => string.Join("|", type.UniqueId,
                type.VersionGuid.ToString("D"), type.Name,
                type.BarModelDiameter.ToString("R", CultureInfo.InvariantCulture))).ToArray();
            return WorkflowFingerprint.Compute(new[] { BeamManualLayoutBuilderV1.Fingerprint(plan), current.HostGeometrySignature,
                GetDocumentKey(), beam.UniqueId, beam.VersionGuid.ToString("D") }.Concat(typeState));
        }

        public IList<Rebar> Generate(BeamManualDetailingPlanV1 plan, FamilyInstance beam,
            IDictionary<string, string> roleByBarId = null)
        {
            if (_document.IsModifiable == false)
                throw new InvalidOperationException("Manual Beam generation requires a caller-owned active Revit transaction.");
            ValidateHostIdentity(plan, beam);
            BeamGeometryHelper.BeamProfile profile = RequireBeamProfile(beam);
            BeamManualFrameV1 actualFrame = SnapshotFrame(beam, profile);
            if (!FrameMatches(plan.Frame, actualFrame))
                throw new InvalidOperationException("Beam geometry or local orientation changed since this Manual v1 plan was authored.");
            BeamManualPlanValidatorV1.Validate(plan, requireResolvedReferences: true).ThrowIfInvalid();

            if ((plan.StirrupGroups ?? new List<BeamManualStirrupGroupV1>()).Any(group => group != null && group.Enabled))
                throw new InvalidOperationException("Manual stirrup generation is not enabled in this adapter. Disable the group or use the separately labeled Legacy Supported workflow; no stirrup is substituted.");
            if (plan.SupportEnds.Count != 2 || plan.SupportEnds.Any(end => end == null || end.Mode != BeamManualSupportMode.UserDefinedFreeEndWithinHost))
                throw new InvalidOperationException("This Manual v1 generator supports only explicit free ends within the Beam host. Verified-support extension is not enabled.");

            ResolvePlanReferences(plan);
            IReadOnlyList<BeamManualResolvedBarV1> bars = BeamManualLayoutBuilderV1.Build(plan);
            var created = new List<Rebar>();
            foreach (BeamManualResolvedBarV1 resolved in bars)
            {
                BeamManualBarGroupV1 group = plan.BarGroups.Single(item => item.Enabled && item.GroupId == resolved.GroupId);
                RebarBarType type = ResolveBarType(group.BarType);
                BeamManualBarV1 source = group.Bars.Single(item =>
                    Math.Abs(item.XMm - resolved.Centerline[0].XMm) <= BeamManualPlanValidatorV1.GeometryToleranceMm &&
                    Math.Abs(item.YMm - resolved.Centerline[0].YMm) <= BeamManualPlanValidatorV1.GeometryToleranceMm &&
                    Math.Abs(item.StartStationMm - resolved.Centerline[0].StationMm) <= BeamManualPlanValidatorV1.GeometryToleranceMm &&
                    Math.Abs(item.EndStationMm - resolved.Centerline[1].StationMm) <= BeamManualPlanValidatorV1.GeometryToleranceMm &&
                    string.Equals(item.LayerId, resolved.LayerId, StringComparison.Ordinal));
                if (source.StartDetail.Kind != BeamManualEndDetailKind.StraightAtStation ||
                    source.FinishDetail.Kind != BeamManualEndDetailKind.StraightAtStation)
                    throw new InvalidOperationException("Only STRAIGHT_AT_STATION end details are currently supported by Manual v1 generation.");

                XYZ start = ToWorld(profile, resolved.Centerline[0]);
                XYZ finish = ToWorld(profile, resolved.Centerline[1]);
                if (start.DistanceTo(finish) <= 1e-9)
                    throw new InvalidOperationException("A Manual v1 bar resolved to a zero-length Revit centerline.");
                var curves = new List<Curve> { Line.CreateBound(start, finish) };
                Rebar bar = RebarShapeCreationHelper.CreateFromCurvesSafe(_document, RebarStyle.Standard,
                    type, null, null, beam, profile.RightVector, curves,
                    allowLegacyFallback: false);
                if (bar == null || !bar.IsValidObject)
                    throw new InvalidOperationException("Revit could not create Manual bar group " + resolved.GroupId + ". " + RebarShapeCreationHelper.LastFailureReason);
                if (bar.GetHostId() != beam.Id || bar.GetTypeId() != type.Id || bar.Quantity != 1)
                    throw new InvalidOperationException("Revit changed the requested Manual bar host, exact type, or single-bar quantity; refusing substituted output.");
                created.Add(bar);
                if (roleByBarId != null)
                    roleByBarId[bar.Id.Value.ToString(CultureInfo.InvariantCulture)] = group.Role == BeamManualBarRole.UserDefined
                        ? "manual-user:" + (group.UserLabel ?? string.Empty)
                        : "manual-" + group.Role.ToString().ToLowerInvariant();
            }
            return created;
        }

        private IEnumerable<RebarBarType> ResolvePlanReferences(BeamManualDetailingPlanV1 plan)
        {
            return (plan.BarGroups ?? new List<BeamManualBarGroupV1>()).Where(group => group != null && group.Enabled).Select(group => ResolveBarType(group.BarType))
                .Concat((plan.StirrupGroups ?? new List<BeamManualStirrupGroupV1>()).Where(group => group != null && group.Enabled).Select(group => ResolveBarType(group.BarType)))
                .GroupBy(type => type.UniqueId, StringComparer.Ordinal).Select(group => group.First()).ToArray();
        }

        private RebarBarType ResolveBarType(BeamManualElementReferenceV1 reference)
        {
            if (reference == null || reference.State != BeamManualReferenceState.Resolved || reference.DocumentKey != GetDocumentKey())
                throw new InvalidOperationException("The exact RebarBarType reference is unresolved or belongs to a different Revit document. Rebind it explicitly.");
            Element element = _document.GetElement(reference.UniqueId);
            RebarBarType type = element as RebarBarType;
            if (type == null || !type.IsValidObject || type.Document != _document || type.Name != reference.Name)
                throw new InvalidOperationException("The stored exact RebarBarType identity no longer resolves; no diameter-based substitution is allowed.");
            double diameterMm = UnitUtils.ConvertFromInternalUnits(type.BarModelDiameter, UnitTypeId.Millimeters);
            if (Math.Abs(diameterMm - reference.DiameterMm) > BeamManualPlanValidatorV1.GeometryToleranceMm)
                throw new InvalidOperationException("The exact RebarBarType diameter changed since the plan was saved.");
            return type;
        }

        private void ValidateHostIdentity(BeamManualDetailingPlanV1 plan, FamilyInstance beam)
        {
            if (plan == null || beam == null || !beam.IsValidObject || beam.Document != _document ||
                beam.Category == null || beam.Category.BuiltInCategory != BuiltInCategory.OST_StructuralFraming ||
                beam.StructuralType != Autodesk.Revit.DB.Structure.StructuralType.Beam)
                throw new InvalidOperationException("A valid Structural Framing Beam in the active document is required.");
            if (!string.Equals(plan.HostUniqueId, beam.UniqueId, StringComparison.Ordinal) ||
                !string.Equals(plan.DocumentKey, GetDocumentKey(), StringComparison.Ordinal))
                throw new InvalidOperationException("Manual plan document/host identity mismatch; explicit rebind is required.");
        }

        private BeamGeometryHelper.BeamProfile RequireBeamProfile(FamilyInstance beam)
        {
            if (beam == null || !beam.IsValidObject || beam.Document != _document)
                throw new InvalidOperationException("A valid Beam in the active Revit document is required.");
            return BeamGeometryHelper.GetBeamProfile(beam);
        }

        private static BeamManualFrameV1 SnapshotFrame(FamilyInstance beam, BeamGeometryHelper.BeamProfile profile)
        {
            double Mm(double feet) => UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);
            string signature = WorkflowFingerprint.Compute(new[]
            {
                beam.UniqueId, beam.VersionGuid.ToString("D"),
                Point(profile.StartPoint), Point(profile.EndPoint),
                Mm(profile.B).ToString("R", CultureInfo.InvariantCulture),
                Mm(profile.H).ToString("R", CultureInfo.InvariantCulture),
                Mm(profile.Length).ToString("R", CultureInfo.InvariantCulture),
                Point(profile.Direction), Point(profile.RightVector), Point(profile.UpVector)
            });
            return new BeamManualFrameV1
            {
                WidthMm = Mm(profile.B), HeightMm = Mm(profile.H), LengthMm = Mm(profile.Length),
                DirectionX = profile.Direction.X, DirectionY = profile.Direction.Y, DirectionZ = profile.Direction.Z,
                RightX = profile.RightVector.X, RightY = profile.RightVector.Y, RightZ = profile.RightVector.Z,
                UpX = profile.UpVector.X, UpY = profile.UpVector.Y, UpZ = profile.UpVector.Z,
                HostGeometrySignature = signature
            };
        }

        private static XYZ ToWorld(BeamGeometryHelper.BeamProfile profile, BeamManualPointV1 point)
        {
            double station = UnitUtils.ConvertToInternalUnits(point.StationMm, UnitTypeId.Millimeters);
            double x = UnitUtils.ConvertToInternalUnits(point.XMm, UnitTypeId.Millimeters);
            double y = UnitUtils.ConvertToInternalUnits(point.YMm, UnitTypeId.Millimeters);
            return BeamGeometryHelper.TransformLocalToWorld(profile, x, y, station);
        }

        private static bool FrameMatches(BeamManualFrameV1 planned, BeamManualFrameV1 current)
        {
            if (planned == null || current == null || !string.Equals(planned.HostGeometrySignature, current.HostGeometrySignature, StringComparison.Ordinal)) return false;
            double tolerance = BeamManualPlanValidatorV1.GeometryToleranceMm;
            return Math.Abs(planned.WidthMm - current.WidthMm) <= tolerance && Math.Abs(planned.HeightMm - current.HeightMm) <= tolerance &&
                Math.Abs(planned.LengthMm - current.LengthMm) <= tolerance &&
                Math.Abs(planned.DirectionX - current.DirectionX) <= 1e-8 && Math.Abs(planned.DirectionY - current.DirectionY) <= 1e-8 && Math.Abs(planned.DirectionZ - current.DirectionZ) <= 1e-8 &&
                Math.Abs(planned.RightX - current.RightX) <= 1e-8 && Math.Abs(planned.RightY - current.RightY) <= 1e-8 && Math.Abs(planned.RightZ - current.RightZ) <= 1e-8 &&
                Math.Abs(planned.UpX - current.UpX) <= 1e-8 && Math.Abs(planned.UpY - current.UpY) <= 1e-8 && Math.Abs(planned.UpZ - current.UpZ) <= 1e-8;
        }

        private static string Point(XYZ point) => string.Join(",",
            point.X.ToString("R", CultureInfo.InvariantCulture),
            point.Y.ToString("R", CultureInfo.InvariantCulture),
            point.Z.ToString("R", CultureInfo.InvariantCulture));
    }

    internal static class BeamManualValidationExtensionsV1
    {
        public static void ThrowIfInvalid(this BeamManualValidationResultV1 result)
        {
            if (result == null || !result.IsValid)
                throw new InvalidOperationException("Manual Beam plan is blocked:" + Environment.NewLine +
                    (result == null ? "No validation result." : result.ToDiagnosticText()));
        }
    }
}
