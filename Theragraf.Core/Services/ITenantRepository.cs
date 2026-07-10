namespace Theragraf.Core.Services;

using Theragraf.Core.Models;

/// <summary>
/// Persistence abstraction for tenant documents.
/// </summary>
public interface ITenantRepository
{
    /// <summary>
    /// Returns the <see cref="TenantDocument"/> for <paramref name="tenantId"/>,
    /// or <see langword="null"/> if no tenant record exists.
    /// </summary>
    Task<TenantDocument?> GetAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or fully replaces the tenant record for <paramref name="tenant"/>.
    /// </summary>
    Task<TenantDocument> UpsertAsync(TenantDocument tenant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments <see cref="TenantDocument.AiCallsThisPeriod"/> by one and returns
    /// the updated document. Used by quota enforcement in the documentation pipeline.
    /// </summary>
    Task<TenantDocument> IncrementAiCallCountAsync(string tenantId, CancellationToken cancellationToken = default);
}
