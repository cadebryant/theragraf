import { makeStyles, tokens, Field, Textarea, Text, Card } from '@fluentui/react-components';
import type { SoapNote } from '@/types';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  title: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase400,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
    gap: tokens.spacingHorizontalL,
  },
});

interface Props {
  value: SoapNote;
  onChange: (note: SoapNote) => void;
  readOnly?: boolean;
}

const SECTIONS: { key: keyof SoapNote; label: string; hint: string }[] = [
  { key: 'subjective', label: 'Subjective', hint: "Patient's report of their condition" },
  { key: 'objective', label: 'Objective', hint: 'Measurable findings and observations' },
  { key: 'assessment', label: 'Assessment', hint: 'Clinical analysis and diagnosis' },
  { key: 'plan', label: 'Plan', hint: 'Treatment plan and goals' },
];

export default function SoapNoteEditor({ value, onChange, readOnly = false }: Props) {
  const styles = useStyles();

  return (
    <Card className={styles.card}>
      <Text className={styles.title}>SOAP Note</Text>
      <div className={styles.grid}>
        {SECTIONS.map(({ key, label, hint }) => (
          <Field key={key} label={label} hint={hint}>
            <Textarea
              value={value[key]}
              onChange={(_e, d) => onChange({ ...value, [key]: d.value })}
              rows={6}
              resize="vertical"
              readOnly={readOnly}
              style={{ fontFamily: 'inherit', fontSize: tokens.fontSizeBase300 }}
            />
          </Field>
        ))}
      </div>
    </Card>
  );
}
