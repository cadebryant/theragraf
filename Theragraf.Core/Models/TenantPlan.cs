namespace Theragraf.Core.Models;

/// <summary>
/// Subscription plan for a hosted tenant. Controls AI call quotas and feature access.
/// </summary>
public enum TenantPlan
{
    /// <summary>50 AI calls/month. Evaluation or very small practices.</summary>
    Free,

    /// <summary>500 AI calls/month. Small clinics.</summary>
    Professional,

    /// <summary>200 AI calls/month. University programs (negotiated).</summary>
    Academic
}
