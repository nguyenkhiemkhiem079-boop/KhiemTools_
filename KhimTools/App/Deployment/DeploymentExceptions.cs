using System;

namespace KhiemToolsApp.Deployment
{
    /// <summary>
    /// Base class for all deployment-related errors.
    /// </summary>
    public class DeploymentException : Exception
    {
        public DeploymentException(string message) : base(message) { }
        public DeploymentException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Thrown when pre-install staging validation or post-install bundle verification fails.
    /// Triggers automatic rollback if post-install.
    /// </summary>
    public class DeploymentValidationException : DeploymentException
    {
        public string ExpectedVersion { get; private set; }
        public string ActualVersion { get; private set; }

        public DeploymentValidationException(string message) : base(message) { }

        public DeploymentValidationException(string message, string expectedVersion, string actualVersion)
            : base(message)
        {
            ExpectedVersion = expectedVersion;
            ActualVersion = actualVersion;
        }

        public DeploymentValidationException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    /// <summary>
    /// Thrown when a URL, manifest, or package violates security policy (e.g. non-HTTPS, untrusted domain).
    /// </summary>
    public class DeploymentSecurityException : DeploymentException
    {
        public DeploymentSecurityException(string message) : base(message) { }
        public DeploymentSecurityException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Thrown when target files or directories are locked by Revit or another process.
    /// </summary>
    public class DeploymentLockException : DeploymentException
    {
        public string LockedPath { get; private set; }

        public DeploymentLockException(string message, string lockedPath) : base(message)
        {
            LockedPath = lockedPath;
        }

        public DeploymentLockException(string message, Exception innerException, string lockedPath)
            : base(message, innerException)
        {
            LockedPath = lockedPath;
        }
    }

    /// <summary>
    /// Thrown when backup of the current installation fails before modification.
    /// </summary>
    public class BackupFailedException : DeploymentException
    {
        public BackupFailedException(string message) : base(message) { }
        public BackupFailedException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Critical exception thrown when rollback of an installation fails.
    /// Requires immediate forensic logging and user alerting; must NEVER be swallowed.
    /// </summary>
    public class RollbackFailedException : DeploymentException
    {
        public string BackupPath { get; private set; }
        public string TargetPath { get; private set; }

        public RollbackFailedException(string message, string backupPath, string targetPath, Exception innerException)
            : base(message, innerException)
        {
            BackupPath = backupPath;
            TargetPath = targetPath;
        }
    }

    /// <summary>
    /// Thrown when a direct file-copy deployment is attempted against an MSI-managed installation,
    /// which would corrupt Windows Installer component registration state.
    /// </summary>
    public class MsiManagedDeploymentException : DeploymentException
    {
        public MsiManagedDeploymentException(string message) : base(message) { }
        public MsiManagedDeploymentException(string message, Exception innerException) : base(message, innerException) { }
    }
}
