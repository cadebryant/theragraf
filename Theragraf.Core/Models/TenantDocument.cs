namespace Theragraf.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Azure Cosmos DB document representing a single hosted tenant (organization or solo practitioner).
///
/// Container : tenants
/// PartitionKey: /tenantId
/// id          : same as tenantId (one document per tenant)
///
/// For self-hosted (BYOA) deployments, a synthetic <see cref="TenantDocument"/> is constructed
/// at runtime from application configuration — no Cosmos document is read or required.
/// </summary>
public class TenantDocument
{
    // ── Identity ─────────────────────────────────────────────────────────────

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;                   // == tenantId

    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;             // partition key

    [JsonPropertyName("organizationName")]
    public string OrganizationName { get; set; } = string.Empty;

    [JsonPropertyName("organizationType")]
    public TenantOrganizationType OrganizationType { get; set; } = TenantOrganizationType.SoloPractitioner;

    // ── Plan / quota ─────────────────────────────────────────────────────────

    [JsonPropertyName("plan")]
    public TenantPlan Plan { get; set; } = TenantPlan.Free;

    /// <summary>
    /// Maximum AI calls allowed per billing period. <see langword="null"/> means unlimited
    /// (used for self-hosted synthetic tenants or special agreements).
    /// </summary>
    [JsonPropertyName("monthlyAiCallQuota")]
    public int? MonthlyAiCallQuota { get; set; }

    /// <summary>Number of AI calls consumed in the current billing period.</summary>
    [JsonPropertyName("aiCallsThisPeriod")]
    public int AiCallsThisPeriod { get; set; }

    [JsonPropertyName("billingPeriodStart")]
    public DateTimeOffset BillingPeriodStart { get; set; }

    // ── Status / lifecycle ───────────────────────────────────────────────────

    [JsonPropertyName("status")]
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }

    // ── Flags ────────────────────────────────────────────────────────────────

    /// <summary>
    /// True when this document was synthesised from configuration rather than loaded from Cosmos.
    /// Always <see langword="true"/> for self-hosted (BYOA) deployments.
    /// </summary>
    [JsonIgnore]
    public bool IsSynthetic { get; set; }
}
