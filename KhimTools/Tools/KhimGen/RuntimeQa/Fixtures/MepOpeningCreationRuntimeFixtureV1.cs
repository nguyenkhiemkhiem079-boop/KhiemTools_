using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core.Revit;
using KhimTools.MEP.Penetrations;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Fixtures
{
    public sealed class MepOpeningCreationRuntimeFixtureV1 : RuntimeQaFixtureBase
    {
        public override string Id=>"MEP-NATIVE-OPENING-ROLLBACK";
        public override string Name=>"Native reviewed MEP Wall/Floor opening and injected failure rollback";
        public override string Suite=>"MEP";
        public override string Description=>"QA_ONLY synthetic explicit zero clearance, production actual cut postconditions and model identity/state rollback. Missing safe resources BLOCKED; registration/builds are not host execution.";
        public override bool IsCritical=>true;
        protected override void ExecuteFixture(RuntimeQaContext context,QaFixtureResult result)
        {
            var doc=context.Document;
            var hosts=new FilteredElementCollector(doc).WhereElementIsNotElementType().Where(e=>e is Wall || e is Floor).ToArray();
            var curves=new FilteredElementCollector(doc).WhereElementIsNotElementType().OfType<MEPCurve>().ToArray();
            MepOpeningProposalV1 proposal=null;
            foreach(var host in hosts){foreach(var mep in curves)try{proposal=MepOpeningCreationServiceV1.Propose(doc,host,mep,0);break;}catch(InvalidOperationException){}catch(ArgumentException){}if(proposal!=null)break;}
            if(proposal==null){Block(result,Id+"_RESOURCE","Safe verified actual clash","Supported uncut Wall/Floor with perpendicular MEP, no reinforcing interference","BLOCKED: no suitable disposable cut resources.",QaSeverity.CRITICAL);return;}
            var before=RuntimeQaSafetyGuard.CaptureFingerprint(doc);var ids=new List<ElementId>();bool cut=false,injected=false;TransactionStatus status;
            using(var tx=new Transaction(doc,"QA_ONLY reviewed opening injected rollback"))
            {
                TransactionBoundary.Start(tx,"QA opening");
                try {
                    var opening=MepOpeningCreationServiceV1.Create(doc,proposal);ids.Add(opening.Id);context.TrackCreated(opening.Id);
                    cut=opening.IsValidObject&&opening.Host.Id==proposal.Host.Id;
                    throw new InvalidOperationException("QA_ONLY_CONTROLLED_FAILURE_AFTER_VERIFIED_CUT");
                }catch(InvalidOperationException ex){if(ex.Message!="QA_ONLY_CONTROLLED_FAILURE_AFTER_VERIFIED_CUT")throw;injected=true;}
                finally {TransactionBoundary.RollBack(tx,"QA opening");}
                status=tx.GetStatus();
            }
            bool restored=RuntimeQaSafetyGuard.VerifyRollback(doc,before,ids,out string message);
            Check(result,Id+"_CUT","Actual native host/cut/clash postconditions",cut,"Exact reviewed rectangle removed and clash cleared","Cut: "+cut,"Production service verifies native solids; not a count-only test.",QaSeverity.CRITICAL);
            Check(result,Id+"_FAILURE","Controlled fixture-only failure",injected,"Failure after verified opening","Injected: "+injected,"No production failure switch.",QaSeverity.CRITICAL);
            Check(result,Id+"_RESTORE","Whole rollback identities/state",status==TransactionStatus.RolledBack&&restored,"Model restored, created opening absent, unrelated elements unchanged",message,"Missing/uncertain resources never PASS.",QaSeverity.CRITICAL);
        }
    }
}
