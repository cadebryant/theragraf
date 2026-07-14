import { apiFetch } from './client';
import type {
  ClientGoalStats,
  CreateGoalRequest,
  GoalResponse,
  GoalSuggestion,
  GoalSuggestRequest,
  TherapistGoalStats,
  UpdateGoalRequest,
} from '@/types';

const base = (clientId: string) => `/api/goals/${encodeURIComponent(clientId)}`;

export async function getGoals(clientId: string): Promise<GoalResponse[]> {
  return apiFetch<GoalResponse[]>(base(clientId));
}

export async function createGoal(
  clientId: string,
  request: CreateGoalRequest,
): Promise<GoalResponse> {
  return apiFetch<GoalResponse>(base(clientId), {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

export async function updateGoal(
  clientId: string,
  goalId: string,
  request: UpdateGoalRequest,
): Promise<GoalResponse> {
  return apiFetch<GoalResponse>(`${base(clientId)}/${encodeURIComponent(goalId)}`, {
    method: 'PATCH',
    body: JSON.stringify(request),
  });
}

export async function deleteGoal(clientId: string, goalId: string): Promise<void> {
  return apiFetch<void>(`${base(clientId)}/${encodeURIComponent(goalId)}`, {
    method: 'DELETE',
  });
}

export async function suggestGoals(
  clientId: string,
  request: GoalSuggestRequest,
): Promise<GoalSuggestion[]> {
  return apiFetch<GoalSuggestion[]>(`${base(clientId)}/suggest`, {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

// ── Stats ─────────────────────────────────────────────────────────────────────

/** GET /api/goals/stats/client/{clientId} — goal progress breakdown for a single client. */
export async function getGoalStatsForClient(clientId: string): Promise<ClientGoalStats> {
  return apiFetch<ClientGoalStats>(
    `/api/goals/stats/client/${encodeURIComponent(clientId)}`,
  );
}

/** GET /api/goals/stats/therapist/{therapistName} — goal progress aggregated across all clients. */
export async function getGoalStatsForTherapist(therapistName: string): Promise<TherapistGoalStats> {
  return apiFetch<TherapistGoalStats>(
    `/api/goals/stats/therapist/${encodeURIComponent(therapistName)}`,
  );
}
