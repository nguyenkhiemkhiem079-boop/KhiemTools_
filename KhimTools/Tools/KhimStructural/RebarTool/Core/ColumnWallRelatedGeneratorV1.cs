using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;
using KhimTools.Core.Workflow;
using Newtonsoft.Json;

namespace KhimTools.RebarTool.Core
{
    internal sealed class ColumnWallRelatedFrameV1
    {
        internal XYZ Origin,X,Up,Depth;
        internal double Width,Height,Thickness;
        internal Solid Concrete;
        internal string Signature;
        internal XYZ World(WallManualPointV1 p)=>Origin+X*WallNativeFrameV1.Feet(p.XMm.Value)+Up*WallNativeFrameV1.Feet(p.YMm.Value)+Depth*WallNativeFrameV1.Feet(p.DepthMm.Value);
        internal static ColumnWallRelatedFrameV1 Read(FamilyInstance column)
        {
            if(column==null||!column.IsValidObject||!column.Category.IsCategory(BuiltInCategory.OST_StructuralColumns)||RebarHostData.GetRebarHostData(column)?.IsValidHost()!=true)
                throw new InvalidOperationException("ContractB requires an actual valid structural Column Rebar host, never a Wall/link or assumed Wall proxy.");
            var p=RectangularColumnGeometryHelper.GetRectangularProfile(column);var transform=column.GetTransform();
            var x=transform.BasisX.Normalize();var depth=transform.BasisY.Normalize();
            var solid=BeamManualSupportGeometryV1.ReadSingleSolid(column);
            return new ColumnWallRelatedFrameV1{Origin=p.BaseCenter-x*(p.B/2)-depth*(p.H/2),X=x,Up=XYZ.BasisZ,Depth=depth,Width=p.B,Thickness=p.H,Height=p.Height,Concrete=solid,
                Signature=WorkflowFingerprint.Compute(new[]{column.UniqueId,column.VersionGuid.ToString("D"),column.Symbol.VersionGuid.ToString("D"),WallNativeFrameV1.P(x),WallNativeFrameV1.P(depth),WallNativeFrameV1.P(p.BaseCenter)}.Concat(solid.Edges.Cast<Edge>().SelectMany(e=>new[]{WallNativeFrameV1.P(e.AsCurve().GetEndPoint(0)),WallNativeFrameV1.P(e.AsCurve().GetEndPoint(1))}).OrderBy(v=>v,StringComparer.Ordinal)))};
        }
    }
    internal sealed class ColumnWallRelatedGeneratorV1
    {
        private readonly Document _doc;
        internal ColumnWallRelatedGeneratorV1(Document doc){_doc=doc??throw new ArgumentNullException(nameof(doc));}
        internal string DocumentKey=>WorkflowFingerprint.Compute(_doc.ProjectInformation.UniqueId,_doc.PathName,_doc.Title);
        internal ColumnWallRelatedPlanV1 Blank(FamilyInstance column)=>new ColumnWallRelatedPlanV1{DocumentKey=DocumentKey,HostColumnUniqueId=column.UniqueId,FrameSignature=ColumnWallRelatedFrameV1.Read(column).Signature};
        private ColumnWallRelatedFrameV1 Validate(FamilyInstance column,ColumnWallRelatedPlanV1 plan)
        {
            ExplicitStraightDetailingV1.RequireKnownReinforcement(_doc);
            var errors=ColumnWallRelatedPlanV1.Validate(plan);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            if(column.Document!=_doc||column.UniqueId!=plan.HostColumnUniqueId||plan.DocumentKey!=DocumentKey)throw new InvalidOperationException("ContractB exact Column document/identity mismatch.");
            var frame=ColumnWallRelatedFrameV1.Read(column);if(frame.Signature!=plan.FrameSignature)throw new InvalidOperationException("Column geometry/rotation/mirror/state changed; explicitly review a new plan.");return frame;
        }
        private RebarBarType Type(BeamManualElementReferenceV1 reference)
        {
            var type=_doc.GetElement(reference.UniqueId) as RebarBarType;
            if(type==null||!type.IsValidObject||reference.DocumentKey!=DocumentKey||type.Name!=reference.Name||Math.Abs(WallNativeFrameV1.Mm(type.BarModelDiameter)-reference.DiameterMm)>0.01)
                throw new InvalidOperationException("Exact ContractB loaded bar type changed/missing; no silent substitution.");return type;
        }
        private Rebar[] Neighbors(FamilyInstance column)
        {
            var b=column.get_BoundingBox(null)??throw new InvalidOperationException("Column bounds unavailable.");
            var points=(from x in new[]{b.Min.X,b.Max.X} from y in new[]{b.Min.Y,b.Max.Y} from z in new[]{b.Min.Z,b.Max.Z} select b.Transform.OfPoint(new XYZ(x,y,z))).ToArray();
            var filter=new BoundingBoxIntersectsFilter(new Outline(new XYZ(points.Min(p=>p.X),points.Min(p=>p.Y),points.Min(p=>p.Z)),new XYZ(points.Max(p=>p.X),points.Max(p=>p.Y),points.Max(p=>p.Z))));
            return new FilteredElementCollector(_doc).OfClass(typeof(Rebar)).Cast<Rebar>().Where(bar=>bar.GetHostId()==column.Id||filter.PassesFilter(_doc,bar.Id)).ToArray();
        }
        internal string Fingerprint(FamilyInstance column,ColumnWallRelatedPlanV1 plan,ISet<string> excludeOwnedBars=null)
        {
            var frame=Validate(column,plan);
            return WorkflowFingerprint.Compute(new[]{JsonConvert.SerializeObject(plan),frame.Signature}.Concat(plan.Bars.Select(b=>Type(b.BarType).VersionGuid.ToString("D")))
                .Concat(Neighbors(column).Where(b=>excludeOwnedBars==null||!excludeOwnedBars.Contains(b.UniqueId)).Select(RebarPreviewService.FingerprintBar).OrderBy(v=>v,StringComparer.Ordinal)));
        }
        internal IList<Rebar> Generate(FamilyInstance column,ColumnWallRelatedPlanV1 plan,IDictionary<string,string> roles=null)
        {
            if(!_doc.IsModifiable)throw new InvalidOperationException("ContractB generation requires caller-owned active transaction.");
            var frame=Validate(column,plan);double F(double? mm)=>WallNativeFrameV1.Feet(mm.Value);
            var interior=BeamManualSupportGeometryV1.Box(frame.Origin,frame.X,frame.Up,frame.Depth,F(plan.Covers.LeftMm),frame.Width-F(plan.Covers.RightMm),F(plan.Covers.BottomMm),frame.Height-F(plan.Covers.TopMm),F(plan.Covers.FrontMm),frame.Thickness-F(plan.Covers.BackMm));
            var envelope=new BeamManualSupportGeometryV1(frame.Concrete,new[]{BeamManualSupportGeometryV1.Boolean(frame.Concrete,interior,BooleanOperationsType.Difference)});
            var neighbors=Neighbors(column).ToList();var created=new List<Rebar>();
            foreach(var input in plan.Bars) {
                var type=Type(input.BarType);var a=frame.World(input.Start);var b=frame.World(input.Finish);var line=Line.CreateBound(a,b);
                ExplicitStraightDetailingV1.Verify(line,frame.Concrete,input);envelope.ValidateCurves(new[]{line},type.BarModelDiameter/2);
                var direction=(b-a).Normalize();var normal=frame.Depth-direction*frame.Depth.DotProduct(direction);if(normal.GetLength()<1e-6)normal=frame.X;
                var bar=RebarShapeCreationHelper.CreateFromCurvesSafe(_doc,RebarStyle.Standard,type,null,null,column,normal.Normalize(),new List<Curve>{line},RebarHookOrientation.Right,RebarHookOrientation.Right,allowLegacyFallback:false);
                if(bar==null||!bar.IsValidObject||bar.GetHostId()!=column.Id||bar.GetTypeId()!=type.Id||bar.Quantity!=1)throw new InvalidOperationException("ContractB Column host/type/quantity postcondition failed.");
                var curves=RebarSolvedCenterlineReaderV1.Read(bar,0);
                if(curves.Count!=1||!(curves[0] is Line actual)||!((actual.GetEndPoint(0).DistanceTo(a)<WallNativeFrameV1.Feet(0.01)&&actual.GetEndPoint(1).DistanceTo(b)<WallNativeFrameV1.Feet(0.01))||(actual.GetEndPoint(0).DistanceTo(b)<WallNativeFrameV1.Feet(0.01)&&actual.GetEndPoint(1).DistanceTo(a)<WallNativeFrameV1.Feet(0.01))))throw new InvalidOperationException("ContractB solver altered explicit straight geometry.");
                envelope.ValidateCurves(curves,type.BarModelDiameter/2);RebarSolvedEnvelopeCollisionV1.Verify(bar,neighbors);
                foreach(var old in neighbors) {
                    var oldType=_doc.GetElement(old.GetTypeId()) as RebarBarType??throw new InvalidOperationException("Neighbor type unavailable.");double radius=(type.BarModelDiameter+oldType.BarModelDiameter)/2-WallNativeFrameV1.Feet(0.01);
                    foreach(var path in RebarPreviewService.ReadPaths(new List<Rebar>{old}))for(int i=0;i+1<path.Points.Count;i++) {
                        var p=path.Points[i];var q=path.Points[i+1];
                        if(radius>0&&BeamManualSegmentDistanceV1.SquaredDistance(new BeamManualPoint3V1(a.X,a.Y,a.Z),new BeamManualPoint3V1(b.X,b.Y,b.Z),new BeamManualPoint3V1(p.X,p.Y,p.Z),new BeamManualPoint3V1(q.X,q.Y,q.Z))<radius*radius)
                            throw new InvalidOperationException("ContractB intersects existing/earlier reinforcing, including other-host bars crossing the Column; rollback required.");
                    }
                }
                created.Add(bar);neighbors.Add(bar);if(roles!=null)roles[bar.UniqueId]="column-hosted-wall-"+input.Role.ToLowerInvariant();
            }
            return created;
        }
    }
}
