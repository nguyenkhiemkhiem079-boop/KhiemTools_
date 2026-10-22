using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    public enum RebarShapeIdentityStatus
    {
        NotLoaded,
        Verified,
        Unverified,
        Ambiguous,
        AssetUnavailable
    }

    /// <summary>
    /// Evidence-bound inspection of a packaged Rebar Shape asset and its loaded
    /// Revit element. The Extensible Storage stamp persists across document saves.
    /// It is a provenance record, not a security signature.
    /// </summary>
    public sealed class RebarShapeIdentityInspection
    {
        public RebarShapeIdentityStatus Status { get; internal set; }
        public RebarShape Shape { get; internal set; }
        public string AssetPath { get; internal set; }
        public string AssetName { get; internal set; }
        public string AssetSha256 { get; internal set; }
        public string ManifestSha256 { get; internal set; }
        public int ManifestVersion { get; internal set; }
        public int ExistingMatches { get; internal set; }
        public string Message { get; internal set; }
    }

    /// <summary>
    /// Verifies the packaged Rebar Shape files against a versioned SHA-256 manifest
    /// and persists the asset identity on the exact RebarShape element loaded by K-TOOLS.
    /// </summary>
    public static class RebarShapeAssetIdentity
    {
        public const int CurrentManifestVersion = 1;
        public const string ManifestFileName = "rebar-shape-assets.v1.sha256";

        private static readonly Guid IdentitySchemaId = new Guid("ab9c6460-1b84-4b52-a1d3-8bb986c5503e");
        private const string IdentitySchemaName = "KToolsRebarShapeAssetV1";

        public static RebarShapeIdentityInspection Inspect(Document doc, string shapeName)
        {
            var result = new RebarShapeIdentityInspection
            {
                Status = RebarShapeIdentityStatus.AssetUnavailable,
                AssetName = string.IsNullOrWhiteSpace(shapeName) ? null : shapeName + ".rfa"
            };

            if (doc == null)
            {
                result.Message = "A Revit document is required to inspect a loaded Rebar Shape.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(shapeName) ||
                !RebarShapeLibrary.AllStandardShapes.Contains(shapeName, StringComparer.OrdinalIgnoreCase))
            {
                result.Message = "The requested name is not in the versioned K-TOOLS Rebar Shape catalog.";
                return result;
            }

            RebarShape[] matches;
            try
            {
                matches = new FilteredElementCollector(doc)
                    .OfClass(typeof(RebarShape))
                    .Cast<RebarShape>()
                    .Where(shape => shape.Name.Equals(shapeName, StringComparison.OrdinalIgnoreCase))
                    .Take(2)
                    .ToArray();
            }
            catch (Exception ex)
            {
                result.Message = "Could not inspect loaded Rebar Shapes: " + ex.Message;
                return result;
            }

            result.ExistingMatches = matches.Length;
            if (matches.Length > 1)
            {
                result.Status = RebarShapeIdentityStatus.Ambiguous;
                result.Message = "More than one exact-name Rebar Shape exists in this document.";
                return result;
            }

            if (matches.Length == 1)
            {
                result.Shape = matches[0];
            }

            string resourcePath = RebarShapeLibrary.ResolveRfaPath(shapeName);
            result.AssetPath = resourcePath;
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                result.Status = result.Shape == null
                    ? RebarShapeIdentityStatus.AssetUnavailable
                    : RebarShapeIdentityStatus.Unverified;
                result.Message = "The packaged Rebar Shape file could not be located.";
                return result;
            }

            string resourceHash;
            string manifestHash;
            string validationError;
            if (!TryVerifyResource(shapeName, resourcePath, out resourceHash, out manifestHash, out validationError))
            {
                result.Status = result.Shape == null
                    ? RebarShapeIdentityStatus.AssetUnavailable
                    : RebarShapeIdentityStatus.Unverified;
                result.Message = validationError;
                return result;
            }

            result.AssetPath = resourcePath;
            result.AssetSha256 = resourceHash;
            result.ManifestSha256 = manifestHash;
            result.ManifestVersion = CurrentManifestVersion;

            if (result.Shape == null)
            {
                result.Status = RebarShapeIdentityStatus.NotLoaded;
                result.Message = "The packaged Rebar Shape asset matches the manifest and is not loaded.";
                return result;
            }

            string identityError;
            if (HasMatchingIdentity(result.Shape, result.AssetName, result.AssetSha256,
                    result.ManifestSha256, result.ManifestVersion, out identityError))
            {
                result.Status = RebarShapeIdentityStatus.Verified;
                result.Message = "The loaded Rebar Shape element is stamped with the matching K-TOOLS asset identity.";
                return result;
            }

            result.Status = RebarShapeIdentityStatus.Unverified;
            result.Message = identityError;
            return result;
        }

        internal static bool TryVerifyResource(string shapeName, string resourcePath,
            out string assetSha256, out string manifestSha256, out string error)
        {
            assetSha256 = null;
            manifestSha256 = null;
            error = null;

            if (string.IsNullOrWhiteSpace(shapeName) ||
                !RebarShapeLibrary.AllStandardShapes.Contains(shapeName, StringComparer.OrdinalIgnoreCase))
            {
                error = "The requested name is not in the versioned K-TOOLS Rebar Shape catalog.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(resourcePath) || !File.Exists(resourcePath))
            {
                error = "The packaged Rebar Shape file is missing.";
                return false;
            }

            string expectedFileName = shapeName + ".rfa";
            if (!Path.GetFileName(resourcePath).Equals(expectedFileName, StringComparison.OrdinalIgnoreCase))
            {
                error = "The resolved file name does not match the exact catalog name.";
                return false;
            }

            string manifestPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(resourcePath)), ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                error = "The versioned Rebar Shape asset manifest is missing beside the packaged family.";
                return false;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(manifestPath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                error = "Could not read the Rebar Shape asset manifest: " + ex.Message;
                return false;
            }

            int version = -1;
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawLine in lines)
            {
                string line = (rawLine ?? string.Empty).Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

                int separator = line.IndexOf('=');
                if (separator <= 0 || separator == line.Length - 1)
                {
                    error = "The Rebar Shape asset manifest contains a malformed entry.";
                    return false;
                }

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                if (key.Equals("manifest-version", StringComparison.OrdinalIgnoreCase))
                {
                    if (version >= 0 || !int.TryParse(value, out version))
                    {
                        error = "The Rebar Shape asset manifest has a duplicate or invalid version.";
                        return false;
                    }
                    continue;
                }

                if (!IsSha256(value) || hashes.ContainsKey(key))
                {
                    error = "The Rebar Shape asset manifest contains an invalid hash or duplicate file entry.";
                    return false;
                }
                hashes.Add(key, value.ToLowerInvariant());
            }

            if (version != CurrentManifestVersion || hashes.Count != RebarShapeLibrary.AllStandardShapes.Length)
            {
                error = "The Rebar Shape asset manifest version or catalog size is unsupported.";
                return false;
            }

            foreach (string catalogName in RebarShapeLibrary.AllStandardShapes)
            {
                if (!hashes.ContainsKey(catalogName + ".rfa"))
                {
                    error = "The Rebar Shape asset manifest is missing " + catalogName + ".rfa.";
                    return false;
                }
            }

            string requestedHash;
            if (!hashes.TryGetValue(expectedFileName, out requestedHash))
            {
                error = "The Rebar Shape asset manifest has no entry for " + expectedFileName + ".";
                return false;
            }

            string canonicalManifest = string.Join("\n", lines.Select(line => (line ?? string.Empty).TrimEnd('\r'))) + "\n";
            manifestSha256 = ComputeSha256(Encoding.UTF8.GetBytes(canonicalManifest));

            try
            {
                using (var stream = File.OpenRead(resourcePath))
                {
                    assetSha256 = ComputeSha256(stream);
                }
            }
            catch (Exception ex)
            {
                error = "Could not hash the packaged Rebar Shape file: " + ex.Message;
                return false;
            }

            if (!assetSha256.Equals(requestedHash, StringComparison.OrdinalIgnoreCase))
            {
                error = "The packaged Rebar Shape SHA-256 does not match the versioned manifest.";
                return false;
            }

            return true;
        }

        internal static bool Stamp(RebarShape shape, RebarShapeIdentityInspection asset, out string error)
        {
            error = null;
            if (shape == null || !shape.IsValidObject || asset == null ||
                asset.Status != RebarShapeIdentityStatus.NotLoaded ||
                string.IsNullOrWhiteSpace(asset.AssetSha256) || string.IsNullOrWhiteSpace(asset.ManifestSha256))
            {
                error = "A verified, not-yet-loaded asset and a valid Rebar Shape element are required.";
                return false;
            }

            if (shape.Document == null || !shape.Document.IsModifiable)
            {
                error = "The Rebar Shape identity stamp requires an open modifiable transaction.";
                return false;
            }

            try
            {
                Schema schema = GetOrCreateIdentitySchema();
                Entity existing = shape.GetEntity(schema);
                if (existing.IsValid())
                {
                    return HasMatchingIdentity(shape, asset.AssetName, asset.AssetSha256,
                        asset.ManifestSha256, asset.ManifestVersion, out error);
                }

                var entity = new Entity(schema);
                entity.Set("ManifestVersion", asset.ManifestVersion);
                entity.Set("AssetName", asset.AssetName);
                entity.Set("AssetSha256", asset.AssetSha256);
                entity.Set("ManifestSha256", asset.ManifestSha256);
                entity.Set("ShapeUniqueId", shape.UniqueId);
                shape.SetEntity(entity);

                return HasMatchingIdentity(shape, asset.AssetName, asset.AssetSha256,
                    asset.ManifestSha256, asset.ManifestVersion, out error);
            }
            catch (Exception ex)
            {
                error = "Could not persist the Rebar Shape asset identity: " + ex.Message;
                return false;
            }
        }

        private static bool HasMatchingIdentity(RebarShape shape, string assetName, string assetSha256,
            string manifestSha256, int manifestVersion, out string error)
        {
            error = null;
            if (shape == null || !shape.IsValidObject)
            {
                error = "The loaded Rebar Shape element is invalid.";
                return false;
            }

            Schema schema = Schema.Lookup(IdentitySchemaId);
            if (schema == null)
            {
                error = "The loaded Rebar Shape has no persistent K-TOOLS asset identity.";
                return false;
            }

            try
            {
                Entity entity = shape.GetEntity(schema);
                if (!entity.IsValid())
                {
                    error = "The loaded Rebar Shape predates asset identity stamping or was loaded outside K-TOOLS.";
                    return false;
                }

                bool matches = entity.Get<int>("ManifestVersion") == manifestVersion &&
                    entity.Get<string>("AssetName").Equals(assetName, StringComparison.OrdinalIgnoreCase) &&
                    entity.Get<string>("AssetSha256").Equals(assetSha256, StringComparison.OrdinalIgnoreCase) &&
                    entity.Get<string>("ManifestSha256").Equals(manifestSha256, StringComparison.OrdinalIgnoreCase) &&
                    entity.Get<string>("ShapeUniqueId").Equals(shape.UniqueId, StringComparison.Ordinal);
                if (!matches)
                {
                    error = "The loaded Rebar Shape identity does not match the current packaged asset manifest.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = "Could not verify the persistent Rebar Shape asset identity: " + ex.Message;
                return false;
            }
        }

        private static Schema GetOrCreateIdentitySchema()
        {
            Schema schema = Schema.Lookup(IdentitySchemaId);
            if (schema != null) return schema;

            var builder = new SchemaBuilder(IdentitySchemaId);
            builder.SetSchemaName(IdentitySchemaName);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField("ManifestVersion", typeof(int));
            builder.AddSimpleField("AssetName", typeof(string));
            builder.AddSimpleField("AssetSha256", typeof(string));
            builder.AddSimpleField("ManifestSha256", typeof(string));
            builder.AddSimpleField("ShapeUniqueId", typeof(string));
            return builder.Finish();
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }

        private static string ComputeSha256(Stream stream)
        {
            using (var sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(stream));
            }
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(bytes));
            }
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
