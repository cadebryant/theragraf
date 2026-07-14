import { apiFetch } from './client';
import type {
  ProviderResponse,
  TenantSummaryResponse,
  TherapistProfileResponse,
  TherapistProfileUpdateRequest,
} from '@/types';

/** GET /api/tenant — returns organization context and AI quota for the authenticated tenant. */
export async function getTenant(): Promise<TenantSummaryResponse> {
  return apiFetch<TenantSummaryResponse>('/api/tenant');
}

/** GET /api/therapists/me — returns the authenticated therapist's profile. */
export async function getTherapistProfile(): Promise<TherapistProfileResponse> {
  return apiFetch<TherapistProfileResponse>('/api/therapists/me');
}

/**
 * PATCH /api/therapists/me — create or update the authenticated therapist's profile.
 * Returns the updated profile.
 */
export async function updateTherapistProfile(
  request: TherapistProfileUpdateRequest,
): Promise<TherapistProfileResponse> {
  return apiFetch<TherapistProfileResponse>('/api/therapists/me', {
    method: 'PATCH',
    body: JSON.stringify(request),
  });
}

/** GET /api/providers/{providerId} — returns practice info for a provider in the caller's tenant. */
export async function getProvider(providerId: string): Promise<ProviderResponse> {
  return apiFetch<ProviderResponse>(`/api/providers/${encodeURIComponent(providerId)}`);
}
