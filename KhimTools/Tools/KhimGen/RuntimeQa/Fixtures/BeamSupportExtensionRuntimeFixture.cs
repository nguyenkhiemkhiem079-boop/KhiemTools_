using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;
using KhimTools.Core.Revit;
using KhimTools.RebarTool.Core;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Fixtures
{
    public sealed class BeamSupportExtensionRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id => "BR-MANUAL-SUPPORT-ROLLBACK";
        public override string Name => "Manual support extension / solid envelope / atomic rollback";
        public override string Suite => "REBAR";
        public override string Description => "QA_ONLY synthetic negative station into exact verified support; real generator preview/Create parity and controlled outer rollback. Disposable model required; missing resources BLOCKED.";
        public override bool IsCritical => true;
        private sealed class QaInjectedFailure : Exception { }
        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc=context.Document;
            var generator=new BeamManualRebarGeneratorV1(doc);
            var type=new FilteredElementCollector(doc).OfClass(typeof(RebarBarType)).Cast<RebarBarType>()
                .Where(item=>item.IsValidObject && item.BarModelDiameter>0).OrderBy(item=>item.BarModelDiameter).FirstOrDefault();
            if(type==null) { Block(result,Id+"_TYPE","Exact type","Loaded usable RebarBarType","BLOCKED: no usable exact type.",QaSeverity.CRITICAL); return; }
            var instances=new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().ToArray();
            FamilyInstance selected=null; BeamManualDetailingPlanV1 plan=null;
            string reason="No supported rectangular Beam with a contacting rectangular Column/Beam at START.";
            foreach(var beam in instances.Where(item=>item.Category.IsCategory(BuiltInCategory.OST_StructuralFraming)).Take(100))
            {
                foreach(var support in instances.Where(item=>item.Id!=beam.Id &&
                    (item.Category.IsCategory(BuiltInCategory.OST_StructuralColumns) || item.Category.IsCategory(BuiltInCategory.OST_StructuralFraming))).Take(100))
                {
                    try
                    {
                        var candidate=generator.CreateBlankPlan(beam);
                        var profile=BeamGeometryHelper.GetBeamProfile(beam);
                        double Mm(double value)=>UnitUtils.ConvertFromInternalUnits(value,UnitTypeId.Millimeters);
                        var bound=generator.BindSupport(beam,BeamManualEnd.Start,support,0,"QA_ONLY synthetic extension; zero explicit QA cover, no structural approval.");
                        var box=support.get_BoundingBox(null) ?? throw new InvalidOperationException("Support bounds unavailable.");
                        var points=from x in new[]{box.Min.X,box.Max.X} from y in new[]{box.Min.Y,box.Max.Y} from z in new[]{box.Min.Z,box.Max.Z}
                            select box.Transform.OfPoint(new XYZ(x,y,z));
                        double start=Mm(points.Min(point=>(point-profile.StartPoint).DotProduct(profile.Direction)))/2;
                        if(start>=-Mm(type.BarModelDiameter))throw new InvalidOperationException("Support has insufficient negative-station extent for synthetic fixture.");
                        candidate.SupportEnds=new List<BeamManualSupportEndV1>{bound,new BeamManualSupportEndV1{End=BeamManualEnd.Finish,
                            Mode=BeamManualSupportMode.UserDefinedFreeEndWithinHost,IntendedContinuation="QA_ONLY inside Beam"}};
                        candidate.Covers=Enum.GetValues(typeof(BeamManualCoverFace)).Cast<BeamManualCoverFace>().Where(face=>face!=BeamManualCoverFace.Unspecified)
                            .Select(face=>new BeamManualCoverV1{Face=face,Source=BeamManualCoverSource.ExplicitUserInput,ClearCoverMm=0}).ToList();
                        candidate.BarGroups=new List<BeamManualBarGroupV1>{new BeamManualBarGroupV1{GroupId="QA_ONLY_SUPPORT",Role=BeamManualBarRole.UserDefined,
                            UserLabel="QA ONLY",Enabled=true,BarType=new BeamManualElementReferenceV1{DocumentKey=candidate.DocumentKey,UniqueId=type.UniqueId,
                            Name=type.Name,DiameterMm=Mm(type.BarModelDiameter),State=BeamManualReferenceState.Resolved},
                            Bars=new List<BeamManualBarV1>{new BeamManualBarV1{XMm=0,YMm=0,StartStationMm=start,EndStationMm=Mm(profile.Length-type.BarModelDiameter),
                            LayerId="QA_ONLY",StartDetail=new BeamManualEndDetailV1{Kind=BeamManualEndDetailKind.StraightAtStation},FinishDetail=new BeamManualEndDetailV1{Kind=BeamManualEndDetailKind.StraightAtStation}}}}};
                        generator.Fingerprint(candidate,beam); selected=beam; plan=candidate; break;
                    }
                    catch(Exception ex) { reason=ex.Message; } // Resource qualification only; never a PASS or fallback geometry.
                }
                if(selected!=null)break;
            }
            if(selected==null) { Block(result,Id+"_RESOURCE","Contacting support resources","Supported exact Beam/support pair",
                "BLOCKED: "+reason+" (fixture search capped at100 hosts as software resource protection).",QaSeverity.CRITICAL);return; }
            var before=RuntimeQaSafetyGuard.CaptureFingerprint(doc);
            string fingerprint=generator.Fingerprint(plan,selected);
            RebarPreviewSnapshot preview;
            try {
                preview=RebarPreviewService.Capture(doc,new[]{new RebarPreviewRequest(fingerprint,()=>generator.Generate(plan,selected),
                    ()=>generator.Fingerprint(plan,selected),new Dictionary<string,string>{{"HostId",selected.UniqueId},{"HostGeometry",plan.Frame.HostGeometrySignature}})});
            } catch(Exception ex) { Block(result,Id+"_SOLVE","Supported synthetic solver resource","Contained exact bar solves",
                "BLOCKED: synthetic selected pair cannot solve: "+ex.Message,QaSeverity.CRITICAL); return; }
            bool previewClean=RuntimeQaSafetyGuard.VerifyRollback(doc,before,Array.Empty<ElementId>(),out string previewMessage);
            Check(result,Id+"_PREVIEW","Preview rollback and actual extension",previewClean && preview.Components.Count==1 && preview.Components[0].BarCount==1,
                "One physically solved extension and unchanged model",previewMessage,"Real support CSG gate ran inside rollback-only solver.",QaSeverity.CRITICAL);
            if(!previewClean)return;
            var ids=new List<ElementId>(); bool parity=false, injected=false;
            try {
                TransactionBoundary.ExecuteGroup<bool>(doc,"QA_ONLY support outer rollback",()=>{
                    IList<Rebar> actual=BeamManualBatchV1.Create(doc,new[]{new BeamManualBatchAssignmentV1(plan,preview)});
                    foreach(var bar in actual){ids.Add(bar.Id);context.TrackCreated(bar.Id);}
                    parity=RebarPreviewService.Matches(preview,fingerprint,actual) && actual.All(bar=>bar.GetHostId()==selected.Id && bar.GetTypeId()==type.Id && bar.Quantity==1);
                    throw new QaInjectedFailure(); // QA-only, after successful production atomic Create.
                });
            } catch(QaInjectedFailure) { injected=true; }
            bool restored=RuntimeQaSafetyGuard.VerifyRollback(doc,before,ids,out string message);
            Check(result,Id+"_PARITY","Atomic production support Create parity",parity,"Exact accepted centerlines/type/quantity/host",parity.ToString(),"No inferred design parameters.",QaSeverity.CRITICAL);
            Check(result,Id+"_ROLLBACK","Injected whole-group rollback preserves identities/state",injected && restored,"Whole model fingerprint restored",message,
                "Intentional failure is isolated to fixture code; production generator has no failure injection.",QaSeverity.CRITICAL);
        }
    }
}
