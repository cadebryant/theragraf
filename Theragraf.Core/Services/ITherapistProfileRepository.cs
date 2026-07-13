namespace Theragraf.Core.Services;

using Theragraf.Core.Models;

/// <summary>
/// Persistence abstraction for therapist profile documents.
/// </summary>
public interface ITherapistProfileRepository
{
    /// <summary>
    /// Returns the <see cref="TherapistProfileDocument"/> for the given tenant and therapist,
    /// or <see langword="null"/> if no profile exists.
    /// </summary>
    Task<TherapistProfileDocument?> GetAsync(string tenantId, string therapistId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or fully replaces the therapist profile for <paramref name="profile"/>.
    /// </summary>
    Task<TherapistProfileDocument> UpsertAsync(TherapistProfileDocument profile, CancellationToken cancellationToken = default);
}
