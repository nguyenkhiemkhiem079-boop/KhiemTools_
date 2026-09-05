using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.FamilyManager.Models;

namespace KhimTools.FamilyManager.Services
{
    /// <summary>
    /// Non-destructive Family Load Options.
    /// Preserves existing element instances and parameter overrides.
    /// </summary>
    public class NonDestructiveFamilyLoadOptions : IFamilyLoadOptions
    {
        private readonly bool _overwriteParameters;

        public NonDestructiveFamilyLoadOptions(bool overwriteParameters = false)
        {
            _overwriteParameters = overwriteParameters;
        }

        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            // Allow reload without destroying placed elements or user modifications
            overwriteParameterValues = _overwriteParameters;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = _overwriteParameters;
            return true;
        }
    }

    /// <summary>
    /// Service responsible for loading families into a Revit Document safely,
    /// isolating errors, tracking progress, and non-destructively updating existing content.
    /// </summary>
    public class FamilyLoaderService
    {
        /// <summary>
        /// Loads a collection of family items into the document.
        /// </summary>
        public static FamilyLoadResult LoadFamilies(
            Document doc,
            IEnumerable<FamilyItemModel> items,
            bool overwriteExisting = false,
            Action<string, int, int> progressCallback = null)
        {
            var result = new FamilyLoadResult();
            if (doc == null || items == null) return result;

            var itemsToLoad = items.ToList();
            if (itemsToLoad.Count == 0) return result;

            var loadOptions = new NonDestructiveFamilyLoadOptions(overwriteExisting);

            using (var tx = new Transaction(doc, "Load KhimTools Families"))
            {
                tx.Start();

                int current = 0;
                int total = itemsToLoad.Count;

                foreach (var item in itemsToLoad)
                {
                    current++;
                    progressCallback?.Invoke(item.FamilyName, current, total);

                    if (!File.Exists(item.FilePath))
                    {
                        item.Status = FamilyItemStatus.NotFound;
                        item.ErrorMessage = $"File not found: {item.FilePath}";
                        result.RecordFailure(item.FamilyName, item.ErrorMessage);
                        continue;
                    }

                    // If not overwriting and already loaded, count as up to date
                    if (!overwriteExisting && item.IsLoadedInProject)
                    {
                        item.Status = FamilyItemStatus.UpToDate;
                        result.RecordUpToDate(item.FamilyName);
                        continue;
                    }

                    try
                    {
                        bool success = doc.LoadFamily(item.FilePath, loadOptions, out Family loadedFamily);
                        if (success && loadedFamily != null)
                        {
                            item.IsLoadedInProject = true;
                            item.Status = FamilyItemStatus.Loaded;
                            item.ErrorMessage = null;
                            try
                            {
                                var symbolIds = loadedFamily.GetFamilySymbolIds();
                                item.LoadedTypeCount = symbolIds != null ? symbolIds.Count : 1;
                            }
                            catch
                            {
                                item.LoadedTypeCount = 1;
                            }

                            result.RecordLoaded(item.FamilyName);
                        }
                        else
                        {
                            // If doc.LoadFamily returns false, it usually means the family is already loaded and identical
                            item.IsLoadedInProject = true;
                            item.Status = FamilyItemStatus.UpToDate;
                            result.RecordUpToDate(item.FamilyName);
                        }
                    }
                    catch (Exception ex)
                    {
                        item.Status = FamilyItemStatus.LoadFailed;
                        item.ErrorMessage = ex.Message;
                        result.RecordFailure(item.FamilyName, ex.Message);
                    }
                }

                tx.Commit();
            }

            return result;
        }

        /// <summary>
        /// Loads a single family from disk.
        /// </summary>
        public static FamilyLoadResult LoadSingleFamily(Document doc, string filePath, bool overwriteExisting = false)
        {
            var result = new FamilyLoadResult();
            if (doc == null || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                result.RecordFailure(Path.GetFileNameWithoutExtension(filePath) ?? "Unknown", "Invalid file path or file does not exist.");
                return result;
            }

            string familyName = Path.GetFileNameWithoutExtension(filePath);
            var item = new FamilyItemModel
            {
                FamilyName = familyName,
                FilePath = filePath,
                IsSelected = true
            };

            return LoadFamilies(doc, new[] { item }, overwriteExisting);
        }

        /// <summary>
        /// Loads all rebar shapes discovered in the environment.
        /// Adheres to rule: "CHECK REBAR = LOAD THE COMPLETE REBAR LIBRARY".
        /// </summary>
        public static FamilyLoadResult LoadAllRebarShapes(Document doc, IEnumerable<FamilyItemModel> rebarItems = null, Action<string, int, int> progressCallback = null)
        {
            if (rebarItems == null)
            {
                var groups = FamilyDiscoveryService.DiscoverGroups();
                var rebarGroup = groups.FirstOrDefault(g => g.GroupType == FamilyGroupType.Rebar);
                rebarItems = rebarGroup?.Families ?? Enumerable.Empty<FamilyItemModel>();
            }

            var itemsList = rebarItems.ToList();
            return LoadFamilies(doc, itemsList, false, progressCallback);
        }
    }
}
