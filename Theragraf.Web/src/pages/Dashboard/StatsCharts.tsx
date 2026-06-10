import { makeStyles, tokens, Card, Text } from '@fluentui/react-components';
import {
  BarChart,
  Bar,
  PieChart,
  Pie,
  Cell,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  Legend,
} from 'recharts';
import type { TherapistStats } from '@/types';

const useStyles = makeStyles({
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))',
    gap: tokens.spacingHorizontalL,
  },
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
});

const COLORS = ['#0078d4', '#2b88d8', '#71afe5', '#c7e0f4', '#004578', '#005a9e'];

interface Props {
  stats: TherapistStats;
}

function toChartData(record: Record<string, number>) {
  return Object.entries(record)
    .map(([name, value]) => ({ name, value }))
    .sort((a, b) => b.value - a.value);
}

export default function StatsCharts({ stats }: Props) {
  const styles = useStyles();

  const disciplineData = toChartData(stats.sessionsByDiscipline);
  const payerData = toChartData(stats.sessionsByPayer);
  const settingData = toChartData(stats.sessionsBySetting);

  return (
    <div className={styles.grid}>
      {/* Sessions by Discipline */}
      <Card className={styles.card}>
        <Text className={styles.title}>Sessions by Discipline</Text>
        <ResponsiveContainer width="100%" height={200}>
          <BarChart data={disciplineData} margin={{ top: 4, right: 8, left: -20, bottom: 4 }}>
            <XAxis dataKey="name" tick={{ fontSize: 11 }} />
            <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
            <Tooltip />
            <Bar dataKey="value" fill="#0078d4" radius={[4, 4, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </Card>

      {/* Sessions by Payer */}
      <Card className={styles.card}>
        <Text className={styles.title}>Sessions by Payer</Text>
        <ResponsiveContainer width="100%" height={200}>
          <PieChart>
            <Pie
              data={payerData}
              dataKey="value"
              nameKey="name"
              cx="50%"
              cy="50%"
              outerRadius={75}
              label={({ name, percent }) =>
                percent > 0.05 ? `${name} ${(percent * 100).toFixed(0)}%` : ''
              }
              labelLine={false}
            >
              {payerData.map((_entry, index) => (
                <Cell key={index} fill={COLORS[index % COLORS.length]} />
              ))}
            </Pie>
            <Tooltip />
          </PieChart>
        </ResponsiveContainer>
      </Card>

      {/* Sessions by Setting */}
      <Card className={styles.card}>
        <Text className={styles.title}>Sessions by Setting</Text>
        <ResponsiveContainer width="100%" height={200}>
          <BarChart data={settingData} margin={{ top: 4, right: 8, left: -20, bottom: 4 }}>
            <XAxis dataKey="name" tick={{ fontSize: 11 }} />
            <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
            <Tooltip />
            <Bar dataKey="value" fill="#2b88d8" radius={[4, 4, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </Card>

      {/* Top CPT Codes */}
      {stats.topCptCodes.length > 0 && (
        <Card className={styles.card}>
          <Text className={styles.title}>Top CPT Codes</Text>
          <ResponsiveContainer width="100%" height={200}>
            <BarChart
              data={stats.topCptCodes.slice(0, 6)}
              layout="vertical"
              margin={{ top: 4, right: 16, left: 8, bottom: 4 }}
            >
              <XAxis type="number" tick={{ fontSize: 11 }} allowDecimals={false} />
              <YAxis type="category" dataKey="code" tick={{ fontSize: 11 }} width={50} />
              <Tooltip
                formatter={(value, _name, props) => [
                  `${value} sessions (${(props.payload as { totalBillableUnits: number }).totalBillableUnits} units)`,
                  'Count',
                ]}
              />
              <Legend />
              <Bar dataKey="count" fill="#004578" radius={[0, 4, 4, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </Card>
      )}
    </div>
  );
}
