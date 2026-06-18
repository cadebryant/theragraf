namespace Theragraf.Core.Helpers;

/// <summary>
/// Provides safe, sanitized error responses that prevent sensitive information
/// from leaking to end users while maintaining correlation for debugging.
/// </summary>
public static class SafeErrorHelper
{
    /// <summary>
    /// Generates a unique correlation ID for tracking errors across logs and user reports.
    /// </summary>
    public static string GenerateCorrelationId() => Guid.NewGuid().ToString("N")[..16];

    /// <summary>
    /// Sanitizes an exception for user-facing HTTP responses.
    /// Returns a generic error message with a correlation ID for support tracking.
    /// </summary>
    /// <param name="operation">User-friendly description of the operation that failed (e.g., "retrieving caseload", "starting documentation")</param>
    /// <param name="correlationId">Correlation ID for this error (optional, will be generated if not provided)</param>
    /// <returns>A sanitized error message safe for end users</returns>
    public static string GetSafeErrorMessage(string operation, string? correlationId = null)
    {
        correlationId ??= GenerateCorrelationId();
        return $"An error occurred while {operation}. If this persists, contact support with reference: {correlationId}";
    }

    /// <summary>
    /// Gets a sanitized generic error message with correlation ID.
    /// Use when the specific operation context is not available.
    /// </summary>
    public static string GetGenericErrorMessage(string? correlationId = null)
    {
        correlationId ??= GenerateCorrelationId();
        return $"An unexpected error occurred. If this persists, contact support with reference: {correlationId}";
    }

    /// <summary>
    /// Creates a structured error detail for audit logging that separates
    /// user-facing messages from internal exception details.
    /// </summary>
    /// <param name="ex">The exception to log</param>
    /// <param name="correlationId">Correlation ID linking this error to the user-facing response</param>
    /// <returns>A structured string suitable for audit log detail field</returns>
    public static string GetAuditLogDetail(Exception ex, string correlationId)
    {
        return $"[{correlationId}] {ex.GetType().Name}: {ex.Message}";
    }

    /// <summary>
    /// Gets the full exception details for internal logging (not audit logs).
    /// Includes stack trace for debugging.
    /// </summary>
    public static string GetInternalLogDetail(Exception ex, string correlationId)
    {
        return $"[{correlationId}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
    }
}
