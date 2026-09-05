using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.FamilyManager.Models;

namespace KhimTools.FamilyManager.Services
{
    /// <summary>
    /// Inspects the active Revit Document to determine which families and rebar shapes are already loaded,
    /// and updates FamilyItemModel status accordingly.
    /// </summary>
    public class FamilyStatusService
    {
        /// <summary>
        /// Scans the document and updates the status of all families across all groups.
        /// </summary>
        public static void UpdateStatuses(Document doc, IEnumerable<FamilyGroupModel> groups)
        {
            if (doc == null || groups == null) return;

            // Collect all loaded families by name
            var loadedFamilies = new Dictionary<string, Family>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var families = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>();

                foreach (var f in families)
                {
                    if (!string.IsNullOrEmpty(f.Name))
                    {
                        loadedFamilies[f.Name] = f;
                    }
                }
            }
            catch
            {
                // Safely handle document access exceptions
            }

            // Also collect loaded RebarShapes (for rebar library matching)
            var loadedRebarShapes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var shapes = new FilteredElementCollector(doc)
                    .OfClass(typeof(RebarShape))
                    .Cast<RebarShape>();

                foreach (var s in shapes)
                {
                    if (!string.IsNullOrEmpty(s.Name))
                    {
                        loadedRebarShapes.Add(s.Name);
                    }
                }
            }
            catch
            {
                // Safely handle shape access exceptions
            }

            foreach (var group in groups)
            {
                foreach (var item in group.Families)
                {
                    bool isLoaded = loadedFamilies.ContainsKey(item.FamilyName);

                    // If it's a rebar shape, also check the RebarShape collector
                    if (!isLoaded && group.GroupType == FamilyGroupType.Rebar)
                    {
                        isLoaded = loadedRebarShapes.Contains(item.FamilyName);
                    }

                    if (isLoaded)
                    {
                        item.IsLoadedInProject = true;
                        item.Status = FamilyItemStatus.Loaded;

                        if (loadedFamilies.TryGetValue(item.FamilyName, out var fam))
                        {
                            try
                            {
                                var symbolIds = fam.GetFamilySymbolIds();
                                item.LoadedTypeCount = symbolIds != null ? symbolIds.Count : 0;
                            }
                            catch
                            {
                                item.LoadedTypeCount = 1;
                            }
                        }
                        else
                        {
                            item.LoadedTypeCount = 1;
                        }
                    }
                    else
                    {
                        item.IsLoadedInProject = false;
                        item.Status = FamilyItemStatus.NotLoaded;
                        item.LoadedTypeCount = 0;
                    }
                }

                group.UpdateSummary();
            }
        }

        /// <summary>
        /// Checks if a single family or rebar shape is currently loaded in the document.
        /// </summary>
        public static bool IsFamilyLoaded(Document doc, string familyName)
        {
            if (doc == null || string.IsNullOrWhiteSpace(familyName)) return false;

            try
            {
                bool inFamilies = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .Any(f => string.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase));

                if (inFamilies) return true;

                bool inRebarShapes = new FilteredElementCollector(doc)
                    .OfClass(typeof(RebarShape))
                    .Cast<RebarShape>()
                    .Any(s => string.Equals(s.Name, familyName, StringComparison.OrdinalIgnoreCase));

                return inRebarShapes;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Finds a loaded family by name.
        /// </summary>
        public static Family FindLoadedFamily(Document doc, string familyName)
        {
            if (doc == null || string.IsNullOrWhiteSpace(familyName)) return null;

            try
            {
                return new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .FirstOrDefault(f => string.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Finds the first active FamilySymbol for a given family in the document.
        /// </summary>
        public static FamilySymbol FindFirstSymbol(Document doc, string familyName)
        {
            var family = FindLoadedFamily(doc, familyName);
            if (family == null) return null;

            var symbolIds = family.GetFamilySymbolIds();
            if (symbolIds == null || symbolIds.Count == 0) return null;

            return doc.GetElement(symbolIds.First()) as FamilySymbol;
        }
    }
}
