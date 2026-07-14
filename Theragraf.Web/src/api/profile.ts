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

/**
 * GET /api/therapists/me — returns the authenticated therapist's profile.
 * Returns a blank unconfigured stub when no profile document exists yet (404),
 * so new users land on the profile page with the setup banner rather than an error.
 */
export async function getTherapistProfile(): Promise<TherapistProfileResponse> {
  try {
    return await apiFetch<TherapistProfileResponse>('/api/therapists/me');
  } catch (err) {
    if (err instanceof Error && (err as Error & { status?: number }).status === 404) {
      const now = new Date().toISOString();
      return {
        therapistId: '',
        tenantId: '',
        firstName: '',
        lastName: '',
        credentials: null,
        discipline: 'SpeechLanguagePathology',
        individualNpi: null,
        providerId: null,
        isConfigured: false,
        createdAt: now,
        updatedAt: now,
      };
    }
    throw err;
  }
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
