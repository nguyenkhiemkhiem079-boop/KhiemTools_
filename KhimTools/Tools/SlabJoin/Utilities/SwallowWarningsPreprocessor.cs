using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace KhimTools.SlabJoin.Utilities
{
    /// <summary>
    /// Swallows/suppresses all non-fatal geometry warnings during Transaction commit,
    /// preventing Revit from showing warning popups or freezing/crashing on minor
    /// geometry overlaps.
    /// </summary>
    public class SwallowWarningsPreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            IList<FailureMessageAccessor> failMessages = failuresAccessor.GetFailureMessages();
            if (failMessages == null || failMessages.Count == 0)
            {
                return FailureProcessingResult.Continue;
            }

            foreach (FailureMessageAccessor failMessage in failMessages)
            {
                FailureSeverity severity = failMessage.GetSeverity();
                if (severity == FailureSeverity.Warning)
                {
                    // Suppress warning without user interaction to prevent freezes
                    failuresAccessor.DeleteWarning(failMessage);
                }
            }

            return FailureProcessingResult.Continue;
        }
    }
}
