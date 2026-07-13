namespace Theragraf.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Request body for <c>PATCH /api/therapists/me</c>.
/// Only user-editable profile fields are included. System fields (therapistId, tenantId,
/// providerId, encryptedTaxId) are managed server-side and cannot be set via this endpoint.
/// All fields are optional; omitted fields are left unchanged.
/// </summary>
public class TherapistProfileUpdateRequest
{
    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }

    /// <summary>Professional credentials suffix, e.g. "OTR/L", "PT, DPT", "CCC-SLP".</summary>
    [JsonPropertyName("credentials")]
    public string? Credentials { get; set; }

    [JsonPropertyName("discipline")]
    public TherapyDiscipline? Discipline { get; set; }

    /// <summary>
    /// Individual 10-digit NPI (Type 1). Send null to clear. Must be exactly 10 digits when
    /// provided.
    /// </summary>
    [JsonPropertyName("individualNpi")]
    public string? IndividualNpi { get; set; }
}
