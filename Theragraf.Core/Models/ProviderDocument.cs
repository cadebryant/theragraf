namespace Theragraf.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Azure Cosmos DB document representing a clinic or group practice (Type 2 NPI entity).
/// Used as the billing provider on 837P claims when therapists belong to a group practice.
///
/// Container : providers
/// PartitionKey: /tenantId + /providerId
/// id          : same as providerId (GUID)
///
/// Solo practitioners do not require a <see cref="ProviderDocument"/>; their billing
/// identifiers live directly on <see cref="TherapistProfileDocument"/>.
/// </summary>
public class ProviderDocument
{
    // ── Identity ─────────────────────────────────────────────────────────────

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;               // == providerId

    /// <summary>GUID assigned at creation. Partition key (second level).</summary>
    [JsonPropertyName("providerId")]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>Tenant this provider belongs to. Partition key (first level).</summary>
    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    // ── Practice information ──────────────────────────────────────────────────

    [JsonPropertyName("practiceName")]
    public string PracticeName { get; set; } = string.Empty;

    /// <summary>
    /// Organization NPI (Type 2). Required for 837P claims when billing as a group practice.
    /// </summary>
    [JsonPropertyName("organizationNpi")]
    public string? OrganizationNpi { get; set; }

    /// <summary>
    /// AES-256-GCM encrypted EIN, base64-encoded (nonce|cipher|tag).
    /// </summary>
    [JsonPropertyName("encryptedEin")]
    public string? EncryptedEin { get; set; }

    // ── Address ───────────────────────────────────────────────────────────────

    [JsonPropertyName("addressLine1")]
    public string? AddressLine1 { get; set; }

    [JsonPropertyName("addressLine2")]
    public string? AddressLine2 { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    /// <summary>Two-letter US state abbreviation.</summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("zip")]
    public string? Zip { get; set; }

    /// <summary>10-digit phone number (digits only, no formatting).</summary>
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }
}
