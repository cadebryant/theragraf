import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { MsalProvider } from '@azure/msal-react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { msalInstance } from '@/auth/msalConfig';
import ErrorBoundary from '@/components/ErrorBoundary';
import App from './App';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      staleTime: 30_000,
    },
  },
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ErrorBoundary>
      <MsalProvider instance={msalInstance}>
        <QueryClientProvider client={queryClient}>
          <FluentProvider theme={webLightTheme}>
            <App />
          </FluentProvider>
        </QueryClientProvider>
      </MsalProvider>
    </ErrorBoundary>
  </StrictMode>,
);
