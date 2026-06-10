import { apiFetch } from './client';
import type {
  CaseloadSummary,
  OrchestrationStartResponse,
  OrchestrationStatus,
  PagedResult,
  SessionResponse,
  SessionUpdateRequest,
  SpeechTokenResponse,
  TranscriptInput,
} from '@/types';

// ── Helpers ───────────────────────────────────────────────────────────────────

/**
 * Converts an ISO 8601 date-time string to the row-key format used by
 * PersistActivity: `yyyy-MM-ddTHH-mm-ssZ`
 *
 * Example: "2025-06-15T14:30:00.000Z" → "2025-06-15T14-30-00Z"
 */
export function toSessionDateKey(isoDate: string): string {
  const d = new Date(isoDate);
  const pad = (n: number) => String(n).padStart(2, '0');
  return [
    d.getUTCFullYear(),
    '-',
    pad(d.getUTCMonth() + 1),
    '-',
    pad(d.getUTCDate()),
    'T',
    pad(d.getUTCHours()),
    '-',
    pad(d.getUTCMinutes()),
    '-',
    pad(d.getUTCSeconds()),
    'Z',
  ].join('');
}

// ── Speech token ──────────────────────────────────────────────────────────────

export async function getSpeechToken(): Promise<SpeechTokenResponse> {
  return apiFetch<SpeechTokenResponse>('/api/speech-token');
}

// ── Documentation pipeline ────────────────────────────────────────────────────

export async function startDocumentation(
  input: TranscriptInput,
): Promise<OrchestrationStartResponse> {
  return apiFetch<OrchestrationStartResponse>('/api/DocumentationStart', {
    method: 'POST',
    body: JSON.stringify(input),
  });
}

export async function getOrchestrationStatus(instanceId: string): Promise<OrchestrationStatus> {
  return apiFetch<OrchestrationStatus>(`/api/status/${instanceId}`);
}

// ── Caseload ──────────────────────────────────────────────────────────────────

export async function getCaseload(): Promise<CaseloadSummary> {
  return apiFetch<CaseloadSummary>('/api/sessions');
}

// ── Sessions ──────────────────────────────────────────────────────────────────

export interface GetSessionsOptions {
  pageSize?: number;
  continuationToken?: string;
  discipline?: string;
  therapist?: string;
  payer?: string;
  dateFrom?: string;
  dateTo?: string;
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
}

export async function getSessionsByClient(
  clientId: string,
  options: GetSessionsOptions = {},
): Promise<PagedResult<SessionResponse>> {
  const params = new URLSearchParams();
  if (options.pageSize) params.set('pageSize', String(options.pageSize));
  if (options.continuationToken) params.set('continuationToken', options.continuationToken);
  if (options.discipline) params.set('discipline', options.discipline);
  if (options.therapist) params.set('therapist', options.therapist);
  if (options.payer) params.set('payer', options.payer);
  if (options.dateFrom) params.set('dateFrom', options.dateFrom);
  if (options.dateTo) params.set('dateTo', options.dateTo);
  if (options.sortBy) params.set('sortBy', options.sortBy);
  if (options.sortOrder) params.set('sortOrder', options.sortOrder);

  const qs = params.toString();
  return apiFetch<PagedResult<SessionResponse>>(
    `/api/sessions/${encodeURIComponent(clientId)}${qs ? `?${qs}` : ''}`,
  );
}

export async function getSessionByClientAndDate(
  clientId: string,
  sessionDate: string,
): Promise<SessionResponse> {
  return apiFetch<SessionResponse>(
    `/api/sessions/${encodeURIComponent(clientId)}/${encodeURIComponent(sessionDate)}`,
  );
}

export async function updateSession(
  clientId: string,
  sessionDate: string,
  request: SessionUpdateRequest,
): Promise<SessionResponse> {
  return apiFetch<SessionResponse>(
    `/api/sessions/${encodeURIComponent(clientId)}/${encodeURIComponent(sessionDate)}`,
    { method: 'PATCH', body: JSON.stringify(request) },
  );
}

export async function deleteSession(clientId: string, sessionDate: string): Promise<void> {
  return apiFetch<void>(
    `/api/sessions/${encodeURIComponent(clientId)}/${encodeURIComponent(sessionDate)}`,
    { method: 'DELETE' },
  );
}
