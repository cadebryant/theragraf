namespace Theragraf.Functions.Configuration;

/// <summary>
/// Configuration for rate limiting behavior.
/// Loaded from appsettings.json or environment variables.
/// </summary>
public sealed class RateLimitConfiguration
{
    public const string Section = "RateLimit";

    /// <summary>
    /// Whether rate limiting is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Use Cosmos DB backend instead of in-memory. Default: true in production, false in tests.
    /// </summary>
    public bool UseDistributedBackend { get; set; } = true;

    /// <summary>
    /// Maximum requests for speech token endpoint per minute. Default: 10.
    /// </summary>
    public int SpeechTokenMaxRequests { get; set; } = 10;

    /// <summary>
    /// Maximum requests for documentation pipeline endpoints per minute. Default: 20.
    /// </summary>
    public int DocumentationMaxRequests { get; set; } = 20;

    /// <summary>
    /// Maximum requests for mutation endpoints per minute. Default: 50.
    /// </summary>
    public int MutationMaxRequests { get; set; } = 50;

    /// <summary>
    /// Maximum requests for read-only endpoints per minute. Default: 100.
    /// </summary>
    public int ReadOnlyMaxRequests { get; set; } = 100;

    /// <summary>
    /// Rate limit time window in seconds. Default: 60.
    /// </summary>
    public int TimeWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Bypass rate limiting for specific user IDs (comma-separated). Useful for testing.
    /// </summary>
    public string? BypassUserIds { get; set; }

    public ISet<string> GetBypassUserIds() =>
        (BypassUserIds ?? string.Empty)
            .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
