using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.ParameterTransfer.Models;

namespace KhimTools.ParameterManager.Models
{
    /// <summary>
    /// Immutable, detached copy of the inputs needed after preview.  The live
    /// request may carry a document or a collector predicate; neither crosses
    /// the preview-to-execution boundary.
    /// </summary>
    public sealed class ParameterManagerPlanRequest
    {
        private readonly IReadOnlyList<ElementId> _elementIds;
        private readonly IReadOnlyList<ParameterRule> _rules;

        private ParameterManagerPlanRequest(
            ElementScopeMode scope,
            ParameterScopeMode parameterScope,
            ElementId activeViewId,
            ElementId categoryId,
            IEnumerable<ElementId> elementIds,
            ParameterKey selectedParameterKey,
            IEnumerable<ParameterRule> rules,
            ParameterManagerPlanOptions options,
            bool previewOnly)
        {
            Scope = scope;
            ParameterScope = parameterScope;
            ActiveViewId = activeViewId ?? ElementId.InvalidElementId;
            CategoryId = categoryId ?? ElementId.InvalidElementId;
            _elementIds = (elementIds ?? Enumerable.Empty<ElementId>()).ToList().AsReadOnly();
            SelectedParameterKey = CloneKey(selectedParameterKey);
            _rules = (rules ?? Enumerable.Empty<ParameterRule>()).Where(rule => rule != null).Select(CloneRule).ToList().AsReadOnly();
            Options = options ?? ParameterManagerPlanOptions.Empty;
            PreviewOnly = previewOnly;
        }

        public ElementScopeMode Scope { get; }
        public ParameterScopeMode ParameterScope { get; }
        public ElementId ActiveViewId { get; }
        public ElementId CategoryId { get; }
        public IReadOnlyList<ElementId> ElementIds { get { return _elementIds; } }
        public ParameterKey SelectedParameterKey { get; }
        public IReadOnlyList<ParameterRule> Rules { get { return _rules; } }
        public ParameterManagerPlanOptions Options { get; }
        public bool PreviewOnly { get; }

        public static ParameterManagerPlanRequest From(ParameterManagerRequest request)
        {
            if (request == null)
            {
                return Empty;
            }

            return new ParameterManagerPlanRequest(
                request.Scope,
                request.ParameterScope,
                request.ActiveViewId,
                request.CategoryId,
                request.ElementIds,
                request.SelectedParameterKey,
                request.Rules,
                ParameterManagerPlanOptions.From(request.Options),
                request.PreviewOnly);
        }

        public ParameterManagerPlanRequest Clone()
        {
            return new ParameterManagerPlanRequest(
                Scope,
                ParameterScope,
                ActiveViewId,
                CategoryId,
                ElementIds,
                SelectedParameterKey,
                Rules,
                Options.Clone(),
                PreviewOnly);
        }

        internal static ParameterKey CloneKey(ParameterKey source)
        {
            if (source == null)
            {
                return null;
            }

            return new ParameterKey
            {
                IdentityKind = source.IdentityKind,
                BuiltInParameter = source.BuiltInParameter,
                SharedGuid = source.SharedGuid,
                DefinitionName = source.DefinitionName ?? string.Empty,
                StorageType = source.StorageType,
                DataTypeId = source.DataTypeId ?? string.Empty
            };
        }

        internal static ParameterRule CloneRule(ParameterRule source)
        {
            if (source == null)
            {
                return null;
            }

            return new ParameterRule
            {
                RuleType = source.RuleType,
                Value = source.Value ?? string.Empty,
                Find = source.Find ?? string.Empty,
                Replace = source.Replace ?? string.Empty,
                OnlyIfMissing = source.OnlyIfMissing,
                CaseSensitive = source.CaseSensitive,
                StartIndex = source.StartIndex,
                Length = source.Length,
                NumericStart = source.NumericStart,
                NumericStep = source.NumericStep,
                Padding = source.Padding,
                SortMode = source.SortMode,
                CultureName = source.CultureName ?? string.Empty
            };
        }

        public static ParameterManagerPlanRequest Empty
        {
            get
            {
                return new ParameterManagerPlanRequest(
                    ElementScopeMode.CURRENT_SELECTION,
                    ParameterScopeMode.INSTANCE,
                    ElementId.InvalidElementId,
                    ElementId.InvalidElementId,
                    Enumerable.Empty<ElementId>(),
                    null,
                    Enumerable.Empty<ParameterRule>(),
                    ParameterManagerPlanOptions.Empty,
                    true);
            }
        }
    }

    /// <summary>
    /// Value-only copy of execution options.  In particular, it deliberately
    /// excludes ParameterManagerOptions.Filter because delegates are valid only
    /// while collecting the live preview set.
    /// </summary>
    public sealed class ParameterManagerPlanOptions
    {
        private readonly CultureInfo _culture;

        private ParameterManagerPlanOptions(
            bool allowElementId,
            bool allowProtectedParameters,
            bool allOrNothing,
            bool preserveManualOverrides,
            bool includeReadOnlyInPreview,
            bool includeTypeImpactWarning,
            bool confirmWholeProject,
            bool prefixOnlyIfMissing,
            bool suffixOnlyIfMissing,
            StringComparison comparison,
            CultureInfo culture)
        {
            AllowElementId = allowElementId;
            AllowProtectedParameters = allowProtectedParameters;
            AllOrNothing = allOrNothing;
            PreserveManualOverrides = preserveManualOverrides;
            IncludeReadOnlyInPreview = includeReadOnlyInPreview;
            IncludeTypeImpactWarning = includeTypeImpactWarning;
            ConfirmWholeProject = confirmWholeProject;
            PrefixOnlyIfMissing = prefixOnlyIfMissing;
            SuffixOnlyIfMissing = suffixOnlyIfMissing;
            Comparison = comparison;
            _culture = CopyCulture(culture);
        }

        public bool AllowElementId { get; }
        public bool AllowProtectedParameters { get; }
        public bool AllOrNothing { get; }
        public bool PreserveManualOverrides { get; }
        public bool IncludeReadOnlyInPreview { get; }
        public bool IncludeTypeImpactWarning { get; }
        public bool ConfirmWholeProject { get; }
        public bool PrefixOnlyIfMissing { get; }
        public bool SuffixOnlyIfMissing { get; }
        public StringComparison Comparison { get; }

        public static ParameterManagerPlanOptions From(ParameterManagerOptions options)
        {
            options = options ?? new ParameterManagerOptions();
            return new ParameterManagerPlanOptions(
                options.AllowElementId,
                options.AllowProtectedParameters,
                options.AllOrNothing,
                options.PreserveManualOverrides,
                options.IncludeReadOnlyInPreview,
                options.IncludeTypeImpactWarning,
                options.ConfirmWholeProject,
                options.PrefixOnlyIfMissing,
                options.SuffixOnlyIfMissing,
                options.Comparison,
                options.Culture);
        }

        public ParameterManagerPlanOptions Clone()
        {
            return new ParameterManagerPlanOptions(
                AllowElementId,
                AllowProtectedParameters,
                AllOrNothing,
                PreserveManualOverrides,
                IncludeReadOnlyInPreview,
                IncludeTypeImpactWarning,
                ConfirmWholeProject,
                PrefixOnlyIfMissing,
                SuffixOnlyIfMissing,
                Comparison,
                _culture);
        }

        public ParameterManagerOptions CreateExecutionOptions()
        {
            return new ParameterManagerOptions
            {
                AllowElementId = AllowElementId,
                AllowProtectedParameters = AllowProtectedParameters,
                AllOrNothing = AllOrNothing,
                PreserveManualOverrides = PreserveManualOverrides,
                IncludeReadOnlyInPreview = IncludeReadOnlyInPreview,
                IncludeTypeImpactWarning = IncludeTypeImpactWarning,
                ConfirmWholeProject = ConfirmWholeProject,
                PrefixOnlyIfMissing = PrefixOnlyIfMissing,
                SuffixOnlyIfMissing = SuffixOnlyIfMissing,
                Comparison = Comparison,
                Culture = CopyCulture(_culture)
            };
        }

        public static ParameterManagerPlanOptions Empty
        {
            get { return From(null); }
        }

        private static CultureInfo CopyCulture(CultureInfo source)
        {
            CultureInfo value = source ?? CultureInfo.InvariantCulture;
            try
            {
                return CultureInfo.ReadOnly((CultureInfo)value.Clone());
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }
    }
}
