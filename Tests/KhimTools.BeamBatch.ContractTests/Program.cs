using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.RebarTool.Core;

// Clearly synthetic API/lifecycle doubles: production batch orchestration and
// TransactionBoundary are linked unchanged. No Revit geometry or host is exercised.
internal static class Program
{
    static int checks;
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
    static bool Rejected(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
    static BeamManualBatchAssignmentV1 Assignment(string host) => new BeamManualBatchAssignmentV1(
        new BeamManualDetailingPlanV1 { DocumentKey="QA_ONLY", HostUniqueId=host, Frame=new BeamManualFrameV1 { HostGeometrySignature=host } },
        new RebarPreviewSnapshot { Fingerprint=host });
    static Document Model() { var d=new Document(); d.Created.Add(new Rebar { Host="UNRELATED" }); return d; }
    static bool Preserved(Document d) => d.Created.Count==1 && d.Created[0].Host=="UNRELATED";
    static void Main()
    {
        var d=Model(); var a=Assignment("A"); var b=Assignment("B");
        Check(BeamManualBatchV1.Create(d,new[]{a,b}).Count==2 && d.Created.Count==3 && d.Assimilated, "All hosts commit together");
        d=Model(); b.Preview.Fingerprint="STALE";
        Check(Rejected(()=>BeamManualBatchV1.Create(d,new[]{a,b})) && Preserved(d) && d.GenerationCalls==0, "All-host stale preflight before mutation");
        b=Assignment("B"); d=Model(); d.DuplicateHost="B";
        Check(Rejected(()=>BeamManualBatchV1.Create(d,new[]{a,b})) && Preserved(d) && d.GenerationCalls==0, "Duplicate later host blocks earlier generation");
        d=Model(); d.FailureHost="B";
        Check(Rejected(()=>BeamManualBatchV1.Create(d,new[]{a,b})) && Preserved(d) && d.GroupRolledBack && d.GenerationCalls==2, "Second generation failure restores first host and unrelated state");
        d=Model(); d.ParityFailureHost="B";
        Check(Rejected(()=>BeamManualBatchV1.Create(d,new[]{a,b})) && Preserved(d) && d.GroupRolledBack, "Second parity failure restores entire batch");
        d=Model(); d.EmptyHost="B";
        Check(Rejected(()=>BeamManualBatchV1.Create(d,new[]{a,b})) && Preserved(d), "Empty output cannot assimilate");
        d=Model();
        Check(Rejected(()=>BeamManualBatchV1.Create(d,new[]{a,a})) && d.GenerationCalls==0, "Duplicate host identity is rejected");
        d=Model(); b=Assignment("B"); b.Plan.DocumentKey="FOREIGN_QA";
        Check(Rejected(()=>BeamManualBatchV1.Create(d,new[]{a,b})) && d.GenerationCalls==0, "Foreign document is rejected");
        d=Model(); b=Assignment("B"); d.ChangeEarlierOutputOnB=true;
        Check(Rejected(()=>BeamManualBatchV1.Create(d,new[]{a,b})) && Preserved(d) && d.GroupRolledBack, "Late commit alteration of earlier output is rejected");
        d=Model(); d.ChangeEarlierGeometryOnB=true;
        Check(Rejected(()=>BeamManualBatchV1.Create(d,new[]{a,b})) && Preserved(d) && d.GroupRolledBack, "Late commit alteration of earlier host frame is rejected");
        d=Model(); d.GroupStartFails=true;
        Check(Rejected(()=>BeamManualBatchV1.Create(d,new[]{a,b})) && d.GenerationCalls==0 && Preserved(d), "Failed group start cannot mutate");
        Console.WriteLine("BEAM_BATCH_CONTRACT=PASS ("+checks+" assertions); synthetic transaction doubles, LIVE_REVIT=NOT_EXECUTED.");
    }
}

namespace Autodesk.Revit.DB.Structure
{
    public class Rebar { public string Host; public bool Altered; }
}
namespace Autodesk.Revit.DB
{
    public enum TransactionStatus { Uninitialized, Started, Committed, RolledBack, Error }
    public class FamilyInstance { public string UniqueId; }
    public class Document
    {
        public List<Rebar> Created=new List<Rebar>();
        public string FailureHost, ParityFailureHost, EmptyHost, DuplicateHost;
        public bool Assimilated, GroupRolledBack, ChangeEarlierOutputOnB, ChangeEarlierGeometryOnB, EarlierGeometryChanged, GroupStartFails;
        public int GenerationCalls;
        public object GetElement(string id) => new FamilyInstance { UniqueId=id };
        public void Regenerate() { }
    }
    public class Transaction : IDisposable
    {
        protected readonly Document d; protected TransactionStatus status; protected List<Rebar> before;
        public Transaction(Document document,string name) { d=document; }
        public TransactionStatus Start() { before=new List<Rebar>(d.Created); return status=TransactionStatus.Started; }
        public TransactionStatus GetStatus()=>status;
        public TransactionStatus Commit()
        {
            if (d.Created.Any(x=>x.Host=="B")) {
                if (d.ChangeEarlierOutputOnB) d.Created.First(x=>x.Host=="A").Altered=true;
                if (d.ChangeEarlierGeometryOnB) d.EarlierGeometryChanged=true;
            }
            return status=TransactionStatus.Committed;
        }
        public TransactionStatus RollBack() { d.Created=new List<Rebar>(before); return status=TransactionStatus.RolledBack; }
        public void Dispose() { }
    }
    public class SubTransaction : Transaction { public SubTransaction(Document d):base(d, "QA") { } }
    public class TransactionGroup : IDisposable
    {
        readonly Document d; TransactionStatus status; List<Rebar> before;
        public TransactionGroup(Document document,string name) { d=document; }
        public TransactionStatus Start() { before=new List<Rebar>(d.Created); return status=d.GroupStartFails?TransactionStatus.Error:TransactionStatus.Started; }
        public TransactionStatus GetStatus()=>status;
        public TransactionStatus Assimilate() { d.Assimilated=true; return status=TransactionStatus.Committed; }
        public TransactionStatus RollBack() { d.Created=new List<Rebar>(before); d.GroupRolledBack=true; return status=TransactionStatus.RolledBack; }
        public void Dispose() { }
    }
}
namespace KhimTools.RebarTool.Core
{
    public class BeamManualFrameV1 { public string HostGeometrySignature; }
    public class BeamManualDetailingPlanV1 { public string HostUniqueId, DocumentKey; public BeamManualFrameV1 Frame; }
    public class RebarPreviewSnapshot { public string Fingerprint; public List<int> Components=new List<int>{1}; }
    public class BeamManualRebarGeneratorV1
    {
        readonly Document d; public BeamManualRebarGeneratorV1(Document document) { d=document; }
        public string Fingerprint(BeamManualDetailingPlanV1 plan,FamilyInstance host,IEnumerable<Rebar> ownedBatchBars=null)=>host.UniqueId;
        public BeamManualFrameV1 GetCurrentFrame(FamilyInstance host)=>new BeamManualFrameV1 { HostGeometrySignature=host.UniqueId=="A" && d.EarlierGeometryChanged?"CHANGED_QA":host.UniqueId };
        public IList<Rebar> Generate(BeamManualDetailingPlanV1 plan,FamilyInstance host)
        {
            d.GenerationCalls++; if (d.FailureHost==host.UniqueId) throw new InvalidOperationException("QA ONLY failure");
            if (d.EmptyHost==host.UniqueId) return new List<Rebar>();
            var bar=new Rebar { Host=host.UniqueId, Altered=d.ParityFailureHost==host.UniqueId }; d.Created.Add(bar); return new List<Rebar>{bar};
        }
    }
    public static class RebarPreviewService
    {
        public static bool MatchesInput(RebarPreviewSnapshot s,string f)=>s.Fingerprint==f;
        public static bool HasExistingDuplicateBar(Document d,FamilyInstance h,RebarPreviewSnapshot s,string f)=>d.DuplicateHost==h.UniqueId;
        public static bool Matches(RebarPreviewSnapshot s,string f,IList<Rebar> bars)=>MatchesInput(s,f) && bars.All(b=>b.Host==f && !b.Altered);
    }
}
