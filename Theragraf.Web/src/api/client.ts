import { InteractionRequiredAuthError } from '@azure/msal-browser';
import { apiRequest, msalInstance } from '@/auth/msalConfig';

/** Acquires a Bearer token silently; falls back to redirect on interaction-required errors. */
async function getAccessToken(): Promise<string> {
  const accounts = msalInstance.getAllAccounts();
  if (accounts.length === 0) {
    await msalInstance.loginRedirect(apiRequest);
    return '';
  }

  try {
    const result = await msalInstance.acquireTokenSilent({
      ...apiRequest,
      account: accounts[0],
    });
    return result.accessToken;
  } catch (err) {
    if (err instanceof InteractionRequiredAuthError) {
      await msalInstance.acquireTokenRedirect({ ...apiRequest, account: accounts[0] });
    }
    throw err;
  }
}

/** Authenticated fetch wrapper. Throws on non-2xx responses. */
export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const token = await getAccessToken();

  const response = await fetch(path, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
      ...(init?.headers ?? {}),
    },
  });

  if (!response.ok) {
    const message = await response.text().catch(() => response.statusText);
    throw new Error(`${response.status} ${response.statusText}: ${message}`);
  }

  // 204 No Content
  if (response.status === 204) return undefined as T;

  return response.json() as Promise<T>;
}
