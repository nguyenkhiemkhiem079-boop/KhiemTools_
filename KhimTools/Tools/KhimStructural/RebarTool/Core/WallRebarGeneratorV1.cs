using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;
using KhimTools.Core.Workflow;
using Newtonsoft.Json;

namespace KhimTools.RebarTool.Core
{
    internal sealed class WallNativeFrameV1
    {
        public XYZ Origin, X, Up, Depth;
        public double Length, Height, Thickness;
        public string Signature;
        public Solid Concrete;
        public XYZ World(WallManualPointV1 p)=>Origin+X*Feet(p.XMm.Value)+Up*Feet(p.YMm.Value)+Depth*Feet(p.DepthMm.Value);
        public static double Feet(double mm)=>UnitUtils.ConvertToInternalUnits(mm,UnitTypeId.Millimeters);
        public static double Mm(double feet)=>UnitUtils.ConvertFromInternalUnits(feet,UnitTypeId.Millimeters);
        public static WallNativeFrameV1 Read(Wall wall)
        {
            if(wall==null || !wall.IsValidObject || wall.WallType.Kind!=WallKind.Basic ||
                wall.get_Parameter(BuiltInParameter.WALL_STRUCTURAL_SIGNIFICANT)?.AsInteger()!=1 || RebarHostData.GetRebarHostData(wall)?.IsValidHost()!=true)
                throw new InvalidOperationException("Select a valid structural Basic Wall concrete reinforcement host, not a Column/link/curtain wall.");
            if(!(wall.Location is LocationCurve location) || !(location.Curve is Line line) || Math.Abs(line.Direction.Z)>1e-6)
                throw new InvalidOperationException("Only straight horizontal-location Walls are supported.");
            XYZ normal=wall.Orientation.Normalize();
            if(Math.Abs(normal.Z)>1e-6)throw new InvalidOperationException("Slanted/tilted Wall faces are unsupported.");
            XYZ x=XYZ.BasisZ.CrossProduct(normal).Normalize(), depth=-normal, up=XYZ.BasisZ;
            Solid solid=BeamManualSupportGeometryV1.ReadSingleSolid(wall);
            var faces=solid.Faces.Cast<Face>().OfType<PlanarFace>().ToArray();
            if(solid.Faces.Size!=6 || faces.Length!=6)throw new InvalidOperationException("Wall requires one six-face rectangular solid; openings, joins/cuts and non-prismatic conditions fail closed.");
            var vertices=solid.Edges.Cast<Edge>().SelectMany(edge=>new[]{edge.AsCurve().GetEndPoint(0),edge.AsCurve().GetEndPoint(1)}).ToArray();
            XYZ[] axes={x,up,depth};double[] min=axes.Select(axis=>vertices.Min(p=>p.DotProduct(axis))).ToArray(), max=axes.Select(axis=>vertices.Max(p=>p.DotProduct(axis))).ToArray();
            double tolerance=WallNativeFrameV1.Feet(0.01);
            if(Enumerable.Range(0,3).Any(i=>max[i]-min[i]<=tolerance))throw new InvalidOperationException("Invalid physical Wall dimensions.");
            for(int i=0;i<3;i++)foreach(int sign in new[]{-1,1})
                if(faces.Count(face=>face.FaceNormal.Normalize().DotProduct(axes[i])*sign>1-1e-6)!=1)
                    throw new InvalidOperationException("Wall physical faces must align with its verified orthogonal frame.");
            var corners=new HashSet<string>();
            foreach(var vertex in vertices) {
                string key="";for(int i=0;i<3;i++) { double v=vertex.DotProduct(axes[i]);
                    if(Math.Abs(v-min[i])<=tolerance)key+="0";else if(Math.Abs(v-max[i])<=tolerance)key+="1";
                    else throw new InvalidOperationException("Wall contains non-prism edge vertices."); } corners.Add(key);
            }
            double volume=(max[0]-min[0])*(max[1]-min[1])*(max[2]-min[2]);
            if(corners.Count!=8 || Math.Abs(volume-solid.Volume)>Math.Max(1e-8,volume*1e-7))throw new InvalidOperationException("Wall solid does not match its actual rectangular envelope.");
            return new WallNativeFrameV1 {Origin=x*min[0]+up*min[1]+depth*min[2],X=x,Up=up,Depth=depth,
                Length=max[0]-min[0],Height=max[1]-min[1],Thickness=max[2]-min[2],Concrete=solid,
                Signature=WorkflowFingerprint.Compute(new[]{wall.UniqueId,wall.VersionGuid.ToString("D"),P(line.GetEndPoint(0)),P(line.GetEndPoint(1)),P(normal)}.Concat(vertices.Select(P).OrderBy(v=>v,StringComparer.Ordinal)))};
        }
        internal static string P(XYZ p)=>string.Join(",",p.X.ToString("R",CultureInfo.InvariantCulture),p.Y.ToString("R",CultureInfo.InvariantCulture),p.Z.ToString("R",CultureInfo.InvariantCulture));
    }
    internal sealed class WallRebarGeneratorV1
    {
        private readonly Document _doc;
        public WallRebarGeneratorV1(Document doc){_doc=doc ?? throw new ArgumentNullException(nameof(doc));}
        public string DocumentKey=>WorkflowFingerprint.Compute(_doc.ProjectInformation.UniqueId,_doc.PathName,_doc.Title);
        public WallManualPlanV1 Blank(Wall wall)=>new WallManualPlanV1{DocumentKey=DocumentKey,HostUniqueId=wall.UniqueId,FrameSignature=WallNativeFrameV1.Read(wall).Signature};
        public WallBoundaryColumnV1 BindColumn(Wall wall,FamilyInstance column,bool left,double coverMm,string intent)
        {
            var input=new WallBoundaryColumnV1{UniqueId=column?.UniqueId,DocumentKey=DocumentKey,ExposedClearCoverMm=coverMm,Intent=intent};
            Column(wall,WallNativeFrameV1.Read(wall),input,left,false,out _,out _,out string signature);
            input.GeometrySignature=signature;return input;
        }
        private FamilyInstance Column(Wall wall,WallNativeFrameV1 frame,WallBoundaryColumnV1 input,bool left,bool checkSignature,out Solid concrete,out Solid interior,out string signature)
        {
            if(input==null || !WallManualPlanValidatorV1.Finite(input.ExposedClearCoverMm) || input.ExposedClearCoverMm<0 || string.IsNullOrWhiteSpace(input.Intent) || input.DocumentKey!=DocumentKey)
                throw new InvalidOperationException("Explicit boundary Column identity/cover/intent in this document is required.");
            var column=_doc.GetElement(input.UniqueId) as FamilyInstance;
            if(column==null || !column.IsValidObject || !column.Category.IsCategory(BuiltInCategory.OST_StructuralColumns) || RebarHostData.GetRebarHostData(column)?.IsValidHost()!=true)
                throw new InvalidOperationException("Boundary support must be an exact structural concrete Column. Wall remains the Rebar host.");
            var p=RectangularColumnGeometryHelper.GetRectangularProfile(column); double c=WallNativeFrameV1.Feet(input.ExposedClearCoverMm.Value);
            XYZ x=new XYZ(Math.Cos(p.RotationRad),Math.Sin(p.RotationRad),0),y=new XYZ(-Math.Sin(p.RotationRad),Math.Cos(p.RotationRad),0);
            interior=BeamManualSupportGeometryV1.Box(p.BaseCenter,x,y,XYZ.BasisZ,-p.B/2+c,p.B/2-c,-p.H/2+c,p.H/2-c,c,p.Height-c);
            concrete=BeamManualSupportGeometryV1.ReadSingleSolid(column);
            double e=WallNativeFrameV1.Feet(0.1), s=left?0:frame.Length;
            var probe=BeamManualSupportGeometryV1.Box(frame.Origin,frame.X,frame.Up,frame.Depth,s-e,s+e,0,frame.Height,0,frame.Thickness);
            if(BeamManualSupportGeometryV1.Boolean(probe,concrete,BooleanOperationsType.Intersect).Volume<=1e-12)throw new InvalidOperationException("Column does not contact the selected physical Wall boundary.");
            signature=WorkflowFingerprint.Compute(new[]{column.UniqueId,column.VersionGuid.ToString("D"),column.Symbol.VersionGuid.ToString("D")}.Concat(
                concrete.Edges.Cast<Edge>().SelectMany(edge=>new[]{WallNativeFrameV1.P(edge.AsCurve().GetEndPoint(0)),WallNativeFrameV1.P(edge.AsCurve().GetEndPoint(1))}).OrderBy(value=>value,StringComparer.Ordinal)));
            if(checkSignature && signature!=input.GeometrySignature)throw new InvalidOperationException("Boundary Column changed; rebind and solve again.");
            return column;
        }
        private BeamManualSupportGeometryV1 Envelope(Wall wall,WallManualPlanV1 plan,WallNativeFrameV1 frame,out FamilyInstance[] columns)
        {
            var supports=new List<FamilyInstance>();var solids=new List<Solid>();var interiors=new List<Solid>();
            foreach(bool left in new[]{true,false}) { var input=left?plan.LeftColumn:plan.RightColumn;if(input==null)continue;
                supports.Add(Column(wall,frame,input,left,true,out Solid solid,out Solid interior,out _));solids.Add(solid);interiors.Add(interior); }
            columns=supports.ToArray();
            double F(double? mm)=>WallNativeFrameV1.Feet(mm.Value);
            Solid inner=BeamManualSupportGeometryV1.Box(frame.Origin,frame.X,frame.Up,frame.Depth,F(plan.Covers.LeftMm),frame.Length-F(plan.Covers.RightMm),
                F(plan.Covers.BottomMm),frame.Height-F(plan.Covers.TopMm),F(plan.Covers.ExteriorMm),frame.Thickness-F(plan.Covers.InteriorMm));
            Solid union=frame.Concrete,wallShell=BeamManualSupportGeometryV1.Boolean(frame.Concrete,inner,BooleanOperationsType.Difference);
            foreach(var solid in solids){union=BeamManualSupportGeometryV1.Boolean(union,solid,BooleanOperationsType.Union);if(wallShell.Volume>1e-12)wallShell=BeamManualSupportGeometryV1.Boolean(wallShell,solid,BooleanOperationsType.Difference);}
            var shells=new List<Solid>{wallShell};
            for(int i=0;i<solids.Count;i++){var shell=BeamManualSupportGeometryV1.Boolean(solids[i],interiors[i],BooleanOperationsType.Difference);
                if(shell.Volume>1e-12)shell=BeamManualSupportGeometryV1.Boolean(shell,frame.Concrete,BooleanOperationsType.Difference);
                for(int j=0;j<solids.Count&&shell.Volume>1e-12;j++)if(i!=j)shell=BeamManualSupportGeometryV1.Boolean(shell,solids[j],BooleanOperationsType.Difference);
                shells.Add(shell);}
            return new BeamManualSupportGeometryV1(union,shells);
        }
        private WallNativeFrameV1 Validate(Wall wall,WallManualPlanV1 plan)
        {
            var errors=WallManualPlanValidatorV1.Validate(plan);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            if(wall.Document!=_doc || plan.DocumentKey!=DocumentKey || plan.HostUniqueId!=wall.UniqueId)throw new InvalidOperationException("Exact Wall host/document identity mismatch.");
            var frame=WallNativeFrameV1.Read(wall);if(frame.Signature!=plan.FrameSignature)throw new InvalidOperationException("Wall geometry/orientation changed; reopen and explicitly review coordinates.");
            foreach(var bar in plan.Bars)foreach(var point in new[]{bar.Start,bar.Finish}) {
                if(point.XMm<0 && plan.LeftColumn==null || point.XMm>WallNativeFrameV1.Mm(frame.Length) && plan.RightColumn==null)
                    throw new InvalidOperationException("Outside-Wall station requires the correct explicitly bound boundary Column."); }
            return frame;
        }
        private RebarBarType Type(BeamManualElementReferenceV1 reference)
        {
            var type=_doc.GetElement(reference.UniqueId) as RebarBarType;
            if(type==null || !type.IsValidObject || reference.DocumentKey!=DocumentKey || type.Name!=reference.Name ||
                Math.Abs(WallNativeFrameV1.Mm(type.BarModelDiameter)-reference.DiameterMm)>0.01)throw new InvalidOperationException("Exact bar type changed/missing; no substitution.");
            return type;
        }
        private Rebar[] Neighbors(Wall wall,FamilyInstance[] columns)
        {
            var ids=new HashSet<ElementId>(columns.Select(c=>c.Id));var filters=columns.Select(c=>{var b=c.get_BoundingBox(null) ?? throw new InvalidOperationException("Column bounds unavailable.");
                var points=(from x in new[]{b.Min.X,b.Max.X} from y in new[]{b.Min.Y,b.Max.Y} from z in new[]{b.Min.Z,b.Max.Z} select b.Transform.OfPoint(new XYZ(x,y,z))).ToArray();
                return new BoundingBoxIntersectsFilter(new Outline(new XYZ(points.Min(p=>p.X),points.Min(p=>p.Y),points.Min(p=>p.Z)),new XYZ(points.Max(p=>p.X),points.Max(p=>p.Y),points.Max(p=>p.Z))));}).ToArray();
            return new FilteredElementCollector(_doc).OfClass(typeof(Rebar)).Cast<Rebar>().Where(bar=>bar.IsValidObject &&
                (bar.GetHostId()==wall.Id || ids.Contains(bar.GetHostId()) || filters.Any(f=>f.PassesFilter(_doc,bar.Id)))).ToArray();
        }
        public string Fingerprint(Wall wall,WallManualPlanV1 plan)
        {
            var frame=Validate(wall,plan);Envelope(wall,plan,frame,out FamilyInstance[] columns);
            return WorkflowFingerprint.Compute(new[]{JsonConvert.SerializeObject(plan),frame.Signature}.Concat(plan.Bars.Select(bar=>Type(bar.BarType).VersionGuid.ToString("D")))
                .Concat(Neighbors(wall,columns).Select(RebarPreviewService.FingerprintBar).OrderBy(value=>value,StringComparer.Ordinal)));
        }
        public IList<Rebar> Generate(Wall wall,WallManualPlanV1 plan,IDictionary<string,string> roles=null)
        {
            if(!_doc.IsModifiable)throw new InvalidOperationException("Caller-owned active transaction required.");
            var frame=Validate(wall,plan);var envelope=Envelope(wall,plan,frame,out FamilyInstance[] columns);
            var neighbors=Neighbors(wall,columns).ToList();var created=new List<Rebar>();
            foreach(var input in plan.Bars)
            {
                var type=Type(input.BarType);XYZ a=frame.World(input.Start),b=frame.World(input.Finish);var line=Line.CreateBound(a,b);
                using(var intersection=frame.Concrete.IntersectWithCurve(line,new SolidCurveIntersectionOptions()))
                    if(intersection.SegmentCount==0 || Enumerable.Range(0,intersection.SegmentCount).Sum(i=>intersection.GetCurveSegment(i).Length)<=WallNativeFrameV1.Feet(0.01))
                        throw new InvalidOperationException("Wall-owned reinforcement must pass through the actual Wall concrete; Column-only bars are not a Wall extension.");
                envelope.ValidateCurves(new[]{line},type.BarModelDiameter/2);
                XYZ direction=(b-a).Normalize(),normal=frame.Depth-direction*frame.Depth.DotProduct(direction);
                if(normal.GetLength()<1e-6)normal=frame.X;
                Rebar bar=RebarShapeCreationHelper.CreateFromCurvesSafe(_doc,RebarStyle.Standard,type,null,null,wall,normal.Normalize(),new List<Curve>{line},RebarHookOrientation.Right,RebarHookOrientation.Right,allowLegacyFallback:false);
                if(bar==null || !bar.IsValidObject || bar.GetHostId()!=wall.Id || bar.GetTypeId()!=type.Id || bar.Quantity!=1)
                    throw new InvalidOperationException("Wall creation substituted/failed host, type or quantity.");
                var curves=bar.GetCenterlineCurves(false,false,false,MultiplanarOption.IncludeAllMultiplanarCurves,0);
                if(curves.Count!=1 || !(curves[0] is Line actual) || !((actual.GetEndPoint(0).DistanceTo(a)<WallNativeFrameV1.Feet(0.01) && actual.GetEndPoint(1).DistanceTo(b)<WallNativeFrameV1.Feet(0.01)) ||
                    (actual.GetEndPoint(0).DistanceTo(b)<WallNativeFrameV1.Feet(0.01) && actual.GetEndPoint(1).DistanceTo(a)<WallNativeFrameV1.Feet(0.01))))throw new InvalidOperationException("Solver altered the explicit Wall straight path.");
                envelope.ValidateCurves(curves,type.BarModelDiameter/2);
                RebarSolvedEnvelopeCollisionV1.Verify(bar,neighbors);
                foreach(var old in neighbors){var oldType=_doc.GetElement(old.GetTypeId()) as RebarBarType ?? throw new InvalidOperationException("Neighbor exact type unavailable.");
                    double radius=(type.BarModelDiameter+oldType.BarModelDiameter)/2-WallNativeFrameV1.Feet(0.01);
                    foreach(var path in RebarPreviewService.ReadPaths(new List<Rebar>{old}))for(int i=0;i+1<path.Points.Count;i++) {
                        var p=path.Points[i];var q=path.Points[i+1];
                        if(radius>0 && BeamManualSegmentDistanceV1.SquaredDistance(new BeamManualPoint3V1(a.X,a.Y,a.Z),new BeamManualPoint3V1(b.X,b.Y,b.Z),new BeamManualPoint3V1(p.X,p.Y,p.Z),new BeamManualPoint3V1(q.X,q.Y,q.Z))<radius*radius)
                            throw new InvalidOperationException("Wall path interferes with existing/earlier reinforcement; whole creation must roll back."); } }
                neighbors.Add(bar);created.Add(bar);if(roles!=null)roles[bar.UniqueId]=input.Role;
            }
            return created;
        }
    }
}
