import { useCallback, useEffect, useRef, useState } from 'react';
import {
  makeStyles,
  tokens,
  Button,
  Card,
  Text,
  Badge,
  Spinner,
} from '@fluentui/react-components';
import { MicrophoneChat24Regular, Stop24Regular } from '@fluentui/react-icons';
import * as SpeechSDK from 'microsoft-cognitiveservices-speech-sdk';
import { getSpeechToken } from '@/api/sessions';
import type { DiarizedSegment } from '@/types';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  title: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase400,
  },
  controls: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
  },
  timer: {
    fontVariantNumeric: 'tabular-nums',
    fontSize: tokens.fontSizeBase300,
    color: tokens.colorNeutralForeground2,
    minWidth: '48px',
  },
  transcript: {
    minHeight: '200px',
    maxHeight: '400px',
    overflowY: 'auto',
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  segment: {
    display: 'flex',
    flexDirection: 'column',
    gap: '2px',
  },
  speakerLabel: {
    fontSize: tokens.fontSizeBase200,
    fontWeight: tokens.fontWeightSemibold,
    textTransform: 'uppercase',
    letterSpacing: '0.05em',
    color: tokens.colorNeutralForeground2,
  },
  speaker1Label: {
    color: tokens.colorBrandForeground1,
  },
  speaker2Label: {
    color: tokens.colorPaletteTealForeground2,
  },
  speakerOtherLabel: {
    color: tokens.colorPaletteMarigoldForeground2,
  },
  segmentText: {
    fontSize: tokens.fontSizeBase300,
    color: tokens.colorNeutralForeground1,
  },
  interim: {
    fontSize: tokens.fontSizeBase300,
    color: tokens.colorNeutralForeground3,
    fontStyle: 'italic',
  },
  emptyHint: {
    color: tokens.colorNeutralForeground3,
    textAlign: 'center',
    padding: tokens.spacingVerticalL,
  },
});

interface Props {
  onTranscriptReady: (rawTranscript: string, durationSeconds: number) => void;
  /** Words the speech recogniser should bias toward, e.g. client and therapist names. */
  phraseHints?: string[];
}

function formatTime(seconds: number) {
  const m = Math.floor(seconds / 60).toString().padStart(2, '0');
  const s = (seconds % 60).toString().padStart(2, '0');
  return `${m}:${s}`;
}

/** Maps speakerId ("Guest-1", "Guest-2", …) to display label with speaker order.
 * Since we can't determine therapist vs client from order alone, we show neutral labels.
 */
function labelFor(speakerId: string, speakerOrder: string[]): string {
  const idx = speakerOrder.indexOf(speakerId);
  if (idx === 0) return 'Speaker 1 (You)';
  if (idx === 1) return 'Speaker 2';
  return `Speaker ${idx + 1}`;
}

export default function AudioRecorder({ onTranscriptReady, phraseHints }: Props) {
  const styles = useStyles();
  const [recording, setRecording] = useState(false);
  const [segments, setSegments] = useState<DiarizedSegment[]>([]);
  const [interimText, setInterimText] = useState('');
  const [elapsed, setElapsed] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [initialising, setInitialising] = useState(false);
  const [awaitingRoleAssignment, setAwaitingRoleAssignment] = useState(false);
  const [speaker1Role, setSpeaker1Role] = useState<'Therapist' | 'Client'>('Therapist');

  const transcriberRef = useRef<SpeechSDK.ConversationTranscriber | null>(null);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const speakerOrderRef = useRef<string[]>([]);
  const transcriptRef = useRef<HTMLDivElement>(null);

  // Mirror state in refs so stopRecording can read the latest values without
  // being re-created on every state change (avoids stale closure captures).
  const segmentsRef = useRef<DiarizedSegment[]>([]);
  const interimTextRef = useRef('');
  const elapsedRef = useRef(0);

  // Auto-scroll transcript
  useEffect(() => {
    if (transcriptRef.current) {
      transcriptRef.current.scrollTop = transcriptRef.current.scrollHeight;
    }
  }, [segments, interimText]);

  const stopTimer = useCallback(() => {
    if (timerRef.current) {
      clearInterval(timerRef.current);
      timerRef.current = null;
    }
  }, []);

  const startRecording = useCallback(async () => {
    setError(null);
    setInitialising(true);
    try {
      const { token, region } = await getSpeechToken();

      const speechConfig = SpeechSDK.SpeechConfig.fromAuthorizationToken(token, region);

      // ── Accuracy tuning ────────────────────────────────────────────────────
      // Explicit locale avoids a silent auto-detect fallback that can reduce accuracy.
      speechConfig.speechRecognitionLanguage = 'en-US';

      // Raw profanity mode prevents the filter from replacing clinical terms
      // (e.g. medications, anatomical names) with asterisks.
      speechConfig.setProfanity(SpeechSDK.ProfanityOption.Raw);

      // Extend the initial silence timeout so the first speaker has time to settle
      // before the SDK decides there is no audio. Default is 5 000 ms.
      speechConfig.setProperty(
        SpeechSDK.PropertyId.SpeechServiceConnection_InitialSilenceTimeoutMs,
        '10000',
      );

      // Extend the end-of-utterance silence timeout. Therapy sessions have natural
      // pauses mid-thought; the 500 ms default cuts sentences too early, producing
      // fragmented segments that hurt diarization accuracy. 1 800 ms is a good
      // balance between responsiveness and complete thought capture.
      speechConfig.setProperty(
        SpeechSDK.PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs,
        '1800',
      );

      // TrueText post-processing inserts punctuation and normalises capitalisation
      // at the sentence level, making transcripts much more readable and improving
      // the downstream LLM's ability to parse clinical context.
      speechConfig.setServiceProperty(
        'wordLevelTimestamps', 'false',
        SpeechSDK.ServicePropertyChannel.UriQueryParameter,
      );
      speechConfig.setProperty(
        SpeechSDK.PropertyId.SpeechServiceResponse_PostProcessingOption,
        'TrueText',
      );
      // ───────────────────────────────────────────────────────────────────────

      const audioConfig = SpeechSDK.AudioConfig.fromDefaultMicrophoneInput();
      const transcriber = new SpeechSDK.ConversationTranscriber(speechConfig, audioConfig);
      transcriberRef.current = transcriber;
      speakerOrderRef.current = [];

      if (phraseHints && phraseHints.length > 0) {
        const phraseList = SpeechSDK.PhraseListGrammar.fromRecognizer(transcriber);
        phraseHints.forEach((hint) => phraseList.addPhrase(hint));
      }

      transcriber.transcribing = (_s, e) => {
        interimTextRef.current = e.result.text;
        setInterimText(e.result.text);
      };

      transcriber.transcribed = (_s, e) => {
        if (
          e.result.reason === SpeechSDK.ResultReason.RecognizedSpeech &&
          e.result.text.trim()
        ) {
          const { speakerId, text } = e.result;
          if (!speakerOrderRef.current.includes(speakerId)) {
            speakerOrderRef.current = [...speakerOrderRef.current, speakerId];
          }
          setSegments((prev) => {
            const next = [...prev, { speakerId, text, isFinal: true }];
            segmentsRef.current = next;
            return next;
          });
          interimTextRef.current = '';
          setInterimText('');
        }
      };

      transcriber.sessionStopped = () => {
        setRecording(false);
        stopTimer();
        window.dispatchEvent(new CustomEvent('theragraf:recording-stop'));
      };

      await new Promise<void>((resolve, reject) => {
        // Wire the canceled event before starting so SDK-level errors (network,
        // auth, WebSocket failure) reject the promise instead of hanging forever.
        transcriber.canceled = (_s, e) => {
          if (e.reason === SpeechSDK.CancellationReason.Error) {
            reject(new Error(`Speech SDK error ${e.errorCode}: ${e.errorDetails}`));
          }
        };

        const timeout = setTimeout(
          () => reject(new Error('Timed out waiting for microphone — check mic permissions and Speech service configuration.')),
          15_000,
        );

        transcriber.startTranscribingAsync(
          () => { clearTimeout(timeout); resolve(); },
          (err) => { clearTimeout(timeout); reject(new Error(String(err))); },
        );
      });

      setRecording(true);
      setElapsed(0);
      timerRef.current = setInterval(() => setElapsed((s) => {
        const next = s + 1;
        elapsedRef.current = next;
        return next;
      }), 1000);
      window.dispatchEvent(new CustomEvent('theragraf:recording-start'));
    } catch (err) {
      setError(`Could not start recording: ${(err as Error).message}`);
    } finally {
      setInitialising(false);
    }
  }, [stopTimer]);

  const stopRecording = useCallback(() => {
    const transcriber = transcriberRef.current;
    if (!transcriber) return;

    void new Promise<void>((resolve) =>
      transcriber.stopTranscribingAsync(resolve, () => resolve()),
    ).then(() => {
      setRecording(false);
      stopTimer();
      window.dispatchEvent(new CustomEvent('theragraf:recording-stop'));

      // Show role assignment UI if we have multiple speakers
      if (speakerOrderRef.current.length >= 2) {
        setAwaitingRoleAssignment(true);
      } else {
        // Single speaker or no diarization - finalize immediately
        finalizeTranscript('Therapist');
      }
    });
  }, [stopTimer, onTranscriptReady]);

  const finalizeTranscript = useCallback((speaker1Role: 'Therapist' | 'Client') => {
    // Read from refs — these always hold the latest values
    const finalSegments = [...segmentsRef.current];
    if (interimTextRef.current.trim()) {
      finalSegments.push({ speakerId: 'unknown', text: interimTextRef.current, isFinal: false });
    }

    const rawTranscript = finalSegments
      .map((seg) => {
        const idx = speakerOrderRef.current.indexOf(seg.speakerId);
        let label: string;
        if (idx === 0) {
          label = speaker1Role;
        } else if (idx === 1) {
          label = speaker1Role === 'Therapist' ? 'Client' : 'Therapist';
        } else {
          label = labelFor(seg.speakerId, speakerOrderRef.current);
        }
        return `[${label}]: ${seg.text}`;
      })
      .join('\n');

    setAwaitingRoleAssignment(false);
    onTranscriptReady(rawTranscript, elapsedRef.current);
  }, [onTranscriptReady]);

  // Cleanup on unmount
  useEffect(
    () => () => {
      stopTimer();
      transcriberRef.current?.stopTranscribingAsync(
        () => undefined,
        () => undefined,
      );
    },
    [stopTimer],
  );

  return (
    <Card className={styles.card}>
      <div className={styles.header}>
        <Text className={styles.title}>Session Recording</Text>
        <div className={styles.controls}>
          {recording && (
            <>
              <Badge appearance="filled" color="danger" shape="circular" size="small" />
              <Text className={styles.timer}>{formatTime(elapsed)}</Text>
            </>
          )}
          {!recording && !initialising ? (
            <Button
              appearance="primary"
              icon={<MicrophoneChat24Regular />}
              onClick={() => void startRecording()}
              disabled={initialising}
            >
              Record
            </Button>
          ) : recording ? (
            <Button
              appearance="secondary"
              icon={<Stop24Regular />}
              onClick={stopRecording}
            >
              Stop
            </Button>
          ) : (
            <Spinner size="tiny" label="Initialising microphone…" />
          )}
        </div>
      </div>

      {error && (
        <Text style={{ color: tokens.colorStatusDangerForeground1 }}>{error}</Text>
      )}

      <div className={styles.transcript} ref={transcriptRef}>
        {segments.length === 0 && !interimText ? (
          <Text className={styles.emptyHint}>
            Press Record to begin. The live transcript will appear here.
          </Text>
        ) : (
          <>
            {segments.map((seg, i) => {
              const label = labelFor(seg.speakerId, speakerOrderRef.current);
              const idx = speakerOrderRef.current.indexOf(seg.speakerId);
              const colorClass = idx === 0 ? styles.speaker1Label : idx === 1 ? styles.speaker2Label : styles.speakerOtherLabel;
              return (
                <div key={i} className={styles.segment}>
                  <Text
                    className={`${styles.speakerLabel} ${colorClass}`}
                  >
                    {label}
                  </Text>
                  <Text className={styles.segmentText}>{seg.text}</Text>
                </div>
              );
            })}
            {interimText && <Text className={styles.interim}>{interimText}</Text>}
          </>
        )}
      </div>

      {/* Speaker role assignment UI - shown after recording stops with multiple speakers */}
      {awaitingRoleAssignment && speakerOrderRef.current.length >= 2 && (
        <div style={{
          padding: tokens.spacingVerticalL,
          border: `1px solid ${tokens.colorBrandStroke1}`,
          borderRadius: tokens.borderRadiusMedium,
          backgroundColor: tokens.colorBrandBackground2,
          display: 'flex',
          flexDirection: 'column',
          gap: tokens.spacingVerticalM,
        }}>
          <Text style={{ fontWeight: tokens.fontWeightSemibold }}>
            Assign speaker roles:
          </Text>
          <div style={{ display: 'flex', gap: tokens.spacingHorizontalM, alignItems: 'center' }}>
            <Text>Speaker 1 (You) is:</Text>
            <Button
              appearance={speaker1Role === 'Therapist' ? 'primary' : 'secondary'}
              onClick={() => setSpeaker1Role('Therapist')}
              size="small"
            >
              Therapist
            </Button>
            <Button
              appearance={speaker1Role === 'Client' ? 'primary' : 'secondary'}
              onClick={() => setSpeaker1Role('Client')}
              size="small"
            >
              Client
            </Button>
          </div>
          <Button
            appearance="primary"
            onClick={() => finalizeTranscript(speaker1Role)}
          >
            Confirm and Continue
          </Button>
        </div>
      )}
    </Card>
  );
}
