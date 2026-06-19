namespace Theragraf.Core.Models;

/// <summary>
/// Typed API response returned by the read and write endpoints.
/// PII placeholders are resolved back to their original values before this record
/// is constructed, so callers always receive human-readable text.
/// </summary>
public record SessionResponse(
    string ClientId,
    string SessionDate,
    string TherapistName,
    string Discipline,
    string NoteFormat,
    string Setting,
    string Payer,
    int? SessionDurationMinutes,
    SoapNote SoapNote,
    IReadOnlyList<CptCode> SuggestedCptCodes,
    IReadOnlyList<IcdCode> SuggestedIcdCodes,
    DateTimeOffset CreatedAt,
    bool IsApproved,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    bool IsSynthetic
);
