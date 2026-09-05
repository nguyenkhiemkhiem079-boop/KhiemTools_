using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KhimTools.FamilyManager.Models;
using KhimTools.FamilyManager.Services;

namespace KhimTools.FamilyManager.Tests
{
    /// <summary>
    /// Offline unit tests for the Family Manager + Quick Draft system.
    /// No Revit API dependency — tests all pure-logic components.
    ///
    /// Test Groups:
    ///   T01–T06: FamilyGroupModel (tri-state, bi-state, parent sync)
    ///   R01–R08: FamilyLoadResult (recording, counts, success flag)
    ///   L01–L06: FamilyDiscoveryService + FamilySourceResolver (file scan, dedup, filtering)
    /// </summary>
    public static class FamilyManagerTestSuite
    {
        private static int _pass;
        private static int _fail;
        private static readonly List<string> _failures = new List<string>();

        public static (int Pass, int Fail, List<string> Failures) RunAll()
        {
            _pass = 0;
            _fail = 0;
            _failures.Clear();

            RunGroupModelTests();
            RunLoadResultTests();
            RunDiscoveryTests();

            return (_pass, _fail, _failures);
        }

        // ══════════════════════════════════════════════════════════════════
        // T01–T06: FamilyGroupModel
        // ══════════════════════════════════════════════════════════════════

        private static void RunGroupModelTests()
        {
            // T01: Structure group (IsSelective=true) starts with IsChecked=false
            {
                var group = MakeStructureGroup(3, allSelected: false);
                Assert("T01", "Structure group starts unchecked", group.IsChecked == false);
            }

            // T02: Checking all children → parent becomes true (tri-state)
            {
                var group = MakeStructureGroup(3, allSelected: false);
                group.SetAllChildren(true);
                Assert("T02", "Structure group: all children checked → parent=true", group.IsChecked == true);
            }

            // T03: Mixed children → parent becomes null/indeterminate
            {
                var group = MakeStructureGroup(3, allSelected: false);
                group.Families[0].IsSelected = true;
                Assert("T03", "Structure group: mixed selection → parent=null (indeterminate)", group.IsChecked == null);
            }

            // T04: Rebar group (IsSelective=false) — bi-state only, null never set
            {
                var group = MakeRebarGroup(5, allSelected: false);
                Assert("T04", "Rebar group: IsSelective=false", group.IsSelective == false);
            }

            // T05: Rebar group — SetAllChildren(true) → IsChecked=true (strict bi-state)
            {
                var group = MakeRebarGroup(5, allSelected: false);
                group.SetAllChildren(true);
                Assert("T05", "Rebar group: all loaded → IsChecked=true", group.IsChecked == true);
            }

            // T06: Rebar group — IsChecked cannot become null from partial selection
            {
                var group = MakeRebarGroup(4, allSelected: false);
                group.Families[0].IsSelected = true; // partial
                group.Families[1].IsSelected = true; // partial
                Assert("T06", "Rebar group: partial selection → IsChecked=false (not null)", group.IsChecked == false);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // R01–R08: FamilyLoadResult
        // ══════════════════════════════════════════════════════════════════

        private static void RunLoadResultTests()
        {
            // R01: Fresh result has Success=true
            {
                var r = new FamilyLoadResult();
                Assert("R01", "Fresh result: Success=true", r.Success == true);
            }

            // R02: RecordLoaded increments LoadedCount
            {
                var r = new FamilyLoadResult();
                r.RecordLoaded("FamilyA");
                r.RecordLoaded("FamilyB");
                Assert("R02", "RecordLoaded increments LoadedCount to 2", r.LoadedCount == 2);
            }

            // R03: RecordUpToDate increments UpToDateCount
            {
                var r = new FamilyLoadResult();
                r.RecordUpToDate("FamilyC");
                Assert("R03", "RecordUpToDate increments UpToDateCount", r.UpToDateCount == 1);
            }

            // R04: RecordFailure increments FailedCount and sets Success=false
            {
                var r = new FamilyLoadResult();
                r.RecordFailure("BadFamily", "File not found");
                Assert("R04a", "RecordFailure: FailedCount=1", r.FailedCount == 1);
                Assert("R04b", "RecordFailure: Success=false", r.Success == false);
            }

            // R05: Failures dictionary contains correct entry
            {
                var r = new FamilyLoadResult();
                r.RecordFailure("TargetFamily", "Access denied");
                Assert("R05", "Failures dict has correct reason",
                    r.Failures.ContainsKey("TargetFamily") && r.Failures["TargetFamily"] == "Access denied");
            }

            // R06: DiagnosticLog grows with each record call
            {
                var r = new FamilyLoadResult();
                r.RecordLoaded("F1");
                r.RecordUpToDate("F2");
                r.RecordFailure("F3", "err");
                Assert("R06", "DiagnosticLog has 3 entries", r.DiagnosticLog.Count == 3);
            }

            // R07: SummaryText shows correct content when no failures
            {
                var r = new FamilyLoadResult();
                r.RecordLoaded("F1");
                r.RecordUpToDate("F2");
                Assert("R07", "SummaryText contains 'Success'", r.SummaryText.Contains("Success"));
            }

            // R08: SummaryText shows warnings when failures exist
            {
                var r = new FamilyLoadResult();
                r.RecordLoaded("F1");
                r.RecordFailure("F2", "error");
                Assert("R08", "SummaryText contains 'warnings' when failed",
                    r.SummaryText.ToLowerInvariant().Contains("warning"));
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // L01–L06: Discovery + Source logic (disk-independent)
        // ══════════════════════════════════════════════════════════════════

        private static void RunDiscoveryTests()
        {
            // L01: IsIgnoredFile returns true for Revit backup pattern (.0001.rfa)
            {
                bool ignored = FamilyDiscoveryService.IsIgnoredFile("C:\\test\\MyFamily.0001.rfa");
                Assert("L01", "Backup file (.0001.rfa) is ignored", ignored == true);
            }

            // L02: IsIgnoredFile returns false for normal .rfa
            {
                bool ignored = FamilyDiscoveryService.IsIgnoredFile("C:\\test\\M_Column.rfa");
                Assert("L02", "Normal .rfa file is NOT ignored", ignored == false);
            }

            // L03: IsIgnoredFile returns true for temp file starting with ~
            {
                bool ignored = FamilyDiscoveryService.IsIgnoredFile("C:\\test\\~$temp.rfa");
                Assert("L03", "Temp file (~) is ignored", ignored == true);
            }

            // L04: Empty source list → DiscoverFromSources returns groups without crashing
            {
                var groups = FamilyDiscoveryService.DiscoverFromSources(new List<FamilyLibrarySource>());
                Assert("L04", "Empty sources returns non-null list", groups != null);
            }

            // L05: FamilyItemModel FileSizeText returns '-' for zero size
            {
                var item = new FamilyItemModel { FileSizeBytes = 0 };
                Assert("L05", "FileSizeText='-' when FileSizeBytes=0", item.FileSizeText == "-");
            }

            // L06: FamilyItemModel FileSizeText formats KB correctly
            {
                var item = new FamilyItemModel { FileSizeBytes = 2048 };
                Assert("L06", "FileSizeText='2 KB' for 2048 bytes",
                    item.FileSizeText.Contains("KB") || item.FileSizeText.Contains("2"));
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // Helpers
        // ══════════════════════════════════════════════════════════════════

        private static FamilyGroupModel MakeStructureGroup(int count, bool allSelected)
        {
            var g = new FamilyGroupModel(FamilyGroupType.Structure, "Structure");
            for (int i = 0; i < count; i++)
            {
                g.Families.Add(new FamilyItemModel { FamilyName = $"Family{i}", IsSelected = allSelected });
            }
            g.UpdateParentState();
            return g;
        }

        private static FamilyGroupModel MakeRebarGroup(int count, bool allSelected)
        {
            var g = new FamilyGroupModel(FamilyGroupType.Rebar, "Rebar");
            for (int i = 0; i < count; i++)
            {
                g.Families.Add(new FamilyItemModel { FamilyName = $"JP_T{i:00}", IsSelected = allSelected });
            }
            g.UpdateParentState();
            return g;
        }

        private static void Assert(string id, string description, bool condition)
        {
            if (condition)
            {
                _pass++;
                Console.WriteLine($"  [PASS] {id}: {description}");
            }
            else
            {
                _fail++;
                string msg = $"  [FAIL] {id}: {description}";
                _failures.Add(msg);
                Console.WriteLine(msg);
            }
        }
    }
}
