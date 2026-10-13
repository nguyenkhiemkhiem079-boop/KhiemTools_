using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core.Revit;

namespace KhimTools.RebarTool.Core
{
    /// <summary>Independent host-bound plans, never rescaled or copied to a different Beam.</summary>
    public sealed class BeamManualBatchAssignmentV1
    {
        public BeamManualDetailingPlanV1 Plan { get; }
        public RebarPreviewSnapshot Preview { get; }
        public BeamManualBatchAssignmentV1(BeamManualDetailingPlanV1 plan, RebarPreviewSnapshot preview)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Preview = preview ?? throw new ArgumentNullException(nameof(preview));
        }
    }

    public static class BeamManualBatchV1
    {
        public static IList<Rebar> Create(Document document, IEnumerable<BeamManualBatchAssignmentV1> assignments)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var batch = (assignments ?? throw new ArgumentNullException(nameof(assignments))).ToArray();
            BeamManualBatchIdentityV1.Validate(batch.Select(item => item?.Plan));
            var generator = new BeamManualRebarGeneratorV1(document);
            var hosts = new FamilyInstance[batch.Length];
            var fingerprints = new string[batch.Length];
            // Validate EVERY acceptance before any mutation. Re-resolve identities, not cached host objects.
            for (int i = 0; i < batch.Length; i++)
            {
                hosts[i] = document.GetElement(batch[i].Plan.HostUniqueId) as FamilyInstance;
                fingerprints[i] = generator.Fingerprint(batch[i].Plan, hosts[i]);
                if (!RebarPreviewService.MatchesInput(batch[i].Preview, fingerprints[i]) ||
                    batch[i].Preview.Components.Count != 1)
                    throw new InvalidOperationException("Stale or incomplete Manual preview for host " + batch[i].Plan.HostUniqueId);
                if (RebarPreviewService.HasExistingDuplicateBar(document, hosts[i], batch[i].Preview, fingerprints[i]))
                    throw new InvalidOperationException("Complete Manual plan already exists on host " + hosts[i].UniqueId);
            }
            return TransactionBoundary.ExecuteGroup(document, "K-TOOLS — Atomic Manual Beam assignment", () =>
            {
                var result = new List<Rebar>();
                var outputByHost = new List<IList<Rebar>>();
                for (int i = 0; i < batch.Length; i++)
                {
                    int index = i;
                    // Earlier commits may trigger model changes; check the next host again inside the group.
                    if (!String.Equals(fingerprints[index], generator.Fingerprint(batch[index].Plan, hosts[index]), StringComparison.Ordinal))
                        throw new InvalidOperationException("Manual host changed during batch; whole assignment rolled back.");
                    IList<Rebar> output = TransactionBoundary.Execute(document, "Create Manual Beam " + (index + 1), () =>
                    {
                        IList<Rebar> bars = generator.Generate(batch[index].Plan, hosts[index]);
                        document.Regenerate();
                        if (bars == null || bars.Count == 0 || !RebarPreviewService.Matches(batch[index].Preview, fingerprints[index], bars))
                            throw new InvalidOperationException("Manual batch geometry parity failed; whole assignment rolled back.");
                        return bars;
                    });
                    outputByHost.Add(output);
                    result.AddRange(output);
                }
                // A later host commit may trigger an updater/join change to an earlier host.
                // Recheck ALL completed output before assimilation, not only immediately after generation.
                TransactionBoundary.Execute(document, "Verify complete Manual batch", () =>
                {
                    document.Regenerate();
                    for (int i = 0; i < batch.Length; i++)
                    {
                        if (!String.Equals(generator.GetCurrentFrame(hosts[i]).HostGeometrySignature,
                            batch[i].Plan.Frame.HostGeometrySignature, StringComparison.Ordinal) ||
                            !RebarPreviewService.Matches(batch[i].Preview, fingerprints[i], outputByHost[i]))
                            throw new InvalidOperationException("A completed Manual host/output changed during batch; whole assignment rolled back.");
                    }
                });
                return (IList<Rebar>)result;
            });
        }
    }
}
