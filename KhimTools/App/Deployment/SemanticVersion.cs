using System;
using System.Text.RegularExpressions;

namespace KhiemToolsApp.Deployment
{
    /// <summary>
    /// Implements SemVer 2.0 specification parsing and comparison.
    /// Supports major.minor.patch with prerelease identifiers (e.g. 2.7.1-beta, 2.7.1-rc1, 2.7.1, 2.7.2).
    /// </summary>
    public class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
    {
        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Patch { get; private set; }
        public string Prerelease { get; private set; }
        public string RawString { get; private set; }

        public bool IsPrerelease
        {
            get { return !string.IsNullOrEmpty(Prerelease); }
        }

        public SemanticVersion(int major, int minor, int patch, string prerelease = null, string raw = null)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Prerelease = string.IsNullOrWhiteSpace(prerelease) ? null : prerelease.TrimStart('-');
            RawString = raw ?? ToString();
        }

        public static SemanticVersion Parse(string versionStr)
        {
            SemanticVersion result;
            if (!TryParse(versionStr, out result))
            {
                throw new FormatException(string.Format("Invalid semantic version string: '{0}'", versionStr));
            }
            return result;
        }

        public static bool TryParse(string versionStr, out SemanticVersion semVer)
        {
            semVer = null;
            if (string.IsNullOrWhiteSpace(versionStr)) return false;

            string cleaned = versionStr.Trim();
            if (cleaned.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(1);
            }

            // Match major.minor[.patch][-prerelease]
            Match m = Regex.Match(cleaned, @"^(\d+)\.(\d+)(?:\.(\d+))?(?:-([0-9A-Za-z\.-]+))?");
            if (!m.Success) return false;

            int major = int.Parse(m.Groups[1].Value);
            int minor = int.Parse(m.Groups[2].Value);
            int patch = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
            string prerelease = m.Groups[4].Success ? m.Groups[4].Value : null;

            semVer = new SemanticVersion(major, minor, patch, prerelease, versionStr);
            return true;
        }

        public int CompareTo(SemanticVersion other)
        {
            if (ReferenceEquals(other, null)) return 1;

            if (Major != other.Major) return Major.CompareTo(other.Major);
            if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
            if (Patch != other.Patch) return Patch.CompareTo(other.Patch);

            // SemVer 2.0 Clause 11:
            // When major, minor, and patch are equal, a pre-release version has lower precedence than a normal version.
            // Example: 1.0.0-alpha < 1.0.0
            if (IsPrerelease && !other.IsPrerelease) return -1;
            if (!IsPrerelease && other.IsPrerelease) return 1;
            if (!IsPrerelease && !other.IsPrerelease) return 0;

            // Both are prereleases: compare prerelease identifiers
            return ComparePrerelease(Prerelease, other.Prerelease);
        }

        private static int ComparePrerelease(string a, string b)
        {
            string[] partsA = a.Split('.');
            string[] partsB = b.Split('.');
            int minLen = Math.Min(partsA.Length, partsB.Length);

            for (int i = 0; i < minLen; i++)
            {
                string idA = partsA[i];
                string idB = partsB[i];

                int numA, numB;
                bool isNumA = int.TryParse(idA, out numA);
                bool isNumB = int.TryParse(idB, out numB);

                if (isNumA && isNumB)
                {
                    if (numA != numB) return numA.CompareTo(numB);
                }
                else if (isNumA && !isNumB)
                {
                    return -1; // Numeric identifiers always have lower precedence than non-numeric identifiers
                }
                else if (!isNumA && isNumB)
                {
                    return 1;
                }
                else
                {
                    int lexical = string.Compare(idA, idB, StringComparison.OrdinalIgnoreCase);
                    if (lexical != 0) return lexical;
                }
            }

            return partsA.Length.CompareTo(partsB.Length);
        }

        public bool Equals(SemanticVersion other)
        {
            if (ReferenceEquals(other, null)) return false;
            return CompareTo(other) == 0;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SemanticVersion);
        }

        public override int GetHashCode()
        {
            return (Major * 397) ^ (Minor * 31) ^ Patch ^ (Prerelease != null ? Prerelease.GetHashCode() : 0);
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Prerelease))
            {
                return string.Format("{0}.{1}.{2}", Major, Minor, Patch);
            }
            return string.Format("{0}.{1}.{2}-{3}", Major, Minor, Patch, Prerelease);
        }
    }
}
