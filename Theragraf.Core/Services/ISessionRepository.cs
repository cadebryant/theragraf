namespace Theragraf.Core.Services;

using Theragraf.Core.Models;

public interface ISessionRepository
{
    Task SaveAsync(SessionRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionResponse>> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    Task<SessionResponse?> GetByClientIdAndDateAsync(string clientId, string rowKey, CancellationToken cancellationToken = default);
}
