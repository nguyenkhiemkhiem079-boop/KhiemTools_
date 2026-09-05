using System.Collections.Generic;

namespace KhimTools.FamilyManager.Models
{
    /// <summary>
    /// Physical source configuration for a logical family library group.
    /// Supports multi-root paths and deterministic priority-based deduplication.
    /// </summary>
    public class FamilyLibrarySource
    {
        /// <summary>Unique identifier for this source (e.g. "default_structure", "user_rebar").</summary>
        public string Id { get; set; } = string.Empty;

        public FamilyGroupType LogicalGroup { get; set; }

        public string DisplayName { get; set; }

        /// <summary>One or more physical root directories to scan for .rfa files.</summary>
        public List<string> RootPaths { get; set; } = new List<string>();

        /// <summary>Whether this source participates in discovery and loading.</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>Higher = wins on name collision between sources.</summary>
        public int Priority { get; set; } = 100;
    }
}
