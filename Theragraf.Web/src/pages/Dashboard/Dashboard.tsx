import { useMsal } from '@azure/msal-react';
import { useQuery } from '@tanstack/react-query';
import { makeStyles, tokens, Spinner, Text, Divider } from '@fluentui/react-components';
import { getTherapistStats } from '@/api/stats';
import { getGoalStatsForTherapist } from '@/api/goals';
import { getCaseload } from '@/api/sessions';
import { getNoteStatus } from '@/utils/noteStatus';
import { formatErrorMessage } from '@/utils/errorMessages';
import StatsCards from './StatsCards';
import StatsCharts from './StatsCharts';
import CaseloadTable from './CaseloadTable';
import GoalStatsChart from '@/components/GoalStatsChart';

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
    staleTime: 5 * 60 * 1000, // treat as fresh for 5 minutes — avoids refetch on tab focus
  });

  const caseloadQuery = useQuery({
    queryKey: ['caseload'],
    queryFn: getCaseload,
    staleTime: 5 * 60 * 1000,
  });

  const goalStatsQuery = useQuery({
    queryKey: ['therapistGoalStats', therapistName],
    queryFn: () => getGoalStatsForTherapist(therapistName),
    enabled: !!therapistName,
    staleTime: 5 * 60 * 1000,
  });

  // Show a slim top-of-page spinner only on the very first load (no cached data yet).
  // Once any data is available, render what we have and let each section show its own
  // skeleton/spinner so the page isn't blank while the slower query finishes.
  const nothingYet = statsQuery.isLoading && caseloadQuery.isLoading;
  const error = statsQuery.error ?? caseloadQuery.error;

  const overdueCount = caseloadQuery.data
    ? caseloadQuery.data.clients.filter((c) => getNoteStatus(c.lastSessionDate) !== null).length
    : 0;

  if (nothingYet) {
    return (
      <div className={styles.center} role="status" aria-live="polite" aria-busy="true">
        <Spinner label="Loading dashboard…" size="large" />
      </div>
    );
  }

  if (error && !statsQuery.data && !caseloadQuery.data) {
    return (
      <div role="alert" aria-live="assertive">
        <Text className={styles.error}>
          {formatErrorMessage(error, 'loading dashboard')}
        </Text>
      </div>
    );
  }

  return (
    <div className={styles.page}>
      <h1 className={styles.pageTitle}>Dashboard</h1>

      {statsQuery.isLoading
        ? <div role="status" aria-live="polite"><Spinner label="Loading stats…" size="small" /></div>
        : statsQuery.data && <StatsCards stats={statsQuery.data} overdueCount={overdueCount} />}

      <Divider />

      {statsQuery.data && <StatsCharts stats={statsQuery.data} />}

      <Divider />

      {goalStatsQuery.data && (
        <GoalStatsChart
          stats={goalStatsQuery.data}
          title="Treatment Goal Progress — All Clients"
        />
      )}

      <Divider />

      {caseloadQuery.isLoading
        ? <div role="status" aria-live="polite"><Spinner label="Loading caseload…" size="small" /></div>
        : caseloadQuery.data && <CaseloadTable caseload={caseloadQuery.data} />}
    </div>
  );
}
