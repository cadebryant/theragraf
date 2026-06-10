import { apiFetch } from './client';
import type { ClientStats, TherapistStats } from '@/types';

export async function getTherapistStats(therapistName: string): Promise<TherapistStats> {
  return apiFetch<TherapistStats>(
    `/api/stats/therapist/${encodeURIComponent(therapistName)}`,
  );
}

export async function getClientStats(clientId: string): Promise<ClientStats> {
  return apiFetch<ClientStats>(`/api/stats/client/${encodeURIComponent(clientId)}`);
}
