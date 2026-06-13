namespace Theragraf.Functions.Logging;

/// <summary>
/// No-op audit logger for use in unit tests and local dev scenarios where
/// Application Insights is not configured.
/// </summary>
public sealed class NullAuditLogger : IAuditLogger
{
    /// <inheritdoc />
    public void Log(AuditEvent auditEvent) { }
}
