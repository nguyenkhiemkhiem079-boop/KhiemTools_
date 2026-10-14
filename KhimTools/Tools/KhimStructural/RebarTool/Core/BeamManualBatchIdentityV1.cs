using System;
using System.Collections.Generic;
using System.Linq;

namespace KhimTools.RebarTool.Core
{
    public static class BeamManualBatchIdentityV1
    {
        public static void Validate(IEnumerable<BeamManualDetailingPlanV1> plans)
        {
            var array = (plans ?? throw new ArgumentNullException(nameof(plans))).ToArray();
            if (array.Length == 0 || array.Any(plan => plan == null || String.IsNullOrWhiteSpace(plan.HostUniqueId) || String.IsNullOrWhiteSpace(plan.DocumentKey)))
                throw new InvalidOperationException("Every selected Beam requires its own identified Manual plan.");
            if (array.Select(plan => plan.DocumentKey).Distinct(StringComparer.Ordinal).Count() != 1)
                throw new InvalidOperationException("All Manual plans must belong to the same current document.");
            if (array.Select(plan => plan.HostUniqueId).Distinct(StringComparer.Ordinal).Count() != array.Length)
                throw new InvalidOperationException("A Beam may only be assigned once in an atomic Manual batch.");
        }
    }
}
