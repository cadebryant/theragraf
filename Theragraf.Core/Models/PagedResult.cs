namespace Theragraf.Core.Models;

/// <summary>
/// Generic cursor-based page envelope returned by list endpoints.
/// </summary>
/// <typeparam name="T">The item type for this page.</typeparam>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int PageSize,
    bool HasMore,
    string? ContinuationToken
);
