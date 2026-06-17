namespace Theragraf.Functions.Services;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Theragraf.Functions.Models;

/// <summary>
/// In-memory rate limit service for unit testing and local development.
/// Uses a sliding window approach with automatic cleanup of expired windows.
/// </summary>
public sealed class MemoryRateLimitService : IRateLimitService
{
    private readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _buckets =
        new();

    public Task<RateLimitResult> CheckRateLimitAsync(
        RateLimitKey key,
        RateLimitPolicy policy,
        CancellationToken cancellationToken = default)
    {
        var documentId = GenerateDocumentId(key, policy);
        var now = DateTime.UtcNow;
        var windowStart = now - policy.TimeWindow;

        // Use a lock-free approach: read first to decide, then update only if allowed.
        while (true)
        {
            if (!_buckets.TryGetValue(documentId, out var existing))
            {
                // First request in window.
                var newValue = (1, now);
                if (_buckets.TryAdd(documentId, newValue))
                {
                    var windowResetTime = now.AddSeconds(policy.TimeWindow.TotalSeconds);
                    return Task.FromResult(RateLimitResult.Allowed(1, policy.MaxRequests, windowResetTime));
                }
                // If TryAdd failed, another thread added it, so loop and read it.
                continue;
            }

            var (count, prevWindowStart) = existing;

            // If the window has expired, reset it.
            if (prevWindowStart < windowStart)
            {
                var newValue = (1, now);
                if (_buckets.TryUpdate(documentId, newValue, existing))
                {
                    var windowResetTime = now.AddSeconds(policy.TimeWindow.TotalSeconds);
                    return Task.FromResult(RateLimitResult.Allowed(1, policy.MaxRequests, windowResetTime));
                }
                // Another thread updated concurrently, try again.
                continue;
            }

            // Window is still active. Check if we've hit the limit BEFORE incrementing.
            var windowResetTime2 = prevWindowStart.AddSeconds(policy.TimeWindow.TotalSeconds);
            if (count >= policy.MaxRequests)
            {
                // Already at or over limit; don't increment.
                return Task.FromResult(
                    RateLimitResult.Denied(count, policy.MaxRequests, windowResetTime2));
            }

            // Increment the counter.
            var newCount = count + 1;
            var newValue2 = (newCount, prevWindowStart);
            if (_buckets.TryUpdate(documentId, newValue2, existing))
            {
                return Task.FromResult(
                    RateLimitResult.Allowed(newCount, policy.MaxRequests, windowResetTime2));
            }
            // Another thread updated concurrently, try again.
        }
    }

    public Task ResetAsync(RateLimitKey key, CancellationToken cancellationToken = default)
    {
        _buckets.Clear();
        return Task.CompletedTask;
    }

    public void ClearExpired()
    {
        var now = DateTime.UtcNow;
        var keysToRemove = _buckets
            .Where(kvp => kvp.Value.WindowStart.AddSeconds(60) < now) // Assume 60s max window
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _buckets.TryRemove(key, out _);
        }
    }

    private static string GenerateDocumentId(RateLimitKey key, RateLimitPolicy policy) =>
        $"ratelimit#{key.UserId}#{key.EndpointName}#{policy.Name}";
}
