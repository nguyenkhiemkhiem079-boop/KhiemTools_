using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using KhimTools.FamilyManager.Models;

namespace KhimTools.FamilyManager.Services
{
    /// <summary>
    /// Scans library source directories for .rfa families, filtering out backups and temp files,
    /// deduplicating across sources by priority, and organizing them into logical FamilyGroupModels.
    /// </summary>
    public class FamilyDiscoveryService
    {
        // Matches Revit backup suffix: .0001.rfa, .0023.rfa, etc.
        private static readonly Regex BackupRegex = new Regex(@"\.\d{3,5}\.rfa$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static List<string> LastScanWarnings { get; } = new List<string>();

        private static List<FamilyGroupModel> _cachedGroups;
        private static DateTime _cacheTimestamp = DateTime.MinValue;
        private static readonly TimeSpan CacheValidityDuration = TimeSpan.FromMinutes(2);
        private static readonly object _cacheLock = new object();

        public static void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cachedGroups = null;
                _cacheTimestamp = DateTime.MinValue;
            }
        }

        public static List<FamilyGroupModel> DiscoverGroups(FamilyManagerSettings settings = null, bool forceRescan = false)
        {
            lock (_cacheLock)
            {
                if (!forceRescan && _cachedGroups != null && (DateTime.UtcNow - _cacheTimestamp) < CacheValidityDuration)
                {
                    return _cachedGroups;
                }
            }

            var sources = FamilySourceResolver.ResolveAllSources(settings);
            var discovered = DiscoverFromSources(sources);

            lock (_cacheLock)
            {
                _cachedGroups = discovered;
                _cacheTimestamp = DateTime.UtcNow;
            }

            return discovered;
        }

        public static List<FamilyGroupModel> DiscoverFromSources(IEnumerable<FamilyLibrarySource> sources)
        {
            LastScanWarnings.Clear();
            // Dictionary by GroupType -> (FamilyName -> FamilyItemModel)
            var groupMap = new Dictionary<FamilyGroupType, Dictionary<string, FamilyItemModel>>();

            // Ensure Structure, Rebar, Annotation groups exist by default
            var groupOrder = new[]
            {
                FamilyGroupType.Structure,
                FamilyGroupType.Rebar,
                FamilyGroupType.Annotation,
                FamilyGroupType.Steel,
                FamilyGroupType.Precast,
                FamilyGroupType.Architecture,
                FamilyGroupType.MEP,
                FamilyGroupType.Detail,
                FamilyGroupType.Formwork
            };

            foreach (var gt in groupOrder)
            {
                groupMap[gt] = new Dictionary<string, FamilyItemModel>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var source in sources.Where(s => s.IsEnabled))
            {
                if (source.RootPaths == null) continue;

                foreach (var path in source.RootPaths)
                {
                    if (!Directory.Exists(path)) continue;

                    try
                    {
                        var files = Directory.GetFiles(path, "*.rfa", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            if (IsIgnoredFile(file)) continue;

                            var familyName = Path.GetFileNameWithoutExtension(file);
                            if (string.IsNullOrWhiteSpace(familyName)) continue;

                            var fileInfo = new FileInfo(file);
                            var item = new FamilyItemModel
                            {
                                FamilyName = familyName,
                                FilePath = file,
                                GroupType = source.LogicalGroup,
                                FileSizeBytes = fileInfo.Length,
                                SourcePriority = source.Priority,
                                Status = FamilyItemStatus.NotLoaded,
                                IsSelected = false // Default unchecked
                            };

                            var targetDict = groupMap[source.LogicalGroup];

                            if (!targetDict.TryGetValue(familyName, out var existing))
                            {
                                targetDict[familyName] = item;
                            }
                            else
                            {
                                // Deduplicate by Priority then File Modified Date
                                if (item.SourcePriority > existing.SourcePriority)
                                {
                                    targetDict[familyName] = item;
                                }
                                else if (item.SourcePriority == existing.SourcePriority)
                                {
                                    if (fileInfo.LastWriteTimeUtc > File.GetLastWriteTimeUtc(existing.FilePath))
                                    {
                                        targetDict[familyName] = item;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // AUDIT-13: Log specific directory path and error instead of bare catch
                        LastScanWarnings.Add($"Directory scan warning [{path}]: {ex.Message}");
                    }
                }
            }

            var result = new List<FamilyGroupModel>();
            foreach (var gt in groupOrder)
            {
                var dict = groupMap[gt];
                if (dict.Count == 0 && gt != FamilyGroupType.Structure && gt != FamilyGroupType.Rebar)
                {
                    // Skip empty non-core groups
                    continue;
                }

                string displayName = GetGroupDisplayName(gt);
                string description = GetGroupDescription(gt);

                var groupModel = new FamilyGroupModel(gt, displayName, description);
                var sortedItems = dict.Values.OrderBy(x => x.FamilyName, StringComparer.OrdinalIgnoreCase).ToList();
                foreach (var item in sortedItems)
                {
                    groupModel.Families.Add(item);
                }

                groupModel.UpdateParentState();
                result.Add(groupModel);
            }

            return result;
        }

        public static bool IsIgnoredFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return true;

            var fileName = Path.GetFileName(filePath);
            if (fileName.StartsWith("~") || fileName.StartsWith(".")) return true;

            // Check Revit backup pattern
            if (BackupRegex.IsMatch(fileName)) return true;

            return false;
        }

        private static string GetGroupDisplayName(FamilyGroupType type)
        {
            switch (type)
            {
                case FamilyGroupType.Structure: return "Structure Families";
                case FamilyGroupType.Rebar: return "Rebar Shapes Library";
                case FamilyGroupType.Annotation: return "Annotation & Tags";
                case FamilyGroupType.Steel: return "Steel Framing & Connections";
                case FamilyGroupType.Precast: return "Precast Concrete";
                case FamilyGroupType.Architecture: return "Architectural Elements";
                case FamilyGroupType.MEP: return "MEP Fixtures & Equipment";
                case FamilyGroupType.Detail: return "Detail Components";
                case FamilyGroupType.Formwork: return "Formwork & Shoring";
                default: return type.ToString();
            }
        }

        private static string GetGroupDescription(FamilyGroupType type)
        {
            switch (type)
            {
                case FamilyGroupType.Structure:
                    return "Columns, Beams, Foundations, Walls. Select only the families you need.";
                case FamilyGroupType.Rebar:
                    return "Complete Rebar Shape Library (T00-T80). Single checkbox loads all shapes.";
                case FamilyGroupType.Annotation:
                    return "Dimensions, tags, symbols, elevation marks.";
                default:
                    return $"Library components for {type}.";
            }
        }
    }
}
