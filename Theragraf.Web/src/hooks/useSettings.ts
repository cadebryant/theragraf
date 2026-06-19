import { useCallback, useMemo } from 'react';
import type { UserSettings } from '@/types/settings';
import { DEFAULT_SETTINGS } from '@/types/settings';

const STORAGE_KEY = 'theragraf_user_settings';

interface UseSettingsReturn {
  settings: UserSettings;
  updateSettings: (partial: Partial<UserSettings>) => void;
  resetSettings: () => void;
}

/**
 * Hook to manage user settings stored in localStorage.
 * In the future, this can be extended to sync with the backend.
 */
export function useSettings(): UseSettingsReturn {
  const settings = useMemo<UserSettings>(() => {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) {
        const parsed = JSON.parse(stored) as UserSettings;
        // Merge with defaults to handle new settings added in updates
        return {
          display: { ...DEFAULT_SETTINGS.display, ...parsed.display },
          documentation: { ...DEFAULT_SETTINGS.documentation, ...parsed.documentation },
          notifications: { ...DEFAULT_SETTINGS.notifications, ...parsed.notifications },
          accessibility: { ...DEFAULT_SETTINGS.accessibility, ...parsed.accessibility },
          privacy: { ...DEFAULT_SETTINGS.privacy, ...parsed.privacy },
          version: parsed.version ?? DEFAULT_SETTINGS.version,
        };
      }
    } catch (err) {
      console.error('Failed to load user settings:', err);
    }
    return DEFAULT_SETTINGS;
  }, []);

  const updateSettings = useCallback((partial: Partial<UserSettings>) => {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      const current = stored ? (JSON.parse(stored) as UserSettings) : DEFAULT_SETTINGS;
      const updated: UserSettings = {
        ...current,
        ...partial,
        display: { ...current.display, ...partial.display },
        documentation: { ...current.documentation, ...partial.documentation },
        notifications: { ...current.notifications, ...partial.notifications },
        accessibility: { ...current.accessibility, ...partial.accessibility },
        privacy: { ...current.privacy, ...partial.privacy },
      };
      localStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
      // Trigger a storage event to notify other tabs/components
      window.dispatchEvent(new StorageEvent('storage', {
        key: STORAGE_KEY,
        newValue: JSON.stringify(updated),
      }));
    } catch (err) {
      console.error('Failed to save user settings:', err);
    }
  }, []);

  const resetSettings = useCallback(() => {
    try {
      localStorage.removeItem(STORAGE_KEY);
      window.dispatchEvent(new StorageEvent('storage', {
        key: STORAGE_KEY,
        newValue: null,
      }));
    } catch (err) {
      console.error('Failed to reset user settings:', err);
    }
  }, []);

  return { settings, updateSettings, resetSettings };
}
