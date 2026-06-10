// TypeScript models mirroring Theragraf.Core Models (camelCase JSON convention).

export type TherapyDiscipline =
  | 'OccupationalTherapy'
  | 'PhysicalTherapy'
  | 'Psychotherapy';

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
  setting: string;
  payer: string;
  sessionDurationMinutes: number | null;
  soapNote: SoapNote;
  suggestedCptCodes: CptCode[];
  suggestedIcdCodes: IcdCode[];
  createdAt: string;
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
}

/** Body for PATCH /api/sessions/{clientId}/{sessionDate}. */
export interface SessionUpdateRequest {
  soapNote?: SoapNoteUpdate;
  suggestedCptCodes?: CptCode[];
  suggestedIcdCodes?: IcdCode[];
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
    suggestedCptCodes: CptCode[];
    suggestedIcdCodes: IcdCode[];
  };
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
