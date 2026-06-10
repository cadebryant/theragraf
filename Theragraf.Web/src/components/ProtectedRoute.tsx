import type { ReactNode } from 'react';
import { useIsAuthenticated, useMsal } from '@azure/msal-react';
import { InteractionStatus } from '@azure/msal-browser';
import { Spinner, makeStyles, tokens } from '@fluentui/react-components';
import { apiRequest, msalInstance } from '@/auth/msalConfig';

const useStyles = makeStyles({
  center: {
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    height: '60vh',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
});

interface Props {
  children: ReactNode;
}

export default function ProtectedRoute({ children }: Props) {
  const styles = useStyles();
  const isAuthenticated = useIsAuthenticated();
  const { inProgress } = useMsal();

  if (inProgress === InteractionStatus.None && !isAuthenticated) {
    void msalInstance.loginRedirect(apiRequest);
    return null;
  }

  if (inProgress !== InteractionStatus.None) {
    return (
      <div className={styles.center}>
        <Spinner label="Signing in…" size="large" />
      </div>
    );
  }

  return <>{children}</>;
}
