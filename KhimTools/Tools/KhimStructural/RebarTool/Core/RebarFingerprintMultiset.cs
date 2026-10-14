using System;
using System.Collections.Generic;
using System.Linq;

namespace KhimTools.RebarTool.Core
{
    /// <summary>Matches a complete planned set while preserving repeated bar fingerprints.</summary>
    internal static class RebarFingerprintMultiset
    {
        internal static bool Contains(IEnumerable<string> expected, IEnumerable<string> actual)
        {
            string[] expectedItems = (expected ?? Enumerable.Empty<string>()).ToArray();
            if (expectedItems.Length == 0) return false;

            var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string fingerprint in expectedItems)
            {
                if (string.IsNullOrEmpty(fingerprint)) return false;
                int count;
                remaining.TryGetValue(fingerprint, out count);
                remaining[fingerprint] = count + 1;
            }

            foreach (string fingerprint in actual ?? Enumerable.Empty<string>())
            {
                int count;
                if (!string.IsNullOrEmpty(fingerprint) && remaining.TryGetValue(fingerprint, out count))
                {
                    if (count == 1) remaining.Remove(fingerprint);
                    else remaining[fingerprint] = count - 1;
                }
            }
            return remaining.Count == 0;
        }
    }
}
