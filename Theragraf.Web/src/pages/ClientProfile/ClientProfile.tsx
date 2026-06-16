import { useParams, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  makeStyles,
  tokens,
  Text,
  Spinner,
  Card,
  Button,
  Divider,
  Badge,
} from '@fluentui/react-components';
import { Add24Regular, ArrowLeft24Regular } from '@fluentui/react-icons';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer } from 'recharts';
import { getClientStats } from '@/api/stats';
import { getSessionsByClient } from '@/api/sessions';
import { stripClientIdPrefix } from '@/utils/clientId';
import type { TherapyDiscipline } from '@/types';
import SessionsTable from './SessionsTable';
import GoalsPanel from './GoalsPanel';
import DemographicsPanel from './DemographicsPanel';

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
    fontSize: tokens.fontSizeBase600,
    fontWeight: tokens.fontWeightSemibold,
    flex: 1,
  },
  statsGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
    gap: tokens.spacingHorizontalL,
  },
  statCard: {
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  statValue: {
    fontSize: tokens.fontSizeBase600,
    fontWeight: tokens.fontWeightSemibold,
  },
  statLabel: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    textTransform: 'uppercase',
  },
  chartsGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
    gap: tokens.spacingHorizontalL,
  },
  chartCard: {
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  chartTitle: {
    fontWeight: tokens.fontWeightSemibold,
  },
});

export default function ClientProfile() {
  const styles = useStyles();
  const { clientId } = useParams<{ clientId: string }>();
  const navigate = useNavigate();

  const statsQuery = useQuery({
    queryKey: ['clientStats', clientId],
    queryFn: () => getClientStats(clientId!),
    enabled: !!clientId,
  });

  // Fetch the single most-recent session to supply a SOAP note for AI goal suggestions.
  const latestSessionQuery = useQuery({
    queryKey: ['sessions', clientId, 'latest'],
    queryFn: () => getSessionsByClient(clientId!, { pageSize: 1, sortOrder: 'desc' }),
    enabled: !!clientId,
  });

  if (!clientId) return null;

  const stats = statsQuery.data;
  const latestSession = latestSessionQuery.data?.items[0];
  const latestDiscipline = latestSession?.discipline as TherapyDiscipline | undefined;

  function rowKeyToDateStr(key: string | null): string {
    if (!key) return '—';
    const iso = key.replace(/T(\d{2})-(\d{2})-(\d{2})Z$/, 'T$1:$2:$3Z');
    return new Date(iso).toLocaleDateString();
  }

  return (
    <div className={styles.page}>
      <div className={styles.headerRow}>
        <Button
          appearance="subtle"
          icon={<ArrowLeft24Regular />}
          onClick={() => navigate('/')}
        />
        <Text className={styles.title}>
          Client: <strong>{stripClientIdPrefix(clientId)}</strong>
        </Text>
        <Button
          appearance="primary"
          icon={<Add24Regular />}
          onClick={() => navigate('/sessions/new', { state: { clientId } })}
        >
          New Session
        </Button>
      </div>

      {statsQuery.isLoading && <Spinner label="Loading client stats…" />}

      {stats && (
        <>
          <div className={styles.statsGrid}>
            {[
              { label: 'Total Sessions', value: stats.totalSessions },
              { label: 'Billable Units', value: stats.totalBillableUnits },
              {
                label: 'Avg Duration',
                value: `${Math.round(stats.averageSessionDurationMinutes)} min`,
              },
              { label: 'First Session', value: rowKeyToDateStr(stats.firstSessionDate) },
              { label: 'Last Session', value: rowKeyToDateStr(stats.lastSessionDate) },
            ].map((item) => (
              <Card key={item.label} className={styles.statCard}>
                <Text className={styles.statValue}>{item.value}</Text>
                <Text className={styles.statLabel}>{item.label}</Text>
              </Card>
            ))}
          </div>

          <div className={styles.chartsGrid}>
            {/* Top CPT codes */}
            {stats.topCptCodes.length > 0 && (
              <Card className={styles.chartCard}>
                <Text className={styles.chartTitle}>Top CPT Codes</Text>
                <ResponsiveContainer width="100%" height={160}>
                  <BarChart
                    data={stats.topCptCodes.slice(0, 5)}
                    layout="vertical"
                    margin={{ left: 8, right: 16 }}
                  >
                    <XAxis type="number" tick={{ fontSize: 11 }} allowDecimals={false} />
                    <YAxis type="category" dataKey="code" tick={{ fontSize: 11 }} width={55} />
                    <Tooltip />
                    <Bar dataKey="count" fill="#0078d4" radius={[0, 4, 4, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </Card>
            )}

            {/* Top ICD-10 codes */}
            {stats.topIcdCodes.length > 0 && (
              <Card className={styles.chartCard}>
                <Text className={styles.chartTitle}>Top ICD-10 Codes</Text>
                <ResponsiveContainer width="100%" height={160}>
                  <BarChart
                    data={stats.topIcdCodes.slice(0, 5)}
                    layout="vertical"
                    margin={{ left: 8, right: 16 }}
                  >
                    <XAxis type="number" tick={{ fontSize: 11 }} allowDecimals={false} />
                    <YAxis type="category" dataKey="code" tick={{ fontSize: 11 }} width={60} />
                    <Tooltip />
                    <Bar dataKey="count" fill="#005a9e" radius={[0, 4, 4, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </Card>
            )}

            {/* Sessions by discipline */}
            {Object.keys(stats.sessionsByDiscipline).length > 0 && (
              <Card className={styles.chartCard}>
                <Text className={styles.chartTitle}>By Discipline</Text>
                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                  {Object.entries(stats.sessionsByDiscipline).map(([d, count]) => (
                    <Badge key={d} appearance="filled" color="brand">
                      {d}: {count}
                    </Badge>
                  ))}
                </div>
              </Card>
            )}
          </div>
        </>
      )}

      <Divider>Demographics &amp; Intake</Divider>

      <DemographicsPanel clientId={clientId} />

      <Divider>Treatment Goals</Divider>

      <GoalsPanel
        clientId={clientId}
        latestSoapNote={latestSession?.soapNote}
        discipline={latestDiscipline}
      />

      <Divider>Session History</Divider>

      <SessionsTable clientId={clientId} />
    </div>
  );
}
