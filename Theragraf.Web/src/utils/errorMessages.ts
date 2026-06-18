/**
 * Centralized error message formatting for user-facing UI.
 * Sanitizes error messages to prevent leaking sensitive information
 * while providing correlation IDs for support.
 */

/** Type guard to check if an error has a correlation ID */
function hasCorrelationId(error: unknown): error is { correlationId: string } {
  return (
    typeof error === 'object' &&
    error !== null &&
    'correlationId' in error &&
    typeof (error as { correlationId: unknown }).correlationId === 'string'
  );
}

/** Type guard to check if an error has an HTTP status code */
function hasStatus(error: unknown): error is { status: number } {
  return (
    typeof error === 'object' &&
    error !== null &&
    'status' in error &&
    typeof (error as { status: unknown }).status === 'number'
  );
}

/**
 * Formats an error for display to the user.
 * Extracts correlation ID and provides context-appropriate messages.
 * Never displays raw exception details or stack traces.
 * 
 * @param error - The error object (typically from a failed query or mutation)
 * @param context - Optional context string (e.g., "loading caseload", "saving session")
 * @returns A sanitized, user-friendly error message
 */
export function formatErrorMessage(error: unknown, context?: string): string {
  // Extract correlation ID if present
  const correlationId = hasCorrelationId(error) ? error.correlationId : undefined;

  // Check for specific HTTP status codes that warrant custom messages
  if (hasStatus(error)) {
    switch (error.status) {
      case 401:
        return 'Your session has expired. Please sign in again.';
      case 403:
        return 'You do not have permission to perform this action.';
      case 404:
        return context 
          ? `The requested resource was not found while ${context}.`
          : 'The requested resource was not found.';
      case 409:
        return 'This action conflicts with the current state. Please refresh and try again.';
      case 429:
        return 'Too many requests. Please wait a moment and try again.';
    }
  }

  // Build the generic error message
  const baseMessage = context 
    ? `An error occurred while ${context}.`
    : 'An unexpected error occurred.';

  // Add correlation ID if present
  if (correlationId) {
    return `${baseMessage} If this persists, contact support with reference: ${correlationId}`;
  }

  // Fallback message without correlation ID
  return `${baseMessage} If this persists, please contact support.`;
}

/**
 * Gets a short, generic error message for inline display (e.g., in a small banner).
 * Does not include correlation IDs to keep the message concise.
 * 
 * @param context - Optional context string
 * @returns A concise error message
 */
export function getShortErrorMessage(context?: string): string {
  return context 
    ? `Error ${context}. Please try again.`
    : 'An error occurred. Please try again.';
}

/**
 * Extracts just the correlation ID from an error if present.
 * Useful for logging or displaying the reference separately.
 * 
 * @param error - The error object
 * @returns The correlation ID string or undefined
 */
export function extractCorrelationId(error: unknown): string | undefined {
  return hasCorrelationId(error) ? error.correlationId : undefined;
}
