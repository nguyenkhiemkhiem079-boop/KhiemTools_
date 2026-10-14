using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace KhimTools.Core.Workflow
{
    /// <summary>
    /// Deterministic fingerprinting for primitive plan tokens. Callers must provide
    /// explicit stable identifiers rather than API object ToString() values.
    /// </summary>
    public static class WorkflowFingerprint
    {
        public static string Compute(IEnumerable<string> tokens)
        {
            IEnumerable<string> normalized = (tokens ?? Enumerable.Empty<string>())
                .Select(token => token ?? string.Empty);
            // Length-prefix each token so embedded separators cannot make distinct
            // plans hash to the same canonical representation.
            string canonical = string.Concat(normalized.Select(token => token.Length + ":" + token));
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                return string.Concat(hash.Select(value => value.ToString("x2")));
            }
        }

        public static string Compute(params string[] tokens)
        {
            return Compute((IEnumerable<string>)tokens);
        }

        public static bool Matches(string expected, string actual)
        {
            return !string.IsNullOrEmpty(expected) &&
                   string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }
    }
}
