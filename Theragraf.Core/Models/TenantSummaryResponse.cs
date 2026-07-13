namespace Theragraf.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// API response DTO for <c>GET /api/tenant</c>.
/// Returns organization context and AI quota information visible to the current user.
/// Omits internal billing/lifecycle fields.
/// </summary>
public class TenantSummaryResponse
{
    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("organizationName")]
    public string OrganizationName { get; set; } = string.Empty;

    [JsonPropertyName("organizationType")]
    public TenantOrganizationType OrganizationType { get; set; }

    [JsonPropertyName("plan")]
    public TenantPlan Plan { get; set; }

    /// <summary>AI calls consumed in the current billing period.</summary>
    [JsonPropertyName("aiCallsThisPeriod")]
    public int AiCallsThisPeriod { get; set; }

    /// <summary>
    /// Maximum AI calls allowed per billing period. <see langword="null"/> means unlimited
    /// (self-hosted or special agreement).
    /// </summary>
    [JsonPropertyName("monthlyAiCallQuota")]
    public int? MonthlyAiCallQuota { get; set; }

    [JsonPropertyName("status")]
    public TenantStatus Status { get; set; }

    /// <summary>
    /// True when this tenant was synthesised from configuration (self-hosted / BYOA deployment)
    /// rather than loaded from Cosmos.
    /// </summary>
    [JsonPropertyName("isSynthetic")]
    public bool IsSynthetic { get; set; }

    /// <summary>Maps a <see cref="TenantDocument"/> to this response DTO.</summary>
    public static TenantSummaryResponse FromDocument(TenantDocument doc) => new()
    {
        TenantId           = doc.TenantId,
        OrganizationName   = doc.OrganizationName,
        OrganizationType   = doc.OrganizationType,
        Plan               = doc.Plan,
        AiCallsThisPeriod  = doc.AiCallsThisPeriod,
        MonthlyAiCallQuota = doc.MonthlyAiCallQuota,
        Status             = doc.Status,
        IsSynthetic        = doc.IsSynthetic,
    };
}
