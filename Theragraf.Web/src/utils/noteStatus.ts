/**
 * Parses a session row-key date string (`yyyy-MM-ddTHH-mm-ssZ`) and returns
 * a status indicating whether the note is overdue.
 *
 * Thresholds:
 *   - null      : session is within 2 days — no alert
 *   - 'overdue' : 2–7 days since last session — amber warning
 *   - 'urgent'  : 7+ days since last session — red warning
 */
export type NoteStatus = 'overdue' | 'urgent' | null;

const TWO_DAYS_MS  = 2 * 24 * 60 * 60 * 1000;
const SEVEN_DAYS_MS = 7 * 24 * 60 * 60 * 1000;

/** Converts the Cosmos row-key date format `yyyy-MM-ddTHH-mm-ssZ` to a Date. */
function parseSessionDate(rowKeyDate: string): Date {
  // Replace `-` separators in the time component with `:`
  const iso = rowKeyDate.replace(/T(\d{2})-(\d{2})-(\d{2})Z/, 'T$1:$2:$3Z');
  return new Date(iso);
}

export function getNoteStatus(lastSessionDate: string | null): NoteStatus {
  if (!lastSessionDate) return null;

  const sessionDate = parseSessionDate(lastSessionDate);
  if (isNaN(sessionDate.getTime())) return null;

  const ageMs = Date.now() - sessionDate.getTime();

  if (ageMs >= SEVEN_DAYS_MS) return 'urgent';
  if (ageMs >= TWO_DAYS_MS)   return 'overdue';
  return null;
}

export function noteStatusLabel(status: NoteStatus): string {
  if (status === 'urgent')  return 'Note overdue';
  if (status === 'overdue') return 'Note overdue';
  return '';
}
