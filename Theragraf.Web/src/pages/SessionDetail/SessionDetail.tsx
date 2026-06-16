import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  makeStyles,
  tokens,
  Text,
  Spinner,
  Button,
  Badge,
  MessageBar,
  MessageBarBody,
} from '@fluentui/react-components';
import {
  ArrowLeft24Regular,
  Edit24Regular,
  Save24Regular,
  Dismiss24Regular,
  ArrowDownload24Regular,
} from '@fluentui/react-icons';
import { getSessionByClientAndDate, updateSession } from '@/api/sessions';
import type { CptCode, IcdCode, SoapNote } from '@/types';
import SoapNoteEditor from '@/pages/SessionReview/SoapNoteEditor';
import { CptCodesEditor, IcdCodesEditor } from '@/pages/SessionReview/CodesEditor';
import { exportSessionPdf } from '@/utils/exportPdf';
import { stripClientIdPrefix } from '@/utils/clientId';
import { exportSession837p } from '@/utils/export837p';

const useStyles = makeStyles({
  page: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXL,
  },
  headerRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
  },
  title: {
    flex: 1,
    fontSize: tokens.fontSizeBase600,
    fontWeight: tokens.fontWeightSemibold,
  },
  metaRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    flexWrap: 'wrap',
    alignItems: 'center',
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    justifyContent: 'flex-end',
  },
});

function rowKeyToDateStr(key: string): string {
  const iso = key.replace(/T(\d{2})-(\d{2})-(\d{2})Z$/, 'T$1:$2:$3Z');
  return new Date(iso).toLocaleString();
}

export default function SessionDetail() {
  const styles = useStyles();
  const { clientId, sessionDate } = useParams<{ clientId: string; sessionDate: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [editing, setEditing] = useState(false);
  const [soapNote, setSoapNote] = useState<SoapNote | null>(null);
  const [cptCodes, setCptCodes] = useState<CptCode[]>([]);
  const [icdCodes, setIcdCodes] = useState<IcdCode[]>([]);

  const { data: session, isLoading, error } = useQuery({
    queryKey: ['session', clientId, sessionDate],
    queryFn: () => getSessionByClientAndDate(clientId!, sessionDate!),
    enabled: !!clientId && !!sessionDate,
  });

  const saveMutation = useMutation({
    mutationFn: () =>
      updateSession(clientId!, sessionDate!, {
        soapNote: soapNote ?? undefined,
        suggestedCptCodes: cptCodes,
        suggestedIcdCodes: icdCodes,
      }),
    onSuccess: (updated) => {
      queryClient.setQueryData(['session', clientId, sessionDate], updated);
      setEditing(false);
    },
  });

  function handleExportPdf() {
    if (!session) return;
    exportSessionPdf({
      clientId: session.clientId,
      sessionDate: session.sessionDate,
      therapistName: session.therapistName,
      discipline: session.discipline,
      setting: session.setting,
      payer: session.payer,
      sessionDurationMinutes: session.sessionDurationMinutes,
      noteFormat: (session.noteFormat ?? 'Soap') as 'Soap' | 'Dap',
      soapNote: session.soapNote,
      cptCodes: session.suggestedCptCodes,
      icdCodes: session.suggestedIcdCodes,
    });
  }

  function handleExport837p() {
    if (!session) return;
    exportSession837p({
      clientId: session.clientId,
      sessionDate: session.sessionDate,
      therapistName: session.therapistName,
      discipline: session.discipline,
      payer: session.payer,
      sessionDurationMinutes: session.sessionDurationMinutes,
      cptCodes: session.suggestedCptCodes,
      icdCodes: session.suggestedIcdCodes,
    });
  }

  function startEdit() {
    if (!session) return;
    setSoapNote(session.soapNote);
    setCptCodes([...session.suggestedCptCodes]);
    setIcdCodes([...session.suggestedIcdCodes]);
    setEditing(true);
  }

  function cancelEdit() {
    setEditing(false);
    setSoapNote(null);
  }

  if (isLoading)
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: '4rem' }}>
        <Spinner label="Loading session…" size="large" />
      </div>
    );

  if (error || !session)
    return (
      <MessageBar intent="error">
        <MessageBarBody>
          {error ? (error as Error).message : 'Session not found.'}
        </MessageBarBody>
      </MessageBar>
    );

  const displaySoap = editing && soapNote ? soapNote : session.soapNote;
  const displayCpt = editing ? cptCodes : session.suggestedCptCodes;
  const displayIcd = editing ? icdCodes : session.suggestedIcdCodes;

  return (
    <div className={styles.page}>
      <div className={styles.headerRow}>
        <Button
          appearance="subtle"
          icon={<ArrowLeft24Regular />}
          onClick={() => navigate(`/sessions/${encodeURIComponent(clientId!)}`)}
        />
        <div className={styles.title}>
          <Text block style={{ fontSize: tokens.fontSizeBase600, fontWeight: tokens.fontWeightSemibold }}>
            {stripClientIdPrefix(clientId!)}
          </Text>
          <Text block style={{ fontSize: tokens.fontSizeBase300, color: tokens.colorNeutralForeground3 }}>
            {rowKeyToDateStr(sessionDate!)}
          </Text>
        </div>
        {!editing ? (
          <>
            <Button appearance="subtle" icon={<ArrowDownload24Regular />} onClick={handleExportPdf}>
              Export PDF
            </Button>
            <Button appearance="subtle" icon={<ArrowDownload24Regular />} onClick={handleExport837p}>
              Export 837P
            </Button>
            <Button appearance="secondary" icon={<Edit24Regular />} onClick={startEdit}>
              Edit
            </Button>
          </>
        ) : (
          <>
            <Button appearance="subtle" icon={<Dismiss24Regular />} onClick={cancelEdit}>
              Cancel
            </Button>
            <Button
              appearance="primary"
              icon={saveMutation.isPending ? <Spinner size="tiny" /> : <Save24Regular />}
              onClick={() => saveMutation.mutate()}
              disabled={saveMutation.isPending}
            >
              Save
            </Button>
          </>
        )}
      </div>

      {/* Metadata row */}
      <div className={styles.metaRow}>
        <Badge appearance="filled" color="brand">{session.discipline}</Badge>
        <Badge appearance="tint">{session.setting}</Badge>
        <Badge appearance="tint">{session.payer}</Badge>
        {session.sessionDurationMinutes && (
          <Badge appearance="outline">{session.sessionDurationMinutes} min</Badge>
        )}
        <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
          Therapist: {session.therapistName}
        </Text>
      </div>

      {saveMutation.error && (
        <MessageBar intent="error">
          <MessageBarBody>Save failed: {(saveMutation.error as Error).message}</MessageBarBody>
        </MessageBar>
      )}

      <SoapNoteEditor
        value={displaySoap}
        onChange={setSoapNote}
        readOnly={!editing}
        noteFormat={(session.noteFormat ?? 'Soap') as 'Soap' | 'Dap'}
      />

      <CptCodesEditor
        codes={displayCpt}
        onChange={setCptCodes}
        readOnly={!editing}
      />

      <IcdCodesEditor
        codes={displayIcd}
        onChange={setIcdCodes}
        readOnly={!editing}
      />
    </div>
  );
}
