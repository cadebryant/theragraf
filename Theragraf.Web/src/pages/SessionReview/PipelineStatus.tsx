import { makeStyles, tokens, Text, Spinner } from '@fluentui/react-components';
import {
  CheckmarkCircle24Regular,
  ErrorCircle24Regular,
  Circle24Regular,
} from '@fluentui/react-icons';
import type { RuntimeStatus } from '@/types';

const STAGES = [
  { key: 'ingestion', label: 'PII Redaction' },
  { key: 'soap', label: 'SOAP Note' },
  { key: 'compliance', label: 'Compliance Check' },
  { key: 'billing', label: 'CPT Billing' },
  { key: 'icd10', label: 'ICD-10 Coding' },
  { key: 'persist', label: 'Saving' },
] as const;

const useStyles = makeStyles({
  container: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    flexWrap: 'wrap',
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
  },
  stage: {
    display: 'flex',
    alignItems: 'center',
    gap: '6px',
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground2,
  },
  active: {
    color: tokens.colorBrandForeground1,
    fontWeight: tokens.fontWeightSemibold,
  },
  done: {
    color: tokens.colorStatusSuccessForeground1,
  },
  failed: {
    color: tokens.colorStatusDangerForeground1,
  },
  separator: {
    color: tokens.colorNeutralForeground4,
    fontSize: tokens.fontSizeBase100,
  },
});

interface Props {
  runtimeStatus: RuntimeStatus;
  /** Approximate active stage index (0–5) derived from elapsed time. */
  activeStageIndex: number;
}

export default function PipelineStatus({ runtimeStatus, activeStageIndex }: Props) {
  const styles = useStyles();
  const isFailed = runtimeStatus === 'Failed' || runtimeStatus === 'Terminated';
  const isDone = runtimeStatus === 'Completed';

  return (
    <div className={styles.container}>
      {STAGES.map((stage, i) => {
        const isPast = isDone || i < activeStageIndex;
        const isActive = !isDone && !isFailed && i === activeStageIndex;
        const stageClass = isFailed && i === activeStageIndex
          ? styles.failed
          : isPast
          ? styles.done
          : isActive
          ? styles.active
          : styles.stage;

        return (
          <span key={stage.key} style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
            <span className={`${styles.stage} ${stageClass}`}>
              {isPast ? (
                <CheckmarkCircle24Regular style={{ fontSize: 16 }} />
              ) : isFailed && i === activeStageIndex ? (
                <ErrorCircle24Regular style={{ fontSize: 16 }} />
              ) : isActive ? (
                <Spinner size="tiny" />
              ) : (
                <Circle24Regular style={{ fontSize: 16 }} />
              )}
              {stage.label}
            </span>
            {i < STAGES.length - 1 && (
              <Text className={styles.separator}>›</Text>
            )}
          </span>
        );
      })}
    </div>
  );
}
