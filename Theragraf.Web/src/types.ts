// TypeScript models mirroring Theragraf.Core Models (camelCase JSON convention).

export type TherapyDiscipline =
  | 'OccupationalTherapy'
  | 'PhysicalTherapy'
  | 'SpeechLanguagePathology'
  | 'Psychotherapy';

export type NoteFormat = 'Soap' | 'Dap';

export type ClinicalSetting =
  | 'Outpatient'
  | 'Inpatient'
  | 'SkilledNursingFacility'
  | 'HomeHealth'
  | 'SchoolBased'
  | 'EarlyIntervention'
  | 'Telehealth';

export type PayerType =
  | 'Medicare'
  | 'MedicareAdvantage'
  | 'Medicaid'
  | 'Commercial'
  | 'WorkersCompensation'
  | 'SelfPay'
  | 'SchoolDistrict';

// ── SOAP / Codes ─────────────────────────────────────────────────────────────

export interface SoapNote {
  subjective: string;
  objective: string;
  assessment: string;
  plan: string;
}

export interface SoapNoteUpdate {
  subjective?: string;
  objective?: string;
  assessment?: string;
  plan?: string;
}

export interface CptCode {
  code: string;
  description: string;
  rationale: string;
  billableUnits: number;
}

export interface IcdCode {
  code: string;
  description: string;
  rationale: string;
}

// ── Session ───────────────────────────────────────────────────────────────────

/** Returned by GET /api/sessions/{clientId}/{sessionDate} and list endpoints. */
export interface SessionResponse {
  clientId: string;
  /** yyyy-MM-ddTHH-mm-ssZ row-key format, e.g. "2025-06-15T14-30-00Z" */
  sessionDate: string;
  therapistName: string;
  discipline: string;
  noteFormat: string;
  setting: string;
  payer: string;
  sessionDurationMinutes: number | null;
  soapNote: SoapNote;
  suggestedCptCodes: CptCode[];
  suggestedIcdCodes: IcdCode[];
  createdAt: string;
  // Therapist approval metadata (backend persisted)
  isApproved?: boolean;
  approvedBy?: string | null;
  approvedAt?: string | null;
  // Synthetic/demo data flag
  isSynthetic?: boolean;
  // Soft-delete metadata for HIPAA data retention
  isDeleted?: boolean;
  deletedAt?: string | null;
  deletedBy?: string | null;
}

/** Sent to POST /api/DocumentationStart. */
export interface TranscriptInput {
  rawTranscript: string;
  therapistName: string;
  clientId: string;
  /** ISO 8601 date-time string, e.g. "2025-06-15T14:30:00Z" */
  sessionDate: string;
  discipline?: TherapyDiscipline;
  sessionDurationMinutes?: number;
  setting?: ClinicalSetting;
  payer?: PayerType;
  noteFormat?: NoteFormat;
  /** Optional non-PII demographics context to improve ICD-10 suggestions. */
  demographics?: ClientDemographicsSummary;
}

/** Body for PATCH /api/sessions/{clientId}/{sessionDate}. */
export interface SessionUpdateRequest {
  soapNote?: SoapNoteUpdate;
  suggestedCptCodes?: CptCode[];
  suggestedIcdCodes?: IcdCode[];
  /** Optional approval action to persist therapist approval state. */
  approval?: ApprovalUpdate;
}

/** Sent to session update endpoint when applying/clearing therapist approval. */
export interface ApprovalUpdate {
  /** Set to true to mark approved. Backend requires this exact property name. */
  verifyAndApprove: boolean;
  /** Optional - backend will populate from JWT if not provided. */
  approvedBy?: string;
}

// ── Caseload / Pagination ─────────────────────────────────────────────────────

export interface ClientSummary {
  clientId: string;
  lastSessionDate: string | null;
  totalSessions: number;
  isSynthetic: boolean;
}

export interface CaseloadSummary {
  therapistName: string;
  clients: ClientSummary[];
}

export interface PagedResult<T> {
  items: T[];
  pageSize: number;
  hasMore: boolean;
  continuationToken: string | null;
}

// ── Stats ─────────────────────────────────────────────────────────────────────

export interface CodeFrequency {
  code: string;
  description: string;
  count: number;
  totalBillableUnits: number;
}

export interface TherapistStats {
  therapistName: string;
  totalSessions: number;
  totalClients: number;
  averageSessionDurationMinutes: number;
  totalBillableUnits: number;
  sessionsByDiscipline: Record<string, number>;
  sessionsBySetting: Record<string, number>;
  sessionsByPayer: Record<string, number>;
  topCptCodes: CodeFrequency[];
  topIcdCodes: CodeFrequency[];
}

export interface ClientStats {
  clientId: string;
  totalSessions: number;
  averageSessionDurationMinutes: number;
  totalBillableUnits: number;
  firstSessionDate: string | null;
  lastSessionDate: string | null;
  sessionsByTherapist: Record<string, number>;
  sessionsByDiscipline: Record<string, number>;
  sessionsBySetting: Record<string, number>;
  sessionsByPayer: Record<string, number>;
  topCptCodes: CodeFrequency[];
  topIcdCodes: CodeFrequency[];
  isSynthetic: boolean;
}

// ── Orchestration ─────────────────────────────────────────────────────────────

export interface OrchestrationStartResponse {
  instanceId: string;
  /** The namespaced clientId the server stored the session under. Always use this for subsequent API calls. */
  clientId: string;
  statusQueryGetUri: string;
  sendEventPostUri: string;
  terminatePostUri: string;
  purgeHistoryDeleteUri: string;
}

export type RuntimeStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Terminated';

export interface OrchestrationStatus {
  instanceId: string;
  runtimeStatus: RuntimeStatus;
  /** Populated once runtimeStatus is "Completed". */
  output?: {
    restoredNote: SoapNote;
    noteFormat: string;
    suggestedCptCodes: CptCode[];
    suggestedIcdCodes: IcdCode[];
  };
}

// ── Goals ─────────────────────────────────────────────────────────────────────

export type GoalStatus = 'Active' | 'Met' | 'Discontinued' | 'NotMet';

export interface GoalProgressNote {
  noteId: string;
  recordedAt: string;
  note: string;
}

export interface GoalResponse {
  goalId: string;
  clientId: string;
  title: string;
  description: string;
  status: GoalStatus;
  createdAt: string;
  targetDate: string | null;
  resolvedAt: string | null;
  progressNotes: GoalProgressNote[];
  isSynthetic?: boolean;
  // Soft-delete metadata for HIPAA data retention
  isDeleted?: boolean;
  deletedAt?: string | null;
  deletedBy?: string | null;
}

export interface CreateGoalRequest {
  title: string;
  description: string;
  targetDate?: string;
}

export interface UpdateGoalRequest {
  title?: string;
  description?: string;
  status?: GoalStatus;
  targetDate?: string;
  progressNote?: string;
}

export interface GoalSuggestion {
  title: string;
  description: string;
}

export interface GoalSuggestRequest {
  soapNote: SoapNote;
  discipline: string;
}

// ── Goal Stats ────────────────────────────────────────────────────────────────

/** Goal progress breakdown for a single client. Returned by GET /api/goals/stats/client/{clientId}. */
export interface ClientGoalStats {
  clientId: string;
  totalGoals: number;
  activeGoals: number;
  metGoals: number;
  notMetGoals: number;
  discontinuedGoals: number;
  overdueGoals: number;
  metRate: number;
  isSynthetic: boolean;
}

/** Goal progress breakdown aggregated across all clients for a therapist. Returned by GET /api/goals/stats/therapist/{therapistName}. */
export interface TherapistGoalStats {
  therapistName: string;
  totalGoals: number;
  activeGoals: number;
  metGoals: number;
  notMetGoals: number;
  discontinuedGoals: number;
  overdueGoals: number;
  clientsWithGoals: number;
  metRate: number;
}

// ── Client Demographics ───────────────────────────────────────────────────────

export type BiologicalSex = 'NotSpecified' | 'Male' | 'Female' | 'Other';

/** Non-PII summary forwarded into the documentation pipeline for better ICD-10 coding. */
export interface ClientDemographicsSummary {
  ageYears: number | null;
  sex: BiologicalSex;
  priorDiagnoses: string | null;
  functionalLimitations: string | null;
}

/** Returned by GET /api/clients/{clientId}. DOB is never returned. */
export interface ClientDemographicsResponse {
  clientId: string;
  ageYears: number | null;
  sex: BiologicalSex;
  priorDiagnoses: string | null;
  functionalLimitations: string | null;
  updatedAt: string;
  isSynthetic?: boolean;
}

/** Body for PUT /api/clients/{clientId}. */
export interface UpsertClientDemographicsRequest {
  /** ISO 8601 date, e.g. "1985-04-12". Send null to clear; omit to leave unchanged. */
  dateOfBirth?: string | null;
  sex: BiologicalSex;
  priorDiagnoses?: string | null;
  functionalLimitations?: string | null;
}

// ── Speech ────────────────────────────────────────────────────────────────────

export interface SpeechTokenResponse {
  token: string;
  region: string;
}

/** A single diarized segment from the Azure Speech ConversationTranscriber. */
export interface DiarizedSegment {
  speakerId: string;
  text: string;
  isFinal: boolean;
}

// ── Multi-tenancy ─────────────────────────────────────────────────────────────

export type TenantOrganizationType =
  | 'SoloPractitioner'
  | 'GroupPractice'
  | 'AcademicProgram'
  | 'Other';

export type TenantPlan = 'Free' | 'Professional' | 'Academic';

export type TenantStatus = 'Active' | 'Suspended' | 'Deprovisioned';

/** Returned by GET /api/tenant. */
export interface TenantSummaryResponse {
  tenantId: string;
  organizationName: string;
  organizationType: TenantOrganizationType;
  plan: TenantPlan;
  /** AI calls consumed in the current billing period. */
  aiCallsThisPeriod: number;
  /** Maximum AI calls per billing period. null means unlimited. */
  monthlyAiCallQuota: number | null;
  status: TenantStatus;
  /** True when synthesised from config (self-hosted/BYOA), not from Cosmos. */
  isSynthetic: boolean;
}

/** Returned by GET /api/providers/{providerId}. */
export interface ProviderResponse {
  providerId: string;
  tenantId: string;
  practiceName: string;
  organizationNpi: string | null;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  state: string | null;
  zip: string | null;
  phone: string | null;
  createdAt: string;
  updatedAt: string;
}

/** Returned by GET /api/therapists/me. */
export interface TherapistProfileResponse {
  therapistId: string;
  tenantId: string;
  firstName: string;
  lastName: string;
  credentials: string | null;
  discipline: TherapyDiscipline;
  individualNpi: string | null;
  /** FK to ProviderResponse.providerId when part of a group practice. */
  providerId: string | null;
  /**
   * True when the profile was explicitly saved by the therapist.
   * False means it was auto-created from JWT claims — prompt profile setup.
   */
  isConfigured: boolean;
  createdAt: string;
  updatedAt: string;
}

/** Body for PATCH /api/therapists/me. All fields optional. */
export interface TherapistProfileUpdateRequest {
  firstName?: string;
  lastName?: string;
  credentials?: string;
  discipline?: TherapyDiscipline;
  /** 10-digit NPI. Send null to clear. */
  individualNpi?: string | null;
}

// ── User Settings ─────────────────────────────────────────────────────────────

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

export interface AccessibilitySettings {
  screenReaderOptimized: boolean;
  highContrastMode: boolean;
  largeText: boolean;
  reducedMotion: boolean;
  keyboardNavigationHints: boolean;
  showKeyboardShortcuts: boolean;
}

export interface PrivacySettings {
  enableOfflineCache: boolean;
  clearCacheOnLogout: boolean;
  autoLogoutMinutes: number;
  confirmBeforeDelete: boolean;
  showMyAuditLog: boolean;
}

export interface UserSettings {
  display: DisplaySettings;
  documentation: DocumentationDefaults;
  notifications: NotificationSettings;
  accessibility: AccessibilitySettings;
  privacy: PrivacySettings;
  version: number; // For migration purposes
}

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
