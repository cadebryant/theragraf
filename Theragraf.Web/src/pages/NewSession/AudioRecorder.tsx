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

export default function AudioRecorder({ onTranscriptReady }: Props) {
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
      const audioConfig = SpeechSDK.AudioConfig.fromDefaultMicrophoneInput();
      const transcriber = new SpeechSDK.ConversationTranscriber(speechConfig, audioConfig);
      transcriberRef.current = transcriber;
      speakerOrderRef.current = [];

      transcriber.transcribing = (_s, e) => {
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
          setSegments((prev) => [
            ...prev,
            { speakerId, text, isFinal: true },
          ]);
          setInterimText('');
        }
      };

      transcriber.sessionStopped = () => {
        setRecording(false);
        stopTimer();
      };

      await new Promise<void>((resolve, reject) =>
        transcriber.startTranscribingAsync(resolve, reject),
      );

      setRecording(true);
      setElapsed(0);
      timerRef.current = setInterval(() => setElapsed((s) => s + 1), 1000);
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

      // Build the raw transcript string
      const finalSegments = [...segments];
      if (interimText.trim()) {
        // include any dangling interim text
        finalSegments.push({ speakerId: 'unknown', text: interimText, isFinal: false });
      }

      const rawTranscript = finalSegments
        .map((seg) => {
          const label = labelFor(seg.speakerId, speakerOrderRef.current);
          return `[${label}]: ${seg.text}`;
        })
        .join('\n');

      onTranscriptReady(rawTranscript, elapsed);
    });
  }, [segments, interimText, elapsed, stopTimer, onTranscriptReady]);

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
