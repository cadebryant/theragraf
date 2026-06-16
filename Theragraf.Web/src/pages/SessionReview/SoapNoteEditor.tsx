import { makeStyles, tokens, Field, Textarea, Text, Card } from '@fluentui/react-components';
import type { NoteFormat, SoapNote } from '@/types';

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
  noteFormat?: NoteFormat;
}

type SectionDef = { key: keyof SoapNote; label: string; hint: string };

const SOAP_SECTIONS: SectionDef[] = [
  { key: 'subjective', label: 'Subjective', hint: "Patient's report of their condition" },
  { key: 'objective',  label: 'Objective',  hint: 'Measurable findings and observations' },
  { key: 'assessment', label: 'Assessment', hint: 'Clinical analysis and diagnosis' },
  { key: 'plan',       label: 'Plan',       hint: 'Treatment plan and goals' },
];

const DAP_SECTIONS: SectionDef[] = [
  { key: 'subjective', label: 'Data',       hint: "Client's report and therapist's observations from the session" },
  { key: 'assessment', label: 'Assessment', hint: 'Clinical interpretation and progress toward goals' },
  { key: 'plan',       label: 'Plan',       hint: 'Next steps, interventions, and follow-up' },
];

export default function SoapNoteEditor({ value, onChange, readOnly = false, noteFormat = 'Soap' }: Props) {
  const styles = useStyles();
  const sections = noteFormat === 'Dap' ? DAP_SECTIONS : SOAP_SECTIONS;
  const title = noteFormat === 'Dap' ? 'DAP Note' : 'SOAP Note';

  return (
    <Card className={styles.card}>
      <Text className={styles.title}>{title}</Text>
      <div className={styles.grid}>
        {sections.map(({ key, label, hint }) => (
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
