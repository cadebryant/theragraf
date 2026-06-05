namespace Theragraf.Core.Models;

/// <summary>
/// Typed API response returned by the read endpoints.
/// All PII fields remain redacted (placeholders) — this is the stored form.
/// </summary>
public record SessionResponse(
    string ClientId,
    string SessionDate,
    string TherapistName,
    string Discipline,
    string Setting,
    string Payer,
    int? SessionDurationMinutes,
    SoapNote SoapNote,
    IReadOnlyList<CptCode> SuggestedCptCodes,
    IReadOnlyList<IcdCode> SuggestedIcdCodes,
    DateTimeOffset CreatedAt
);
