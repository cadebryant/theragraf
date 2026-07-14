import { apiFetch } from './client';
import type { ClientDemographicsResponse, UpsertClientDemographicsRequest } from '@/types';

const base = (clientId: string) => `/api/clients/${encodeURIComponent(clientId)}`;

/** Returns null (404) when no intake record exists yet. */
export async function getClientDemographics(
  clientId: string,
): Promise<ClientDemographicsResponse | null> {
  try {
    return await apiFetch<ClientDemographicsResponse>(base(clientId));
  } catch (err) {
    if (err instanceof Error && (err as Error & { status?: number }).status === 404) return null;
    throw err;
  }
}

export async function upsertClientDemographics(
  clientId: string,
  body: UpsertClientDemographicsRequest,
): Promise<ClientDemographicsResponse> {
  return apiFetch<ClientDemographicsResponse>(base(clientId), {
    method: 'PUT',
    body: JSON.stringify(body),
  });
}
