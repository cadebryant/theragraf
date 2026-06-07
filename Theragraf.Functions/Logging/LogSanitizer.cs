namespace Theragraf.Functions.Logging;

/// <summary>
/// Provides sanitised values safe to write to logs and telemetry.
///
/// SECURITY RULES — never pass the following to any log call:
///   - Raw or redacted transcript text
///   - SOAP note field content (Subjective, Objective, Assessment, Plan)
///   - Redaction map keys or values (PII placeholders or originals)
///   - API keys, connection strings, or tokens
///
/// Use this class to derive log-safe summaries (counts, lengths, IDs) instead.
/// </summary>
public static class LogSanitizer
{
    /// <summary>Returns the character length of a string, or 0 if null — never the content.</summary>
    public static int TextLength(string? text) => text?.Length ?? 0;

    /// <summary>Returns the number of entries in a collection — never the values.</summary>
    public static int Count<T>(IEnumerable<T>? items) => items?.Count() ?? 0;

    /// <summary>
    /// Returns a safe representation of a client ID.
    /// Client IDs are not PII, but we still confirm they are non-empty before logging.
    /// </summary>
    public static string ClientId(string? clientId) =>
        string.IsNullOrWhiteSpace(clientId) ? "(empty)" : clientId;
}
