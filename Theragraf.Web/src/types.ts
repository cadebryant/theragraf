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
