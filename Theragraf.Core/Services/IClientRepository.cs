namespace Theragraf.Core.Services;

using Theragraf.Core.Models;

/// <summary>
/// Persistence abstraction for client demographic / intake records.
/// </summary>
public interface IClientRepository
{
    /// <summary>
    /// Returns the demographics record for <paramref name="clientId"/>, or
    /// <see langword="null"/> if no intake record exists yet.
    /// </summary>
    Task<ClientDemographicsResponse?> GetAsync(
        string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or fully replaces the demographics record for <paramref name="clientId"/>.
    /// Returns the saved record (with <c>AgeYears</c> computed from the encrypted DOB).
    /// </summary>
    Task<ClientDemographicsResponse> UpsertAsync(
        string clientId, UpsertClientDemographicsRequest request,
        CancellationToken cancellationToken = default);
}
