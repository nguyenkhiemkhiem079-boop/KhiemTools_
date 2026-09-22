using System;
using System.Collections.Generic;
using System.Linq;
using KhimTools.FilterManager.Models;

namespace KhimTools.FilterManager.Services
{
    public static class FilterComparisonService
    {
        public static string StateFingerprint(AppliedFilterState state)
        {
            if (state == null) return "<null>";
            return string.Join("|", state.FilterId, state.UniqueId, state.OrderIndex, state.IsApplied, state.Visible, state.Enabled, state.EnabledStateSupported, state.Overrides == null ? string.Empty : state.Overrides.Fingerprint);
        }

        public static string PlanFingerprint(IEnumerable<AppliedFilterState> source, FilterSyncOptions options)
        {
            return string.Join("||", (source ?? Enumerable.Empty<AppliedFilterState>()).Select(StateFingerprint)) + "|" + options.Mode + ":" + options.CopyVisibility + ":" + options.CopyEnabled + ":" + options.CopyGraphicOverrides + ":" + options.CopyOrder + ":" + options.AddMissingFilters + ":" + options.ClearOverrides;
        }

        public static FilterManagerStatusCode Classify(AppliedFilterState source, AppliedFilterState target, FilterSyncOptions options)
        {
            if (source == null) return FilterManagerStatusCode.UNSUPPORTED_FILTER_TYPE;
            if (target == null || !target.IsApplied) return FilterManagerStatusCode.ADD_FILTER_REQUIRED;
            if (options.CopyVisibility && source.Visible != target.Visible) return FilterManagerStatusCode.VISIBILITY_DIFF;
            if (options.CopyEnabled && source.EnabledStateSupported && target.EnabledStateSupported && source.Enabled != target.Enabled) return FilterManagerStatusCode.ENABLED_DIFF;
            if (options.CopyEnabled && source.EnabledStateSupported != target.EnabledStateSupported) return FilterManagerStatusCode.FILTER_ENABLED_STATE_UNSUPPORTED;
            if (options.CopyGraphicOverrides && !GraphicOverrideSnapshotService.Equivalent(source.Overrides, target.Overrides)) return FilterManagerStatusCode.OVERRIDE_DIFF;
            if (options.CopyOrder && source.OrderIndex != target.OrderIndex) return FilterManagerStatusCode.ORDER_DIFF;
            return FilterManagerStatusCode.NO_CHANGE;
        }

        public static bool IsSameDefinition(AppliedFilterState left, AppliedFilterState right)
        {
            return left != null && right != null && left.FilterId == right.FilterId && string.Equals(left.UniqueId, right.UniqueId, StringComparison.Ordinal);
        }
    }
}
