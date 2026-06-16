import { apiFetch } from './client';
import type {
  CreateGoalRequest,
  GoalResponse,
  GoalSuggestion,
  GoalSuggestRequest,
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
