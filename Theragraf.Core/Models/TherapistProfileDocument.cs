namespace Theragraf.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Azure Cosmos DB document representing a therapist's billing and professional profile.
/// Used to populate 837P claim fields (NPI, tax ID, credentials) and associate sessions
/// with the treating therapist.
///
/// Container : therapist-profiles
/// PartitionKey: /tenantId + /therapistId
/// id          : same as therapistId (Entra Object ID)
/// </summary>
public class TherapistProfileDocument
{
    // ── Identity ─────────────────────────────────────────────────────────────

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;               // == therapistId

    /// <summary>Entra Object ID of the therapist. Partition key (second level).</summary>
    [JsonPropertyName("therapistId")]
    public string TherapistId { get; set; } = string.Empty;

    /// <summary>Tenant this therapist belongs to. Partition key (first level).</summary>
    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    // ── Profile ───────────────────────────────────────────────────────────────

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>Professional credentials suffix, e.g. "OTR/L", "PT, DPT", "CCC-SLP".</summary>
    [JsonPropertyName("credentials")]
    public string? Credentials { get; set; }

    [JsonPropertyName("discipline")]
    public TherapyDiscipline Discipline { get; set; }

    // ── Billing identifiers ───────────────────────────────────────────────────

    /// <summary>
    /// Individual 10-digit NPI (Type 1). Required for 837P billing as the rendering provider.
    /// </summary>
    [JsonPropertyName("individualNpi")]
    public string? IndividualNpi { get; set; }

    /// <summary>
    /// AES-256-GCM encrypted Tax ID (SSN or EIN), base64-encoded (nonce|cipher|tag).
    /// Only set for sole proprietors billing under their own SSN/EIN.
    /// Group practice therapists use <see cref="ProviderDocument.EncryptedEin"/> instead.
    /// </summary>
    [JsonPropertyName("encryptedTaxId")]
    public string? EncryptedTaxId { get; set; }

    // ── Group practice link ───────────────────────────────────────────────────

    /// <summary>
    /// Optional FK to <see cref="ProviderDocument.ProviderId"/>.
    /// Null for solo practitioners. Populated when this therapist is a member of a group practice.
    /// </summary>
    [JsonPropertyName("providerId")]
    public string? ProviderId { get; set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }
}
