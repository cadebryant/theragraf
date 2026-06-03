namespace Theragraf.Core.Services;

using Theragraf.Core.Models;

public interface ISessionRepository
{
    Task SaveAsync(SessionRecord record, CancellationToken cancellationToken = default);
}
