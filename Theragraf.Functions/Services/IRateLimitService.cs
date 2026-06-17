namespace Theragraf.Functions.Services;

using Theragraf.Functions.Models;

/// <summary>
/// Result of a rate limit check.
/// </summary>
public record RateLimitResult(
    bool IsAllowed,
    int CurrentCount,
    int Limit,
    DateTime WindowResetTime,
    TimeSpan TimeUntilReset)
{
    /// <summary>
    /// Creates a successful (allowed) result.
    /// </summary>
    public static RateLimitResult Allowed(int currentCount, int limit, DateTime windowResetTime) =>
        new(true, currentCount, limit, windowResetTime, windowResetTime - DateTime.UtcNow);

    /// <summary>
    /// Creates a failed (rate limited) result.
    /// </summary>
    public static RateLimitResult Denied(int currentCount, int limit, DateTime windowResetTime) =>
        new(false, currentCount, limit, windowResetTime, windowResetTime - DateTime.UtcNow);
}

/// <summary>
/// Service for enforcing rate limits on a per-user, per-endpoint basis.
/// Implementations may use in-memory storage (for testing) or distributed storage (Cosmos DB).
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Checks if a request should be allowed under the given rate limit policy.
    /// If allowed, increments the request count. If denied, does not increment.
    /// </summary>
    /// <param name="key">Identifies the user and endpoint being rate limited.</param>
    /// <param name="policy">The rate limit policy (max requests per time window).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating whether the request was allowed and the current window state.</returns>
    Task<RateLimitResult> CheckRateLimitAsync(RateLimitKey key, RateLimitPolicy policy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the rate limit counter for a specific key (e.g., for admin/override scenarios).
    /// </summary>
    Task ResetAsync(RateLimitKey key, CancellationToken cancellationToken = default);
}
