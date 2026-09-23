using Autodesk.Revit.DB;

namespace KhimTools.Core.Workflow
{
    /// <summary>Stable document identity used by preflight and verification.</summary>
    public sealed class DocumentIdentity
    {
        public string Path { get; }
        public string Title { get; }
        public string Version { get; }
        public bool IsWorkshared { get; }

        private DocumentIdentity(string path, string title, string version, bool isWorkshared)
        {
            Path = path ?? string.Empty;
            Title = title ?? string.Empty;
            Version = version ?? string.Empty;
            IsWorkshared = isWorkshared;
        }

        public static DocumentIdentity From(Document document)
        {
            if (document == null)
            {
                return new DocumentIdentity(string.Empty, string.Empty, string.Empty, false);
            }

            return new DocumentIdentity(
                document.PathName,
                document.Title,
                document.Application == null ? string.Empty : document.Application.VersionNumber,
                document.IsWorkshared);
        }

        public string StableKey => string.Join("|", Path, Title, Version, IsWorkshared ? "workshared" : "standalone");
    }
}
