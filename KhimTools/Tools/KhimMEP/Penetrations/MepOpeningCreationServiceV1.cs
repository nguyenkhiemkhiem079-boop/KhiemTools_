using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;
using KhimTools.Core.Workflow;
using KhimTools.RebarTool.Core;

namespace KhimTools.MEP.Penetrations
{
    internal sealed class MepOpeningProposalV1
    {
        internal Element Host;
        internal MEPCurve Mep;
        internal XYZ Origin,X,Y,Depth;
        internal double Thickness;
        internal double Width,Height;
        internal Solid Before,Cut;
        internal MepOpeningCutPlanV1 Plan;
        internal string Fingerprint;
        internal XYZ Point(double x,double y)=>Origin+X*WallNativeFrameV1.Feet(x)+Y*WallNativeFrameV1.Feet(y);
    }
    /// <summary>Caller-owned transaction only. Verified exact solid clash, conservative reviewed rectangle, native cut postconditions.</summary>
    internal static class MepOpeningCreationServiceV1
    {
        private const double VolumeTolerance=1e-9; // ft³ numerical tolerance, not design clearance.
        internal static MepOpeningProposalV1 Propose(Document doc,Element host,MEPCurve mep,double? clearanceMm)
            =>Propose(doc,host,mep,clearanceMm,0,0,0,0);
        internal static MepOpeningProposalV1 Propose(Document doc,Element host,MEPCurve mep,double? clearanceMm,
            double? minimumWidthMm,double? minimumHeightMm,double? offsetXMm,double? offsetYMm)
        {
            if(doc==null || host?.Document!=doc || mep?.Document!=doc || !host.IsValidObject || !mep.IsValidObject)
                throw new InvalidOperationException("Exact same-document host and MEP identity required; links unsupported.");
            if(!MepCurveMeasurementService.IsSupported(mep) || !(mep.Location is LocationCurve location) || !(location.Curve is Line axis))
                throw new InvalidOperationException("Only straight local duct/pipe/tray penetrations are supported.");
            if(JoinGeometryUtils.GetJoinedElements(doc,host).Count>0)
                throw new InvalidOperationException("Joined host cut side effects are not supported; no connected element may be altered implicitly.");
            if(host is Wall && host.Location is LocationCurve wallLocation &&
                new[]{0,1}.Any(end=>wallLocation.get_ElementsAtJoin(end).Cast<Element>().Any(element=>element.Id!=host.Id)))
                throw new InvalidOperationException("Wall end joins are unsupported for opening creation; use independently reviewed unjoined QA hosts.");
            var p=new MepOpeningProposalV1{Host=host,Mep=mep};double width,height;
            if(host is Wall wall)
            {
                var frame=WallNativeFrameV1.Read(wall);p.Origin=frame.Origin;p.X=frame.X;p.Y=frame.Up;p.Depth=frame.Depth;
                p.Thickness=frame.Thickness;p.Before=frame.Concrete;width=frame.Length;height=frame.Height;
            }
            else if(host is Floor)
            {
                if(host.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL)?.AsInteger()!=1)
                    throw new InvalidOperationException("Opening creation requires an explicitly structural Floor.");
                p.Before=BeamManualSupportGeometryV1.ReadSingleSolid(host);
                var faces=p.Before.Faces.Cast<Face>().OfType<PlanarFace>().ToArray();
                if(p.Before.Faces.Size!=6 || faces.Length!=6)throw new InvalidOperationException("Floor requires one uncut rectangular horizontal prism; slopes/openings/non-prismatic floors unsupported.");
                var vertices=p.Before.Edges.Cast<Edge>().SelectMany(e=>new[]{e.AsCurve().GetEndPoint(0),e.AsCurve().GetEndPoint(1)}).ToArray();
                XYZ[] axes={XYZ.BasisX,XYZ.BasisY,XYZ.BasisZ};double[] min=axes.Select(a=>vertices.Min(v=>v.DotProduct(a))).ToArray(),max=axes.Select(a=>vertices.Max(v=>v.DotProduct(a))).ToArray();
                for(int i=0;i<3;i++)foreach(int sign in new[]{-1,1})if(faces.Count(f=>f.FaceNormal.Normalize().DotProduct(axes[i])*sign>1-1e-6)!=1)
                    throw new InvalidOperationException("Floor must have axis-aligned orthogonal physical faces.");
                p.Origin=new XYZ(min[0],min[1],min[2]);p.X=XYZ.BasisX;p.Y=XYZ.BasisY;p.Depth=XYZ.BasisZ;
                width=max[0]-min[0];height=max[1]-min[1];p.Thickness=max[2]-min[2];
                if(width<=0 || height<=0 || p.Thickness<=0 || Math.Abs(width*height*p.Thickness-p.Before.Volume)>Math.Max(VolumeTolerance,p.Before.Volume*1e-7))
                    throw new InvalidOperationException("Floor prism envelope does not match actual concrete.");
            }
            else throw new InvalidOperationException("Creation supports verified rectangular Wall/Floor only; Beam, linked and family cuts unsupported.");
            p.Width=width;p.Height=height;
            if(Math.Abs(axis.Direction.DotProduct(p.Depth))<1-1e-6)throw new InvalidOperationException("Oblique or parallel penetration unsupported; axis must be perpendicular to the host.");
            double d0=(axis.GetEndPoint(0)-p.Origin).DotProduct(p.Depth),d1=(axis.GetEndPoint(1)-p.Origin).DotProduct(p.Depth);
            if(Math.Min(d0,d1)>=0 || Math.Max(d0,d1)<=p.Thickness)throw new InvalidOperationException("MEP must completely traverse both host faces; endpoint-only clash is not a through opening.");
            var mepSolid=BeamManualSupportGeometryV1.ReadSingleSolid(mep);
            var overlap=BeamManualSupportGeometryV1.Boolean(p.Before,mepSolid,BooleanOperationsType.Intersect);
            if(overlap.Volume<=VolumeTolerance)throw new InvalidOperationException("No actual solid clash; no opening proposal.");
            // Native solid bounds contain the entire actual clash. Projection is deliberately
            // conservative (not a minimum profile); the user reviews the resulting rectangle.
            var points=Bounds(overlap.GetBoundingBox()).ToArray();
            double Mm(double ft)=>WallNativeFrameV1.Mm(ft);
            p.Plan=MepOpeningCutPlanV1.Resolve(Mm(points.Min(v=>(v-p.Origin).DotProduct(p.X))),Mm(points.Max(v=>(v-p.Origin).DotProduct(p.X))),
                Mm(points.Min(v=>(v-p.Origin).DotProduct(p.Y))),Mm(points.Max(v=>(v-p.Origin).DotProduct(p.Y))),Mm(width),Mm(height),clearanceMm,
                minimumWidthMm,minimumHeightMm,offsetXMm,offsetYMm);
            p.Cut=BeamManualSupportGeometryV1.Box(p.Origin,p.X,p.Y,p.Depth,WallNativeFrameV1.Feet(p.Plan.X0),WallNativeFrameV1.Feet(p.Plan.X1),
                WallNativeFrameV1.Feet(p.Plan.Y0),WallNativeFrameV1.Feet(p.Plan.Y1),0,p.Thickness);
            RejectReinforcement(doc,p);
            p.Fingerprint=WorkflowFingerprint.Compute(doc.ProjectInformation.UniqueId,doc.PathName,doc.Title,host.UniqueId,host.VersionGuid.ToString("D"),mep.UniqueId,mep.VersionGuid.ToString("D"),
                string.Join(";",new[]{p.Plan.X0,p.Plan.X1,p.Plan.Y0,p.Plan.Y1,p.Plan.ClearanceMm,p.Plan.MinimumWidthMm,p.Plan.MinimumHeightMm,p.Plan.OffsetXMm,p.Plan.OffsetYMm,p.Thickness,p.Before.Volume}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
            return p;
        }
        private static IEnumerable<XYZ> Bounds(BoundingBoxXYZ box)
        {
            if(box==null)throw new InvalidOperationException("Exact native bounds unavailable.");
            foreach(double x in new[]{box.Min.X,box.Max.X})foreach(double y in new[]{box.Min.Y,box.Max.Y})foreach(double z in new[]{box.Min.Z,box.Max.Z})
                yield return box.Transform.OfPoint(new XYZ(x,y,z));
        }
        private static void RejectReinforcement(Document doc,MepOpeningProposalV1 p)
        {
            foreach(var element in new FilteredElementCollector(doc).WhereElementIsNotElementType())
                if(element is RebarInSystem || element is FabricSheet || element is FabricArea || element is AreaReinforcement || element is PathReinforcement)
                    throw new InvalidOperationException("System/fabric reinforcement exists; its cut safety is not implemented. Use separately approved coordination, never silently ignore it.");
            var corners=Bounds(p.Cut.GetBoundingBox()).ToArray();
            XYZ min=new XYZ(corners.Min(v=>v.X),corners.Min(v=>v.Y),corners.Min(v=>v.Z)),max=new XYZ(corners.Max(v=>v.X),corners.Max(v=>v.Y),corners.Max(v=>v.Z));
            // Conservative safety guard: do not cut through any nearby Rebar envelope,
            // regardless of host. Uncertain/missing geometry fails closed, never trimmed.
            foreach(var bar in new FilteredElementCollector(doc).OfClass(typeof(Rebar)).Cast<Rebar>())
            {
                var type=doc.GetElement(bar.GetTypeId()) as RebarBarType ?? throw new InvalidOperationException("Rebar type unavailable; cannot prove safe cut.");
                double radius=type.BarModelDiameter/2;if(!(radius>0))throw new InvalidOperationException("Rebar diameter unavailable.");
                var vertices=Bounds(bar.get_BoundingBox(null)).ToArray();
                if(vertices.Max(v=>v.X)+radius>=min.X && vertices.Min(v=>v.X)-radius<=max.X && vertices.Max(v=>v.Y)+radius>=min.Y && vertices.Min(v=>v.Y)-radius<=max.Y && vertices.Max(v=>v.Z)+radius>=min.Z && vertices.Min(v=>v.Z)-radius<=max.Z)
                    throw new InvalidOperationException("Opening intersects a conservative existing-Rebar envelope; requires separate approved reinforcement redesign. No bar is deleted or trimmed.");
            }
        }
        internal static Opening Create(Document doc,MepOpeningProposalV1 accepted)
        {
            if(!doc.IsModifiable)throw new InvalidOperationException("Caller-owned active transaction required.");
            var fresh=Propose(doc,accepted.Host,accepted.Mep,accepted.Plan.ClearanceMm,accepted.Plan.MinimumWidthMm,accepted.Plan.MinimumHeightMm,accepted.Plan.OffsetXMm,accepted.Plan.OffsetYMm);
            if(fresh.Fingerprint!=accepted.Fingerprint)throw new InvalidOperationException("Opening proposal stale; review again.");
            Opening opening;
            if(fresh.Host is Wall wall)opening=doc.Create.NewOpening(wall,fresh.Point(fresh.Plan.X0,fresh.Plan.Y0),fresh.Point(fresh.Plan.X1,fresh.Plan.Y1));
            else {
                var loop=new CurveArray();var points=new[]{fresh.Point(fresh.Plan.X0,fresh.Plan.Y0),fresh.Point(fresh.Plan.X1,fresh.Plan.Y0),fresh.Point(fresh.Plan.X1,fresh.Plan.Y1),fresh.Point(fresh.Plan.X0,fresh.Plan.Y1)};
                for(int i=0;i<4;i++)loop.Append(Line.CreateBound(points[i],points[(i+1)%4]));opening=doc.Create.NewOpening(fresh.Host,loop,false);
            }
            doc.Regenerate();
            if(opening==null || !opening.IsValidObject || opening.Host?.Id!=fresh.Host.Id)throw new InvalidOperationException("Native opening host/identity postcondition failed.");
            var remaining=BeamManualSupportGeometryV1.ReadSingleSolid(fresh.Host);
            var removed=BeamManualSupportGeometryV1.Boolean(fresh.Before,remaining,BooleanOperationsType.Difference);
            double tolerance=Math.Max(VolumeTolerance,fresh.Cut.Volume*1e-7);
            if(Math.Abs(removed.Volume-fresh.Cut.Volume)>tolerance || BeamManualSupportGeometryV1.Boolean(removed,fresh.Cut,BooleanOperationsType.Difference).Volume>tolerance ||
                BeamManualSupportGeometryV1.Boolean(remaining,fresh.Before,BooleanOperationsType.Difference).Volume>tolerance ||
                BeamManualSupportGeometryV1.Boolean(fresh.Cut,removed,BooleanOperationsType.Difference).Volume>tolerance ||
                BeamManualSupportGeometryV1.Boolean(remaining,BeamManualSupportGeometryV1.ReadSingleSolid(fresh.Mep),BooleanOperationsType.Intersect).Volume>tolerance)
                throw new InvalidOperationException("Actual cut geometry/clash clearance differs from reviewed proposal; whole creation must roll back.");
            return opening;
        }
    }
}
