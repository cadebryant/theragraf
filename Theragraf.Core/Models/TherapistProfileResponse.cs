namespace Theragraf.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// API response DTO for <c>GET /api/therapists/me</c>.
/// Omits encrypted/sensitive fields (encryptedTaxId) and internal Cosmos fields.
/// </summary>
public class TherapistProfileResponse
{
    [JsonPropertyName("therapistId")]
    public string TherapistId { get; set; } = string.Empty;

    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>Professional credentials suffix, e.g. "OTR/L", "PT, DPT", "CCC-SLP".</summary>
    [JsonPropertyName("credentials")]
    public string? Credentials { get; set; }

    [JsonPropertyName("discipline")]
    public TherapyDiscipline Discipline { get; set; }

    /// <summary>Individual 10-digit NPI (Type 1). Null until configured by the therapist.</summary>
    [JsonPropertyName("individualNpi")]
    public string? IndividualNpi { get; set; }

    /// <summary>
    /// FK to <see cref="ProviderResponse.ProviderId"/> when this therapist belongs to a group
    /// practice. Null for solo practitioners.
    /// </summary>
    [JsonPropertyName("providerId")]
    public string? ProviderId { get; set; }

    /// <summary>
    /// True when the profile was auto-created from JWT claims and has not yet been
    /// explicitly saved by the therapist. Prompts the frontend to show a profile setup flow.
    /// </summary>
    [JsonPropertyName("isConfigured")]
    public bool IsConfigured { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Maps a <see cref="TherapistProfileDocument"/> to this response DTO.</summary>
    public static TherapistProfileResponse FromDocument(TherapistProfileDocument doc, bool isConfigured = true) => new()
    {
        TherapistId  = doc.TherapistId,
        TenantId     = doc.TenantId,
        FirstName    = doc.FirstName,
        LastName     = doc.LastName,
        Credentials  = doc.Credentials,
        Discipline   = doc.Discipline,
        IndividualNpi = doc.IndividualNpi,
        ProviderId   = doc.ProviderId,
        IsConfigured = isConfigured,
        CreatedAt    = doc.CreatedAt,
        UpdatedAt    = doc.UpdatedAt,
    };
}
