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
    /// isolating errors per-family, tracking progress, and non-destructively updating existing content.
    /// Each family is loaded in its own isolated transaction so one failure cannot roll back the
    /// entire batch — critical for large Rebar libraries with 200+ shapes.
    /// </summary>
    public class FamilyLoaderService
    {
        /// <summary>
        /// Loads a collection of family items into the document.
        /// Each family uses its own transaction for fault isolation.
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

                // If not overwriting and already confirmed loaded, skip (but do not trust stale flag).
                if (!overwriteExisting && item.IsLoadedInProject)
                {
                    item.Status = FamilyItemStatus.UpToDate;
                    result.RecordUpToDate(item.FamilyName);
                    continue;
                }

                // Each family in its own transaction — one failure cannot abort the entire batch.
                using (var tx = new Transaction(doc, $"Load Family: {item.FamilyName}"))
                {
                    try
                    {
                        tx.Start();
                        bool success = doc.LoadFamily(item.FilePath, loadOptions, out Family loadedFamily);

                        if (success && loadedFamily != null)
                        {
                            // Explicit success: API confirmed new load.
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
                            tx.Commit();
                        }
                        else
                        {
                            // doc.LoadFamily() returned false — two distinct cases:
                            //   (A) Family already loaded and identical  → UpToDate
                            //   (B) Silent load failure                  → LoadFailed
                            // Disambiguate by verifying the family is actually in the document.
                            bool actuallyInDocument = IsFamilyInDocument(doc, item.FamilyName);
                            if (actuallyInDocument)
                            {
                                item.IsLoadedInProject = true;
                                item.Status = FamilyItemStatus.UpToDate;
                                result.RecordUpToDate(item.FamilyName);
                                tx.RollBack(); // Nothing was modified.
                            }
                            else
                            {
                                // Family is NOT in the document: genuine silent failure.
                                item.IsLoadedInProject = false;
                                item.Status = FamilyItemStatus.LoadFailed;
                                item.ErrorMessage = "LoadFamily() returned false and family is not present in document.";
                                result.RecordFailure(item.FamilyName, item.ErrorMessage);
                                tx.RollBack();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (tx.GetStatus() == TransactionStatus.Started)
                        {
                            try { tx.RollBack(); } catch { }
                        }
                        item.Status = FamilyItemStatus.LoadFailed;
                        item.ErrorMessage = ex.Message;
                        result.RecordFailure(item.FamilyName, ex.Message);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Verifies that a family with the given name is actually present in the Revit document.
        /// Used to disambiguate a silent false-returning doc.LoadFamily() call.
        /// </summary>
        private static bool IsFamilyInDocument(Document doc, string familyName)
        {
            try
            {
                return new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .Any(f => string.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
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
