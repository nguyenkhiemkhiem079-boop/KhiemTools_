using System;

namespace KhimTools.RuntimeQa.Models
{
    /// <summary>Explicit runtime QA state. BLOCKED is never treated as PASS.</summary>
    public enum QaStatus
    {
        PASS,
        FAIL,
        BLOCKED,
        SKIPPED,
        NOT_RUN
    }

    public enum QaSeverity
    {
        INFO,
        WARNING,
        ERROR,
        CRITICAL
    }
}
