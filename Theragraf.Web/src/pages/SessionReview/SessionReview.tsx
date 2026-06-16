import { useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  makeStyles,
  tokens,
  Button,
  Text,
  Spinner,
  MessageBar,
  MessageBarBody,
} from '@fluentui/react-components';
import { Save24Regular, ArrowLeft24Regular, ArrowDownload24Regular } from '@fluentui/react-icons';
import { getOrchestrationStatus, updateSession } from '@/api/sessions';
import type { CptCode, IcdCode, SoapNote } from '@/types';
import PipelineStatus from './PipelineStatus';
import SoapNoteEditor from './SoapNoteEditor';
import { CptCodesEditor, IcdCodesEditor } from './CodesEditor';
import { exportSessionPdf } from '@/utils/exportPdf';
import { exportSession837p } from '@/utils/export837p';

const useStyles = makeStyles({
  page: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXL,
  },
  headerRow: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  title: {
    fontSize: tokens.fontSizeBase600,
    fontWeight: tokens.fontWeightSemibold,
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    justifyContent: 'flex-end',
  },
  center: {
    display: 'flex',
    justifyContent: 'center',
    padding: tokens.spacingVerticalXXL,
  },
});

interface ReviewState {
  instanceId: string;
  clientId: string;
  sessionDateKey: string;
  therapistName?: string;
  discipline?: string;
  setting?: string;
  payer?: string;
  sessionDurationMinutes?: number | null;
}

export default function SessionReview() {
  const styles = useStyles();
  const navigate = useNavigate();
  const location = useLocation();
  const state = location.state as ReviewState | null;

  const [soapNote, setSoapNote] = useState<SoapNote | null>(null);
  const [noteFormat, setNoteFormat] = useState<string>('Soap');
  const [cptCodes, setCptCodes] = useState<CptCode[]>([]);
  const [icdCodes, setIcdCodes] = useState<IcdCode[]>([]);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [activeStage, setActiveStage] = useState(0);
  const stageTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // Advance the visual stage every ~3 seconds while running
  const stageAdvanceRef = useRef(0);

  const { data: status, error: pollError } = useQuery({
    queryKey: ['orchestration', state?.instanceId],
    queryFn: () => getOrchestrationStatus(state!.instanceId),
    enabled: !!state?.instanceId && soapNote === null,
    refetchInterval: (query) => {
      const s = query.state.data?.runtimeStatus;
      return s === 'Completed' || s === 'Failed' || s === 'Terminated' ? false : 2000;
    },
  });

  // Populate editors when pipeline completes
  useEffect(() => {
    if (status?.runtimeStatus === 'Completed' && status.output && soapNote === null) {
      setSoapNote(status.output.restoredNote);
      setNoteFormat(status.output.noteFormat ?? 'Soap');
      setCptCodes(status.output.suggestedCptCodes);
      setIcdCodes(status.output.suggestedIcdCodes);
      if (stageTimerRef.current) clearInterval(stageTimerRef.current);
      setActiveStage(5);
    }
  }, [status, soapNote]);

  // Visual stage advancement while running
  useEffect(() => {
    if (status?.runtimeStatus === 'Running' || status?.runtimeStatus === 'Pending') {
      if (!stageTimerRef.current) {
        stageTimerRef.current = setInterval(() => {
          stageAdvanceRef.current += 1;
          setActiveStage((s) => Math.min(s + 1, 4));
        }, 3000);
      }
    }
    return () => {
      if (stageTimerRef.current) {
        clearInterval(stageTimerRef.current);
        stageTimerRef.current = null;
      }
    };
  }, [status?.runtimeStatus]);

  async function handleSave() {
    if (!soapNote || !state) return;
    setSaving(true);
    setSaveError(null);

    try {
      await updateSession(state.clientId, state.sessionDateKey, {
        soapNote,
        suggestedCptCodes: cptCodes,
        suggestedIcdCodes: icdCodes,
      });
      navigate(`/sessions/${encodeURIComponent(state.clientId)}/${encodeURIComponent(state.sessionDateKey)}`);
    } catch (err) {
      setSaveError((err as Error).message);
      setSaving(false);
    }
  }

  if (!state) {
    return (
      <MessageBar intent="warning">
        <MessageBarBody>No session in progress. Start a new session from the dashboard.</MessageBarBody>
      </MessageBar>
    );
  }

  const runtimeStatus = status?.runtimeStatus ?? 'Pending';
  const isFailed = runtimeStatus === 'Failed' || runtimeStatus === 'Terminated';
  const isComplete = runtimeStatus === 'Completed' && soapNote !== null;

  return (
    <div className={styles.page}>
      <div className={styles.headerRow}>
        <Text className={styles.title}>Review Documentation</Text>
        <Text style={{ color: tokens.colorNeutralForeground3 }}>
          Client: <strong>{state.clientId}</strong>
        </Text>
      </div>

      <PipelineStatus runtimeStatus={runtimeStatus} activeStageIndex={activeStage} />

      {pollError && (
        <MessageBar intent="error">
          <MessageBarBody>Polling failed: {(pollError as Error).message}</MessageBarBody>
        </MessageBar>
      )}

      {isFailed && (
        <MessageBar intent="error">
          <MessageBarBody>
            The documentation pipeline failed. Please go back and try again.
          </MessageBarBody>
        </MessageBar>
      )}

      {!isComplete && !isFailed && (
        <div className={styles.center}>
          <Spinner label="Generating documentation — this usually takes 15–30 seconds…" size="large" />
        </div>
      )}

      {isComplete && soapNote && (
        <>
          <SoapNoteEditor value={soapNote} onChange={setSoapNote} noteFormat={noteFormat as 'Soap' | 'Dap'} />
          <CptCodesEditor codes={cptCodes} onChange={setCptCodes} />
          <IcdCodesEditor codes={icdCodes} onChange={setIcdCodes} />

          {saveError && (
            <MessageBar intent="error">
              <MessageBarBody>{saveError}</MessageBarBody>
            </MessageBar>
          )}

          <div className={styles.actions}>
            <Button
              appearance="subtle"
              icon={<ArrowDownload24Regular />}
              onClick={() => {
                if (!state || !soapNote) return;
                exportSessionPdf({
                  clientId: state.clientId,
                  sessionDate: state.sessionDateKey,
                  therapistName: state.therapistName ?? '',
                  discipline: state.discipline ?? '',
                  setting: state.setting ?? '',
                  payer: state.payer ?? '',
                  sessionDurationMinutes: state.sessionDurationMinutes,
                  soapNote,
                  cptCodes,
                  icdCodes,
                });
              }}
            >
              Export PDF
            </Button>
            <Button
              appearance="subtle"
              icon={<ArrowDownload24Regular />}
              onClick={() => {
                if (!state || !soapNote) return;
                exportSession837p({
                  clientId: state.clientId,
                  sessionDate: state.sessionDateKey,
                  therapistName: state.therapistName ?? '',
                  discipline: state.discipline ?? '',
                  payer: state.payer ?? '',
                  sessionDurationMinutes: state.sessionDurationMinutes,
                  cptCodes,
                  icdCodes,
                });
              }}
            >
              Export 837P
            </Button>
            <Button
              appearance="secondary"
              icon={<ArrowLeft24Regular />}
              onClick={() => navigate('/')}
            >
              Discard
            </Button>
            <Button
              appearance="primary"
              icon={saving ? <Spinner size="tiny" /> : <Save24Regular />}
              onClick={() => void handleSave()}
              disabled={saving}
            >
              Save Session
            </Button>
          </div>
        </>
      )}
    </div>
  );
}
