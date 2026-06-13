namespace Theragraf.Functions.Logging;

/// <summary>
/// Categorises every operation that touches PHI.
/// Used as the <c>action</c> field in <see cref="AuditEvent"/>.
/// </summary>
public enum AuditAction
{
    /// <summary>A session record or caseload summary was read.</summary>
    Read,

    /// <summary>A session record was created or updated.</summary>
    Write,

    /// <summary>A session record was deleted.</summary>
    Delete,

    /// <summary>An Azure Speech authorisation token was issued to a caller.</summary>
    SpeechTokenIssued,

    /// <summary>
    /// An operation was attempted but denied due to failed ownership or auth checks.
    /// </summary>
    AccessDenied,
}

/// <summary>
/// Immutable structured record of a single PHI-touching operation.
///
/// HIPAA §164.312(b) requires audit controls that record and examine
/// activity in systems containing ePHI.  Every instance of this type
/// is written to Application Insights as a custom <c>TraceTelemetry</c>
/// event so it is queryable via Kusto in Log Analytics.
///
/// IMPORTANT: No PHI content is ever stored here — only resource
/// identifiers (clientId, sessionDate) and operational metadata.
/// </summary>
public record AuditEvent
{
    /// <summary>UTC timestamp of the event (set automatically).</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Identity of the actor — the therapist's email / UPN from the JWT,
    /// or <c>"system"</c> for background activity (e.g. PersistActivity).
    /// </summary>
    public required string Actor { get; init; }

    /// <summary>The PHI operation that was performed or attempted.</summary>
    public required AuditAction Action { get; init; }

    /// <summary>
    /// High-level resource type, e.g. <c>"Session"</c>, <c>"Caseload"</c>,
    /// <c>"SpeechToken"</c>.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    /// Stable identifier for the affected resource — typically
    /// <c>"{clientId}/{sessionDate}"</c> or just <c>"{clientId}"</c>.
    /// Never contains PHI content.
    /// </summary>
    public string? ResourceId { get; init; }

    /// <summary>
    /// <c>"Success"</c> when the operation completed normally,
    /// <c>"Failure"</c> when an exception or auth denial occurred.
    /// </summary>
    public required string Outcome { get; init; }

    /// <summary>
    /// Azure Functions invocation ID — correlates this audit event with the
    /// function's trace logs in Application Insights.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>Optional human-readable detail, e.g. "Ownership check failed".</summary>
    public string? Detail { get; init; }

    // ── Factory helpers ───────────────────────────────────────────────────────

    public static AuditEvent Success(
        string actor,
        AuditAction action,
        string resourceType,
        string? resourceId = null,
        string? correlationId = null,
        string? detail = null) =>
        new()
        {
            Actor        = actor,
            Action       = action,
            ResourceType = resourceType,
            ResourceId   = resourceId,
            Outcome      = "Success",
            CorrelationId = correlationId,
            Detail       = detail,
        };

    public static AuditEvent Failure(
        string actor,
        AuditAction action,
        string resourceType,
        string? resourceId = null,
        string? correlationId = null,
        string? detail = null) =>
        new()
        {
            Actor        = actor,
            Action       = action,
            ResourceType = resourceType,
            ResourceId   = resourceId,
            Outcome      = "Failure",
            CorrelationId = correlationId,
            Detail       = detail,
        };
}
