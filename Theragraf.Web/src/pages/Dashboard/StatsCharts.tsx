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

/** Maps legacy abbreviations and alternate spellings to the canonical enum name. */
const CANONICAL: Record<string, string> = {
  OT:                  'OccupationalTherapy',
  PT:                  'PhysicalTherapy',
  SLP:                 'SpeechLanguagePathology',
  Psych:               'Psychotherapy',
  SNF:                 'SkilledNursingFacility',
  HH:                  'HomeHealth',
  EI:                  'EarlyIntervention',
};

/** Splits a PascalCase or camelCase string into space-separated words. */
function splitPascalCase(s: string): string {
  return s
    .replace(/([A-Z][a-z]+)/g, ' $1')
    .replace(/([A-Z]{2,})(?=[A-Z][a-z]|\d|\b)/g, ' $1')
    .trim();
}

/**
 * Returns a human-friendly display label for a raw stat key.
 * Normalises legacy abbreviations first, then splits PascalCase words.
 * e.g.  "OT" → "Occupational Therapy"
 *        "HomeHealth" → "Home Health"
 *        "SkilledNursingFacility" → "Skilled Nursing Facility"
 */
function friendlyLabel(raw: string): string {
  const canonical = CANONICAL[raw] ?? raw;
  return splitPascalCase(canonical);
}

/**
 * Builds chart data from a stats dictionary, normalising keys to their
 * canonical friendly labels and merging any duplicates that result.
 */
function toChartData(record: Record<string, number>) {
  const merged: Record<string, number> = {};
  for (const [raw, count] of Object.entries(record)) {
    const label = friendlyLabel(raw);
    merged[label] = (merged[label] ?? 0) + count;
  }
  return Object.entries(merged)
    .map(([name, value]) => ({ name, value }))
    .sort((a, b) => b.value - a.value);
}

export default function StatsCharts({ stats }: Props) {
  const styles = useStyles();

  const disciplineData = toChartData(stats.sessionsByDiscipline);
  const payerData      = toChartData(stats.sessionsByPayer);
  const settingData    = toChartData(stats.sessionsBySetting);

  return (
    <div className={styles.grid}>
      {/* Sessions by Discipline */}
      <Card className={styles.card}>
        <Text className={styles.title}>Sessions by Discipline</Text>
        <ResponsiveContainer width="100%" height={220}>
          <BarChart data={disciplineData} margin={{ top: 4, right: 8, left: -20, bottom: 4 }}>
            <XAxis dataKey="name" tick={false} axisLine={false} tickLine={false} />
            <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
            <Tooltip />
            <Legend
              formatter={(value) => <span style={{ fontSize: 11 }}>{value}</span>}
              payload={disciplineData.map((d, i) => ({
                value: d.name,
                type: 'square' as const,
                color: COLORS[i % COLORS.length],
              }))}
            />
            {disciplineData.map((d, i) => (
              <Bar key={d.name} dataKey="value" data={[d]} fill={COLORS[i % COLORS.length]} radius={[4, 4, 0, 0]} name={d.name} />
            ))}
          </BarChart>
        </ResponsiveContainer>
      </Card>

      {/* Sessions by Payer */}
      <Card className={styles.card}>
        <Text className={styles.title}>Sessions by Payer</Text>
        <ResponsiveContainer width="100%" height={220}>
          <PieChart>
            <Pie
              data={payerData}
              dataKey="value"
              nameKey="name"
              cx="50%"
              cy="50%"
              outerRadius={70}
              label={false}
              labelLine={false}
            >
              {payerData.map((_entry, index) => (
                <Cell key={index} fill={COLORS[index % COLORS.length]} />
              ))}
            </Pie>
            <Tooltip formatter={(value, name) => [value, name]} />
            <Legend
              formatter={(value) => <span style={{ fontSize: 11 }}>{value}</span>}
            />
          </PieChart>
        </ResponsiveContainer>
      </Card>

      {/* Sessions by Setting */}
      <Card className={styles.card}>
        <Text className={styles.title}>Sessions by Setting</Text>
        <ResponsiveContainer width="100%" height={220}>
          <BarChart data={settingData} margin={{ top: 4, right: 8, left: -20, bottom: 4 }}>
            <XAxis dataKey="name" tick={false} axisLine={false} tickLine={false} />
            <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
            <Tooltip />
            <Legend
              formatter={(value) => <span style={{ fontSize: 11 }}>{value}</span>}
              payload={settingData.map((d, i) => ({
                value: d.name,
                type: 'square' as const,
                color: COLORS[i % COLORS.length],
              }))}
            />
            {settingData.map((d, i) => (
              <Bar key={d.name} dataKey="value" data={[d]} fill={COLORS[i % COLORS.length]} radius={[4, 4, 0, 0]} name={d.name} />
            ))}
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

