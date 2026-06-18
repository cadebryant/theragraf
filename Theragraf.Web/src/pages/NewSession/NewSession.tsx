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
import { toDateTimeLocalValue } from '@/utils/dateFormat';
import AudioRecorder from './AudioRecorder';
import { startDocumentation, toSessionDateKey } from '@/api/sessions';
import { getClientDemographics } from '@/api/clients';
import type { TranscriptInput, TherapyDiscipline } from '@/types';

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

/**
 * Discipline-specific clinical vocabulary that the speech recogniser should bias
 * toward. These are terms that standard ASR models commonly mishear because they
 * are rare in general-purpose training data.
 */
const CLINICAL_PHRASES: Record<string, string[]> = {
  OccupationalTherapy: [
    'ADL', 'IADL', 'COPM', 'MOHO', 'FIM', 'Barthel', 'MMSE', 'MoCA',
    'hemiplegia', 'hemiparesis', 'spasticity', 'contracture', 'ataxia',
    'dysmetria', 'tremor', 'apraxia', 'agnosia', 'sensory processing',
    'fine motor', 'gross motor', 'bilateral coordination', 'proprioception',
    'vestibular', 'tactile discrimination', 'pinch strength', 'grip strength',
    'range of motion', 'functional mobility', 'transfers', 'adaptive equipment',
    'orthosis', 'splint', 'upper extremity', 'lower extremity',
    'therapeutic activities', 'neuromuscular reeducation', 'sensory integration',
    'constraint-induced movement therapy', 'CIMT', 'mirror therapy',
  ],
  PhysicalTherapy: [
    'MMT', 'manual muscle test', 'goniometry', 'Berg Balance', 'TUG', 'timed up and go',
    'Oswestry', 'LEFS', 'FABQ', 'PSFS',
    'radiculopathy', 'myelopathy', 'stenosis', 'spondylosis', 'spondylolisthesis',
    'meniscus', 'rotator cuff', 'labrum', 'patellofemoral', 'plantar fasciitis',
    'Achilles tendinopathy', 'piriformis', 'iliotibial band', 'sciatica',
    'lumbar', 'cervical', 'thoracic', 'sacroiliac', 'glenohumeral',
    'tibiofemoral', 'calcaneofibular',
    'manual therapy', 'mobilization', 'manipulation', 'soft tissue mobilization',
    'IASTM', 'dry needling', 'therapeutic ultrasound', 'TENS', 'iontophoresis',
    'aquatic therapy', 'kinesiotaping', 'gait training', 'proprioceptive training',
  ],
  SpeechLanguagePathology: [
    'GFTA', 'CELF', 'PPVT', 'EVT', 'ROWPVT', 'EOWPVT', 'BESA', 'CASL',
    'MBSS', 'FEES', 'MBS', 'VFSS', 'bedside swallowing evaluation',
    'aphasia', 'dysphasia', 'apraxia of speech', 'dysarthria', 'anarthria',
    'dysphagia', 'odynophagia', 'aspiration', 'laryngeal penetration',
    'phonological disorder', 'articulation disorder', 'fluency disorder',
    'stuttering', 'cluttering', 'voice disorder', 'dysphonia', 'aphonia',
    'hypernasality', 'hyponasality', 'resonance disorder',
    'receptive language', 'expressive language', 'pragmatics',
    'social communication', 'AAC', 'augmentative and alternative communication',
    'Lidcombe', 'PROMPT', 'minimal pairs', 'Cycles approach', 'Hanen',
    'oral motor therapy', 'Shaker exercise', 'Mendelsohn maneuver',
    'supraglottic swallow', 'effortful swallow', 'Masako maneuver',
    'VitalStim', 'LSVT', 'Lee Silverman Voice Treatment',
  ],
  Psychotherapy: [
    'CBT', 'cognitive behavioral therapy', 'DBT', 'dialectical behavior therapy',
    'EMDR', 'ACT', 'acceptance and commitment therapy', 'CPT',
    'motivational interviewing', 'psychodynamic', 'somatic therapy',
    'mindfulness-based cognitive therapy', 'MBCT', 'exposure therapy',
    'prolonged exposure', 'narrative therapy',
    'PHQ-9', 'GAD-7', 'PCL-5', 'BDI', 'BAI', 'C-SSRS', 'Columbia Suicide Severity',
    'Beck Depression Inventory', 'hypervigilance',
    'PTSD', 'post-traumatic stress', 'major depressive disorder',
    'generalized anxiety', 'panic disorder', 'social anxiety',
    'borderline personality', 'dysthymia', 'cyclothymia',
    'rumination', 'catastrophizing', 'avoidance', 'dissociation',
    'affect regulation', 'emotional dysregulation', 'suicidal ideation',
    'safety plan', 'therapeutic alliance', 'transference', 'countertransference',
  ],
};

/**
 * Builds a phrase-hint list for the Azure Speech recogniser, biasing toward
 * names found in the client ID and therapist account, plus discipline-specific
 * clinical vocabulary that general-purpose ASR models commonly mishear.
 */
function buildPhraseHints(
  clientId: string,
  account: AccountInfo | undefined,
  discipline: TherapyDiscipline,
): string[] {
  const hints = new Set<string>();

  // Split client ID on common separators and add each word-like token
  clientId
    .split(/[-_\s.]+/)
    .map((t) => t.trim())
    .filter((t) => t.length > 1 && /[a-zA-Z]/.test(t))
    .forEach((t) => hints.add(t));

  // Add individual name tokens from the therapist's account
  const nameSource = account?.name ?? account?.username ?? '';
  nameSource
    .split(/[\s@.,]+/)
    .map((t) => t.trim())
    .filter((t) => t.length > 1 && /[a-zA-Z]/.test(t))
    .forEach((t) => hints.add(t));

  // Add discipline-specific clinical vocabulary
  (CLINICAL_PHRASES[discipline] ?? []).forEach((phrase) => hints.add(phrase));

  return Array.from(hints);
}

export default function NewSession() {
  const styles = useStyles();
  const navigate = useNavigate();
  const location = useLocation();
  const { accounts } = useMsal();

  const [metadata, setMetadata] = useState<SessionMetadata>({
    clientId: stripClientIdPrefix((location.state as { clientId?: string } | null)?.clientId ?? ''),
    discipline: 'OccupationalTherapy',
    noteFormat: 'Soap',
    setting: 'Outpatient',
    payer: 'Medicare',
    sessionDate: toDateTimeLocalValue(),
  });
  const [rawTranscript, setRawTranscript] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  // Pre-fetch demographics once the user finishes typing the client ID (on blur).
  // Using blur instead of onChange avoids network requests for every partial character.
  const [committedClientId, setCommittedClientId] = useState(
    metadata.clientId.trim()
  );
  const demographicsQuery = useQuery({
    queryKey: ['clientDemographics', committedClientId],
    queryFn: () => getClientDemographics(committedClientId),
    enabled: committedClientId.length > 0,
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
        noteFormat: metadata.noteFormat,
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
          // Carry metadata so the review page can populate PDF/837P exports.
          therapistName,
          discipline: metadata.discipline,
          setting: metadata.setting,
          payer: metadata.payer,
          sessionDurationMinutes: metadata.sessionDurationMinutes ?? null,
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

      <SessionMetadataForm
        value={metadata}
        onChange={setMetadata}
        onClientIdBlur={() => setCommittedClientId(metadata.clientId.trim())}
      />

      <AudioRecorder onTranscriptReady={handleTranscriptReady} phraseHints={buildPhraseHints(metadata.clientId, accounts[0], metadata.discipline)} />

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
