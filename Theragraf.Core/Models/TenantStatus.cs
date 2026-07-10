namespace Theragraf.Core.Models;

/// <summary>
/// Lifecycle status of a hosted tenant.
/// </summary>
public enum TenantStatus
{
    /// <summary>Tenant is fully operational.</summary>
    Active,

    /// <summary>Tenant has been temporarily suspended (e.g. non-payment, policy violation).</summary>
    Suspended,

    /// <summary>Tenant has been permanently deprovisioned. PHI keys scheduled for deletion.</summary>
    Deprovisioned
}
