using System;
using System.Runtime.CompilerServices;
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
        public string SessionId { get; }
        private static readonly ConditionalWeakTable<Document, SessionToken> SessionTokens =
            new ConditionalWeakTable<Document, SessionToken>();

        private DocumentIdentity(string path, string title, string version, bool isWorkshared, string sessionId)
        {
            Path = path ?? string.Empty;
            Title = title ?? string.Empty;
            Version = version ?? string.Empty;
            IsWorkshared = isWorkshared;
            SessionId = sessionId ?? string.Empty;
        }

        public static DocumentIdentity From(Document document)
        {
            if (document == null)
            {
                return new DocumentIdentity(string.Empty, string.Empty, string.Empty, false, string.Empty);
            }

            return new DocumentIdentity(
                document.PathName,
                document.Title,
                document.Application == null ? string.Empty : document.Application.VersionNumber,
                document.IsWorkshared,
                SessionTokens.GetValue(document, _ => new SessionToken()).Value);
        }

        public string StableKey => WorkflowFingerprint.Compute(
            Path, Title, Version, IsWorkshared ? "workshared" : "standalone", SessionId);

        private sealed class SessionToken
        {
            public string Value { get; } = Guid.NewGuid().ToString("N");
        }
    }
}
