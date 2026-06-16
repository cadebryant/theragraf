import { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useMsal } from '@azure/msal-react';
import { useQuery } from '@tanstack/react-query';
import type { AccountInfo } from '@azure/msal-browser';
import {
  makeStyles,
  tokens,
  Button,
  Text,
  Divider,
  MessageBar,
  MessageBarBody,
  Spinner,
} from '@fluentui/react-components';
import { DocumentBulletList24Regular } from '@fluentui/react-icons';
import SessionMetadataForm, { type SessionMetadata } from './SessionMetadataForm';
import { stripClientIdPrefix } from '@/utils/clientId';
import AudioRecorder from './AudioRecorder';
import { startDocumentation, toSessionDateKey } from '@/api/sessions';
import { getClientDemographics } from '@/api/clients';
import type { TranscriptInput } from '@/types';

const useStyles = makeStyles({
  page: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXL,
  },
  title: {
    fontSize: tokens.fontSizeBase600,
    fontWeight: tokens.fontWeightSemibold,
  },
  transcript: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
    fontFamily: 'monospace',
    fontSize: tokens.fontSizeBase200,
    whiteSpace: 'pre-wrap',
    maxHeight: '200px',
    overflowY: 'auto',
    color: tokens.colorNeutralForeground2,
  },
  actions: {
    display: 'flex',
    justifyContent: 'flex-end',
    gap: tokens.spacingHorizontalM,
  },
});

function defaultDatetimeLocal() {
  const now = new Date();
  now.setSeconds(0, 0);
  return now.toISOString().slice(0, 16);
}

/**
 * Extracts individual word tokens from the client ID and the therapist's display
 * name / username so the speech recogniser can bias toward those specific words.
 * e.g. clientId="Cade-02" + name="Uche Obi" → ["Cade", "Uche", "Obi"]
 */
function buildPhraseHints(clientId: string, account: AccountInfo | undefined): string[] {
  const tokens = new Set<string>();

  // Split client ID on common separators and add each word-like token
  clientId
    .split(/[-_\s.]+/)
    .map((t) => t.trim())
    .filter((t) => t.length > 1 && /[a-zA-Z]/.test(t))
    .forEach((t) => tokens.add(t));

  // Add individual name tokens from the therapist's account
  const nameSource = account?.name ?? account?.username ?? '';
  nameSource
    .split(/[\s@.,]+/)
    .map((t) => t.trim())
    .filter((t) => t.length > 1 && /[a-zA-Z]/.test(t))
    .forEach((t) => tokens.add(t));

  return Array.from(tokens);
}

export default function NewSession() {
  const styles = useStyles();
  const navigate = useNavigate();
  const location = useLocation();
  const { accounts } = useMsal();

  const [metadata, setMetadata] = useState<SessionMetadata>({
    clientId: stripClientIdPrefix((location.state as { clientId?: string } | null)?.clientId ?? ''),
    discipline: 'OccupationalTherapy',
    setting: 'Outpatient',
    payer: 'Medicare',
    sessionDate: defaultDatetimeLocal(),
  });
  const [rawTranscript, setRawTranscript] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  // Pre-fetch demographics so they can be forwarded in the pipeline for better ICD-10 coding.
  // Only triggered once a clientId has been entered.
  const trimmedClientId = metadata.clientId.trim();
  const demographicsQuery = useQuery({
    queryKey: ['clientDemographics', trimmedClientId],
    queryFn: () => getClientDemographics(trimmedClientId),
    enabled: trimmedClientId.length > 0,
    staleTime: 5 * 60 * 1000, // 5 min — stable intake data
  });

  function handleTranscriptReady(transcript: string, durationSeconds: number) {
    setRawTranscript(transcript);
    setMetadata((m) => ({
      ...m,
      sessionDurationMinutes: Math.ceil(durationSeconds / 60) || undefined,
    }));
  }

  async function handleGenerate() {
    if (!metadata.clientId.trim()) {
      setSubmitError('Client ID is required.');
      return;
    }
    if (!rawTranscript.trim()) {
      setSubmitError('Please record or enter a transcript before generating documentation.');
      return;
    }

    setSubmitError(null);
    setSubmitting(true);

    try {
      const therapistName = accounts[0]?.username ?? '';
      const sessionDateIso = new Date(metadata.sessionDate).toISOString();

      const input: TranscriptInput = {
        rawTranscript,
        therapistName,
        clientId: metadata.clientId.trim(),
        sessionDate: sessionDateIso,
        discipline: metadata.discipline,
        sessionDurationMinutes: metadata.sessionDurationMinutes,
        setting: metadata.setting,
        payer: metadata.payer,
        // Forward non-PII summary when available — only ageYears (never DOB), sex, and clinical text.
        demographics: demographicsQuery.data
          ? {
              ageYears: demographicsQuery.data.ageYears,
              sex: demographicsQuery.data.sex,
              priorDiagnoses: demographicsQuery.data.priorDiagnoses,
              functionalLimitations: demographicsQuery.data.functionalLimitations,
            }
          : undefined,
      };

      const response = await startDocumentation(input);

      navigate('/sessions/review', {
        state: {
          instanceId: response.instanceId,
          // Use the namespaced clientId returned by the server — it may differ from
          // what the user typed if server-side namespacing was applied.
          clientId: response.clientId,
          sessionDateKey: toSessionDateKey(sessionDateIso),
        },
      });
    } catch (err) {
      setSubmitError((err as Error).message);
      setSubmitting(false);
    }
  }

  const canGenerate = !!rawTranscript.trim() && !!metadata.clientId.trim();

  return (
    <div className={styles.page}>
      <Text className={styles.title}>New Session</Text>

      <SessionMetadataForm value={metadata} onChange={setMetadata} />

      <AudioRecorder onTranscriptReady={handleTranscriptReady} phraseHints={buildPhraseHints(metadata.clientId, accounts[0])} />

      {rawTranscript && (
        <>
          <Divider>Transcript Preview</Divider>
          <div className={styles.transcript}>{rawTranscript}</div>
        </>
      )}

      {submitError && (
        <MessageBar intent="error">
          <MessageBarBody>{submitError}</MessageBarBody>
        </MessageBar>
      )}

      <div className={styles.actions}>
        <Button appearance="secondary" onClick={() => navigate('/')}>
          Cancel
        </Button>
        <Button
          appearance="primary"
          icon={submitting ? <Spinner size="tiny" /> : <DocumentBulletList24Regular />}
          onClick={() => void handleGenerate()}
          disabled={!canGenerate || submitting}
        >
          Generate Documentation
        </Button>
      </div>
    </div>
  );
}
