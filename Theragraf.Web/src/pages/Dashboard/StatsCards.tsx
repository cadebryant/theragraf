import { makeStyles, tokens, Card, Text } from '@fluentui/react-components';
import {
  CalendarLtr24Regular,
  People24Regular,
  Money24Regular,
  Clock24Regular,
  Warning24Regular,
} from '@fluentui/react-icons';
import type { TherapistStats } from '@/types';

const useStyles = makeStyles({
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
    gap: tokens.spacingHorizontalL,
  },
  card: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    padding: tokens.spacingVerticalL,
  },
  iconRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    color: tokens.colorBrandForeground1,
  },
  iconRowWarning: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    color: tokens.colorStatusWarningForeground3,
  },
  value: {
    fontSize: tokens.fontSizeBase600,
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorNeutralForeground1,
  },
  valueWarning: {
    fontSize: tokens.fontSizeBase600,
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorStatusWarningForeground3,
  },
  label: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    textTransform: 'uppercase',
    letterSpacing: '0.05em',
  },
});

interface Props {
  stats: TherapistStats;
  overdueCount?: number;
}

export default function StatsCards({ stats, overdueCount = 0 }: Props) {
  const styles = useStyles();

  const items = [
    { icon: <CalendarLtr24Regular />, value: stats.totalSessions, label: 'Total Sessions' },
    { icon: <People24Regular />, value: stats.totalClients, label: 'Active Clients' },
    { icon: <Money24Regular />, value: stats.totalBillableUnits, label: 'Billable Units' },
    {
      icon: <Clock24Regular />,
      value: `${Math.round(stats.averageSessionDurationMinutes)} min`,
      label: 'Avg Duration',
    },
  ];

  return (
    <div className={styles.grid}>
      {items.map((item) => (
        <Card key={item.label} className={styles.card}>
          <div className={styles.iconRow}>{item.icon}</div>
          <Text className={styles.value}>{item.value}</Text>
          <Text className={styles.label}>{item.label}</Text>
        </Card>
      ))}

      <Card className={styles.card}>
        <div className={overdueCount > 0 ? styles.iconRowWarning : styles.iconRow}>
          <Warning24Regular />
        </div>
        <Text className={overdueCount > 0 ? styles.valueWarning : styles.value}>
          {overdueCount}
        </Text>
        <Text className={styles.label}>Overdue Notes</Text>
      </Card>
    </div>
  );
}
