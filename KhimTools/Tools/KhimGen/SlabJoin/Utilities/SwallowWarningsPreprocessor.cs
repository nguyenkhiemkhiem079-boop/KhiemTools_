using Autodesk.Revit.DB;
using KhimTools.Core.Revit.Failures;

namespace KhimTools.SlabJoin.Utilities
{
    /// <summary>
    /// Compatibility name retained for existing SlabJoin call sites. The implementation is
    /// intentionally no longer a generic swallow: only an explicitly supplied allow-list may
    /// delete warnings and errors remain rollback-capable.
    /// </summary>
    [System.Obsolete("Use KnownWarningFailurePreprocessor with an explicit warning allow-list.")]
    public class SwallowWarningsPreprocessor : IFailuresPreprocessor
    {
        private readonly KnownWarningFailurePreprocessor _policy;

        public SwallowWarningsPreprocessor()
        {
            _policy = new KnownWarningFailurePreprocessor();
        }

        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            return _policy.PreprocessFailures(failuresAccessor);
        }
    }
}
