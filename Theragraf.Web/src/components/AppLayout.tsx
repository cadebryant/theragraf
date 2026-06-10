import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import { useMsal } from '@azure/msal-react';
import {
  makeStyles,
  tokens,
  Button,
  Text,
  Avatar,
  Tooltip,
} from '@fluentui/react-components';
import {
  Add24Regular,
  Grid24Regular,
  SignOut24Regular,
} from '@fluentui/react-icons';

const useStyles = makeStyles({
  shell: {
    display: 'flex',
    flexDirection: 'column',
    minHeight: '100vh',
    backgroundColor: tokens.colorNeutralBackground2,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: `${tokens.spacingVerticalM} ${tokens.spacingHorizontalXL}`,
    backgroundColor: tokens.colorBrandBackground,
    boxShadow: tokens.shadow4,
    flexShrink: 0,
  },
  brand: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    cursor: 'pointer',
  },
  brandName: {
    color: tokens.colorNeutralForegroundOnBrand,
    fontSize: tokens.fontSizeBase500,
    fontWeight: tokens.fontWeightSemibold,
  },
  nav: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  userArea: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
  },
  userName: {
    color: tokens.colorNeutralForegroundOnBrand,
    fontSize: tokens.fontSizeBase300,
  },
  main: {
    flex: 1,
    padding: `${tokens.spacingVerticalXL} ${tokens.spacingHorizontalXL}`,
    maxWidth: '1280px',
    width: '100%',
    marginLeft: 'auto',
    marginRight: 'auto',
  },
});

export default function AppLayout() {
  const styles = useStyles();
  const navigate = useNavigate();
  const location = useLocation();
  const { instance, accounts } = useMsal();
  const account = accounts[0];

  function handleSignOut() {
    void instance.logoutRedirect({ account });
  }

  const isNewSession = location.pathname === '/sessions/new';

  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <div className={styles.brand} onClick={() => navigate('/')}>
          <Text className={styles.brandName}>TheraGraf</Text>
        </div>

        <nav className={styles.nav}>
          <Tooltip content="Dashboard" relationship="label">
            <Button
              appearance="subtle"
              icon={<Grid24Regular />}
              style={{ color: 'white' }}
              onClick={() => navigate('/')}
            />
          </Tooltip>
          {!isNewSession && (
            <Button
              appearance="secondary"
              icon={<Add24Regular />}
              onClick={() => navigate('/sessions/new')}
            >
              New Session
            </Button>
          )}
        </nav>

        <div className={styles.userArea}>
          <Text className={styles.userName}>{account?.name ?? account?.username}</Text>
          <Avatar
            name={account?.name ?? account?.username}
            size={32}
            color="brand"
          />
          <Tooltip content="Sign out" relationship="label">
            <Button
              appearance="subtle"
              icon={<SignOut24Regular />}
              style={{ color: 'white' }}
              onClick={handleSignOut}
            />
          </Tooltip>
        </div>
      </header>

      <main className={styles.main}>
        <Outlet />
      </main>
    </div>
  );
}
