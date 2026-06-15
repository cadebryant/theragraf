import { useState } from 'react';

// Incrementing the version suffix forces the modal to re-appear for all
// existing users whenever significant new content is added.
const STORAGE_KEY = 'theragraf:onboardingSeen:v2';

/**
 * Returns whether the getting-started modal should be shown and a `dismiss`
 * callback. Pass `alwaysShow: true` to skip writing the "seen" flag so the
 * modal re-appears on the next visit.
 */
export function useFirstVisit() {
  const [open, setOpen] = useState(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) !== 'true';
    } catch {
      // localStorage unavailable (private browsing strict mode, etc.)
      return false;
    }
  });

  function dismiss(alwaysShow: boolean) {
    if (!alwaysShow) {
      try {
        localStorage.setItem(STORAGE_KEY, 'true');
      } catch {
        // ignore
      }
    }
    setOpen(false);
  }

  return { open, dismiss };
}
