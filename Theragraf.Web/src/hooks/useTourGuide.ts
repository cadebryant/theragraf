import { useState, useCallback } from 'react';

const STORAGE_KEY = 'theragraf:tourCompleted:v1';

/**
 * Manages the product tour state with localStorage persistence.
 * Returns whether the tour should run and functions to control it.
 */
export function useTourGuide() {
  const [run, setRun] = useState(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) !== 'true';
    } catch {
      // localStorage unavailable (private browsing, etc.)
      return false;
    }
  });

  const startTour = useCallback(() => {
    setRun(true);
  }, []);

  const completeTour = useCallback(() => {
    try {
      localStorage.setItem(STORAGE_KEY, 'true');
    } catch {
      // ignore storage errors
    }
    setRun(false);
  }, []);

  const resetTour = useCallback(() => {
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // ignore storage errors
    }
    setRun(true);
  }, []);

  return {
    run,
    startTour,
    completeTour,
    resetTour,
  };
}
