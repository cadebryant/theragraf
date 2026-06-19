namespace Theragraf.Core.Models;

/// <summary>
/// A single client entry in a therapist's caseload overview.
/// Returned as part of <see cref="CaseloadSummary"/>.
/// </summary>
public record ClientSummary(
    string         ClientId,
    string?        LastSessionDate,
    int            TotalSessions,
    bool           IsSynthetic
);

/// <summary>
/// Aggregated caseload overview for an authenticated therapist.
/// Returned by GET /api/sessions — the therapist identity is resolved from the JWT.
/// </summary>
public record CaseloadSummary(
    string                      TherapistName,
    IReadOnlyList<ClientSummary> Clients
);
