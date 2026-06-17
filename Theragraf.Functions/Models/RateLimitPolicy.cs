namespace Theragraf.Functions.Models;

/// <summary>
/// Defines a rate limit policy with a maximum number of requests allowed
/// within a time window.
/// </summary>
public record RateLimitPolicy(
    string Name,
    int MaxRequests,
    TimeSpan TimeWindow)
{
    /// <summary>
    /// Predefined policies for different endpoint categories.
    /// </summary>
    public static class Presets
    {
        /// <summary>
        /// Speech token endpoint: 10 requests per minute.
        /// Speech tokens cost quota and should be carefully limited.
        /// </summary>
        public static readonly RateLimitPolicy SpeechToken = new("SpeechToken", 10, TimeSpan.FromMinutes(1));

        /// <summary>
        /// Documentation pipeline (start, status, etc.): 20 requests per minute.
        /// Heavy operation that triggers Durable Functions and AI agents.
        /// </summary>
        public static readonly RateLimitPolicy DocumentationPipeline = new("DocumentationPipeline", 20, TimeSpan.FromMinutes(1));

        /// <summary>
        /// Mutations (create/update/delete goals, sessions, client data): 50 requests per minute.
        /// Write-intensive but less resource-heavy than documentation.
        /// </summary>
        public static readonly RateLimitPolicy Mutation = new("Mutation", 50, TimeSpan.FromMinutes(1));

        /// <summary>
        /// Read-only endpoints (stats, goals list, sessions list, etc.): 100 requests per minute.
        /// Least resource-intensive; allow higher rate.
        /// </summary>
        public static readonly RateLimitPolicy ReadOnly = new("ReadOnly", 100, TimeSpan.FromMinutes(1));
    }
}

/// <summary>
/// Identifies a rate limit bucket uniquely by user/actor and endpoint.
/// </summary>
public record RateLimitKey(string UserId, string EndpointName);
