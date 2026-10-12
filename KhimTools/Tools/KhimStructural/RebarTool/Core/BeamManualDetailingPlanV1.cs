using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace KhimTools.RebarTool.Core
{
    public enum BeamManualBarRole { Unspecified, TopContinuous, BottomContinuous, TopLeftExtra, TopRightExtra, BottomMidExtra, SideLeft, SideRight, UserDefined }
    public enum BeamManualEndDetailKind { Unspecified, Unsupported, StraightAtStation, ExplicitCurveReturn, ExplicitLoadedRevitHook }
    public enum BeamManualHookOrientation { Left, Right }
    public enum BeamManualReferenceState { Unresolved, Resolved }
    public enum BeamManualCoverFace { Unspecified, Top, Bottom, Left, Right, StirrupOutside }
    public enum BeamManualCoverSource { Unspecified, RevitFaceAssignment, ExplicitUserInput }
    public enum BeamManualSupportMode { Unspecified, Unsupported, VerifiedColumn, VerifiedStructuralBeam, UserDefinedFreeEndWithinHost }
    public enum BeamManualEnd { Unspecified, Start, Finish }

    /// <summary>Identity only; a Revit adapter must resolve this exact identity in the active document.</summary>
    public sealed class BeamManualElementReferenceV1
    {
        public string DocumentKey { get; set; }
        public string UniqueId { get; set; }
        public string Name { get; set; }
        public double DiameterMm { get; set; }
        public BeamManualReferenceState State { get; set; }
    }

    public sealed class BeamManualElementIdentityV1
    {
        public string DocumentKey { get; set; }
        public string UniqueId { get; set; }
        public string Name { get; set; }
        public BeamManualReferenceState State { get; set; }
    }

    /// <summary>Physical-section-centered local axes and extents, canonicalized in millimetres.</summary>
    public sealed class BeamManualFrameV1
    {
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public double LengthMm { get; set; }
        public double DirectionX { get; set; }
        public double DirectionY { get; set; }
        public double DirectionZ { get; set; }
        public double RightX { get; set; }
        public double RightY { get; set; }
        public double RightZ { get; set; }
        public double UpX { get; set; }
        public double UpY { get; set; }
        public double UpZ { get; set; }
        public string HostGeometrySignature { get; set; }
    }

    public sealed class BeamManualPointV1
    {
        public double StationMm { get; set; }
        public double XMm { get; set; }
        public double YMm { get; set; }
    }

    /// <summary>Detached 3D point used by the pure line-segment interference calculation.</summary>
    public struct BeamManualPoint3V1
    {
        public BeamManualPoint3V1(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
    }

    public static class BeamManualSegmentDistanceV1
    {
        /// <summary>Minimum squared Euclidean distance between finite 3D segments.</summary>
        public static double SquaredDistance(BeamManualPoint3V1 p0, BeamManualPoint3V1 p1, BeamManualPoint3V1 q0, BeamManualPoint3V1 q1)
        {
            double ux = p1.X - p0.X, uy = p1.Y - p0.Y, uz = p1.Z - p0.Z;
            double vx = q1.X - q0.X, vy = q1.Y - q0.Y, vz = q1.Z - q0.Z;
            double wx = p0.X - q0.X, wy = p0.Y - q0.Y, wz = p0.Z - q0.Z;
            double a = ux * ux + uy * uy + uz * uz;
            double b = ux * vx + uy * vy + uz * vz;
            double c = vx * vx + vy * vy + vz * vz;
            double d = ux * wx + uy * wy + uz * wz;
            double e = vx * wx + vy * wy + vz * wz;
            const double epsilon = 1e-24;
            double s, t;
            if (a <= epsilon && c <= epsilon)
                return wx * wx + wy * wy + wz * wz;
            if (a <= epsilon)
            {
                s = 0;
                t = Clamp(e / c);
            }
            else if (c <= epsilon)
            {
                t = 0;
                s = Clamp(-d / a);
            }
            else
            {
                double denominator = a * c - b * b;
                s = denominator <= epsilon ? 0 : Clamp((b * e - c * d) / denominator);
                t = (b * s + e) / c;
                if (t < 0) { t = 0; s = Clamp(-d / a); }
                else if (t > 1) { t = 1; s = Clamp((b - d) / a); }
            }
            double dx = wx + s * ux - t * vx;
            double dy = wy + s * uy - t * vy;
            double dz = wz + s * uz - t * vz;
            return dx * dx + dy * dy + dz * dz;
        }

        private static double Clamp(double value) => value < 0 ? 0 : value > 1 ? 1 : value;
    }

    public sealed class BeamManualEndDetailV1
    {
        public BeamManualEndDetailKind Kind { get; set; }
        public BeamManualElementIdentityV1 HookType { get; set; }
        public BeamManualHookOrientation? HookOrientation { get; set; }
        /// <summary>Explicit user-authored curve points; currently reserved until the Revit path adapter validates the curve.</summary>
        public List<BeamManualPointV1> ReturnPoints { get; set; } = new List<BeamManualPointV1>();
    }

    public sealed class BeamManualBarV1
    {
        public double XMm { get; set; }
        public double YMm { get; set; }
        public double StartStationMm { get; set; }
        public double EndStationMm { get; set; }
        public string LayerId { get; set; }
        public BeamManualEndDetailV1 StartDetail { get; set; }
        public BeamManualEndDetailV1 FinishDetail { get; set; }
    }

    public sealed class BeamManualBarGroupV1
    {
        public string GroupId { get; set; }
        public BeamManualBarRole Role { get; set; }
        public bool Enabled { get; set; }
        public string UserLabel { get; set; }
        public BeamManualElementReferenceV1 BarType { get; set; }
        public List<BeamManualBarV1> Bars { get; set; } = new List<BeamManualBarV1>();
    }

    public sealed class BeamManualStirrupGroupV1
    {
        public string GroupId { get; set; }
        public bool Enabled { get; set; }
        public BeamManualElementReferenceV1 BarType { get; set; }
        public BeamManualElementIdentityV1 RebarShape { get; set; }
        public BeamManualEndDetailV1 StartDetail { get; set; }
        public BeamManualEndDetailV1 FinishDetail { get; set; }
        public List<double> StationsMm { get; set; } = new List<double>();
        public List<BeamManualPointV1> SectionPath { get; set; } = new List<BeamManualPointV1>();
        public string CoincidentStationPolicy { get; set; }
    }

    public sealed class BeamManualCoverV1
    {
        public BeamManualCoverFace Face { get; set; }
        public BeamManualCoverSource Source { get; set; }
        public double ClearCoverMm { get; set; }
        public string RevitCoverTypeDocumentKey { get; set; }
        public string RevitCoverTypeUniqueId { get; set; }
    }

    public sealed class BeamManualSupportEndV1
    {
        public BeamManualEnd End { get; set; }
        public BeamManualSupportMode Mode { get; set; }
        public string SupportHostUniqueId { get; set; }
        public string IntendedContinuation { get; set; }
    }

    /// <summary>
    /// Structured manual geometry input. It deliberately contains no engineering-value defaults and never asserts code compliance.
    /// </summary>
    public sealed class BeamManualDetailingPlanV1
    {
        public const int CurrentSchemaVersion = 1;
        public const string CoordinateContractId = "BEAM_PHYSICAL_SECTION_CENTERED_S_X_Y_MM_V1";
        public const string UnitConvention = "mm";
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public string Mode { get; set; } = "MANUAL_V1";
        public string DocumentKey { get; set; }
        public string HostUniqueId { get; set; }
        public string CoordinateContract { get; set; } = CoordinateContractId;
        public string Units { get; set; } = UnitConvention;
        public int Revision { get; set; }
        public BeamManualFrameV1 Frame { get; set; }
        public List<BeamManualBarGroupV1> BarGroups { get; set; } = new List<BeamManualBarGroupV1>();
        public List<BeamManualStirrupGroupV1> StirrupGroups { get; set; } = new List<BeamManualStirrupGroupV1>();
        public List<BeamManualCoverV1> Covers { get; set; } = new List<BeamManualCoverV1>();
        public List<BeamManualSupportEndV1> SupportEnds { get; set; } = new List<BeamManualSupportEndV1>();
        public string Source { get; set; } = "MANUAL_DETAILING";
        public string GeometryStatus { get; set; } = "NOT_SOLVED";
        public bool CodeVerified { get; set; }
    }

    public sealed class BeamManualIssueV1
    {
        public BeamManualIssueV1(string groupId, string field, string message)
        {
            GroupId = groupId ?? string.Empty;
            Field = field ?? string.Empty;
            Message = message ?? string.Empty;
        }
        public string GroupId { get; }
        public string Field { get; }
        public string Message { get; }
        public override string ToString() => (string.IsNullOrEmpty(GroupId) ? string.Empty : "Group " + GroupId + " / ") + Field + ": " + Message;
    }

    public sealed class BeamManualValidationResultV1
    {
        internal BeamManualValidationResultV1(IList<BeamManualIssueV1> issues) { Issues = issues.ToArray(); }
        public IReadOnlyList<BeamManualIssueV1> Issues { get; }
        public bool IsValid => Issues.Count == 0;
        public string ToDiagnosticText() => string.Join(Environment.NewLine, Issues.Select(issue => issue.ToString()));
    }

    public static class BeamManualPlanValidatorV1
    {
        /// <summary>Numerical comparison tolerance only (0.01 mm); not a cover, spacing, or engineering minimum.</summary>
        public const double GeometryToleranceMm = 0.01;
        public const int MaximumGroups = 200;
        public const int MaximumBars = 10000;

        public static BeamManualValidationResultV1 Validate(BeamManualDetailingPlanV1 plan, bool requireResolvedReferences)
        {
            var issues = new List<BeamManualIssueV1>();
            void Add(string group, string field, string message) => issues.Add(new BeamManualIssueV1(group, field, message));
            if (plan == null) { Add(string.Empty, "plan", "A manual detailing plan is required."); return new BeamManualValidationResultV1(issues); }
            if (plan.SchemaVersion != BeamManualDetailingPlanV1.CurrentSchemaVersion) Add(string.Empty, "schemaVersion", "Unsupported manual-plan schema version.");
            if (plan.Mode != "MANUAL_V1") Add(string.Empty, "mode", "Only MANUAL_V1 plans are accepted by this validator.");
            if (string.IsNullOrWhiteSpace(plan.DocumentKey)) Add(string.Empty, "documentKey", "The originating Revit document identity is required.");
            if (string.IsNullOrWhiteSpace(plan.HostUniqueId)) Add(string.Empty, "hostUniqueId", "A Beam host UniqueId is required.");
            if (plan.CoordinateContract != BeamManualDetailingPlanV1.CoordinateContractId || plan.Units != BeamManualDetailingPlanV1.UnitConvention)
                Add(string.Empty, "coordinateContract", "Plan coordinate or unit convention is unsupported.");
            if (plan.Revision < 0) Add(string.Empty, "revision", "Revision must be non-negative.");
            if (plan.CodeVerified) Add(string.Empty, "codeVerified", "Manual geometry plans cannot claim structural-code verification.");
            BeamManualFrameV1 frame = plan.Frame;
            if (frame == null) Add(string.Empty, "frame", "A verified physical Beam frame is required.");
            else
            {
                Positive(frame.WidthMm, string.Empty, "frame.widthMm", Add);
                Positive(frame.HeightMm, string.Empty, "frame.heightMm", Add);
                Positive(frame.LengthMm, string.Empty, "frame.lengthMm", Add);
                if (string.IsNullOrWhiteSpace(frame.HostGeometrySignature)) Add(string.Empty, "frame.hostGeometrySignature", "A supported-host geometry signature is required.");
                ValidateVector(frame.DirectionX, frame.DirectionY, frame.DirectionZ, "direction", Add);
                ValidateVector(frame.RightX, frame.RightY, frame.RightZ, "right", Add);
                ValidateVector(frame.UpX, frame.UpY, frame.UpZ, "up", Add);
                if (Math.Abs(VectorDot(frame.DirectionX, frame.DirectionY, frame.DirectionZ, frame.RightX, frame.RightY, frame.RightZ)) > 1e-6)
                    Add(string.Empty, "frame.direction/right", "Local frame axes must be orthogonal.");
                if (Math.Abs(VectorDot(frame.DirectionX, frame.DirectionY, frame.DirectionZ, frame.UpX, frame.UpY, frame.UpZ)) > 1e-6)
                    Add(string.Empty, "frame.direction/up", "Local frame axes must be orthogonal.");
                if (Math.Abs(VectorDot(frame.RightX, frame.RightY, frame.RightZ, frame.UpX, frame.UpY, frame.UpZ)) > 1e-6)
                    Add(string.Empty, "frame.right/up", "Local frame axes must be orthogonal.");
                double handedness = (frame.DirectionY * frame.UpZ - frame.DirectionZ * frame.UpY) * frame.RightX +
                    (frame.DirectionZ * frame.UpX - frame.DirectionX * frame.UpZ) * frame.RightY +
                    (frame.DirectionX * frame.UpY - frame.DirectionY * frame.UpX) * frame.RightZ;
                if (IsFinite(handedness) && Math.Abs(handedness - 1.0) > 1e-6)
                    Add(string.Empty, "frame.handedness", "Local frame orientation must satisfy Right = Direction cross Up.");
            }
            if (plan.BarGroups == null || plan.BarGroups.Count > MaximumGroups) Add(string.Empty, "barGroups", "Group collection is missing or exceeds the software safety limit.");
            if (plan.Covers == null) Add(string.Empty, "covers", "Face cover inputs are required.");
            if (plan.SupportEnds == null) Add(string.Empty, "supportEnds", "Explicit start/end conditions are required.");

            BeamManualBarGroupV1[] groups = (plan.BarGroups ?? new List<BeamManualBarGroupV1>()).ToArray();
            if (plan.StirrupGroups == null || plan.StirrupGroups.Count > MaximumGroups) Add(string.Empty, "stirrupGroups", "Stirrup group collection is missing or exceeds the software safety limit.");
            string[] allGroupIds = groups.Where(group => group != null).Select(group => group.GroupId)
                .Concat((plan.StirrupGroups ?? new List<BeamManualStirrupGroupV1>()).Where(group => group != null).Select(group => group.GroupId))
                .Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
            foreach (var duplicate in allGroupIds.GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1))
                Add(duplicate.Key, "groupId", "Group id must be unique within the plan.");

            var groupBars = new List<Tuple<string, BeamManualBarGroupV1, BeamManualBarV1>>();
            int totalBars = 0;
            foreach (BeamManualBarGroupV1 group in groups)
            {
                if (group == null) { Add(string.Empty, "barGroups", "Null group entries are not allowed."); continue; }
                string id = group.GroupId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id)) Add(id, "groupId", "A stable group id is required.");
                if (!Enum.IsDefined(typeof(BeamManualBarRole), group.Role) || group.Role == BeamManualBarRole.Unspecified) Add(id, "role", "An explicit supported bar role is required.");
                if (!group.Enabled) continue;
                ValidateReference(group.BarType, plan.DocumentKey, requireResolvedReferences, id, "barType", Add);
                if (group.Bars == null || group.Bars.Count == 0) { Add(id, "bars", "Enabled group requires at least one explicitly positioned physical bar."); continue; }
                totalBars += group.Bars.Count;
                for (int index = 0; index < group.Bars.Count; index++)
                {
                    BeamManualBarV1 bar = group.Bars[index];
                    string field = "bars[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                    if (bar == null) { Add(id, field, "Null bar entries are not allowed."); continue; }
                    Finite(bar.XMm, id, field + ".xMm", Add);
                    Finite(bar.YMm, id, field + ".yMm", Add);
                    Finite(bar.StartStationMm, id, field + ".startStationMm", Add);
                    Finite(bar.EndStationMm, id, field + ".endStationMm", Add);
                    if (bar.EndStationMm <= bar.StartStationMm) Add(id, field + ".stations", "End station must be greater than start station.");
                    if (frame != null && (bar.StartStationMm < -GeometryToleranceMm || bar.EndStationMm > frame.LengthMm + GeometryToleranceMm))
                        Add(id, field + ".stations", "Station must remain within the verified Beam concrete; support extension is not enabled by this plan validator.");
                    if (frame != null && group.BarType != null && IsFinite(group.BarType.DiameterMm) && group.BarType.DiameterMm > 0)
                    {
                        double endRadius = group.BarType.DiameterMm / 2.0;
                        if (bar.StartStationMm < endRadius - GeometryToleranceMm || bar.EndStationMm > frame.LengthMm - endRadius + GeometryToleranceMm)
                            Add(id, field + ".stations", "Straight bar diameter envelope must remain inside both physical Beam end faces; end-face cover is not inferred.");
                    }
                    if (string.IsNullOrWhiteSpace(bar.LayerId)) Add(id, field + ".layerId", "Explicit layer identity is required, including for a single layer.");
                    ValidateEnd(bar.StartDetail, group.BarType, requireResolvedReferences, id, field + ".startDetail", Add);
                    ValidateEnd(bar.FinishDetail, group.BarType, requireResolvedReferences, id, field + ".finishDetail", Add);
                    if (group.BarType != null && frame != null && IsFinite(bar.XMm) && IsFinite(bar.YMm))
                        ValidateEnvelope(bar, group.BarType.DiameterMm, frame, plan.Covers, id, field, Add);
                    groupBars.Add(Tuple.Create(id, group, bar));
                }
            }
            foreach (BeamManualStirrupGroupV1 stirrup in plan.StirrupGroups ?? new List<BeamManualStirrupGroupV1>())
            {
                if (stirrup == null) { Add(string.Empty, "stirrupGroups", "Null stirrup group entries are not allowed."); continue; }
                if (string.IsNullOrWhiteSpace(stirrup.GroupId)) Add(string.Empty, "stirrupGroups.groupId", "A stable stirrup group id is required.");
                if (!stirrup.Enabled) continue;
                ValidateReference(stirrup.BarType, plan.DocumentKey, requireResolvedReferences, stirrup.GroupId, "barType", Add);
                ValidateIdentity(stirrup.RebarShape, plan.DocumentKey, requireResolvedReferences, stirrup.GroupId, "rebarShape", Add);
                ValidateEnd(stirrup.StartDetail, stirrup.BarType, requireResolvedReferences, stirrup.GroupId, "startDetail", Add);
                ValidateEnd(stirrup.FinishDetail, stirrup.BarType, requireResolvedReferences, stirrup.GroupId, "finishDetail", Add);
                if (stirrup.StationsMm == null || stirrup.StationsMm.Count == 0) Add(stirrup.GroupId, "stationsMm", "Enabled stirrup group requires explicit stations; no inferred zone or spacing is supplied.");
                else
                {
                    foreach (double station in stirrup.StationsMm)
                    {
                        if (!IsFinite(station) || frame == null || station < -GeometryToleranceMm || station > frame.LengthMm + GeometryToleranceMm)
                            Add(stirrup.GroupId, "stationsMm", "Every explicit stirrup station must be finite and within the supported Beam concrete.");
                    }
                    if (stirrup.StationsMm.Any(station => !IsFinite(station)) == false &&
                        stirrup.StationsMm.Zip(stirrup.StationsMm.Skip(1), (left, right) => right <= left + GeometryToleranceMm).Any(overlap => overlap))
                        Add(stirrup.GroupId, "stationsMm", "Stations must be strictly increasing; same-role deduplication must be explicit before serialization.");
                }
                if (stirrup.SectionPath == null || stirrup.SectionPath.Count < 3) Add(stirrup.GroupId, "sectionPath", "An explicit stirrup centerline contour is required.");
                else if (stirrup.SectionPath.Any(point => point == null || !IsFinite(point.XMm) || !IsFinite(point.YMm))) Add(stirrup.GroupId, "sectionPath", "Every section point must be finite and explicitly supplied in millimetres.");
                if (string.IsNullOrWhiteSpace(stirrup.CoincidentStationPolicy)) Add(stirrup.GroupId, "coincidentStationPolicy", "Explicit same-role coincident-station policy is required.");
            }
            if (totalBars > MaximumBars) Add(string.Empty, "barGroups", "Physical bar count exceeds the software safety limit.");
            ValidateCoverMap(plan.Covers, Add);
            ValidateSupportMap(plan.SupportEnds, plan.HostUniqueId, Add);
            ValidateCollisions(groupBars, issues);
            return new BeamManualValidationResultV1(issues);
        }

        private delegate void AddIssue(string group, string field, string message);

        private static void ValidateEnd(BeamManualEndDetailV1 detail, BeamManualElementReferenceV1 barType,
            bool requireResolved, string group, string field, AddIssue add)
        {
            if (detail == null) { add(group, field, "Explicit end-detail selection is required."); return; }
            if (!Enum.IsDefined(typeof(BeamManualEndDetailKind), detail.Kind) || detail.Kind == BeamManualEndDetailKind.Unspecified) { add(group, field, "An explicit end-detail choice is required."); return; }
            switch (detail.Kind)
            {
                case BeamManualEndDetailKind.StraightAtStation:
                    if (detail.HookType != null || (detail.ReturnPoints != null && detail.ReturnPoints.Count != 0))
                        add(group, field, "Straight-at-station detail cannot also carry a hook or return path.");
                    break;
                case BeamManualEndDetailKind.ExplicitLoadedRevitHook:
                    ValidateIdentity(detail.HookType, barType?.DocumentKey, requireResolved, group, field + ".hookType", add);
                    if (!detail.HookOrientation.HasValue || !Enum.IsDefined(typeof(BeamManualHookOrientation), detail.HookOrientation.Value))
                        add(group, field + ".hookOrientation", "Exact hook orientation is required.");
                    add(group, field, "Loaded RebarHookType generation is not enabled until the Revit adapter has passed hook-shape parity validation.");
                    break;
                case BeamManualEndDetailKind.ExplicitCurveReturn:
                    if (detail.ReturnPoints == null || detail.ReturnPoints.Count < 2) add(group, field + ".returnPoints", "Explicit curve return requires its fully dimensioned path points.");
                    else if (detail.ReturnPoints.Any(point => point == null || !IsFinite(point.StationMm) || !IsFinite(point.XMm) || !IsFinite(point.YMm)))
                        add(group, field + ".returnPoints", "Every explicit return point must have finite local millimetre coordinates.");
                    add(group, field, "Explicit return-curve generation is not enabled until its Revit shape and containment adapter is validated.");
                    break;
                case BeamManualEndDetailKind.Unsupported:
                    add(group, field, "Unsupported end detail cannot be enabled.");
                    break;
            }
        }

        private static void ValidateReference(BeamManualElementReferenceV1 reference, string documentKey,
            bool requireResolved, string group, string field, AddIssue add)
        {
            if (reference == null) { add(group, field, "Exact loaded Revit element identity is required."); return; }
            if (string.IsNullOrWhiteSpace(reference.UniqueId) || string.IsNullOrWhiteSpace(reference.DocumentKey) || string.IsNullOrWhiteSpace(reference.Name))
                add(group, field, "Document key, UniqueId and type name are required; diameter-based substitution is prohibited.");
            if (!IsFinite(reference.DiameterMm) || reference.DiameterMm <= 0) add(group, field + ".diameterMm", "Actual model diameter must be finite and positive.");
            if (!Enum.IsDefined(typeof(BeamManualReferenceState), reference.State)) add(group, field + ".state", "Reference resolution state is invalid.");
            if (documentKey != null && reference.DocumentKey != documentKey) add(group, field + ".documentKey", "Reference belongs to another Revit document and requires explicit rebind.");
            if (requireResolved && reference.State != BeamManualReferenceState.Resolved) add(group, field + ".state", "Reference is UNRESOLVED; explicitly rebind the exact loaded Revit type before solving.");
        }

        private static void ValidateIdentity(BeamManualElementIdentityV1 reference, string documentKey,
            bool requireResolved, string group, string field, AddIssue add)
        {
            if (reference == null) { add(group, field, "Exact loaded Revit element identity is required."); return; }
            if (string.IsNullOrWhiteSpace(reference.UniqueId) || string.IsNullOrWhiteSpace(reference.DocumentKey) || string.IsNullOrWhiteSpace(reference.Name))
                add(group, field, "Document key, UniqueId and name are required; substitution is prohibited.");
            if (!Enum.IsDefined(typeof(BeamManualReferenceState), reference.State)) add(group, field + ".state", "Reference resolution state is invalid.");
            if (documentKey != null && reference.DocumentKey != documentKey) add(group, field + ".documentKey", "Reference belongs to another Revit document and requires explicit rebind.");
            if (requireResolved && reference.State != BeamManualReferenceState.Resolved) add(group, field + ".state", "Reference is UNRESOLVED; explicitly rebind the exact loaded Revit element before solving.");
        }

        private static void ValidateCoverMap(IList<BeamManualCoverV1> covers, AddIssue add)
        {
            if (covers == null) return;
            foreach (BeamManualCoverFace face in Enum.GetValues(typeof(BeamManualCoverFace)))
            {
                if (face == BeamManualCoverFace.Unspecified) continue;
                BeamManualCoverV1[] matches = covers.Where(cover => cover != null && cover.Face == face).ToArray();
                if (matches.Length != 1) { add(string.Empty, "covers." + face, "Exactly one independently resolved face-cover input is required."); continue; }
                BeamManualCoverV1 cover = matches[0];
                if (!Enum.IsDefined(typeof(BeamManualCoverSource), cover.Source) || cover.Source == BeamManualCoverSource.Unspecified) add(string.Empty, "covers." + face + ".source", "Cover provenance is required.");
                if (!IsFinite(cover.ClearCoverMm) || cover.ClearCoverMm < 0) add(string.Empty, "covers." + face + ".clearCoverMm", "Explicit/assigned clear cover must be finite and non-negative.");
                if (cover.Source == BeamManualCoverSource.RevitFaceAssignment &&
                    (string.IsNullOrWhiteSpace(cover.RevitCoverTypeDocumentKey) || string.IsNullOrWhiteSpace(cover.RevitCoverTypeUniqueId)))
                    add(string.Empty, "covers." + face, "Revit-assigned cover requires its exact cover type identity.");
            }
            if (covers.Any(cover => cover != null && (!Enum.IsDefined(typeof(BeamManualCoverFace), cover.Face) || cover.Face == BeamManualCoverFace.Unspecified))) add(string.Empty, "covers", "Unknown cover face.");
        }

        private static void ValidateSupportMap(IList<BeamManualSupportEndV1> supports, string hostUniqueId, AddIssue add)
        {
            if (supports == null) return;
            foreach (BeamManualEnd end in Enum.GetValues(typeof(BeamManualEnd)).Cast<BeamManualEnd>().Where(value => value != BeamManualEnd.Unspecified))
            {
                BeamManualSupportEndV1[] matches = supports.Where(support => support != null && support.End == end).ToArray();
                if (matches.Length != 1) { add(string.Empty, "supportEnds." + end, "Exactly one explicit end condition is required."); continue; }
                BeamManualSupportEndV1 support = matches[0];
                if (!Enum.IsDefined(typeof(BeamManualSupportMode), support.Mode) || support.Mode == BeamManualSupportMode.Unspecified) add(string.Empty, "supportEnds." + end + ".mode", "An explicit support/end condition is required.");
                if (support.Mode == BeamManualSupportMode.Unsupported) add(string.Empty, "supportEnds." + end + ".mode", "Unsupported support mode cannot be enabled.");
                if ((support.Mode == BeamManualSupportMode.VerifiedColumn || support.Mode == BeamManualSupportMode.VerifiedStructuralBeam) && string.IsNullOrWhiteSpace(support.SupportHostUniqueId))
                    add(string.Empty, "supportEnds." + end + ".supportHostUniqueId", "Verified support mode requires the exact support host identity.");
                if (support.Mode == BeamManualSupportMode.UserDefinedFreeEndWithinHost && string.IsNullOrWhiteSpace(support.IntendedContinuation))
                    add(string.Empty, "supportEnds." + end + ".intendedContinuation", "Explicit free-end intent is required; this does not certify development length.");
                if (support.SupportHostUniqueId == hostUniqueId) add(string.Empty, "supportEnds." + end + ".supportHostUniqueId", "Support identity cannot point back to the Beam host itself.");
            }
            if (supports.Any(support => support != null && (!Enum.IsDefined(typeof(BeamManualEnd), support.End) || support.End == BeamManualEnd.Unspecified))) add(string.Empty, "supportEnds", "Unknown Beam end.");
        }

        private static void ValidateEnvelope(BeamManualBarV1 bar, double diameterMm, BeamManualFrameV1 frame,
            IList<BeamManualCoverV1> covers, string group, string field, AddIssue add)
        {
            if (!IsFinite(diameterMm) || diameterMm <= 0 || covers == null) return;
            double radius = diameterMm / 2.0;
            BeamManualCoverV1 left = FindCover(covers, BeamManualCoverFace.Left);
            BeamManualCoverV1 right = FindCover(covers, BeamManualCoverFace.Right);
            BeamManualCoverV1 vertical = FindCover(covers, bar.YMm < 0 ? BeamManualCoverFace.Bottom : BeamManualCoverFace.Top);
            if (left == null || right == null || vertical == null) return;
            double leftEdge = -frame.WidthMm / 2.0 + left.ClearCoverMm;
            double rightEdge = frame.WidthMm / 2.0 - right.ClearCoverMm;
            if (bar.XMm - radius < leftEdge - GeometryToleranceMm || bar.XMm + radius > rightEdge + GeometryToleranceMm)
                add(group, field + ".xMm", "Physical bar diameter envelope violates the assigned clear cover or Beam side face.");
            if (bar.YMm - radius < -frame.HeightMm / 2.0 + vertical.ClearCoverMm - GeometryToleranceMm ||
                bar.YMm + radius > frame.HeightMm / 2.0 - vertical.ClearCoverMm + GeometryToleranceMm)
                add(group, field + ".yMm", "Physical bar diameter envelope violates the assigned clear cover or Beam top/bottom face.");
        }

        private static void ValidateCollisions(IList<Tuple<string, BeamManualBarGroupV1, BeamManualBarV1>> bars, IList<BeamManualIssueV1> issues)
        {
            for (int i = 0; i < bars.Count; i++)
            for (int j = i + 1; j < bars.Count; j++)
            {
                var a = bars[i]; var b = bars[j];
                if (a.Item3.EndStationMm <= b.Item3.StartStationMm + GeometryToleranceMm || b.Item3.EndStationMm <= a.Item3.StartStationMm + GeometryToleranceMm) continue;
                double radiusA = (a.Item2.BarType?.DiameterMm ?? 0) / 2.0;
                double radiusB = (b.Item2.BarType?.DiameterMm ?? 0) / 2.0;
                double separation = Math.Sqrt(Math.Pow(a.Item3.XMm - b.Item3.XMm, 2) + Math.Pow(a.Item3.YMm - b.Item3.YMm, 2));
                if (separation + GeometryToleranceMm < radiusA + radiusB)
                    issues.Add(new BeamManualIssueV1(a.Item1, "bars", "Physical bar envelope collides with group " + b.Item1 + "; adjust explicit geometry before solving."));
            }
        }

        private static BeamManualCoverV1 FindCover(IEnumerable<BeamManualCoverV1> covers, BeamManualCoverFace face) => covers.FirstOrDefault(cover => cover != null && cover.Face == face);
        private static void Positive(double value, string group, string field, AddIssue add) { if (!IsFinite(value) || value <= 0) add(group, field, "A finite positive geometry extent is required."); }
        private static void Finite(double value, string group, string field, AddIssue add) { if (!IsFinite(value)) add(group, field, "Value must be finite."); }
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static double VectorDot(double ax, double ay, double az, double bx, double by, double bz) => ax * bx + ay * by + az * bz;
        private static void ValidateVector(double x, double y, double z, string field, AddIssue add)
        {
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z)) { add(string.Empty, "frame." + field, "Frame vector must be finite."); return; }
            double length = Math.Sqrt(x * x + y * y + z * z);
            if (Math.Abs(length - 1.0) > 1e-6) add(string.Empty, "frame." + field, "Frame vector must be normalized.");
        }
    }

    public sealed class BeamManualResolvedBarV1
    {
        public string GroupId { get; internal set; }
        public BeamManualBarRole Role { get; internal set; }
        public string LayerId { get; internal set; }
        public string BarTypeUniqueId { get; internal set; }
        public double DiameterMm { get; internal set; }
        public IReadOnlyList<BeamManualPointV1> Centerline { get; internal set; }
    }

    public static class BeamManualLayoutBuilderV1
    {
        /// <summary>Builds canonical manual centerlines in local millimetres; it invents no stations or distribution.</summary>
        public static IReadOnlyList<BeamManualResolvedBarV1> Build(BeamManualDetailingPlanV1 plan)
        {
            BeamManualValidationResultV1 validation = BeamManualPlanValidatorV1.Validate(plan, requireResolvedReferences: true);
            if (!validation.IsValid) throw new InvalidOperationException("Manual Beam plan is blocked:" + Environment.NewLine + validation.ToDiagnosticText());
            var result = new List<BeamManualResolvedBarV1>();
            foreach (BeamManualBarGroupV1 group in plan.BarGroups.Where(item => item.Enabled))
            foreach (BeamManualBarV1 bar in group.Bars)
            {
                result.Add(new BeamManualResolvedBarV1
                {
                    GroupId = group.GroupId,
                    Role = group.Role,
                    LayerId = bar.LayerId,
                    BarTypeUniqueId = group.BarType.UniqueId,
                    DiameterMm = group.BarType.DiameterMm,
                    Centerline = new[]
                    {
                        new BeamManualPointV1 { StationMm = bar.StartStationMm, XMm = bar.XMm, YMm = bar.YMm },
                        new BeamManualPointV1 { StationMm = bar.EndStationMm, XMm = bar.XMm, YMm = bar.YMm }
                    }
                });
            }
            return result;
        }

        public static string Fingerprint(BeamManualDetailingPlanV1 plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var values = new List<string> { plan.SchemaVersion.ToString(CultureInfo.InvariantCulture), plan.Mode, plan.DocumentKey, plan.HostUniqueId,
                plan.CoordinateContract, plan.Units, plan.Revision.ToString(CultureInfo.InvariantCulture), plan.Frame?.HostGeometrySignature,
                Number(plan.Frame?.WidthMm), Number(plan.Frame?.HeightMm), Number(plan.Frame?.LengthMm),
                Number(plan.Frame?.DirectionX), Number(plan.Frame?.DirectionY), Number(plan.Frame?.DirectionZ),
                Number(plan.Frame?.RightX), Number(plan.Frame?.RightY), Number(plan.Frame?.RightZ),
                Number(plan.Frame?.UpX), Number(plan.Frame?.UpY), Number(plan.Frame?.UpZ), plan.CodeVerified.ToString() };
            foreach (BeamManualCoverV1 cover in (plan.Covers ?? new List<BeamManualCoverV1>()).OrderBy(item => item?.Face))
                values.Add(string.Join("|", cover?.Face.ToString(), cover?.Source.ToString(), Number(cover?.ClearCoverMm), cover?.RevitCoverTypeDocumentKey, cover?.RevitCoverTypeUniqueId));
            foreach (BeamManualSupportEndV1 support in (plan.SupportEnds ?? new List<BeamManualSupportEndV1>()).OrderBy(item => item?.End))
                values.Add(string.Join("|", support?.End.ToString(), support?.Mode.ToString(), support?.SupportHostUniqueId, support?.IntendedContinuation));
            foreach (BeamManualBarGroupV1 group in (plan.BarGroups ?? new List<BeamManualBarGroupV1>()).OrderBy(item => item?.GroupId, StringComparer.Ordinal))
            {
                values.Add(string.Join("|", group?.GroupId, group?.Role.ToString(), group?.Enabled.ToString(), group?.UserLabel,
                    group?.BarType?.DocumentKey, group?.BarType?.UniqueId, group?.BarType?.Name, Number(group?.BarType?.DiameterMm), group?.BarType?.State.ToString()));
                foreach (BeamManualBarV1 bar in (group?.Bars ?? new List<BeamManualBarV1>()).OrderBy(item => item?.LayerId, StringComparer.Ordinal).ThenBy(item => item?.StartStationMm))
                {
                    values.Add(string.Join("|", Number(bar?.XMm), Number(bar?.YMm), Number(bar?.StartStationMm), Number(bar?.EndStationMm), bar?.LayerId));
                    AddEnd(values, bar?.StartDetail); AddEnd(values, bar?.FinishDetail);
                }
            }
            foreach (BeamManualStirrupGroupV1 stirrup in (plan.StirrupGroups ?? new List<BeamManualStirrupGroupV1>()).OrderBy(item => item?.GroupId, StringComparer.Ordinal))
            {
                values.Add(string.Join("|", stirrup?.GroupId, stirrup?.Enabled.ToString(), stirrup?.BarType?.DocumentKey,
                    stirrup?.BarType?.UniqueId, stirrup?.RebarShape?.DocumentKey, stirrup?.RebarShape?.UniqueId,
                    stirrup?.CoincidentStationPolicy));
                foreach (double station in stirrup?.StationsMm ?? new List<double>()) values.Add(Number(station));
                foreach (BeamManualPointV1 point in stirrup?.SectionPath ?? new List<BeamManualPointV1>())
                    values.Add(string.Join("|", Number(point?.StationMm), Number(point?.XMm), Number(point?.YMm)));
                AddEnd(values, stirrup?.StartDetail); AddEnd(values, stirrup?.FinishDetail);
            }
            using (SHA256 sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", values))).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static void AddEnd(ICollection<string> values, BeamManualEndDetailV1 detail)
        {
            values.Add(string.Join("|", detail?.Kind.ToString(), detail?.HookType?.DocumentKey, detail?.HookType?.UniqueId,
                detail?.HookType?.State.ToString(), detail?.HookOrientation?.ToString()));
            foreach (BeamManualPointV1 point in detail?.ReturnPoints ?? new List<BeamManualPointV1>())
                values.Add(string.Join("|", Number(point?.StationMm), Number(point?.XMm), Number(point?.YMm)));
        }

        private static string Number(double? value) => value.HasValue ? value.Value.ToString("R", CultureInfo.InvariantCulture) : string.Empty;
    }
}
