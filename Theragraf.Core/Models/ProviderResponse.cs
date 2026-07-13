namespace Theragraf.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// API response DTO for <c>GET /api/providers/{providerId}</c>.
/// Omits encrypted fields (encryptedEin).
/// </summary>
public class ProviderResponse
{
    [JsonPropertyName("providerId")]
    public string ProviderId { get; set; } = string.Empty;

    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("practiceName")]
    public string PracticeName { get; set; } = string.Empty;

    /// <summary>Organization NPI (Type 2). Null until configured.</summary>
    [JsonPropertyName("organizationNpi")]
    public string? OrganizationNpi { get; set; }

    [JsonPropertyName("addressLine1")]
    public string? AddressLine1 { get; set; }

    [JsonPropertyName("addressLine2")]
    public string? AddressLine2 { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("zip")]
    public string? Zip { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Maps a <see cref="ProviderDocument"/> to this response DTO.</summary>
    public static ProviderResponse FromDocument(ProviderDocument doc) => new()
    {
        ProviderId      = doc.ProviderId,
        TenantId        = doc.TenantId,
        PracticeName    = doc.PracticeName,
        OrganizationNpi = doc.OrganizationNpi,
        AddressLine1    = doc.AddressLine1,
        AddressLine2    = doc.AddressLine2,
        City            = doc.City,
        State           = doc.State,
        Zip             = doc.Zip,
        Phone           = doc.Phone,
        CreatedAt       = doc.CreatedAt,
        UpdatedAt       = doc.UpdatedAt,
    };
}
