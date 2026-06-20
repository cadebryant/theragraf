import { useState } from 'react';
import { makeStyles, tokens, Card, Textarea, Button, Text } from '@fluentui/react-components';
import { Send24Regular } from '@fluentui/react-icons';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  testBadge: {
    backgroundColor: tokens.colorPaletteYellowBackground2,
    padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
    borderRadius: tokens.borderRadiusMedium,
    fontSize: tokens.fontSizeBase200,
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorNeutralForeground1,
    textTransform: 'uppercase',
    letterSpacing: '0.05em',
  },
  textarea: {
    minHeight: '200px',
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    justifyContent: 'flex-end',
  },
});

interface Props {
  onTranscriptReady: (rawTranscript: string, durationSeconds: number) => void;
}

/**
 * Test-mode replacement for AudioRecorder that allows manual transcript entry.
 * Only shown when VITE_E2E_TEST_MODE=true environment variable is set.
 */
export default function TestTranscriptInput({ onTranscriptReady }: Props) {
  const styles = useStyles();
  const [transcript, setTranscript] = useState('');

  const handleSubmit = () => {
    if (transcript.trim()) {
      // Simulate ~30 second recording (1 second per 5 words)
      const wordCount = transcript.trim().split(/\s+/).length;
      const estimatedDuration = Math.max(10, Math.ceil(wordCount / 5));
      onTranscriptReady(transcript, estimatedDuration);
    }
  };

  return (
    <Card className={styles.card}>
      <div className={styles.testBadge}>⚠️ Test Mode - Manual Transcript Entry</div>
      <Text>Enter a transcript for testing (replaces audio recording):</Text>
      <Textarea
        className={styles.textarea}
        value={transcript}
        onChange={(_, data) => setTranscript(data.value)}
        placeholder="Enter session transcript here..."
        data-testid="test-transcript-input"
      />
      <div className={styles.actions}>
        <Button
          appearance="primary"
          icon={<Send24Regular />}
          onClick={handleSubmit}
          disabled={!transcript.trim()}
          data-testid="test-transcript-submit"
        >
          Use This Transcript
        </Button>
      </div>
    </Card>
  );
}
