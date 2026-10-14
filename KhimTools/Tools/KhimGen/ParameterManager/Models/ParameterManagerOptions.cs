using System;
using System.Globalization;

namespace KhimTools.ParameterManager.Models
{
    public sealed class ParameterManagerOptions
    {
        public bool AllowElementId { get; set; }
        public bool AllowProtectedParameters { get; set; }
        public bool AllOrNothing { get; set; }
        public bool PreserveManualOverrides { get; set; } = true;
        public bool IncludeReadOnlyInPreview { get; set; } = true;
        public bool IncludeTypeImpactWarning { get; set; } = true;
        public bool ConfirmWholeProject { get; set; }
        public bool PrefixOnlyIfMissing { get; set; }
        public bool SuffixOnlyIfMissing { get; set; }
        public StringComparison Comparison { get; set; } = StringComparison.Ordinal;
        public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;
        public Func<Autodesk.Revit.DB.Element, bool> Filter { get; set; }
    }
}
