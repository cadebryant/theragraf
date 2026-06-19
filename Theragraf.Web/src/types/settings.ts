// User settings types for personalization and preferences

import type { TherapyDiscipline, ClinicalSetting, PayerType, NoteFormat } from '@/types';

// ── Display & Appearance ──────────────────────────────────────────────────────

export type Theme = 'light' | 'dark' | 'system';
export type DefaultView = 'caseload' | 'stats';
export type DateFormat = 'MM/DD/YYYY' | 'DD/MM/YYYY' | 'YYYY-MM-DD';
export type TimeFormat = '12h' | '24h';
export type ChartType = 'bar' | 'line' | 'area';

export interface DisplaySettings {
  theme: Theme;
  defaultView: DefaultView;
  caseloadPageSize: 10 | 25 | 50 | 100;
  dateFormat: DateFormat;
  timeFormat: TimeFormat;
  timezone: string;
  preferredChartType: ChartType;
  showSyntheticDataByDefault: boolean;
}

// ── Documentation Defaults ────────────────────────────────────────────────────

export interface DocumentationDefaults {
  defaultDiscipline: TherapyDiscipline | '';
  defaultSetting: ClinicalSetting | '';
  defaultPayer: PayerType | '';
  defaultSessionDuration: number; // minutes
  defaultNoteFormat: NoteFormat;
  autoFillLastUsedValues: boolean;
  favoriteCptCodes: string[];
  favoriteIcdCodes: string[];
}

// ── Notifications & Reminders ─────────────────────────────────────────────────

export interface NotificationSettings {
  enableSessionReminders: boolean;
  reminderThresholdDays: number;
  notifyOnApprovalRequired: boolean;
  notifyOnApprovalReceived: boolean;
  goalExpirationWarning: boolean;
  goalExpirationWarningDays: number;
  enableBrowserNotifications: boolean;
  notificationSound: boolean;
}

// ── Accessibility ─────────────────────────────────────────────────────────────

export interface AccessibilitySettings {
  screenReaderOptimized: boolean;
  highContrastMode: boolean;
  largeText: boolean;
  reducedMotion: boolean;
  keyboardNavigationHints: boolean;
  showKeyboardShortcuts: boolean;
}

// ── Privacy & Data ────────────────────────────────────────────────────────────

export interface PrivacySettings {
  enableOfflineCache: boolean;
  clearCacheOnLogout: boolean;
  autoLogoutMinutes: number;
  confirmBeforeDelete: boolean;
  showMyAuditLog: boolean;
}

// ── Combined User Settings ────────────────────────────────────────────────────

export interface UserSettings {
  display: DisplaySettings;
  documentation: DocumentationDefaults;
  notifications: NotificationSettings;
  accessibility: AccessibilitySettings;
  privacy: PrivacySettings;
  version: number; // For migration purposes
}

// ── Default Settings ──────────────────────────────────────────────────────────

export const DEFAULT_SETTINGS: UserSettings = {
  display: {
    theme: 'system',
    defaultView: 'caseload',
    caseloadPageSize: 25,
    dateFormat: 'MM/DD/YYYY',
    timeFormat: '12h',
    timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
    preferredChartType: 'bar',
    showSyntheticDataByDefault: false,
  },
  documentation: {
    defaultDiscipline: '',
    defaultSetting: '',
    defaultPayer: '',
    defaultSessionDuration: 45,
    defaultNoteFormat: 'Soap',
    autoFillLastUsedValues: true,
    favoriteCptCodes: [],
    favoriteIcdCodes: [],
  },
  notifications: {
    enableSessionReminders: true,
    reminderThresholdDays: 7,
    notifyOnApprovalRequired: true,
    notifyOnApprovalReceived: true,
    goalExpirationWarning: true,
    goalExpirationWarningDays: 30,
    enableBrowserNotifications: false,
    notificationSound: true,
  },
  accessibility: {
    screenReaderOptimized: false,
    highContrastMode: false,
    largeText: false,
    reducedMotion: false,
    keyboardNavigationHints: false,
    showKeyboardShortcuts: true,
  },
  privacy: {
    enableOfflineCache: true,
    clearCacheOnLogout: true,
    autoLogoutMinutes: 30,
    confirmBeforeDelete: true,
    showMyAuditLog: false,
  },
  version: 1,
};
