namespace Theragraf.Functions.Configuration;

using Theragraf.Core.Models;

/// <summary>
/// Configuration section for data retention policy settings.
/// Maps to "RetentionPolicy" section in application configuration.
/// </summary>
public class RetentionPolicyConfiguration : RetentionPolicy
{
    /// <summary>Configuration section name in appsettings or environment variables.</summary>
    public const string Section = "RetentionPolicy";
}
