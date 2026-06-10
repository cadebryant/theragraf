import {
  makeStyles,
  tokens,
  Field,
  Input,
  Select,
  Card,
  Text,
} from '@fluentui/react-components';
import type { ClinicalSetting, PayerType, TherapyDiscipline } from '@/types';

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
    gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
    gap: tokens.spacingHorizontalL,
  },
});

export interface SessionMetadata {
  clientId: string;
  discipline: TherapyDiscipline;
  setting: ClinicalSetting;
  payer: PayerType;
  sessionDate: string; // ISO 8601 local datetime-local value
  sessionDurationMinutes?: number;
}

interface Props {
  value: SessionMetadata;
  onChange: (value: SessionMetadata) => void;
}

export default function SessionMetadataForm({ value, onChange }: Props) {
  const styles = useStyles();

  function update<K extends keyof SessionMetadata>(key: K, val: SessionMetadata[K]) {
    onChange({ ...value, [key]: val });
  }

  return (
    <Card className={styles.card}>
      <Text className={styles.title}>Session Details</Text>
      <div className={styles.grid}>
        <Field label="Client ID" required>
          <Input
            value={value.clientId}
            onChange={(_e, d) => update('clientId', d.value)}
            placeholder="e.g. patient-001"
          />
        </Field>

        <Field label="Discipline" required>
          <Select
            value={value.discipline}
            onChange={(_e, d) => update('discipline', d.value as TherapyDiscipline)}
          >
            <option value="OccupationalTherapy">Occupational Therapy</option>
            <option value="PhysicalTherapy">Physical Therapy</option>
            <option value="Psychotherapy">Psychotherapy</option>
          </Select>
        </Field>

        <Field label="Setting" required>
          <Select
            value={value.setting}
            onChange={(_e, d) => update('setting', d.value as ClinicalSetting)}
          >
            <option value="Outpatient">Outpatient</option>
            <option value="Inpatient">Inpatient</option>
            <option value="SkilledNursingFacility">Skilled Nursing Facility</option>
            <option value="HomeHealth">Home Health</option>
            <option value="SchoolBased">School-Based</option>
            <option value="EarlyIntervention">Early Intervention</option>
            <option value="Telehealth">Telehealth</option>
          </Select>
        </Field>

        <Field label="Payer" required>
          <Select
            value={value.payer}
            onChange={(_e, d) => update('payer', d.value as PayerType)}
          >
            <option value="Medicare">Medicare</option>
            <option value="MedicareAdvantage">Medicare Advantage</option>
            <option value="Medicaid">Medicaid</option>
            <option value="Commercial">Commercial</option>
            <option value="WorkersCompensation">Workers' Compensation</option>
            <option value="SelfPay">Self-Pay</option>
            <option value="SchoolDistrict">School District</option>
          </Select>
        </Field>

        <Field label="Session Date & Time" required>
          <Input
            type="datetime-local"
            value={value.sessionDate}
            onChange={(_e, d) => update('sessionDate', d.value)}
          />
        </Field>

        <Field label="Duration (minutes)">
          <Input
            type="number"
            value={value.sessionDurationMinutes !== undefined ? String(value.sessionDurationMinutes) : ''}
            onChange={(_e, d) =>
              update(
                'sessionDurationMinutes',
                d.value ? parseInt(d.value, 10) : undefined,
              )
            }
            placeholder="Auto-filled from recording"
          />
        </Field>
      </div>
    </Card>
  );
}
