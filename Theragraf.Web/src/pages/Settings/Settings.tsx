import { useState, useEffect } from 'react';
import {
  makeStyles,
  tokens,
  Text,
  Button,
  Switch,
  Select,
  Input,
  Label,
  Card,
  Divider,
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
  Spinner,
} from '@fluentui/react-components';
import {
  Settings24Regular,
  CheckmarkCircle24Filled,
  DismissCircle24Filled,
} from '@fluentui/react-icons';
import { useSettings } from '@/hooks/useSettings';
import type {
  Theme,
  DefaultView,
  DateFormat,
  TimeFormat,
  ChartType,
  DisplaySettings,
  DocumentationDefaults,
  NotificationSettings,
  AccessibilitySettings,
  PrivacySettings,
  TherapyDiscipline,
  ClinicalSetting,
  PayerType,
  NoteFormat,
} from '@/types';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXL,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalM,
  },
  title: {
    fontSize: tokens.fontSizeBase500,
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorNeutralForeground1,
  },
  section: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  sectionTitle: {
    fontSize: tokens.fontSizeBase400,
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorNeutralForeground1,
    marginBottom: tokens.spacingVerticalS,
  },
  sectionDescription: {
    fontSize: tokens.fontSizeBase300,
    color: tokens.colorNeutralForeground3,
    marginBottom: tokens.spacingVerticalM,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))',
    gap: tokens.spacingVerticalL,
  },
  field: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  fieldRow: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: tokens.spacingHorizontalM,
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    marginTop: tokens.spacingVerticalXL,
    paddingTop: tokens.spacingVerticalL,
    borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
  },
});

export default function Settings() {
  const styles = useStyles();
  const { settings, updateSettings, resetSettings } = useSettings();

  // Local state for editing
  const [display, setDisplay] = useState<DisplaySettings>(settings.display);
  const [documentation, setDocumentation] = useState<DocumentationDefaults>(settings.documentation);
  const [notifications, setNotifications] = useState<NotificationSettings>(settings.notifications);
  const [accessibility, setAccessibility] = useState<AccessibilitySettings>(settings.accessibility);
  const [privacy, setPrivacy] = useState<PrivacySettings>(settings.privacy);

  const [saveStatus, setSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle');
  const [hasChanges, setHasChanges] = useState(false);

  // Track changes
  useEffect(() => {
    const changed = JSON.stringify({ display, documentation, notifications, accessibility, privacy }) 
      !== JSON.stringify({ 
        display: settings.display, 
        documentation: settings.documentation,
        notifications: settings.notifications,
        accessibility: settings.accessibility,
        privacy: settings.privacy,
      });
    setHasChanges(changed);
  }, [display, documentation, notifications, accessibility, privacy, settings]);

  const handleSave = () => {
    setSaveStatus('saving');
    try {
      updateSettings({ display, documentation, notifications, accessibility, privacy });
      setSaveStatus('saved');
      setHasChanges(false);
      setTimeout(() => setSaveStatus('idle'), 3000);
    } catch (err) {
      console.error('Failed to save settings:', err);
      setSaveStatus('error');
      setTimeout(() => setSaveStatus('idle'), 5000);
    }
  };

  const handleReset = () => {
    if (confirm('Are you sure you want to reset all settings to defaults? This cannot be undone.')) {
      resetSettings();
      // Reload from defaults
      window.location.reload();
    }
  };

  const handleCancel = () => {
    setDisplay(settings.display);
    setDocumentation(settings.documentation);
    setNotifications(settings.notifications);
    setAccessibility(settings.accessibility);
    setPrivacy(settings.privacy);
    setHasChanges(false);
  };

  return (
    <div className={styles.container}>
      {/* Header */}
      <div className={styles.header}>
        <Settings24Regular />
        <Text className={styles.title}>Settings</Text>
      </div>

      {saveStatus === 'saved' && (
        <MessageBar intent="success">
          <MessageBarBody>
            <MessageBarTitle>Settings saved successfully</MessageBarTitle>
          </MessageBarBody>
        </MessageBar>
      )}

      {saveStatus === 'error' && (
        <MessageBar intent="error">
          <MessageBarBody>
            <MessageBarTitle>Failed to save settings</MessageBarTitle>
            Please try again or contact support if the problem persists.
          </MessageBarBody>
        </MessageBar>
      )}

      {/* Display & Appearance */}
      <Card className={styles.section}>
        <Text className={styles.sectionTitle}>Display & Appearance</Text>
        <Text className={styles.sectionDescription}>
          Customize how Theragraf looks and feels for you.
        </Text>
        <div className={styles.grid}>
          <div className={styles.field}>
            <Label htmlFor="theme">Theme</Label>
            <Select
              id="theme"
              value={display.theme}
              onChange={(_, data) => setDisplay({ ...display, theme: data.value as Theme })}
            >
              <option value="light">Light</option>
              <option value="dark">Dark</option>
              <option value="system">System Default</option>
            </Select>
          </div>

          <div className={styles.field}>
            <Label htmlFor="defaultView">Default Dashboard View</Label>
            <Select
              id="defaultView"
              value={display.defaultView}
              onChange={(_, data) => setDisplay({ ...display, defaultView: data.value as DefaultView })}
            >
              <option value="caseload">Caseload</option>
              <option value="stats">Statistics</option>
            </Select>
          </div>

          <div className={styles.field}>
            <Label htmlFor="pageSize">Caseload Page Size</Label>
            <Select
              id="pageSize"
              value={String(display.caseloadPageSize)}
              onChange={(_, data) => setDisplay({ ...display, caseloadPageSize: Number(data.value) as 10 | 25 | 50 | 100 })}
            >
              <option value="10">10 clients</option>
              <option value="25">25 clients</option>
              <option value="50">50 clients</option>
              <option value="100">100 clients</option>
            </Select>
          </div>

          <div className={styles.field}>
            <Label htmlFor="dateFormat">Date Format</Label>
            <Select
              id="dateFormat"
              value={display.dateFormat}
              onChange={(_, data) => setDisplay({ ...display, dateFormat: data.value as DateFormat })}
            >
              <option value="MM/DD/YYYY">MM/DD/YYYY (US)</option>
              <option value="DD/MM/YYYY">DD/MM/YYYY (EU)</option>
              <option value="YYYY-MM-DD">YYYY-MM-DD (ISO)</option>
            </Select>
          </div>

          <div className={styles.field}>
            <Label htmlFor="timeFormat">Time Format</Label>
            <Select
              id="timeFormat"
              value={display.timeFormat}
              onChange={(_, data) => setDisplay({ ...display, timeFormat: data.value as TimeFormat })}
            >
              <option value="12h">12-hour (AM/PM)</option>
              <option value="24h">24-hour</option>
            </Select>
          </div>

          <div className={styles.field}>
            <Label htmlFor="chartType">Preferred Chart Type</Label>
            <Select
              id="chartType"
              value={display.preferredChartType}
              onChange={(_, data) => setDisplay({ ...display, preferredChartType: data.value as ChartType })}
            >
              <option value="bar">Bar</option>
              <option value="line">Line</option>
              <option value="area">Area</option>
            </Select>
          </div>
        </div>

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="showSynthetic">Show Synthetic Data by Default</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Display AI-generated practice data on dashboard
            </Text>
          </div>
          <Switch
            id="showSynthetic"
            checked={display.showSyntheticDataByDefault}
            onChange={(_, data) => setDisplay({ ...display, showSyntheticDataByDefault: data.checked })}
          />
        </div>
      </Card>

      {/* Documentation Defaults */}
      <Card className={styles.section}>
        <Text className={styles.sectionTitle}>Documentation Defaults</Text>
        <Text className={styles.sectionDescription}>
          Set default values for new session documentation.
        </Text>
        <div className={styles.grid}>
          <div className={styles.field}>
            <Label htmlFor="defaultDiscipline">Default Discipline</Label>
            <Select
              id="defaultDiscipline"
              value={documentation.defaultDiscipline}
              onChange={(_, data) => setDocumentation({ ...documentation, defaultDiscipline: data.value as TherapyDiscipline | '' })}
            >
              <option value="">None</option>
              <option value="OccupationalTherapy">Occupational Therapy</option>
              <option value="PhysicalTherapy">Physical Therapy</option>
              <option value="SpeechLanguagePathology">Speech-Language Pathology</option>
              <option value="Psychotherapy">Psychotherapy</option>
            </Select>
          </div>

          <div className={styles.field}>
            <Label htmlFor="defaultSetting">Default Setting</Label>
            <Select
              id="defaultSetting"
              value={documentation.defaultSetting}
              onChange={(_, data) => setDocumentation({ ...documentation, defaultSetting: data.value as ClinicalSetting | '' })}
            >
              <option value="">None</option>
              <option value="Outpatient">Outpatient</option>
              <option value="Inpatient">Inpatient</option>
              <option value="SkilledNursingFacility">Skilled Nursing Facility</option>
              <option value="HomeHealth">Home Health</option>
              <option value="SchoolBased">School-Based</option>
              <option value="EarlyIntervention">Early Intervention</option>
              <option value="Telehealth">Telehealth</option>
            </Select>
          </div>

          <div className={styles.field}>
            <Label htmlFor="defaultPayer">Default Payer</Label>
            <Select
              id="defaultPayer"
              value={documentation.defaultPayer}
              onChange={(_, data) => setDocumentation({ ...documentation, defaultPayer: data.value as PayerType | '' })}
            >
              <option value="">None</option>
              <option value="Medicare">Medicare</option>
              <option value="MedicareAdvantage">Medicare Advantage</option>
              <option value="Medicaid">Medicaid</option>
              <option value="Commercial">Commercial</option>
              <option value="WorkersCompensation">Workers' Compensation</option>
              <option value="SelfPay">Self-Pay</option>
              <option value="SchoolDistrict">School District</option>
            </Select>
          </div>

          <div className={styles.field}>
            <Label htmlFor="sessionDuration">Default Session Duration (minutes)</Label>
            <Input
              id="sessionDuration"
              type="number"
              min={15}
              max={240}
              step={15}
              value={String(documentation.defaultSessionDuration)}
              onChange={(_, data) => setDocumentation({ ...documentation, defaultSessionDuration: Number(data.value) })}
            />
          </div>

          <div className={styles.field}>
            <Label htmlFor="noteFormat">Default Note Format</Label>
            <Select
              id="noteFormat"
              value={documentation.defaultNoteFormat}
              onChange={(_, data) => setDocumentation({ ...documentation, defaultNoteFormat: data.value as NoteFormat })}
            >
              <option value="Soap">SOAP</option>
              <option value="Dap">DAP</option>
            </Select>
          </div>
        </div>

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="autoFill">Auto-fill Last Used Values</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Automatically populate fields with your most recent selections
            </Text>
          </div>
          <Switch
            id="autoFill"
            checked={documentation.autoFillLastUsedValues}
            onChange={(_, data) => setDocumentation({ ...documentation, autoFillLastUsedValues: data.checked })}
          />
        </div>
      </Card>

      {/* Notifications & Reminders */}
      <Card className={styles.section}>
        <Text className={styles.sectionTitle}>Notifications & Reminders</Text>
        <Text className={styles.sectionDescription}>
          Manage how Theragraf notifies you about important events.
        </Text>

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="sessionReminders">Session Reminders</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Notify me about sessions with missing documentation
            </Text>
          </div>
          <Switch
            id="sessionReminders"
            checked={notifications.enableSessionReminders}
            onChange={(_, data) => setNotifications({ ...notifications, enableSessionReminders: data.checked })}
          />
        </div>

        {notifications.enableSessionReminders && (
          <div className={styles.field}>
            <Label htmlFor="reminderDays">Reminder Threshold (days)</Label>
            <Input
              id="reminderDays"
              type="number"
              min={1}
              max={30}
              value={String(notifications.reminderThresholdDays)}
              onChange={(_, data) => setNotifications({ ...notifications, reminderThresholdDays: Number(data.value) })}
            />
          </div>
        )}

        <Divider />

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="notifyApprovalRequired">Approval Required Notifications</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Notify me when my notes need approval
            </Text>
          </div>
          <Switch
            id="notifyApprovalRequired"
            checked={notifications.notifyOnApprovalRequired}
            onChange={(_, data) => setNotifications({ ...notifications, notifyOnApprovalRequired: data.checked })}
          />
        </div>

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="notifyApprovalReceived">Approval Received Notifications</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Notify me when my notes are approved
            </Text>
          </div>
          <Switch
            id="notifyApprovalReceived"
            checked={notifications.notifyOnApprovalReceived}
            onChange={(_, data) => setNotifications({ ...notifications, notifyOnApprovalReceived: data.checked })}
          />
        </div>

        <Divider />

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="goalWarning">Goal Expiration Warning</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Warn me when client goals are approaching expiration
            </Text>
          </div>
          <Switch
            id="goalWarning"
            checked={notifications.goalExpirationWarning}
            onChange={(_, data) => setNotifications({ ...notifications, goalExpirationWarning: data.checked })}
          />
        </div>

        {notifications.goalExpirationWarning && (
          <div className={styles.field}>
            <Label htmlFor="goalWarningDays">Warning Threshold (days)</Label>
            <Input
              id="goalWarningDays"
              type="number"
              min={7}
              max={90}
              value={String(notifications.goalExpirationWarningDays)}
              onChange={(_, data) => setNotifications({ ...notifications, goalExpirationWarningDays: Number(data.value) })}
            />
          </div>
        )}

        <Divider />

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="browserNotifications">Browser Notifications</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Show desktop notifications when the app is in the background
            </Text>
          </div>
          <Switch
            id="browserNotifications"
            checked={notifications.enableBrowserNotifications}
            onChange={(_, data) => setNotifications({ ...notifications, enableBrowserNotifications: data.checked })}
          />
        </div>

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="notificationSound">Notification Sound</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Play sound for notifications
            </Text>
          </div>
          <Switch
            id="notificationSound"
            checked={notifications.notificationSound}
            onChange={(_, data) => setNotifications({ ...notifications, notificationSound: data.checked })}
          />
        </div>
      </Card>

      {/* Accessibility */}
      <Card className={styles.section}>
        <Text className={styles.sectionTitle}>Accessibility</Text>
        <Text className={styles.sectionDescription}>
          Configure options to improve usability and accessibility.
        </Text>

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="screenReader">Screen Reader Optimization</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Enhance experience for screen reader users
            </Text>
          </div>
          <Switch
            id="screenReader"
            checked={accessibility.screenReaderOptimized}
            onChange={(_, data) => setAccessibility({ ...accessibility, screenReaderOptimized: data.checked })}
          />
        </div>

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="highContrast">High Contrast Mode</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Increase contrast for better visibility
            </Text>
          </div>
          <Switch
            id="highContrast"
            checked={accessibility.highContrastMode}
            onChange={(_, data) => setAccessibility({ ...accessibility, highContrastMode: data.checked })}
          />
        </div>

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="largeText">Large Text</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Increase base font size throughout the app
            </Text>
          </div>
          <Switch
            id="largeText"
            checked={accessibility.largeText}
            onChange={(_, data) => setAccessibility({ ...accessibility, largeText: data.checked })}
          />
        </div>

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="reducedMotion">Reduced Motion</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Minimize animations and transitions
            </Text>
          </div>
          <Switch
            id="reducedMotion"
            checked={accessibility.reducedMotion}
            onChange={(_, data) => setAccessibility({ ...accessibility, reducedMotion: data.checked })}
          />
        </div>

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="keyboardHints">Keyboard Navigation Hints</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Show visual indicators for keyboard-focused elements
            </Text>
          </div>
          <Switch
            id="keyboardHints"
            checked={accessibility.keyboardNavigationHints}
            onChange={(_, data) => setAccessibility({ ...accessibility, keyboardNavigationHints: data.checked })}
          />
        </div>

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="keyboardShortcuts">Show Keyboard Shortcuts</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Display keyboard shortcuts in tooltips and menus
            </Text>
          </div>
          <Switch
            id="keyboardShortcuts"
            checked={accessibility.showKeyboardShortcuts}
            onChange={(_, data) => setAccessibility({ ...accessibility, showKeyboardShortcuts: data.checked })}
          />
        </div>
      </Card>

      {/* Privacy & Data */}
      <Card className={styles.section}>
        <Text className={styles.sectionTitle}>Privacy & Data</Text>
        <Text className={styles.sectionDescription}>
          Manage how your data is stored and when you're automatically logged out.
        </Text>

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="offlineCache">Enable Offline Cache</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Cache data locally for faster loading (HIPAA-compliant encryption)
            </Text>
          </div>
          <Switch
            id="offlineCache"
            checked={privacy.enableOfflineCache}
            onChange={(_, data) => setPrivacy({ ...privacy, enableOfflineCache: data.checked })}
          />
        </div>

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="clearCache">Clear Cache on Logout</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Automatically clear local cache when you log out
            </Text>
          </div>
          <Switch
            id="clearCache"
            checked={privacy.clearCacheOnLogout}
            onChange={(_, data) => setPrivacy({ ...privacy, clearCacheOnLogout: data.checked })}
          />
        </div>

        <div className={styles.field}>
          <Label htmlFor="autoLogout">Auto-logout After Inactivity (minutes)</Label>
          <Input
            id="autoLogout"
            type="number"
            min={5}
            max={120}
            step={5}
            value={String(privacy.autoLogoutMinutes)}
            onChange={(_, data) => setPrivacy({ ...privacy, autoLogoutMinutes: Number(data.value) })}
          />
        </div>

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="confirmDelete">Confirm Before Delete</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Require confirmation before deleting items
            </Text>
          </div>
          <Switch
            id="confirmDelete"
            checked={privacy.confirmBeforeDelete}
            onChange={(_, data) => setPrivacy({ ...privacy, confirmBeforeDelete: data.checked })}
          />
        </div>

        <div className={styles.fieldRow}>
          <div>
            <Label htmlFor="auditLog">Show My Audit Log</Label>
            <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
              Display my activity history in session details
            </Text>
          </div>
          <Switch
            id="auditLog"
            checked={privacy.showMyAuditLog}
            onChange={(_, data) => setPrivacy({ ...privacy, showMyAuditLog: data.checked })}
          />
        </div>
      </Card>

      {/* Actions */}
      <div className={styles.actions}>
        <Button
          appearance="primary"
          disabled={!hasChanges || saveStatus === 'saving'}
          onClick={handleSave}
          icon={saveStatus === 'saving' ? <Spinner size="tiny" /> : saveStatus === 'saved' ? <CheckmarkCircle24Filled /> : undefined}
        >
          {saveStatus === 'saving' ? 'Saving...' : saveStatus === 'saved' ? 'Saved!' : 'Save Changes'}
        </Button>
        <Button
          appearance="secondary"
          disabled={!hasChanges || saveStatus === 'saving'}
          onClick={handleCancel}
        >
          Cancel
        </Button>
        <Button
          appearance="subtle"
          onClick={handleReset}
          icon={<DismissCircle24Filled />}
        >
          Reset to Defaults
        </Button>
      </div>
    </div>
  );
}
