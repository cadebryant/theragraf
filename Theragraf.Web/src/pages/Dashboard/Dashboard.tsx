import { useMsal } from '@azure/msal-react';
import { useQuery } from '@tanstack/react-query';
import { makeStyles, tokens, Spinner, Text, Divider } from '@fluentui/react-components';
import { getTherapistStats } from '@/api/stats';
import { getCaseload } from '@/api/sessions';
import StatsCards from './StatsCards';
import StatsCharts from './StatsCharts';
import CaseloadTable from './CaseloadTable';

const useStyles = makeStyles({
  page: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXL,
  },
  pageTitle: {
    fontSize: tokens.fontSizeBase600,
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorNeutralForeground1,
  },
  center: {
    display: 'flex',
    justifyContent: 'center',
    padding: tokens.spacingVerticalXXL,
  },
  error: {
    color: tokens.colorStatusDangerForeground1,
  },
});

export default function Dashboard() {
  const styles = useStyles();
  const { accounts } = useMsal();
  const therapistName = accounts[0]?.username ?? '';

  const statsQuery = useQuery({
    queryKey: ['therapistStats', therapistName],
    queryFn: () => getTherapistStats(therapistName),
    enabled: !!therapistName,
  });

  const caseloadQuery = useQuery({
    queryKey: ['caseload'],
    queryFn: getCaseload,
  });

  const isLoading = statsQuery.isLoading || caseloadQuery.isLoading;
  const error = statsQuery.error ?? caseloadQuery.error;

  if (isLoading) {
    return (
      <div className={styles.center}>
        <Spinner label="Loading dashboard…" size="large" />
      </div>
    );
  }

  if (error) {
    return (
      <Text className={styles.error}>
        Failed to load dashboard: {(error as Error).message}
      </Text>
    );
  }

  return (
    <div className={styles.page}>
      <Text className={styles.pageTitle}>Dashboard</Text>

      {statsQuery.data && <StatsCards stats={statsQuery.data} />}

      <Divider />

      {statsQuery.data && <StatsCharts stats={statsQuery.data} />}

      <Divider />

      {caseloadQuery.data && <CaseloadTable caseload={caseloadQuery.data} />}
    </div>
  );
}
