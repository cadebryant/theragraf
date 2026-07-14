import { makeStyles, tokens, Card, Text, Badge } from '@fluentui/react-components';
import { PieChart, Pie, Cell, Tooltip, ResponsiveContainer, Legend } from 'recharts';
import type { ClientGoalStats, TherapistGoalStats } from '@/types';

// ── Styles ────────────────────────────────────────────────────────────────────

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  title: {
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorNeutralForeground1,
  },
  metaRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    flexWrap: 'wrap',
    alignItems: 'center',
  },
  metaLabel: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
  summaryGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(100px, 1fr))',
    gap: tokens.spacingHorizontalS,
  },
  summaryCell: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: tokens.spacingVerticalXS,
    padding: tokens.spacingVerticalS,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground2,
  },
  summaryValue: {
    fontSize: tokens.fontSizeBase500,
    fontWeight: tokens.fontWeightSemibold,
  },
  summaryLabel: {
    fontSize: tokens.fontSizeBase100,
    color: tokens.colorNeutralForeground3,
    textAlign: 'center',
  },
  empty: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    textAlign: 'center',
    padding: tokens.spacingVerticalM,
  },
});

// ── Colours ───────────────────────────────────────────────────────────────────

const STATUS_COLORS: Record<string, string> = {
  Active:       '#0078d4',  // brand blue
  Met:          '#107c10',  // success green
  'Not Met':    '#d13438',  // danger red
  Discontinued: '#797775',  // neutral grey
};

// ── Helpers ───────────────────────────────────────────────────────────────────

function buildPieData(stats: ClientGoalStats | TherapistGoalStats) {
  return [
    { name: 'Active',       value: stats.activeGoals },
    { name: 'Met',          value: stats.metGoals },
    { name: 'Not Met',      value: stats.notMetGoals },
    { name: 'Discontinued', value: stats.discontinuedGoals },
  ].filter((d) => d.value > 0);
}

// ── Props ─────────────────────────────────────────────────────────────────────

interface Props {
  stats: ClientGoalStats | TherapistGoalStats;
  /** Optional chart title — defaults to "Goal Progress" */
  title?: string;
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function GoalStatsChart({ stats, title = 'Goal Progress' }: Props) {
  const styles = useStyles();
  const pieData = buildPieData(stats);
  const isTherapist = 'clientsWithGoals' in stats;

  if (stats.totalGoals === 0) {
    return (
      <Card className={styles.card}>
        <Text className={styles.title}>{title}</Text>
        <Text className={styles.empty}>No goals recorded yet.</Text>
      </Card>
    );
  }

  return (
    <Card className={styles.card}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: tokens.spacingHorizontalS }}>
        <Text className={styles.title}>{title}</Text>
        <div className={styles.metaRow}>
          <Text className={styles.metaLabel}>{stats.metRate}% met</Text>
          {stats.overdueGoals > 0 && (
            <Badge appearance="filled" color="danger" size="small">
              {stats.overdueGoals} overdue
            </Badge>
          )}
          {isTherapist && (
            <Text className={styles.metaLabel}>
              {(stats as TherapistGoalStats).clientsWithGoals} clients
            </Text>
          )}
        </div>
      </div>

      {/* Summary tiles */}
      <div className={styles.summaryGrid}>
        {[
          { label: 'Total',        value: stats.totalGoals },
          { label: 'Active',       value: stats.activeGoals },
          { label: 'Met',          value: stats.metGoals },
          { label: 'Not Met',      value: stats.notMetGoals },
          { label: 'Discontinued', value: stats.discontinuedGoals },
        ].map(({ label, value }) => (
          <div key={label} className={styles.summaryCell}>
            <Text className={styles.summaryValue}>{value}</Text>
            <Text className={styles.summaryLabel}>{label}</Text>
          </div>
        ))}
      </div>

      {/* Donut chart */}
      {pieData.length > 0 && (
        <ResponsiveContainer width="100%" height={200}>
          <PieChart>
            <Pie
              data={pieData}
              dataKey="value"
              nameKey="name"
              cx="50%"
              cy="50%"
              innerRadius={50}
              outerRadius={80}
              paddingAngle={2}
              label={false}
              labelLine={false}
            >
              {pieData.map((entry) => (
                <Cell
                  key={entry.name}
                  fill={STATUS_COLORS[entry.name] ?? '#605e5c'}
                />
              ))}
            </Pie>
            <Tooltip formatter={(value, name) => [`${value} goals`, name]} />
            <Legend
              iconType="circle"
              iconSize={10}
              formatter={(value) => <span style={{ fontSize: 11 }}>{value}</span>}
            />
          </PieChart>
        </ResponsiveContainer>
      )}
    </Card>
  );
}
