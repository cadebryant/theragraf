import { PublicClientApplication, type Configuration, type RedirectRequest } from '@azure/msal-browser';

export const msalConfig: Configuration = {
  auth: {
    clientId: import.meta.env.VITE_AZURE_AD_CLIENT_ID as string,
    authority: `https://login.microsoftonline.com/${import.meta.env.VITE_AZURE_AD_TENANT_ID as string}`,
    redirectUri: window.location.origin,
    postLogoutRedirectUri: window.location.origin,
  },
  cache: {
    cacheLocation: 'localStorage',
    storeAuthStateInCookie: false,
  },
};

/** Scopes used when acquiring tokens for the Function App API. */
export const apiRequest: RedirectRequest = {
  scopes: [import.meta.env.VITE_AZURE_AD_API_SCOPE as string],
};

/**
 * Singleton MSAL instance.
 * Exported here so both MsalProvider and the API client share the same instance
 * without creating a circular dependency.
 */
export const msalInstance = new PublicClientApplication(msalConfig);
