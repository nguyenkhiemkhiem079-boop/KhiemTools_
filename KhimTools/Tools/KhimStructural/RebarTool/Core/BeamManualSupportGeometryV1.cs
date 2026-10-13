using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;
using KhimTools.Core.Workflow;

namespace KhimTools.RebarTool.Core
{
    /// <summary>Detached transient solids only; no transactions/elements are created here.
    /// Supported supports are physically verified rectangular Column/Beam prisms.
    /// Cover shells exclude concrete interfaces, not exposed exterior faces.</summary>
    internal sealed class BeamManualSupportGeometryV1
    {
        private readonly Solid _concrete;
        private readonly List<Solid> _coverShells = new List<Solid>();
        public IReadOnlyList<FamilyInstance> Supports { get; }
        public string StateFingerprint { get; }

        // Shared exact envelope engine; caller must independently verify its host geometry.
        internal BeamManualSupportGeometryV1(Solid concrete, IEnumerable<Solid> coverShells)
        {
            _concrete=concrete ?? throw new ArgumentNullException(nameof(concrete));
            _coverShells.AddRange(coverShells.Where(HasVolume));
            Supports=Array.Empty<FamilyInstance>(); StateFingerprint=string.Empty;
        }

        public static BeamManualSupportEndV1 Bind(Document document, FamilyInstance beam, BeamManualEnd end,
            FamilyInstance support, double coverMm, string intent, string documentKey)
        {
            var input = new BeamManualSupportEndV1 { End=end, SupportHostUniqueId=support?.UniqueId,
                Mode=support?.Category.IsCategory(BuiltInCategory.OST_StructuralColumns) == true ? BeamManualSupportMode.VerifiedColumn : BeamManualSupportMode.VerifiedStructuralBeam,
                SupportDocumentKey=documentKey, SupportClearCoverMm=coverMm, IntendedContinuation=intent };
            Resolve(document, beam, input, false, out string signature, out _, out _);
            input.SupportGeometrySignature = signature;
            return input;
        }

        public BeamManualSupportGeometryV1(Document document, FamilyInstance beam, BeamManualDetailingPlanV1 plan,
            BeamGeometryHelper.BeamProfile profile)
        {
            Solid beamSolid = ReadSingleSolid(beam);
            var supports = new List<FamilyInstance>();
            var supportSolids = new List<Solid>();
            var supportInteriors = new List<Solid>();
            var state = new List<string>();
            foreach (var end in plan.SupportEnds.Where(item => item.Mode != BeamManualSupportMode.UserDefinedFreeEndWithinHost))
            {
                FamilyInstance support = Resolve(document, beam, end, true, out string signature, out Solid solid, out Solid interior);
                if (end.SupportDocumentKey != plan.DocumentKey) throw new InvalidOperationException("Support belongs to another document; explicitly rebind.");
                supports.Add(support); supportSolids.Add(solid); supportInteriors.Add(interior); state.Add(signature);
            }
            Supports = supports;
            StateFingerprint = WorkflowFingerprint.Compute(state);
            _concrete = beamSolid;
            foreach (Solid solid in supportSolids) _concrete = Boolean(_concrete, solid, BooleanOperationsType.Union);
            // Beam lateral face covers only; an internal support interface is not an exposed Beam end.
            double Face(BeamManualCoverFace face) => UnitUtils.ConvertToInternalUnits(plan.Covers.Single(item => item.Face == face).ClearCoverMm, UnitTypeId.Millimeters);
            Solid beamInterior = Box(profile.StartPoint, profile.RightVector, profile.UpVector, profile.Direction,
                -profile.B/2+Face(BeamManualCoverFace.Left), profile.B/2-Face(BeamManualCoverFace.Right),
                -profile.H/2+Face(BeamManualCoverFace.Bottom), profile.H/2-Face(BeamManualCoverFace.Top), 0, profile.Length);
            Solid beamShell = Boolean(beamSolid, beamInterior, BooleanOperationsType.Difference);
            foreach (Solid solid in supportSolids) if (HasVolume(beamShell)) beamShell = Boolean(beamShell, solid, BooleanOperationsType.Difference);
            if (HasVolume(beamShell)) _coverShells.Add(beamShell);
            for (int i=0; i<supportSolids.Count; i++)
            {
                Solid shell = Boolean(supportSolids[i], supportInteriors[i], BooleanOperationsType.Difference);
                if (HasVolume(shell)) shell = Boolean(shell, beamSolid, BooleanOperationsType.Difference);
                for (int j=0; j<supportSolids.Count && HasVolume(shell); j++) if (i != j) shell = Boolean(shell, supportSolids[j], BooleanOperationsType.Difference);
                if (HasVolume(shell)) _coverShells.Add(shell);
            }
        }

        public void ValidateCurves(IEnumerable<Curve> curves, double radius)
        {
            Curve[] array = curves?.ToArray();
            if (array == null || array.Length == 0 || !(radius > 0) || double.IsInfinity(radius) || double.IsNaN(radius))
                throw new InvalidOperationException("Finite physical bar radius and solved curves are required.");
            foreach (Curve curve in array)
            {
                if (!(curve is Line || curve is Arc) || !curve.IsBound)
                    throw new InvalidOperationException("Support envelope currently supports exact bound Line/Arc centerlines only.");
                XYZ origin = curve.GetEndPoint(0);
                XYZ tangent = curve.ComputeDerivatives(curve.GetEndParameter(0), false).BasisX.Normalize();
                XYZ x = tangent.CrossProduct(Math.Abs(tangent.Z) < 0.9 ? XYZ.BasisZ : XYZ.BasisX).Normalize();
                XYZ y = tangent.CrossProduct(x).Normalize();
                var circle = new CurveLoop();
                circle.Append(Arc.Create(origin, radius, 0, Math.PI, x, y));
                circle.Append(Arc.Create(origin, radius, Math.PI, 2*Math.PI, x, y));
                var path = new CurveLoop(); path.Append(curve);
                CheckEnvelope(GeometryCreationUtilities.CreateSweptGeometry(path, 0, curve.GetEndParameter(0), new List<CurveLoop>{circle}));
                // Conservative spherical endpoint envelope preserves the existing physical end-radius rule.
                CheckEnvelope(Sphere(origin, radius));
                CheckEnvelope(Sphere(curve.GetEndPoint(1), radius));
            }
        }

        private void CheckEnvelope(Solid envelope)
        {
            double tolerance = Math.Max(1e-12, envelope.Volume*1e-8); // CSG numerical volume tolerance (ft³), not engineering cover.
            Solid outside = Boolean(envelope, _concrete, BooleanOperationsType.Difference);
            if (outside.Volume > tolerance) throw new InvalidOperationException("Physical Rebar envelope leaves the verified Beam/support concrete union.");
            foreach (Solid shell in _coverShells)
                if (Boolean(envelope, shell, BooleanOperationsType.Intersect).Volume > tolerance)
                    throw new InvalidOperationException("Physical Rebar envelope violates explicit cover on an exposed Beam/support face.");
        }

        private static FamilyInstance Resolve(Document document, FamilyInstance beam, BeamManualSupportEndV1 end, bool requireSignature,
            out string signature, out Solid solid, out Solid interior)
        {
            if (end == null || (end.End != BeamManualEnd.Start && end.End != BeamManualEnd.Finish) ||
                !end.SupportClearCoverMm.HasValue || double.IsNaN(end.SupportClearCoverMm.Value) || double.IsInfinity(end.SupportClearCoverMm.Value) || end.SupportClearCoverMm.Value < 0 ||
                string.IsNullOrWhiteSpace(end.IntendedContinuation) || string.IsNullOrWhiteSpace(end.SupportHostUniqueId))
                throw new InvalidOperationException("Explicit support identity, end, finite non-negative cover and continuation intent are required.");
            var support = document.GetElement(end.SupportHostUniqueId) as FamilyInstance;
            if (support == null || !support.IsValidObject || support.Document != document || support.Id == beam.Id ||
                RebarHostData.GetRebarHostData(support)?.IsValidHost() != true)
                throw new InvalidOperationException("Selected support must be a distinct valid same-document concrete reinforcement host.");
            double cover = UnitUtils.ConvertToInternalUnits(end.SupportClearCoverMm.Value, UnitTypeId.Millimeters);
            if (end.Mode == BeamManualSupportMode.VerifiedColumn && support.Category.IsCategory(BuiltInCategory.OST_StructuralColumns))
            {
                var p = RectangularColumnGeometryHelper.GetRectangularProfile(support);
                XYZ x=new XYZ(Math.Cos(p.RotationRad),Math.Sin(p.RotationRad),0), y=new XYZ(-Math.Sin(p.RotationRad),Math.Cos(p.RotationRad),0);
                interior=Box(p.BaseCenter,x,y,XYZ.BasisZ,-p.B/2+cover,p.B/2-cover,-p.H/2+cover,p.H/2-cover,cover,p.Height-cover);
            }
            else if (end.Mode == BeamManualSupportMode.VerifiedStructuralBeam && support.Category.IsCategory(BuiltInCategory.OST_StructuralFraming))
            {
                var p=BeamGeometryHelper.GetBeamProfile(support);
                interior=Box(p.StartPoint,p.RightVector,p.UpVector,p.Direction,-p.B/2+cover,p.B/2-cover,-p.H/2+cover,p.H/2-cover,cover,p.Length-cover);
            }
            else throw new InvalidOperationException("Support mode/category mismatch. Only verified rectangular structural Column/Beam supports are implemented.");
            solid=ReadSingleSolid(support);
            var beamProfile=BeamGeometryHelper.GetBeamProfile(beam);
            double probe=UnitUtils.ConvertToInternalUnits(0.1,UnitTypeId.Millimeters); // Numerical contact probe, not a support length/default.
            double station=end.End==BeamManualEnd.Start?0:beamProfile.Length;
            Solid endProbe=Box(beamProfile.StartPoint,beamProfile.RightVector,beamProfile.UpVector,beamProfile.Direction,
                -beamProfile.B/2,beamProfile.B/2,-beamProfile.H/2,beamProfile.H/2,station-probe,station+probe);
            if (!HasVolume(Boolean(endProbe,solid,BooleanOperationsType.Intersect)))
                throw new InvalidOperationException("Selected support does not contact the specified physical Beam end; wrong-end, gapped or unrelated support is rejected.");
            var vertices=solid.Edges.Cast<Edge>().SelectMany(edge=>new[]{edge.AsCurve().GetEndPoint(0),edge.AsCurve().GetEndPoint(1)})
                .Select(point=>string.Join(",",N(point.X),N(point.Y),N(point.Z))).OrderBy(value=>value,StringComparer.Ordinal);
            signature=WorkflowFingerprint.Compute(new[]{support.UniqueId,support.VersionGuid.ToString("D"),support.GetTypeId().ToLongValue().ToString(CultureInfo.InvariantCulture),N(solid.Volume)}.Concat(vertices));
            if (requireSignature && !String.Equals(signature,end.SupportGeometrySignature,StringComparison.Ordinal))
                throw new InvalidOperationException("Support geometry/type/state changed since binding. Explicitly rebind and solve again.");
            return support;
        }

        private static Solid Sphere(XYZ center, double radius)
        {
            var loop=new CurveLoop();
            loop.Append(Arc.Create(center+XYZ.BasisZ*radius,center-XYZ.BasisZ*radius,center+XYZ.BasisX*radius));
            loop.Append(Line.CreateBound(center-XYZ.BasisZ*radius,center+XYZ.BasisZ*radius));
            return GeometryCreationUtilities.CreateRevolvedGeometry(new Frame(center,XYZ.BasisX,XYZ.BasisY,XYZ.BasisZ),new List<CurveLoop>{loop},0,2*Math.PI);
        }
        internal static Solid Box(XYZ origin,XYZ x,XYZ y,XYZ z,double x0,double x1,double y0,double y1,double z0,double z1)
        {
            if (!(x1>x0 && y1>y0 && z1>z0)) throw new InvalidOperationException("Explicit cover consumes the entire verified support/Beam prism.");
            var p=new[]{origin+x*x0+y*y0+z*z0,origin+x*x1+y*y0+z*z0,origin+x*x1+y*y1+z*z0,origin+x*x0+y*y1+z*z0};
            var loop=new CurveLoop(); for(int i=0;i<4;i++) loop.Append(Line.CreateBound(p[i],p[(i+1)%4]));
            return GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop>{loop},z,z1-z0);
        }
        internal static Solid ReadSingleSolid(Element element)
        {
            var result=new List<Solid>();
            void Read(GeometryElement geometry) { if(geometry==null)return; foreach(GeometryObject item in geometry) {
                if(item is Solid solid && HasVolume(solid))result.Add(solid);
                else if(item is GeometryInstance instance)Read(instance.GetInstanceGeometry()); } }
            Read(element.get_Geometry(new Options{DetailLevel=ViewDetailLevel.Fine,ComputeReferences=false}));
            if(result.Count!=1)throw new InvalidOperationException("Support geometry must contain one verified concrete solid.");
            return result[0];
        }
        internal static Solid Boolean(Solid a,Solid b,BooleanOperationsType operation)
        {
            try { return BooleanOperationsUtils.ExecuteBooleanOperation(a,b,operation) ?? throw new InvalidOperationException("Solid operation returned no result."); }
            catch(Exception ex) { throw new InvalidOperationException("Cannot prove support physical envelope/cover ("+operation+"); generation is blocked.",ex); }
        }
        private static bool HasVolume(Solid solid)=>solid!=null && solid.Volume>1e-12;
        private static string N(double value)=>value.ToString("R",CultureInfo.InvariantCulture);
    }
}
