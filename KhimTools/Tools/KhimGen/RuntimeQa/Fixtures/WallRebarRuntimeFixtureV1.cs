using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core.Revit;
using KhimTools.RebarTool.Core;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Fixtures
{
    public sealed class WallRebarRuntimeFixtureV1 : RuntimeQaFixtureBase
    {
        public override string Id=>"WALL-MANUAL-OWNERSHIP-ROLLBACK";
        public override string Name=>"Wall-owned bars / boundary Column extension / parity / rollback";
        public override string Suite=>"REBAR";
        public override string Description=>"QA_ONLY explicit synthetic paths. Confirms Wall remains host including extension into a verified boundary Column. Missing Wall/type/Column resources BLOCKED, never live PASS from registration.";
        public override bool IsCritical=>true;
        protected override void ExecuteFixture(RuntimeQaContext context,QaFixtureResult result)
        {
            var doc=context.Document;var generator=new WallRebarGeneratorV1(doc);
            var type=new FilteredElementCollector(doc).OfClass(typeof(RebarBarType)).Cast<RebarBarType>()
                .Where(t=>t.IsValidObject&&t.BarModelDiameter>0).OrderBy(t=>t.BarModelDiameter).FirstOrDefault();
            Wall wall=null;WallNativeFrameV1 frame=null;
            foreach(var candidate in new FilteredElementCollector(doc).OfClass(typeof(Wall)).Cast<Wall>())
                try {var p=WallNativeFrameV1.Read(candidate);if(type!=null&&p.Height>4*type.BarModelDiameter&&p.Thickness>type.BarModelDiameter&&p.Length>type.BarModelDiameter){wall=candidate;frame=p;break;}} catch(InvalidOperationException){ }
            if(wall==null||type==null){Block(result,Id+"_RESOURCES","Usable exact Wall/type","Verified rectangular structural Wall and loaded type","BLOCKED: compatible disposable Wall/type resources unavailable.",QaSeverity.CRITICAL);return;}
            var plan=generator.Blank(wall);double diameter=WallNativeFrameV1.Mm(type.BarModelDiameter),length=WallNativeFrameV1.Mm(frame.Length),height=WallNativeFrameV1.Mm(frame.Height),depth=WallNativeFrameV1.Mm(frame.Thickness)/2;
            plan.Covers=new WallManualCoversV1{ExteriorMm=0,InteriorMm=0,LeftMm=0,RightMm=0,TopMm=0,BottomMm=0}; // QA_ONLY, never a production default.
            var reference=new BeamManualElementReferenceV1{DocumentKey=plan.DocumentKey,UniqueId=type.UniqueId,Name=type.Name,DiameterMm=diameter,State=BeamManualReferenceState.Resolved};
            plan.Bars.Add(new WallManualBarV1{GroupId="QA_ONLY_VERTICAL",Role="VERTICAL",BarType=reference,
                Start=new WallManualPointV1{XMm=length/2,YMm=diameter,DepthMm=depth},Finish=new WallManualPointV1{XMm=length/2,YMm=height-diameter,DepthMm=depth}});
            Exercise(context,result,generator,wall,plan,"BASELINE");
            WallBoundaryColumnV1 bound=null;double start=0;
            foreach(var column in new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())
                try {
                    var item=generator.BindColumn(wall,column,true,0,"QA_ONLY synthetic boundary extension; no development/design approval.");
                    var box=column.get_BoundingBox(null);if(box==null)continue;
                    var points=from x in new[]{box.Min.X,box.Max.X} from y in new[]{box.Min.Y,box.Max.Y} from z in new[]{box.Min.Z,box.Max.Z} select box.Transform.OfPoint(new XYZ(x,y,z));
                    double minimum=WallNativeFrameV1.Mm(points.Min(point=>(point-frame.Origin).DotProduct(frame.X)));
                    if(minimum>=-diameter)continue;bound=item;start=minimum/2;break;
                }catch(InvalidOperationException){ }
            if(bound==null){Block(result,Id+"_COLUMN","Boundary extension resources","Exact contacting structural Column","BLOCKED: no supported left boundary Column; baseline does not certify extension.",QaSeverity.CRITICAL);return;}
            plan.LeftColumn=bound;plan.Bars=new List<WallManualBarV1>{new WallManualBarV1{GroupId="QA_ONLY_EXTENSION",Role="HORIZONTAL",BarType=reference,
                Start=new WallManualPointV1{XMm=start,YMm=height/2,DepthMm=depth},Finish=new WallManualPointV1{XMm=length/2,YMm=height/2,DepthMm=depth}}};
            Exercise(context,result,generator,wall,plan,"COLUMN_EXTENSION");
        }
        private void Exercise(RuntimeQaContext context,QaFixtureResult result,WallRebarGeneratorV1 generator,Wall wall,WallManualPlanV1 plan,string suffix)
        {
            var doc=context.Document;var before=RuntimeQaSafetyGuard.CaptureFingerprint(doc);string fingerprint=generator.Fingerprint(wall,plan);
            RebarPreviewSnapshot preview;
            try {preview=RebarPreviewService.Capture(doc,new[]{new RebarPreviewRequest(fingerprint,()=>generator.Generate(wall,plan),()=>generator.Fingerprint(wall,plan),new Dictionary<string,string>{{"HostId",wall.UniqueId},{"HostGeometry",plan.FrameSignature}})});}
            catch(Exception ex){Block(result,Id+suffix+"_SOLVE","Synthetic production solver resource","Exact contained path solves","BLOCKED: "+ex.Message,QaSeverity.CRITICAL);return;}
            bool clean=RuntimeQaSafetyGuard.VerifyRollback(doc,before,Array.Empty<ElementId>(),out string message);
            Check(result,Id+suffix+"_PREVIEW","Rollback-only real preview",clean&&preview.Components.Count==1&&preview.Components[0].BarCount==1,"One exact bar and unchanged model",message,"Registration is not execution.",QaSeverity.CRITICAL);
            if(!clean)return;
            var ids=new List<ElementId>();bool parity=false;TransactionStatus status;
            using(var transaction=new Transaction(doc,"QA_ONLY Wall generation rollback"))
            {
                TransactionBoundary.Start(transaction,"QA Wall");
                try {var actual=generator.Generate(wall,plan);doc.Regenerate();foreach(var bar in actual){ids.Add(bar.Id);context.TrackCreated(bar.Id);}
                    ElementId expectedType=doc.GetElement(plan.Bars[0].BarType.UniqueId).Id;
                    parity=actual.Count==plan.Bars.Count&&RebarPreviewService.Matches(preview,fingerprint,actual)&&actual.All(bar=>bar.GetHostId()==wall.Id&&bar.GetTypeId()==expectedType&&bar.Quantity==1);}
                finally {TransactionBoundary.RollBack(transaction,"QA Wall");}status=transaction.GetStatus();
            }
            bool restored=RuntimeQaSafetyGuard.VerifyRollback(doc,before,ids,out message);
            Check(result,Id+suffix+"_PARITY","Exact output parity / Wall ownership",parity,"Accepted physical path/type/quantity and Wall host",parity.ToString(),"Boundary Column NEVER becomes host.",QaSeverity.CRITICAL);
            Check(result,Id+suffix+"_ROLLBACK","Model identities/state restored",status==TransactionStatus.RolledBack&&restored,"Whole rollback and unrelated reinforcement preservation",message,"Real safety framework fingerprints.",QaSeverity.CRITICAL);
        }
    }
}
