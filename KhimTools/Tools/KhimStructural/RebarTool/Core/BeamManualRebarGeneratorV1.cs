using KhimTools.Core;
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
    /// Strict Revit adapter for explicitly positioned longitudinal/stirrup paths
    /// fully contained in one verified Beam. Unsupported detailing fails closed.
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

        public string Fingerprint(BeamManualDetailingPlanV1 plan, FamilyInstance beam, IEnumerable<Rebar> ownedBatchBars = null)
        {
            ValidateHostIdentity(plan, beam);
            BeamManualFrameV1 current = GetCurrentFrame(beam);
            if (!FrameMatches(plan.Frame, current))
                throw new InvalidOperationException("Beam geometry or local orientation changed since this Manual v1 plan was authored. Refresh and review the plan before solving.");
            BeamManualPlanValidatorV1.Validate(plan, requireResolvedReferences: true).ThrowIfInvalid();
            string[] typeState = ResolvePlanReferences(plan).Select(type => string.Join("|", type.UniqueId,
                type.VersionGuid.ToString("D"), type.Name,
                type.BarModelDiameter.ToString("R", CultureInfo.InvariantCulture)))
                .Concat(ResolveHookReferences(plan).Select(type => string.Join("HOOK", type.UniqueId, type.VersionGuid.ToString("D"), type.Name)))
                .Concat(ResolveShapeReferences(plan).Select(shape => string.Join("SHAPE", shape.UniqueId, shape.VersionGuid.ToString("D"), shape.Name, shape.RebarStyle)))
                .ToArray();
            var supports = ResolveSupportGeometry(plan, beam);
            var excluded = new HashSet<string>((ownedBatchBars ?? Enumerable.Empty<Rebar>()).Select(bar => bar.UniqueId), StringComparer.Ordinal);
            string[] existingRebarState = GetRelevantRebar(beam, supports).Where(bar => !excluded.Contains(bar.UniqueId))
                .Select(RebarPreviewService.FingerprintBar)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return WorkflowFingerprint.Compute(new[] { BeamManualLayoutBuilderV1.Fingerprint(plan), current.HostGeometrySignature,
                GetDocumentKey(), beam.UniqueId, beam.VersionGuid.ToString("D"), supports?.StateFingerprint }.Concat(typeState).Concat(existingRebarState));
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

            BeamManualSupportGeometryV1 supports = ResolveSupportGeometry(plan, beam);

            ResolvePlanReferences(plan);
            ResolveHookReferences(plan);
            ResolveShapeReferences(plan);
            IReadOnlyList<BeamManualResolvedBarV1> bars = BeamManualLayoutBuilderV1.Build(plan);
            Rebar[] existingBars = GetRelevantRebar(beam, supports).ToArray();
            var created = new List<Rebar>();
            foreach (BeamManualResolvedBarV1 resolved in bars)
            {
                BeamManualBarGroupV1 group = plan.BarGroups.Single(item => item.Enabled && item.GroupId == resolved.GroupId);
                RebarBarType type = ResolveBarType(group.BarType);
                BeamManualBarV1 source = group.Bars[resolved.SourceBarIndex];
                RebarHookType startHook = ResolveHookType(source.StartDetail);
                RebarHookType finishHook = ResolveHookType(source.FinishDetail);
                XYZ[] worldPoints = resolved.Centerline.Select(point => ToWorld(profile, point)).ToArray();
                if (!BeamManualPathGeometryV1.TryGetPlaneNormal(resolved.Centerline.Select(point =>
                    new BeamManualPoint3V1(point.XMm, point.YMm, point.StationMm)).ToArray(), out BeamManualPoint3V1 localNormal))
                    throw new InvalidOperationException("Manual bar group " + resolved.GroupId + " must have one finite coplanar centerline path.");
                XYZ curveNormal = (profile.RightVector * localNormal.X + profile.UpVector * localNormal.Y +
                    profile.Direction * localNormal.Z).Normalize();
                if (worldPoints.Length < 2 || worldPoints.Zip(worldPoints.Skip(1), (a, b) => a.DistanceTo(b) <= 1e-9).Any(zero => zero))
                    throw new InvalidOperationException("A Manual v1 bar resolved to an empty or zero-length Revit centerline segment.");
                ValidateNoExistingBarOverlap(existingBars, worldPoints, type);
                foreach (Rebar createdBar in created)
                    ValidateNoExistingBarOverlap(new[] { createdBar }, worldPoints, type);
                var curves = new List<Curve>();
                for (int index = 0; index + 1 < worldPoints.Length; index++)
                    curves.Add(Line.CreateBound(worldPoints[index], worldPoints[index + 1]));
                supports?.ValidateCurves(curves, type.BarModelDiameter * 0.5);
                Rebar bar = RebarShapeCreationHelper.CreateFromCurvesSafe(_document, RebarStyle.Standard,
                    type, startHook, finishHook, beam, curveNormal, curves,
                    ToHookOrientation(source.StartDetail), ToHookOrientation(source.FinishDetail),
                    allowLegacyFallback: false);
                if (bar == null || !bar.IsValidObject)
                    throw new InvalidOperationException("Revit could not create Manual bar group " + resolved.GroupId + ". " + RebarShapeCreationHelper.LastFailureReason);
                if (bar.GetHostId() != beam.Id || bar.GetTypeId() != type.Id || bar.Quantity != 1)
                    throw new InvalidOperationException("Revit changed the requested Manual bar host, exact type, or single-bar quantity; refusing substituted output.");
                if ((startHook != null && bar.GetHookTypeId(0) != startHook.Id) || (finishHook != null && bar.GetHookTypeId(1) != finishHook.Id))
                    throw new InvalidOperationException("Revit did not preserve the exact explicitly selected hook identities; creation is rolled back.");
                if (supports == null) ValidateCreatedEnvelope(bar, profile, type.BarModelDiameter * 0.5, plan.Covers, false);
                else supports.ValidateCurves(bar.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeAllMultiplanarCurves, 0), type.BarModelDiameter * 0.5);
                ValidateCreatedAgainstExisting(bar, type, existingBars.Concat(created));
                created.Add(bar);
                if (roleByBarId != null)
                    roleByBarId[bar.Id.ToLongValue().ToString(CultureInfo.InvariantCulture)] = group.Role == BeamManualBarRole.UserDefined
                        ? "manual-user:" + (group.UserLabel ?? string.Empty)
                        : "manual-" + group.Role.ToString().ToLowerInvariant();
            }

            foreach (BeamManualResolvedStirrupV1 resolved in BeamManualLayoutBuilderV1.BuildStirrups(plan))
            {
                BeamManualStirrupGroupV1 group = plan.StirrupGroups.Single(item => item.Enabled && item.GroupId == resolved.GroupId);
                RebarBarType type = ResolveBarType(group.BarType);
                RebarShape expectedShape = ResolveShape(group.RebarShape);
                RebarHookType startHook = ResolveHookType(group.StartDetail);
                RebarHookType finishHook = ResolveHookType(group.FinishDetail);
                XYZ[] points = resolved.Centerline.Select(point => ToWorld(profile, point)).ToArray();
                var curves = new List<Curve>();
                for (int index = 0; index + 1 < points.Length; index++)
                    curves.Add(Line.CreateBound(points[index], points[index + 1]));
                Rebar stirrup = RebarShapeCreationHelper.CreateFromCurvesSafe(_document, RebarStyle.StirrupTie,
                    type, startHook, finishHook, beam, profile.Direction, curves,
                    ToHookOrientation(group.StartDetail), ToHookOrientation(group.FinishDetail), allowLegacyFallback: false);
                if (stirrup == null || !stirrup.IsValidObject)
                    throw new InvalidOperationException("Revit could not create exact Manual " + group.Role + " group " + group.GroupId + ". " + RebarShapeCreationHelper.LastFailureReason);
                RebarShape actualShape = _document.GetElement(stirrup.GetShapeId()) as RebarShape;
                if (stirrup.GetHostId() != beam.Id || stirrup.GetTypeId() != type.Id || stirrup.Quantity != 1 ||
                    actualShape == null || actualShape.UniqueId != expectedShape.UniqueId || actualShape.RebarStyle != RebarStyle.StirrupTie)
                    throw new InvalidOperationException("Revit changed the exact requested stirrup shape, type, host or one-station quantity; the enclosing transaction will roll back.");
                if ((startHook != null && stirrup.GetHookTypeId(0) != startHook.Id) || (finishHook != null && stirrup.GetHookTypeId(1) != finishHook.Id))
                    throw new InvalidOperationException("Revit did not preserve the exact stirrup hook identities; creation is rolled back.");
                ValidateCreatedEnvelope(stirrup, profile, type.BarModelDiameter * 0.5, plan.Covers, true);
                ValidateCreatedAgainstExisting(stirrup, type, existingBars.Concat(created));
                created.Add(stirrup);
                if (roleByBarId != null)
                    roleByBarId[stirrup.Id.ToLongValue().ToString(CultureInfo.InvariantCulture)] = "manual-" + group.Role.ToString().ToLowerInvariant();
            }
            return created;
        }

        public BeamManualSupportEndV1 BindSupport(FamilyInstance beam, BeamManualEnd end, FamilyInstance support, double coverMm, string intent) =>
            BeamManualSupportGeometryV1.Bind(_document, beam, end, support, coverMm, intent, GetDocumentKey());

        private BeamManualSupportGeometryV1 ResolveSupportGeometry(BeamManualDetailingPlanV1 plan, FamilyInstance beam) =>
            plan.SupportEnds.Any(end => end.Mode != BeamManualSupportMode.UserDefinedFreeEndWithinHost)
                ? new BeamManualSupportGeometryV1(_document, beam, plan, RequireBeamProfile(beam)) : null;

        private IEnumerable<Rebar> GetRelevantRebar(FamilyInstance beam, BeamManualSupportGeometryV1 geometry)
        {
            var supportIds = new HashSet<ElementId>((geometry?.Supports ?? Array.Empty<FamilyInstance>()).Select(host => host.Id));
            var filters = (geometry?.Supports ?? Array.Empty<FamilyInstance>()).Select(host =>
            {
                BoundingBoxXYZ box = host.get_BoundingBox(null) ?? throw new InvalidOperationException("Support bounds are unavailable for interference preflight.");
                XYZ[] points = (from x in new[]{box.Min.X,box.Max.X} from y in new[]{box.Min.Y,box.Max.Y}
                    from z in new[]{box.Min.Z,box.Max.Z} select (box.Transform ?? Transform.Identity).OfPoint(new XYZ(x,y,z))).ToArray();
                return new BoundingBoxIntersectsFilter(new Outline(
                    new XYZ(points.Min(p=>p.X),points.Min(p=>p.Y),points.Min(p=>p.Z)),
                    new XYZ(points.Max(p=>p.X),points.Max(p=>p.Y),points.Max(p=>p.Z))));
            }).ToArray();
            return new FilteredElementCollector(_document).OfClass(typeof(Rebar)).Cast<Rebar>()
                .Where(bar => bar.IsValidObject && bar.Document == _document && (bar.GetHostId() == beam.Id ||
                    supportIds.Contains(bar.GetHostId()) || filters.Any(filter => filter.PassesFilter(_document, bar.Id))))
                .ToArray();
        }

        private void ValidateNoExistingBarOverlap(IEnumerable<Rebar> existingBars, XYZ start, XYZ finish, RebarBarType requestedType)
        {
            ValidateNoExistingBarOverlap(existingBars, new[] { start, finish }, requestedType);
        }

        private void ValidateNoExistingBarOverlap(IEnumerable<Rebar> existingBars, IList<XYZ> candidatePoints, RebarBarType requestedType)
        {
            double requestedRadius = requestedType.BarModelDiameter * 0.5;
            double tolerance = UnitUtils.ConvertToInternalUnits(0.01, UnitTypeId.Millimeters);
            foreach (Rebar existing in existingBars)
            {
                if (existing == null || !existing.IsValidObject)
                    throw new InvalidOperationException("Existing Beam reinforcement changed during Manual generation; refusing to continue.");
                RebarBarType existingType = _document.GetElement(existing.GetTypeId()) as RebarBarType;
                if (existingType == null || !existingType.IsValidObject)
                    throw new InvalidOperationException("Existing Beam reinforcement has no resolvable exact bar type; Manual v1 cannot prove clearance safely.");
                IReadOnlyList<RebarPreviewPath> paths = RebarPreviewService.ReadPaths(new List<Rebar> { existing });
                double minimumDistance = requestedRadius + existingType.BarModelDiameter * 0.5 - tolerance;
                if (minimumDistance <= 0) continue;
                double minimumDistanceSquared = minimumDistance * minimumDistance;
                foreach (RebarPreviewPath path in paths)
                {
                    for (int index = 0; index + 1 < path.Points.Count; index++)
                    {
                        RebarPreviewPoint p0 = path.Points[index];
                        RebarPreviewPoint p1 = path.Points[index + 1];
                        var existingStart = new BeamManualPoint3V1(p0.X, p0.Y, p0.Z);
                        var existingFinish = new BeamManualPoint3V1(p1.X, p1.Y, p1.Z);
                        for (int candidateIndex = 0; candidateIndex + 1 < candidatePoints.Count; candidateIndex++)
                        {
                            XYZ candidateStart = candidatePoints[candidateIndex];
                            XYZ candidateFinish = candidatePoints[candidateIndex + 1];
                            if (BeamManualSegmentDistanceV1.SquaredDistance(
                                new BeamManualPoint3V1(candidateStart.X, candidateStart.Y, candidateStart.Z), new BeamManualPoint3V1(candidateFinish.X, candidateFinish.Y, candidateFinish.Z),
                                existingStart, existingFinish) < minimumDistanceSquared)
                                throw new InvalidOperationException("The requested Manual centerline overlaps existing or earlier generated Beam reinforcement. Review the explicit geometry before creating; the enclosing transaction will roll back.");
                        }
                    }
                }
            }
        }

        private void ValidateCreatedAgainstExisting(Rebar candidate, RebarBarType requestedType, IEnumerable<Rebar> existingBars)
        {
            existingBars=existingBars.ToArray();
            RebarSolvedEnvelopeCollisionV1.Verify(candidate,existingBars);
            IReadOnlyList<RebarPreviewPath> candidatePaths = RebarPreviewService.ReadPaths(new List<Rebar> { candidate });
            double radius = requestedType.BarModelDiameter * 0.5;
            double tolerance = UnitUtils.ConvertToInternalUnits(0.01, UnitTypeId.Millimeters);
            foreach (Rebar existing in existingBars)
            {
                if (existing == null || !existing.IsValidObject) throw new InvalidOperationException("Existing reinforcement changed during the transaction.");
                RebarBarType existingType = _document.GetElement(existing.GetTypeId()) as RebarBarType;
                if (existingType == null || !existingType.IsValidObject)
                    throw new InvalidOperationException("Existing reinforcement type cannot be resolved; refusing to certify Manual output clearance.");
                double minimum = radius + existingType.BarModelDiameter * 0.5 - tolerance;
                if (minimum <= 0) continue;
                double minimumSquared = minimum * minimum;
                IReadOnlyList<RebarPreviewPath> existingPaths = RebarPreviewService.ReadPaths(new List<Rebar> { existing });
                foreach (RebarPreviewPath newPath in candidatePaths)
                foreach (RebarPreviewPath oldPath in existingPaths)
                for (int i = 0; i + 1 < newPath.Points.Count; i++)
                for (int j = 0; j + 1 < oldPath.Points.Count; j++)
                {
                    RebarPreviewPoint a0 = newPath.Points[i], a1 = newPath.Points[i + 1];
                    RebarPreviewPoint b0 = oldPath.Points[j], b1 = oldPath.Points[j + 1];
                    if (BeamManualSegmentDistanceV1.SquaredDistance(
                        new BeamManualPoint3V1(a0.X, a0.Y, a0.Z), new BeamManualPoint3V1(a1.X, a1.Y, a1.Z),
                        new BeamManualPoint3V1(b0.X, b0.Y, b0.Z), new BeamManualPoint3V1(b1.X, b1.Y, b1.Z)) < minimumSquared)
                        throw new InvalidOperationException("Revit-solved Manual Rebar overlaps existing or earlier generated Beam reinforcement; the enclosing transaction will roll back.");
                }
            }
        }

        private IEnumerable<RebarBarType> ResolvePlanReferences(BeamManualDetailingPlanV1 plan)
        {
            return (plan.BarGroups ?? new List<BeamManualBarGroupV1>()).Where(group => group != null && group.Enabled).Select(group => ResolveBarType(group.BarType))
                .Concat((plan.StirrupGroups ?? new List<BeamManualStirrupGroupV1>()).Where(group => group != null && group.Enabled).Select(group => ResolveBarType(group.BarType)))
                .GroupBy(type => type.UniqueId, StringComparer.Ordinal).Select(group => group.First()).ToArray();
        }

        private IEnumerable<RebarHookType> ResolveHookReferences(BeamManualDetailingPlanV1 plan)
        {
            var references = (plan.BarGroups ?? new List<BeamManualBarGroupV1>()).Where(group => group != null && group.Enabled)
                .SelectMany(group => group.Bars ?? new List<BeamManualBarV1>())
                .SelectMany(bar => new[] { bar.StartDetail, bar.FinishDetail })
                .Concat((plan.StirrupGroups ?? new List<BeamManualStirrupGroupV1>()).Where(group => group != null && group.Enabled)
                    .SelectMany(group => new[] { group.StartDetail, group.FinishDetail }))
                .Where(detail => detail != null && detail.Kind == BeamManualEndDetailKind.ExplicitLoadedRevitHook)
                .Select(ResolveHookType).Where(hook => hook != null)
                .GroupBy(hook => hook.UniqueId, StringComparer.Ordinal).Select(group => group.First()).ToArray();
            return references;
        }

        private IEnumerable<RebarShape> ResolveShapeReferences(BeamManualDetailingPlanV1 plan)
        {
            return (plan.StirrupGroups ?? new List<BeamManualStirrupGroupV1>()).Where(group => group != null && group.Enabled)
                .Select(group => ResolveShape(group.RebarShape)).GroupBy(shape => shape.UniqueId, StringComparer.Ordinal)
                .Select(group => group.First()).ToArray();
        }

        private RebarShape ResolveShape(BeamManualElementIdentityV1 reference)
        {
            if (reference == null || reference.State != BeamManualReferenceState.Resolved || reference.DocumentKey != GetDocumentKey())
                throw new InvalidOperationException("The exact loaded RebarShape is unresolved or belongs to another document; explicitly rebind it.");
            RebarShape shape = _document.GetElement(reference.UniqueId) as RebarShape;
            if (shape == null || !shape.IsValidObject || shape.Document != _document || shape.Name != reference.Name || shape.RebarStyle != RebarStyle.StirrupTie)
                throw new InvalidOperationException("The exact RebarShape identity is missing or is not a StirrupTie shape; no substitute is allowed.");
            return shape;
        }

        private RebarHookType ResolveHookType(BeamManualEndDetailV1 detail)
        {
            if (detail == null || detail.Kind != BeamManualEndDetailKind.ExplicitLoadedRevitHook) return null;
            BeamManualElementIdentityV1 reference = detail.HookType;
            if (reference == null || reference.State != BeamManualReferenceState.Resolved || reference.DocumentKey != GetDocumentKey())
                throw new InvalidOperationException("The exact loaded RebarHookType is unresolved or belongs to another document; explicitly rebind it.");
            RebarHookType hook = _document.GetElement(reference.UniqueId) as RebarHookType;
            if (hook == null || !hook.IsValidObject || hook.Document != _document || hook.Name != reference.Name)
                throw new InvalidOperationException("The exact RebarHookType identity no longer resolves; no hook substitution is permitted.");
            return hook;
        }

        private static RebarHookOrientation ToHookOrientation(BeamManualEndDetailV1 detail)
        {
            if (detail?.Kind != BeamManualEndDetailKind.ExplicitLoadedRevitHook) return RebarHookOrientation.Right;
            return detail.HookOrientation == BeamManualHookOrientation.Left ? RebarHookOrientation.Left : RebarHookOrientation.Right;
        }

        private static void ValidateCreatedEnvelope(Rebar rebar, BeamGeometryHelper.BeamProfile profile,
            double radiusFeet, IList<BeamManualCoverV1> covers, bool stirrup)
        {
            IReadOnlyList<RebarPreviewPath> paths = RebarPreviewService.ReadPaths(new List<Rebar> { rebar });
            double radiusMm = UnitUtils.ConvertFromInternalUnits(radiusFeet, UnitTypeId.Millimeters);
            double widthMm = UnitUtils.ConvertFromInternalUnits(profile.B, UnitTypeId.Millimeters);
            double heightMm = UnitUtils.ConvertFromInternalUnits(profile.H, UnitTypeId.Millimeters);
            double lengthMm = UnitUtils.ConvertFromInternalUnits(profile.Length, UnitTypeId.Millimeters);
            BeamManualCoverV1 top = covers.Single(item => item.Face == (stirrup ? BeamManualCoverFace.StirrupOutside : BeamManualCoverFace.Top));
            BeamManualCoverV1 bottom = covers.Single(item => item.Face == (stirrup ? BeamManualCoverFace.StirrupOutside : BeamManualCoverFace.Bottom));
            BeamManualCoverV1 left = covers.Single(item => item.Face == (stirrup ? BeamManualCoverFace.StirrupOutside : BeamManualCoverFace.Left));
            BeamManualCoverV1 right = covers.Single(item => item.Face == (stirrup ? BeamManualCoverFace.StirrupOutside : BeamManualCoverFace.Right));
            foreach (RebarPreviewPath path in paths)
            foreach (RebarPreviewPoint point in path.Points)
            {
                XYZ world = new XYZ(point.X, point.Y, point.Z) - profile.StartPoint;
                double x = UnitUtils.ConvertFromInternalUnits(world.DotProduct(profile.RightVector), UnitTypeId.Millimeters);
                double y = UnitUtils.ConvertFromInternalUnits(world.DotProduct(profile.UpVector), UnitTypeId.Millimeters);
                double station = UnitUtils.ConvertFromInternalUnits(world.DotProduct(profile.Direction), UnitTypeId.Millimeters);
                double tolerance = BeamManualPlanValidatorV1.GeometryToleranceMm;
                if (x - radiusMm < -widthMm / 2.0 + left.ClearCoverMm - tolerance ||
                    x + radiusMm > widthMm / 2.0 - right.ClearCoverMm + tolerance ||
                    y + radiusMm > heightMm / 2.0 - top.ClearCoverMm + tolerance ||
                    y - radiusMm < -heightMm / 2.0 + bottom.ClearCoverMm - tolerance ||
                    !BeamManualPathGeometryV1.IsStationEnvelopeInside(station, lengthMm, 2 * radiusMm))
                    throw new InvalidOperationException("Revit's solved Manual Rebar centerline/bend violates the explicit Beam face-cover or physical host envelope. The transaction will roll back.");
            }
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
                beam.Category == null || beam.Category.GetBuiltInCategoryCompat() != BuiltInCategory.OST_StructuralFraming ||
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
