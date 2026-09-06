using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using KhimTools.FamilyManager.Models;
using KhimTools.FamilyManager.Services;
using KhimTools.FamilyManager.Views;

namespace KhimTools.QuickDraft.Services
{
    /// <summary>
    /// Coordinates quick draft workflows, missing family resolution prompts,
    /// symbol activation, and native interactive placement.
    /// </summary>
    public class QuickDraftService
    {
        public static Result PlaceLoadableElement(UIDocument uidoc, BuiltInCategory category, string preferredFamilyName, string elementName)
        {
            if (uidoc == null) return Result.Failed;
            var doc = uidoc.Document;

            // 1. Try to find the symbol in the active project
            FamilySymbol targetSymbol = FindSymbol(doc, category, preferredFamilyName);

            // 2. If missing, prompt user to load
            if (targetSymbol == null)
            {
                var prompt = new QuickLoadPromptDialog(preferredFamilyName, isRebar: false);
                bool? dialogResult = prompt.ShowDialog();

                if (dialogResult != true || prompt.UserAction == QuickLoadPromptAction.Cancel)
                {
                    return Result.Cancelled;
                }

                if (prompt.UserAction == QuickLoadPromptAction.OpenFamilyManager)
                {
                    var managerWin = new FamilyManagerWindow(doc);
                    managerWin.ShowDialog();

                    // Re-check after closing Family Manager: reload settings in case preferred family was changed
                    var settings = FamilyManagerSettings.Load();
                    if (settings.PreferredFamilies != null && settings.PreferredFamilies.TryGetValue(elementName, out var updatedPref) && !string.IsNullOrWhiteSpace(updatedPref))
                    {
                        preferredFamilyName = updatedPref;
                    }
                    targetSymbol = FindSymbol(doc, category, preferredFamilyName);
                }
                else if (prompt.UserAction == QuickLoadPromptAction.LoadSingleFamily)
                {
                    // Find the file path on disk
                    string familyPath = FindFamilyFileOnDisk(preferredFamilyName);
                    if (!string.IsNullOrEmpty(familyPath) && File.Exists(familyPath))
                    {
                        var loadRes = FamilyLoaderService.LoadSingleFamily(doc, familyPath);
                        if (loadRes.Success)
                        {
                            targetSymbol = FindSymbol(doc, category, preferredFamilyName);
                        }
                        else
                        {
                            TaskDialog.Show("K-TOOLS — Quick Structure", $"Failed to load family '{preferredFamilyName}':\n{loadRes.Failures.FirstOrDefault().Value}");
                            return Result.Failed;
                        }
                    }
                    else
                    {
                        TaskDialog.Show("K-TOOLS — Quick Structure", $"Family file for '{preferredFamilyName}' was not found in library folders.");
                        return Result.Failed;
                    }
                }
            }

            // ZERO SILENT FALLBACK POLICY:
            // If the exact preferred family could not be found or loaded after all user-facing options
            // were exhausted, we MUST NOT substitute any other family. Report the failure explicitly.
            if (targetSymbol == null)
            {
                TaskDialog.Show(
                    "K-TOOLS — Quick Structure",
                    $"The required family '{preferredFamilyName}' is not available in the active project.\n\n" +
                    $"Please use Family Manager to load the correct '{preferredFamilyName}' family, then try again.\n\n" +
                    "Do NOT substitute another family — the exact structural type is required.");
                return Result.Failed;
            }

            // 3. Ensure symbol is active
            if (!targetSymbol.IsActive)
            {
                using (var tx = new Transaction(doc, $"Activate {elementName} Symbol"))
                {
                    tx.Start();
                    targetSymbol.Activate();
                    tx.Commit();
                }
            }

            // 4. Prompt user for native Revit interactive placement
            try
            {
                uidoc.PromptForFamilyInstancePlacement(targetSymbol);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User pressed Escape to end placement — normal Revit behavior, not a failure
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("K-TOOLS — Quick Structure", $"Placement error:\n{ex.Message}");
                return Result.Failed;
            }
        }

        public static FamilySymbol FindSymbol(Document doc, BuiltInCategory category, string familyName)
        {
            if (doc == null) return null;

            var collector = new FilteredElementCollector(doc)
                .OfCategory(category)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>();

            if (!string.IsNullOrWhiteSpace(familyName))
            {
                var matching = collector.FirstOrDefault(s => s.Family != null && string.Equals(s.Family.Name, familyName, StringComparison.OrdinalIgnoreCase));
                if (matching != null) return matching;
            }

            return null;
        }

        public static string FindFamilyFileOnDisk(string familyName)
        {
            var groups = FamilyDiscoveryService.DiscoverGroups();
            var item = groups.SelectMany(g => g.Families)
                             .FirstOrDefault(f => string.Equals(f.FamilyName, familyName, StringComparison.OrdinalIgnoreCase));

            return item?.FilePath;
        }

        /// <summary>
        /// Validates that Rebar Shapes are loaded in the project before running rebar commands.
        /// If missing, prompts the user to load the complete Rebar library at once.
        /// </summary>
        public static bool EnsureRebarLibraryLoaded(UIDocument uidoc)
        {
            if (uidoc == null) return false;
            var doc = uidoc.Document;

            bool anyShapes = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarShape))
                .Any();

            if (anyShapes) return true;

            var prompt = new QuickLoadPromptDialog("Rebar Shapes", isRebar: true);
            bool? dialogResult = prompt.ShowDialog();

            if (dialogResult != true || prompt.UserAction == QuickLoadPromptAction.Cancel)
            {
                return false;
            }

            if (prompt.UserAction == QuickLoadPromptAction.OpenFamilyManager)
            {
                var managerWin = new FamilyManagerWindow(doc);
                managerWin.ShowDialog();
                return new FilteredElementCollector(doc).OfClass(typeof(RebarShape)).Any();
            }

            if (prompt.UserAction == QuickLoadPromptAction.LoadAllRebar)
            {
                var loadRes = FamilyLoaderService.LoadAllRebarShapes(doc);
                if (loadRes.Success)
                {
                    TaskDialog.Show("KhimTools Rebar Library", $"Successfully loaded {loadRes.LoadedCount} rebar shapes into the project.");
                    return true;
                }
                else
                {
                    TaskDialog.Show("KhimTools Rebar Library", $"Rebar library load encountered issues:\n{loadRes.Failures.FirstOrDefault().Value}");
                    return false;
                }
            }

            return false;
        }
    }
}
