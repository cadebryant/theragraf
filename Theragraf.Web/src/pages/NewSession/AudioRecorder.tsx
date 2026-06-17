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
  },
  therapistLabel: {
    color: tokens.colorBrandForeground1,
  },
  clientLabel: {
    color: tokens.colorPaletteTealForeground2,
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

/** Maps speakerId ("Guest-1", "Guest-2", …) to "Therapist" / "Client" / "Speaker N" */
function labelFor(speakerId: string, speakerOrder: string[]): string {
  const idx = speakerOrder.indexOf(speakerId);
  if (idx === 0) return 'Therapist';
  if (idx === 1) return 'Client';
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

      // Read from refs — these always hold the latest values regardless of
      // when this closure was created, avoiding the stale-state race.
      const finalSegments = [...segmentsRef.current];
      if (interimTextRef.current.trim()) {
        finalSegments.push({ speakerId: 'unknown', text: interimTextRef.current, isFinal: false });
      }

      const rawTranscript = finalSegments
        .map((seg) => {
          const label = labelFor(seg.speakerId, speakerOrderRef.current);
          return `[${label}]: ${seg.text}`;
        })
        .join('\n');

      onTranscriptReady(rawTranscript, elapsedRef.current);
    });
  }, [stopTimer, onTranscriptReady]);

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
              const isTherapist = label === 'Therapist';
              return (
                <div key={i} className={styles.segment}>
                  <Text
                    className={`${styles.speakerLabel} ${isTherapist ? styles.therapistLabel : styles.clientLabel}`}
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
    </Card>
  );
}
