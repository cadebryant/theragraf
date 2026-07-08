namespace Theragraf.Core.Models;

/// <summary>
/// Full ePHI export for a single client, returned by
/// <c>GET /api/clients/{clientId}/export</c>.
///
/// Satisfies the HIPAA §164.524 right-of-access requirement: covered entities
/// (therapy practices using Theragraf) must be able to produce all records held
/// about a patient on request.  This response bundles every record type
/// (demographics, session notes, treatment goals) into one document.
///
/// IMPORTANT: this response contains fully restored ePHI (PII placeholders are
/// resolved before the session records are returned).  It must be transmitted
/// over HTTPS only and must not be written to any log channel.
/// </summary>
public record ClientExportResponse(
    /// <summary>Namespaced client identifier.</summary>
    string ClientId,

    /// <summary>UTC timestamp when this export was generated.</summary>
    DateTimeOffset ExportedAt,

    /// <summary>Identity (email/UPN) of the therapist who requested the export.</summary>
    string ExportedBy,

    /// <summary>
    /// Demographic / intake record for this client.
    /// Null when no intake record has been created yet.
    /// </summary>
    ClientDemographicsResponse? Demographics,

    /// <summary>
    /// All active session notes for this client, ordered by session date descending.
    /// PII placeholders are resolved — values are human-readable.
    /// </summary>
    IReadOnlyList<SessionResponse> Sessions,

    /// <summary>All treatment goals for this client, newest first.</summary>
    IReadOnlyList<GoalResponse> Goals
);
