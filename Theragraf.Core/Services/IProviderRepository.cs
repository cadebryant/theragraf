namespace Theragraf.Core.Services;

using Theragraf.Core.Models;

/// <summary>
/// Persistence abstraction for provider (group practice) documents.
/// </summary>
public interface IProviderRepository
{
    /// <summary>
    /// Returns the <see cref="ProviderDocument"/> for the given tenant and provider,
    /// or <see langword="null"/> if not found.
    /// </summary>
    Task<ProviderDocument?> GetAsync(string tenantId, string providerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or fully replaces the provider record for <paramref name="provider"/>.
    /// </summary>
    Task<ProviderDocument> UpsertAsync(ProviderDocument provider, CancellationToken cancellationToken = default);
}
