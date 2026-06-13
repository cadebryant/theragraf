namespace Theragraf.Functions.Logging;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;

/// <summary>
/// Writes structured HIPAA audit events to a persistent, queryable store.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Records a PHI-touching event.  Fire-and-forget safe — implementations
    /// must not throw; failures are swallowed and emitted as warnings on the
    /// standard <see cref="ILogger"/> channel instead.
    /// </summary>
    void Log(AuditEvent auditEvent);
}

/// <summary>
/// Writes audit events to Application Insights as <see cref="TraceTelemetry"/>
/// items with <c>SeverityLevel.Information</c> and a custom property
/// <c>audit = true</c> so they can be isolated in Kusto:
///
/// <code>
/// traces
/// | where customDimensions["audit"] == "true"
/// | project timestamp, actor=customDimensions["actor"],
///           action=customDimensions["action"],
///           resourceType=customDimensions["resourceType"],
///           resourceId=customDimensions["resourceId"],
///           outcome=customDimensions["outcome"],
///           correlationId=customDimensions["correlationId"],
///           detail=customDimensions["detail"]
/// | order by timestamp desc
/// </code>
///
/// Audit events are excluded from adaptive sampling in <c>host.json</c>
/// so none are dropped.
/// </summary>
public sealed class ApplicationInsightsAuditLogger(
    TelemetryClient telemetryClient,
    ILogger<ApplicationInsightsAuditLogger> logger) : IAuditLogger
{
    public void Log(AuditEvent auditEvent)
    {
        try
        {
            var telemetry = new TraceTelemetry(
                $"AUDIT {auditEvent.Action} {auditEvent.ResourceType} by {auditEvent.Actor} — {auditEvent.Outcome}",
                SeverityLevel.Information);

            telemetry.Timestamp = auditEvent.Timestamp;

            // Structured properties — queryable as customDimensions in Kusto.
            telemetry.Properties["audit"]         = "true";
            telemetry.Properties["actor"]         = auditEvent.Actor;
            telemetry.Properties["action"]        = auditEvent.Action.ToString();
            telemetry.Properties["resourceType"]  = auditEvent.ResourceType;
            telemetry.Properties["resourceId"]    = auditEvent.ResourceId ?? string.Empty;
            telemetry.Properties["outcome"]       = auditEvent.Outcome;
            telemetry.Properties["correlationId"] = auditEvent.CorrelationId ?? string.Empty;
            telemetry.Properties["detail"]        = auditEvent.Detail ?? string.Empty;

            telemetryClient.TrackTrace(telemetry);
        }
        catch (Exception ex)
        {
            // Audit logging must never crash the request pipeline.
            // Emit a warning on the standard channel so the failure is visible.
            logger.LogWarning(ex, "Audit log write failed for action={Action} actor={Actor}",
                auditEvent.Action, auditEvent.Actor);
        }
    }
}
