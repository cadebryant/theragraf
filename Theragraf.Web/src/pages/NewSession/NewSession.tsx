import { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useMsal } from '@azure/msal-react';
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

      <AudioRecorder onTranscriptReady={handleTranscriptReady} />

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
