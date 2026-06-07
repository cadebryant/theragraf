namespace Theragraf.Core.Services;

using Theragraf.Core.Models;

public interface ISessionRepository
{
    Task SaveAsync(SessionRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionResponse>> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    Task<PagedResult<SessionResponse>> GetByClientIdPagedAsync(
        string clientId,
        int pageSize,
        string? continuationToken,
        SessionQueryOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<SessionResponse?> GetByClientIdAndDateAsync(string clientId, string rowKey, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string clientId, string rowKey, CancellationToken cancellationToken = default);
}
