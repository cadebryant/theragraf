import { useEffect, useRef, useCallback } from 'react';
import { useMsal } from '@azure/msal-react';

const IDLE_MS    = 15 * 60 * 1000; // 15 minutes — HIPAA automatic logoff requirement
const WARNING_MS =       60 * 1000; // Show warning 60 seconds before logout

interface IdleTimeoutOptions {
  /** Called when the idle warning window begins (IDLE_MS - WARNING_MS after last activity). */
  onWarning: () => void;
  /** Called when the warning is dismissed — resets the timer. */
  onReset: () => void;
}

/**
 * Attaches window-level activity listeners and schedules an automatic
 * logoutRedirect after IDLE_MS of inactivity, with a 60-second warning
 * callback fired before the logout occurs.
 *
 * The timer is automatically suspended while a speech recording is active
 * (signalled via the custom `theragraf:recording-start` / `theragraf:recording-stop`
 * window events dispatched by AudioRecorder) and resumes when recording ends.
 *
 * Mount this hook once in AppLayout so it covers all authenticated routes.
 */
export function useIdleTimeout({ onWarning, onReset }: IdleTimeoutOptions) {
  const { instance, accounts } = useMsal();

  const warningTimerRef  = useRef<ReturnType<typeof setTimeout> | null>(null);
  const logoutTimerRef   = useRef<ReturnType<typeof setTimeout> | null>(null);
  const isWarningActive  = useRef(false);
  const isRecording      = useRef(false);

  const clearTimers = useCallback(() => {
    if (warningTimerRef.current) clearTimeout(warningTimerRef.current);
    if (logoutTimerRef.current)  clearTimeout(logoutTimerRef.current);
  }, []);

  const scheduleLogout = useCallback(() => {
    // Never start the idle timer while a recording is in progress.
    if (isRecording.current) return;

    clearTimers();
    isWarningActive.current = false;

    warningTimerRef.current = setTimeout(() => {
      isWarningActive.current = true;
      onWarning();

      logoutTimerRef.current = setTimeout(() => {
        const account = accounts[0] ?? null;
        void instance.logoutRedirect({ account: account ?? undefined });
      }, WARNING_MS);
    }, IDLE_MS - WARNING_MS);
  }, [clearTimers, onWarning, instance, accounts]);

  // Allow AppLayout to reset the timer when the user dismisses the warning.
  const resetTimer = useCallback(() => {
    isWarningActive.current = false;
    onReset();
    scheduleLogout();
  }, [onReset, scheduleLogout]);

  useEffect(() => {
    // Only run when the user is actually authenticated.
    if (accounts.length === 0) return;

    const activityEvents: (keyof WindowEventMap)[] = [
      'mousemove', 'mousedown', 'keydown', 'touchstart', 'scroll',
    ];

    const handleActivity = () => {
      // Ignore activity while the warning is showing — user must explicitly
      // dismiss it rather than accidentally moving the mouse to reset.
      if (isWarningActive.current) return;
      scheduleLogout();
    };

    const handleRecordingStart = () => {
      isRecording.current = true;
      // Suspend the idle timer for the duration of the recording.
      clearTimers();
      isWarningActive.current = false;
    };

    const handleRecordingStop = () => {
      isRecording.current = false;
      // Resume the idle timer now that the recording has ended.
      scheduleLogout();
    };

    activityEvents.forEach(evt => window.addEventListener(evt, handleActivity, { passive: true }));
    window.addEventListener('theragraf:recording-start', handleRecordingStart);
    window.addEventListener('theragraf:recording-stop',  handleRecordingStop);
    scheduleLogout();

    return () => {
      clearTimers();
      activityEvents.forEach(evt => window.removeEventListener(evt, handleActivity));
      window.removeEventListener('theragraf:recording-start', handleRecordingStart);
      window.removeEventListener('theragraf:recording-stop',  handleRecordingStop);
    };
  }, [accounts.length, scheduleLogout, clearTimers]);

  return { resetTimer };
}
