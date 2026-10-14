using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.RebarTool.Core;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Fixtures
{
    public sealed class DualWallHostRuntimeFixtureV1 : RuntimeQaFixtureBase
    {
        public override string Id=>"DUAL-WALL-COLUMN-OWNERSHIP-ATOMICITY";
        public override string Name=>"Explicit ContractA Wall and ContractB Column ownership / multi-host atomic rollback";
        public override string Suite=>"REBAR";
        public override bool IsCritical=>true;
        public override string Description=>"QA_ONLY explicit synthetic zero-cover/terminal-detail values. Production A/B solve and batch; test-only failure after Column generation, whole model identity/state restoration. Missing resources BLOCKED; registration is not live execution.";
        protected override void ExecuteFixture(RuntimeQaContext context,QaFixtureResult result)
        {
            var doc=context.Document;var type=new FilteredElementCollector(doc).OfClass(typeof(RebarBarType)).Cast<RebarBarType>().Where(t=>t.BarModelDiameter>0).OrderBy(t=>t.BarModelDiameter).FirstOrDefault();
            Wall wall=null;FamilyInstance column=null;WallNativeFrameV1 wf=null;ColumnWallRelatedFrameV1 cf=null;
            foreach(var w in new FilteredElementCollector(doc).OfClass(typeof(Wall)).Cast<Wall>())try{wf=WallNativeFrameV1.Read(w);wall=w;break;}catch(InvalidOperationException){}
            foreach(var c in new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())try{cf=ColumnWallRelatedFrameV1.Read(c);column=c;break;}catch(InvalidOperationException){}
            if(type==null||wall==null||column==null||wf.Height<4*type.BarModelDiameter||wf.Thickness<2*type.BarModelDiameter||cf.Height<4*type.BarModelDiameter||cf.Width<2*type.BarModelDiameter||cf.Thickness<2*type.BarModelDiameter){Block(result,Id+"_RESOURCE","Both actual host contracts","Compatible Wall/Column/type in disposable model","BLOCKED: dual-host resources unsuitable.",QaSeverity.CRITICAL);return;}
            var ag=new WallRebarGeneratorV1(doc);var bg=new ColumnWallRelatedGeneratorV1(doc);var a=ag.Blank(wall);var b=bg.Blank(column);
            a.Covers=new WallManualCoversV1{ExteriorMm=0,InteriorMm=0,LeftMm=0,RightMm=0,BottomMm=0,TopMm=0};
            b.Covers=new ColumnWallRelatedCoversV1{FrontMm=0,BackMm=0,LeftMm=0,RightMm=0,BottomMm=0,TopMm=0};b.UserConfirmedActualColumnHost=true;b.ScenarioIntent="QA_ONLY explicit Column-host wall-related ownership test, not a Wall proxy";
            WallManualBarV1 Bar(string key,double x,double depth,double height)=>new WallManualBarV1{GroupId=key,Role="VERTICAL",Start=new WallManualPointV1{XMm=WallNativeFrameV1.Mm(x),YMm=WallNativeFrameV1.Mm(type.BarModelDiameter),DepthMm=WallNativeFrameV1.Mm(depth)},Finish=new WallManualPointV1{XMm=WallNativeFrameV1.Mm(x),YMm=WallNativeFrameV1.Mm(height-type.BarModelDiameter),DepthMm=WallNativeFrameV1.Mm(depth)},StartAnchorageMm=0,FinishAnchorageMm=0,StartExtensionMm=0,FinishExtensionMm=0,BarType=new BeamManualElementReferenceV1{DocumentKey=ag.DocumentKey,UniqueId=type.UniqueId,Name=type.Name,DiameterMm=WallNativeFrameV1.Mm(type.BarModelDiameter),State=BeamManualReferenceState.Resolved}};
            a.Bars.Add(Bar("QA_A",wf.Length/2,wf.Thickness/2,wf.Height));b.Bars.Add(Bar("QA_B",cf.Width/2,cf.Thickness/2,cf.Height));
            string af=ag.Fingerprint(wall,a),bf=bg.Fingerprint(column,b);RebarPreviewSnapshot ap,bp;
            try{
                ap=RebarPreviewService.Capture(doc,new[]{new RebarPreviewRequest(af,()=>ag.Generate(wall,a),()=>ag.Fingerprint(wall,a),new Dictionary<string,string>{{"HostId",wall.UniqueId}})});
                bp=RebarPreviewService.Capture(doc,new[]{new RebarPreviewRequest(bf,()=>bg.Generate(column,b),()=>bg.Fingerprint(column,b),new Dictionary<string,string>{{"HostId",column.UniqueId}})});
            }catch(Exception ex){Block(result,Id+"_SOLVE","Both real production previews","Exact synthetic contained paths solve","BLOCKED: "+ex.Message,QaSeverity.CRITICAL);return;}
            var aa=DualWallHostAssignmentV1.Wall(doc,wall,a,ap);var ba=DualWallHostAssignmentV1.Column(doc,column,b,bp);var original=ba.Generate;
            var before=RuntimeQaSafetyGuard.CaptureFingerprint(doc);var created=new List<ElementId>();bool first=false,second=false,injected=false;
            var firstOriginal=aa.Generate;aa.Generate=()=>{var bars=firstOriginal();first=bars.Count==1&&bars.All(r=>r.GetHostId()==wall.Id);foreach(var bar in bars){created.Add(bar.Id);context.TrackCreated(bar.Id);}return bars;};
            ba.Generate=()=>{var bars=original();second=bars.Count==1&&bars.All(r=>r.GetHostId()==column.Id);foreach(var bar in bars){created.Add(bar.Id);context.TrackCreated(bar.Id);}throw new InvalidOperationException("QA_ONLY_DUAL_HOST_FAILURE_AFTER_COLUMN");};
            try{DualWallHostBatchV1.Create(doc,new[]{aa,ba});}
            catch(InvalidOperationException ex){if(ex.Message!="QA_ONLY_DUAL_HOST_FAILURE_AFTER_COLUMN")throw;injected=true;}
            bool restored=RuntimeQaSafetyGuard.VerifyRollback(doc,before,created,out string message);
            Check(result,Id+"_OWNERS","Actual A Wall / B Column ownership",first&&second,"Each generated result belongs to its explicit primary host",first+" / "+second,"No implicit host switch.",QaSeverity.CRITICAL);
            Check(result,Id+"_ATOMIC","Later-host failure rolls back ALL hosts",injected&&restored,"Wall and Column outputs absent; unrelated identities/state unchanged",message,"Test-only delegate injection; production has no failure switch.",QaSeverity.CRITICAL);
        }
    }
}
