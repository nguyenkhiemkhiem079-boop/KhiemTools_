using System;
using System.Collections.Generic;
using System.Linq;

namespace KhimTools.SlabJoin.Models
{
    /// <summary>
    /// Aggregated result of running a Join Slabs or Unjoin Slabs command end-to-end.
    /// Consumed by the logging service to produce a run summary, and by the
    /// command to build the completion dialog.
    /// </summary>
    public sealed class OperationSummary
    {
        public OperationType OperationType { get; set; }

        /// <summary>
        /// Total number of structural floors that passed all eligibility filters.
        /// </summary>
        public int TotalStructuralFloorsScanned { get; set; }

        /// <summary>
        /// Number of candidate pairs discovered via spatial filtering.
        /// </summary>
        public int CandidatePairsFound { get; set; }

        /// <summary>
        /// Elements excluded before pairing (types, groups, demolished, linked, non-primary design option, non-structural).
        /// </summary>
        public List<SkippedElementInfo> SkippedElements { get; } = new List<SkippedElementInfo>();

        /// <summary>
        /// Per-pair results of the join/unjoin attempts.
        /// </summary>
        public List<JoinPairResult> ProcessedPairs { get; } = new List<JoinPairResult>();

        /// <summary>
        /// Unhandled/unexpected error messages captured during the run.
        /// </summary>
        public List<string> Errors { get; } = new List<string>();

        /// <summary>
        /// Total wall-clock time for the command execution.
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        /// <summary>
        /// Number of pairs where geometry state was actually changed.
        /// </summary>
        public int SuccessCount => ProcessedPairs.Count(p => p.Success && !p.IsError);

        /// <summary>
        /// Number of pairs that were already in the desired state and required no action.
        /// </summary>
        public int AlreadyInDesiredStateCount => ProcessedPairs.Count(p => !p.Success && !p.IsError);

        /// <summary>
        /// Number of pairs that threw an exception during the operation.
        /// </summary>
        public int ErrorCount => ProcessedPairs.Count(p => p.IsError);
    }
}
