/**
 * Centralized date and time formatting utilities for TheraGraf.
 * All dates are stored as UTC in the backend and displayed in the user's local timezone.
 */

/** Options for date/time formatting */
export interface DateFormatOptions {
  /** Include time component (default: true) */
  includeTime?: boolean;
  /** Use short date format (default: false) */
  short?: boolean;
}

/**
 * Format a session date key (e.g., "2025-06-15T14-30-00Z") for display
 * in the user's local timezone and locale.
 * 
 * @param rowKey - Session date key in format "yyyy-MM-ddTHH-mm-ssZ"
 * @param options - Formatting options
 * @returns Formatted date string in user's locale and timezone
 * 
 * @example
 * formatSessionDate("2025-06-15T14-30-00Z")
 * // => "June 15, 2025, 10:30 AM" (if user is in EDT timezone)
 * 
 * formatSessionDate("2025-06-15T14-30-00Z", { includeTime: false })
 * // => "June 15, 2025"
 * 
 * formatSessionDate("2025-06-15T14-30-00Z", { short: true })
 * // => "Jun 15, 2025, 10:30 AM"
 */
export function formatSessionDate(
  rowKey: string,
  options: DateFormatOptions = {}
): string {
  const { includeTime = true, short = false } = options;

  // Convert row key to ISO format: "2025-06-15T14-30-00Z" -> "2025-06-15T14:30:00Z"
  const iso = rowKey.replace(/T(\d{2})-(\d{2})-(\d{2})Z$/, 'T$1:$2:$3Z');
  const date = new Date(iso);

  if (includeTime) {
    return date.toLocaleString(undefined, {
      year: 'numeric',
      month: short ? 'short' : 'long',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
      hour12: true,
    });
  }

  return date.toLocaleDateString(undefined, {
    year: 'numeric',
    month: short ? 'short' : 'long',
    day: 'numeric',
  });
}

/**
 * Format an ISO timestamp (e.g., approval timestamp) for display
 * in the user's local timezone and locale.
 * 
 * @param isoString - ISO 8601 timestamp string
 * @param options - Formatting options
 * @returns Formatted timestamp string in user's locale and timezone
 * 
 * @example
 * formatTimestamp("2025-06-17T18:45:00.000Z")
 * // => "June 17, 2025, 2:45 PM" (if user is in EDT timezone)
 */
export function formatTimestamp(
  isoString: string,
  options: DateFormatOptions = {}
): string {
  const { short = false } = options;
  const date = new Date(isoString);

  return date.toLocaleString(undefined, {
    year: 'numeric',
    month: short ? 'short' : 'long',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  });
}

/**
 * Get the current date/time formatted for an HTML datetime-local input.
 * Returns the current time in the user's local timezone.
 * 
 * @param date - Date to format (defaults to current time)
 * @returns Formatted string for datetime-local input (YYYY-MM-DDTHH:MM)
 * 
 * @example
 * toDateTimeLocalValue()
 * // => "2025-06-17T14:30" (current local time)
 */
export function toDateTimeLocalValue(date: Date = new Date()): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  const hours = String(date.getHours()).padStart(2, '0');
  const minutes = String(date.getMinutes()).padStart(2, '0');

  return `${year}-${month}-${day}T${hours}:${minutes}`;
}

/**
 * Format a nullable session date key for display.
 * Returns a placeholder if the date is null or undefined.
 * 
 * @param rowKey - Session date key or null/undefined
 * @param placeholder - Text to show when date is null (default: "—")
 * @param options - Formatting options
 * @returns Formatted date string or placeholder
 * 
 * @example
 * formatSessionDateOrPlaceholder(null)
 * // => "—"
 * 
 * formatSessionDateOrPlaceholder("2025-06-15T14-30-00Z")
 * // => "June 15, 2025, 10:30 AM"
 */
export function formatSessionDateOrPlaceholder(
  rowKey: string | null | undefined,
  placeholder: string = '—',
  options: DateFormatOptions = {}
): string {
  if (!rowKey) return placeholder;
  return formatSessionDate(rowKey, options);
}
