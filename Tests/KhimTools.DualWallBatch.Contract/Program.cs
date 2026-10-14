using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.RebarTool.Core;

// Synthetic API/transaction doubles, production batch and boundary linked unchanged.
// This executes no native geometry, Revit host, real rollback or Undo.
static class Program
{
    static int checks;
    static void Check(bool p,string s){if(!p)throw new Exception(s);checks++;}
    static bool Reject(Action a){try{a();return false;}catch(InvalidOperationException){return true;}}
    static Document Model(){var d=new Document();d.Created.Add(new Rebar{UniqueId="UNRELATED",Host="OTHER",Document=d});return d;}
    static bool Preserved(Document d)=>d.Created.Count==1&&d.Created[0].UniqueId=="UNRELATED"&&!d.GeometryChanged;
    static DualWallHostAssignmentV1[] Plans(Document d)=>new[]{DualWallHostAssignmentV1.Wall(d,new Wall{UniqueId="A",Document=d},new WallManualPlanV1(),new RebarPreviewSnapshot("A")),DualWallHostAssignmentV1.Column(d,new FamilyInstance{UniqueId="B",Document=d},new ColumnWallRelatedPlanV1(),new RebarPreviewSnapshot("B"))};
    static void Main(){
        var d=Model();var p=Plans(d);Check(DualWallHostBatchV1.Create(d,p).Count==2&&d.Assimilated,"A/B commit atomically with distinct ownership");
        d=Model();p=Plans(d);p[1].Preview.Components[0].InputFingerprint="STALE";Check(Reject(()=>DualWallHostBatchV1.Create(d,p))&&d.Calls==0&&Preserved(d),"All-host stale preflight before mutation");
        d=Model();p=Plans(d);d.Duplicate="B";Check(Reject(()=>DualWallHostBatchV1.Create(d,p))&&d.Calls==0&&Preserved(d),"Later duplicate cannot create first host");
        d=Model();p=Plans(d);d.Failure="B";Check(Reject(()=>DualWallHostBatchV1.Create(d,p))&&d.Calls==2&&d.RolledBack&&Preserved(d),"Later creation failure rolls back both and unrelated state");
        d=Model();p=Plans(d);d.ParityFailure="B";Check(Reject(()=>DualWallHostBatchV1.Create(d,p))&&d.RolledBack&&Preserved(d),"Later parity failure rolls back all");
        d=Model();p=Plans(d);d.ChangeFirstOutput=true;Check(Reject(()=>DualWallHostBatchV1.Create(d,p))&&d.RolledBack&&Preserved(d),"Final earlier-output change detected");
        d=Model();p=Plans(d);d.ChangeFirstFrame=true;Check(Reject(()=>DualWallHostBatchV1.Create(d,p))&&d.RolledBack&&Preserved(d),"Final earlier-frame change detected");
        d=Model();p=Plans(d);p[1].Host.Document=new Document();Check(Reject(()=>DualWallHostBatchV1.Create(d,p))&&d.Calls==0,"Foreign document rejected");
        d=Model();p=Plans(d);p[1].Contract="A_WALL";Check(Reject(()=>DualWallHostBatchV1.Create(d,p))&&d.Calls==0,"Contract/category mismatch never silently rehosts");
        d=Model();p=Plans(d);Check(Reject(()=>DualWallHostBatchV1.Create(d,new[]{p[0],p[0]}))&&d.Calls==0,"Duplicate host rejected");
        d=Model();p=Plans(d);p[1].Preview.Components.Add(new PreviewComponent{HostId="B",InputFingerprint="B"});Check(Reject(()=>DualWallHostBatchV1.Create(d,p))&&d.Calls==0,"Ambiguous preview cannot pass");
        d=Model();p=Plans(d);d.StartFails=true;Check(Reject(()=>DualWallHostBatchV1.Create(d,p))&&d.Calls==0&&Preserved(d),"Failed outer start cannot mutate");
        d=Model();p=Plans(d);d.Empty="B";Check(Reject(()=>DualWallHostBatchV1.Create(d,p))&&Preserved(d)&&!d.Assimilated,"Empty later output cannot assimilate");
        Console.WriteLine($"DUAL_WALL_BATCH_CONTRACT=PASS ({checks} assertions); synthetic doubles only; LIVE_REVIT=NOT_EXECUTED");
    }
}
namespace Autodesk.Revit.DB
{
    public enum BuiltInCategory{OST_StructuralColumns}
    public enum TransactionStatus{Uninitialized,Started,Committed,RolledBack,Error}
    public class ElementId{public string Value;public static bool operator==(ElementId a,ElementId b)=>a?.Value==b?.Value;public static bool operator!=(ElementId a,ElementId b)=>!(a==b);public override bool Equals(object o)=>o is ElementId i&&i==this;public override int GetHashCode()=>Value.GetHashCode();}
    public class Category{}
    public class Element{public string UniqueId;public Document Document;public bool IsValidObject=true;public Category Category=new Category();public ElementId Id=>new ElementId{Value=UniqueId};}
    public class Wall:Element{}
    public class FamilyInstance:Element{}
    public class Document
    {
        public List<Rebar> Created=new List<Rebar>();public int Calls;public string Duplicate,Failure,ParityFailure,Empty;
        public bool Assimilated,RolledBack,GeometryChanged,ChangeFirstOutput,ChangeFirstFrame,StartFails;
        public void Regenerate(){}
        public IList<Rebar> Generate(Element h){Calls++;var b=new Rebar{Document=this,UniqueId=h.UniqueId+"_BAR",Host=h.UniqueId,Altered=ParityFailure==h.UniqueId};if(Empty==h.UniqueId)return new List<Rebar>();Created.Add(b);if(Failure==h.UniqueId)throw new InvalidOperationException("QA ONLY failure");return new List<Rebar>{b};}
    }
    public class Transaction:IDisposable
    {
        protected Document d;protected TransactionStatus status;protected List<Rebar> before;protected bool geometry;
        public Transaction(Document doc,string name){d=doc;}
        public TransactionStatus Start(){before=new List<Rebar>(d.Created);geometry=d.GeometryChanged;return status=TransactionStatus.Started;}
        public TransactionStatus GetStatus()=>status;
        public TransactionStatus Commit(){if(d.Created.Any(b=>b.Host=="B")){if(d.ChangeFirstOutput)d.Created.First(b=>b.Host=="A").Altered=true;if(d.ChangeFirstFrame)d.GeometryChanged=true;}return status=TransactionStatus.Committed;}
        public TransactionStatus RollBack(){d.Created=new List<Rebar>(before);d.GeometryChanged=geometry;return status=TransactionStatus.RolledBack;}
        public void Dispose(){}
    }
    public class SubTransaction:Transaction{public SubTransaction(Document d):base(d,""){}}
    public class TransactionGroup:IDisposable
    {
        Document d;List<Rebar> before;bool geometry;TransactionStatus status;
        public TransactionGroup(Document doc,string name){d=doc;}
        public TransactionStatus Start(){before=new List<Rebar>(d.Created);geometry=d.GeometryChanged;return status=d.StartFails?TransactionStatus.Error:TransactionStatus.Started;}
        public TransactionStatus GetStatus()=>status;
        public TransactionStatus Assimilate(){d.Assimilated=true;return status=TransactionStatus.Committed;}
        public TransactionStatus RollBack(){d.Created=new List<Rebar>(before);d.GeometryChanged=geometry;d.RolledBack=true;return status=TransactionStatus.RolledBack;}
        public void Dispose(){}
    }
}
namespace Autodesk.Revit.DB.Structure{public class Rebar:Element{public string Host;public bool Altered;public ElementId GetHostId()=>new ElementId{Value=Host};}}
namespace KhimTools.Core{public static class Extensions{public static bool IsCategory(this Category c,BuiltInCategory b)=>true;public static long ToLongValue(this ElementId i)=>i.Value.GetHashCode();}}
namespace KhimTools.RebarTool.Core
{
    public class WallManualPlanV1{}
    public class ColumnWallRelatedPlanV1{}
    public class PreviewComponent{public string HostId,InputFingerprint;}
    public class RebarPreviewSnapshot{public List<PreviewComponent> Components;public RebarPreviewSnapshot(string host){Components=new List<PreviewComponent>{new PreviewComponent{HostId=host,InputFingerprint=host}};}}
    public class WallRebarGeneratorV1{Document d;public WallRebarGeneratorV1(Document doc){d=doc;}public string Fingerprint(Wall h,WallManualPlanV1 p,ISet<string> exclude=null)=>h.UniqueId=="A"&&d.GeometryChanged?"CHANGED":h.UniqueId;public IList<Rebar> Generate(Wall h,WallManualPlanV1 p)=>d.Generate(h);}
    public class ColumnWallRelatedGeneratorV1{Document d;public ColumnWallRelatedGeneratorV1(Document doc){d=doc;}public string Fingerprint(FamilyInstance h,ColumnWallRelatedPlanV1 p,ISet<string> exclude=null)=>h.UniqueId;public IList<Rebar> Generate(FamilyInstance h,ColumnWallRelatedPlanV1 p)=>d.Generate(h);}
    public static class RebarPreviewService
    {
        public static bool MatchesInput(RebarPreviewSnapshot s,string f)=>s!=null&&s.Components.Any(c=>c.InputFingerprint==f);
        public static bool HasExistingDuplicateBar(Document d,Element h,RebarPreviewSnapshot s,string f)=>d.Duplicate==h.UniqueId;
        public static bool Matches(RebarPreviewSnapshot s,string f,IList<Rebar> b)=>MatchesInput(s,f)&&b.Count>0&&b.All(r=>!r.Altered&&r.Host==s.Components[0].HostId);
    }
}
