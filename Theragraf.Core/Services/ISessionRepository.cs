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

    /// <summary>
    /// Soft-deletes a session by marking it as deleted rather than removing it from storage.
    /// Returns true if the session was found and marked deleted, false if not found.
    /// </summary>
    Task<bool> DeleteAsync(string clientId, string rowKey, string deletedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a soft-deleted session by clearing the deletion markers.
    /// Returns true if the session was found and restored, false if not found or already active.
    /// </summary>
    Task<bool> RestoreAsync(string clientId, string rowKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a partial update to an existing session document.
    /// Returns the updated <see cref="SessionResponse"/>, or <see langword="null"/> if not found.
    /// <para>
    /// <paramref name="soapNoteUpdate"/> carries only the fields the caller changed; any null
    /// field is left unchanged in the stored document. Non-null fields must already have PII
    /// replaced with placeholders before this method is called.
    /// </para>
    /// </summary>
    Task<SessionResponse?> UpdateAsync(
        string                             clientId,
        string                             rowKey,
        SoapNoteUpdate?                    soapNoteUpdate,
        IReadOnlyDictionary<string,string> newRedactionMap,
        IReadOnlyList<CptCode>?            cptCodes,
        IReadOnlyList<IcdCode>?            icdCodes,
        ApprovalUpdate?                    approval,
        CancellationToken                  cancellationToken = default);

    /// <summary>
    /// Returns a caseload overview for <paramref name="therapistName"/>: one
    /// <see cref="ClientSummary"/> per distinct client, ordered by most-recent session
    /// date descending. This is a cross-partition scan.
    /// </summary>
    Task<CaseloadSummary> GetCaseloadAsync(string therapistName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns aggregated statistics for every session belonging to <paramref name="therapistName"/>
    /// across all clients. This is a cross-partition scan.
    /// </summary>
    Task<TherapistStats> GetTherapistStatsAsync(string therapistName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns aggregated statistics for every session belonging to <paramref name="clientId"/>.
    /// This is a single-partition read.
    /// </summary>
    Task<ClientStats> GetClientStatsAsync(string clientId, CancellationToken cancellationToken = default);
}
