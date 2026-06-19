namespace Theragraf.Core.Models;

/// <summary>
/// Configurable data retention policy for HIPAA compliance.
/// Controls how long records are retained before automatic purge.
/// </summary>
public class RetentionPolicy
{
    /// <summary>
    /// Number of years to retain records. Default is 6 (Federal HIPAA minimum).
    /// Can be extended based on state requirements or organizational policy.
    /// </summary>
    public int RetentionYears { get; set; } = 6;

    /// <summary>
    /// When true, Cosmos DB TTL will automatically purge expired records.
    /// When false, records are retained indefinitely until manual purge.
    /// Default is false (manual review required).
    /// </summary>
    public bool AutoPurgeEnabled { get; set; } = false;

    /// <summary>
    /// Determines when the retention period starts.
    /// CreatedAt: retention starts from document creation date.
    /// DeletedAt: retention starts from deletion date (extends total lifetime).
    /// </summary>
    public RetentionStartMode RetentionStartsFrom { get; set; } = RetentionStartMode.CreatedAt;

    /// <summary>
    /// Calculates the purge date for a record.
    /// Returns null if auto-purge is disabled.
    /// </summary>
    public DateTimeOffset? CalculatePurgeDate(DateTimeOffset createdAt, DateTimeOffset? deletedAt)
    {
        if (!AutoPurgeEnabled)
            return null;

        var startDate = RetentionStartsFrom == RetentionStartMode.DeletedAt && deletedAt.HasValue
            ? deletedAt.Value
            : createdAt;

        return startDate.AddYears(RetentionYears);
    }

    /// <summary>
    /// Converts a DateTimeOffset to Unix timestamp (seconds since epoch) for Cosmos DB TTL.
    /// Returns null if purgeDate is null.
    /// </summary>
    public int? ConvertToTtl(DateTimeOffset? purgeDate)
    {
        if (!purgeDate.HasValue)
            return null;

        // Cosmos TTL is relative to document's _ts field, but we can also use absolute Unix timestamp
        var unixTimestamp = (int)(purgeDate.Value.ToUnixTimeSeconds());
        return unixTimestamp;
    }
}

/// <summary>
/// Determines when the retention period clock starts.
/// </summary>
public enum RetentionStartMode
{
    /// <summary>Retention period starts from document creation date.</summary>
    CreatedAt,

    /// <summary>Retention period starts from deletion date (extends total lifetime).</summary>
    DeletedAt
}
