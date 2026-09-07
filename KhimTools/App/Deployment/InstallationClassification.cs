namespace KhiemToolsApp.Deployment
{
    /// <summary>
    /// Classification of an existing K-TOOLS or addin installation artifact.
    /// Used to determine safe migration, retention, or cleanup policies.
    /// </summary>
    public enum InstallationClassification
    {
        /// <summary>
        /// Authoritative active installation managed by the installer (%ProgramData%\Autodesk\ApplicationPlugins\KhimTools.bundle).
        /// </summary>
        Current,

        /// <summary>
        /// Verified legacy K-TOOLS installation artifact from prior versions (e.g. KhimTools.addin pointing to legacy paths).
        /// Eligible for safe migration/cleanup ONLY AFTER backup is secured.
        /// </summary>
        Legacy,

        /// <summary>
        /// Installation in user space (%AppData%) or customized location that contains user modifications,
        /// developer builds, or does not match authoritative installer signature.
        /// Must NEVER be deleted blindly.
        /// </summary>
        UserManaged,

        /// <summary>
        /// Authoritative active installation managed by Windows Installer (MSI).
        /// Direct file modifications are forbidden; updates must be performed via MSI MajorUpgrade.
        /// </summary>
        MsiManaged,

        /// <summary>
        /// Third-party or unrecognized addin/bundle. Must NEVER be modified or removed.
        /// </summary>
        Unknown
    }
}
