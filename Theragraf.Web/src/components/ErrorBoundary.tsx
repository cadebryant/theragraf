import { Component, type ErrorInfo, type ReactNode } from 'react';
import { makeStyles, tokens, Button, Text } from '@fluentui/react-components';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: '60vh',
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingVerticalXL,
    textAlign: 'center',
  },
  title: {
    fontSize: tokens.fontSizeBase600,
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorStatusDangerForeground1,
  },
  message: {
    color: tokens.colorNeutralForeground2,
    maxWidth: '480px',
  },
});

function ErrorFallback({ onReset }: { onReset: () => void }) {
  const styles = useStyles();
  return (
    <div className={styles.container}>
      <Text className={styles.title}>Something went wrong</Text>
      <Text className={styles.message}>
        An unexpected error occurred. Your session data has not been affected.
        Please try refreshing the page. If the problem persists, sign out and
        sign back in.
      </Text>
      <Button appearance="primary" onClick={onReset}>
        Reload page
      </Button>
    </div>
  );
}

interface Props {
  children: ReactNode;
}

interface State {
  hasError: boolean;
}

/**
 * Catches unhandled React rendering errors and shows a safe fallback UI
 * instead of a blank screen or raw stack trace that could expose app internals.
 *
 * Must be a class component — React does not support error boundaries as
 * function components (getDerivedStateFromError / componentDidCatch hooks
 * are not available).
 */
export default class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // Log to the browser console only — never log error.message to any
    // remote service here in case it contains partial PHI from component state.
    console.error('[ErrorBoundary]', error, info.componentStack);
  }

  handleReset = () => {
    this.setState({ hasError: false });
    window.location.reload();
  };

  render() {
    if (this.state.hasError) {
      return <ErrorFallback onReset={this.handleReset} />;
    }
    return this.props.children;
  }
}
