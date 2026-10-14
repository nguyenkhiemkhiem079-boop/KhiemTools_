using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;
using KhimTools.Core.Revit;

namespace KhimTools.RebarTool.Core
{
    internal sealed class DualWallHostAssignmentV1
    {
        internal Element Host;
        internal string Contract;
        internal RebarPreviewSnapshot Preview;
        internal Func<ISet<string>,string> Fingerprint;
        internal Func<IList<Rebar>> Generate;
        internal static DualWallHostAssignmentV1 Wall(Document doc,Wall host,WallManualPlanV1 plan,RebarPreviewSnapshot preview)
        {
            var generator=new WallRebarGeneratorV1(doc);
            return new DualWallHostAssignmentV1{Host=host,Contract="A_WALL",Preview=preview,Fingerprint=exclude=>generator.Fingerprint(host,plan,exclude),Generate=()=>generator.Generate(host,plan)};
        }
        internal static DualWallHostAssignmentV1 Column(Document doc,FamilyInstance host,ColumnWallRelatedPlanV1 plan,RebarPreviewSnapshot preview)
        {
            var generator=new ColumnWallRelatedGeneratorV1(doc);
            return new DualWallHostAssignmentV1{Host=host,Contract="B_COLUMN",Preview=preview,Fingerprint=exclude=>generator.Fingerprint(host,plan,exclude),Generate=()=>generator.Generate(host,plan)};
        }
    }
    internal static class DualWallHostBatchV1
    {
        internal static IReadOnlyList<Rebar> Create(Document doc,IReadOnlyList<DualWallHostAssignmentV1> assignments)
        {
            if(doc==null||assignments==null||assignments.Count==0)throw new InvalidOperationException("Explicit per-host assignments required.");
            if(assignments.Any(a=>a==null||a.Host==null||!a.Host.IsValidObject||a.Host.Document!=doc)||assignments.GroupBy(a=>a.Host.UniqueId).Any(g=>g.Count()>1))
                throw new InvalidOperationException("Distinct valid same-document hosts required; no copy/rehost/implicit assignment.");
            foreach(var a in assignments) {
                if(a.Contract=="A_WALL"?!(a.Host is Wall):a.Contract=="B_COLUMN"?!(a.Host is FamilyInstance column&&column.Category.IsCategory(BuiltInCategory.OST_StructuralColumns)):true)
                    throw new InvalidOperationException("Explicit contract/category mismatch; ownership must never switch.");
                string fingerprint=a.Fingerprint(null);
                if(!RebarPreviewService.MatchesInput(a.Preview,fingerprint)||a.Preview.Components.Count!=1||a.Preview.Components.Any(c=>c.HostId!=a.Host.UniqueId))throw new InvalidOperationException("Missing/stale/foreign/ambiguous accepted per-host preview.");
                if(RebarPreviewService.HasExistingDuplicateBar(doc,a.Host,a.Preview,fingerprint))throw new InvalidOperationException("Complete plan already exists on this exact host.");
            }
            return TransactionBoundary.ExecuteGroup(doc,"K-TOOLS explicit Wall/Column host assignments",()=>{
                var outputs=new Dictionary<DualWallHostAssignmentV1,IList<Rebar>>();var owned=new HashSet<string>(StringComparer.Ordinal);
                foreach(var a in assignments) {
                    string expected=a.Preview.Components[0].InputFingerprint;
                    var bars=TransactionBoundary.Execute(doc,"Create "+a.Contract+" actual host "+a.Host.Id.ToLongValue(),()=>{
                        var result=a.Generate();doc.Regenerate();
                        if(result.Any(b=>b.GetHostId()!=a.Host.Id)||!RebarPreviewService.Matches(a.Preview,expected,result))throw new InvalidOperationException("Exact host/output parity failed; entire multi-host group rolls back.");return result;
                    });
                    outputs.Add(a,bars);foreach(var bar in bars)owned.Add(bar.UniqueId);
                }
                doc.Regenerate();
                foreach(var a in assignments) {
                    string current=a.Fingerprint(owned);
                    if(!RebarPreviewService.MatchesInput(a.Preview,current)||outputs[a].Any(b=>!b.IsValidObject||b.GetHostId()!=a.Host.Id)||!RebarPreviewService.Matches(a.Preview,current,outputs[a]))
                        throw new InvalidOperationException("Final all-host input/ownership/output recheck failed; entire group rolls back.");
                }
                return (IReadOnlyList<Rebar>)outputs.Values.SelectMany(b=>b).ToArray();
            });
        }
    }
}
