import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  makeStyles,
  tokens,
  Button,
  Field,
  Input,
  Select,
  Textarea,
  Text,
  Card,
  Spinner,
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogContent,
  DialogActions,
} from '@fluentui/react-components';
import { Edit24Regular, Person24Regular } from '@fluentui/react-icons';
import { getClientDemographics, upsertClientDemographics } from '@/api/clients';
import type {
  BiologicalSex,
  ClientDemographicsResponse,
  UpsertClientDemographicsRequest,
} from '@/types';

const useStyles = makeStyles({
  panel: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    justifyContent: 'space-between',
  },
  headerLeft: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    color: tokens.colorNeutralForeground2,
  },
  card: {
    padding: tokens.spacingVerticalM,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
    gap: tokens.spacingHorizontalL,
  },
  fieldPair: {
    display: 'flex',
    flexDirection: 'column',
    gap: '2px',
  },
  label: {
    fontSize: tokens.fontSizeBase100,
    color: tokens.colorNeutralForeground3,
    textTransform: 'uppercase',
    letterSpacing: '0.04em',
  },
  value: {
    fontSize: tokens.fontSizeBase300,
  },
  empty: {
    color: tokens.colorNeutralForeground3,
    fontStyle: 'italic',
    fontSize: tokens.fontSizeBase300,
  },
  formGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
    gap: tokens.spacingHorizontalL,
    alignItems: 'start',
  },
  fullWidth: {
    gridColumn: '1 / -1',
  },
  hipaaNote: {
    fontSize: tokens.fontSizeBase100,
    color: tokens.colorNeutralForeground3,
    gridColumn: '1 / -1',
  },
});

function sexLabel(sex: BiologicalSex): string {
  switch (sex) {
    case 'Male':   return 'Male';
    case 'Female': return 'Female';
    case 'Other':  return 'Other';
    default:       return '—';
  }
}

interface EditFormState {
  dateOfBirth: string;
  sex: BiologicalSex;
  priorDiagnoses: string;
  functionalLimitations: string;
}

interface Props {
  clientId: string;
}

export default function DemographicsPanel({ clientId }: Props) {
  const styles = useStyles();
  const queryClient = useQueryClient();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState<EditFormState>({
    dateOfBirth: '',
    sex: 'NotSpecified',
    priorDiagnoses: '',
    functionalLimitations: '',
  });

  const query = useQuery({
    queryKey: ['clientDemographics', clientId],
    queryFn: () => getClientDemographics(clientId),
  });

  const mutation = useMutation({
    mutationFn: (body: UpsertClientDemographicsRequest) =>
      upsertClientDemographics(clientId, body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['clientDemographics', clientId] });
      setDialogOpen(false);
    },
  });

  function openDialog(existing: ClientDemographicsResponse | null) {
    setForm({
      dateOfBirth: '',  // never pre-fill — DOB is write-only
      sex: existing?.sex ?? 'NotSpecified',
      priorDiagnoses: existing?.priorDiagnoses ?? '',
      functionalLimitations: existing?.functionalLimitations ?? '',
    });
    setDialogOpen(true);
  }

  function handleSave() {
    const body: UpsertClientDemographicsRequest = {
      sex: form.sex,
      priorDiagnoses: form.priorDiagnoses || null,
      functionalLimitations: form.functionalLimitations || null,
    };
    // Only include dateOfBirth if the therapist typed something — omitting it
    // preserves the existing encrypted DOB on the server.
    if (form.dateOfBirth.trim()) {
      body.dateOfBirth = form.dateOfBirth.trim();
    }
    mutation.mutate(body);
  }

  const record = query.data;

  return (
    <div className={styles.panel}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <Person24Regular />
          <Text weight="semibold">Demographics / Intake</Text>
        </div>
        <Button
          appearance="subtle"
          icon={<Edit24Regular />}
          onClick={() => openDialog(record ?? null)}
        >
          {record ? 'Edit' : 'Add intake'}
        </Button>
      </div>

      {query.isLoading && <Spinner size="tiny" label="Loading…" />}

      {!query.isLoading && !record && (
        <Text className={styles.empty}>
          No intake record yet. Click <strong>Add intake</strong> to record demographics.
        </Text>
      )}

      {record && (
        <Card className={styles.card}>
          <div className={styles.grid}>
            <div className={styles.fieldPair}>
              <Text className={styles.label}>Age</Text>
              <Text className={styles.value}>
                {record.ageYears != null ? `${record.ageYears} yrs` : '—'}
              </Text>
            </div>
            <div className={styles.fieldPair}>
              <Text className={styles.label}>Sex</Text>
              <Text className={styles.value}>{sexLabel(record.sex)}</Text>
            </div>
            {record.priorDiagnoses && (
              <div className={styles.fieldPair} style={{ gridColumn: '1 / -1' }}>
                <Text className={styles.label}>Prior Diagnoses / History</Text>
                <Text className={styles.value}>{record.priorDiagnoses}</Text>
              </div>
            )}
            {record.functionalLimitations && (
              <div className={styles.fieldPair} style={{ gridColumn: '1 / -1' }}>
                <Text className={styles.label}>Functional Limitations</Text>
                <Text className={styles.value}>{record.functionalLimitations}</Text>
              </div>
            )}
          </div>
        </Card>
      )}

      {/* ── Edit / Add dialog ───────────────────────────────────────────── */}
      <Dialog open={dialogOpen} onOpenChange={(_e, d) => !mutation.isPending && setDialogOpen(d.open)}>
        <DialogSurface style={{ maxWidth: 520 }}>
          <DialogBody>
            <DialogTitle>{record ? 'Edit demographics' : 'Add intake record'}</DialogTitle>
            <DialogContent>
              <div className={styles.formGrid}>
                <Field label="Date of birth" hint="ISO date, e.g. 1985-04-12. Leave blank to keep existing.">
                  <Input
                    type="date"
                    value={form.dateOfBirth}
                    onChange={(_e, d) => setForm(f => ({ ...f, dateOfBirth: d.value }))}
                    placeholder="yyyy-mm-dd"
                  />
                </Field>

                <Field label="Biological sex">
                  <Select
                    value={form.sex}
                    onChange={(_e, d) => setForm(f => ({ ...f, sex: d.value as BiologicalSex }))}
                  >
                    <option value="NotSpecified">Not specified</option>
                    <option value="Male">Male</option>
                    <option value="Female">Female</option>
                    <option value="Other">Other</option>
                  </Select>
                </Field>

                <div className={styles.fullWidth}>
                  <Field label="Prior diagnoses / relevant history">
                    <Textarea
                      value={form.priorDiagnoses}
                      onChange={(_e, d) => setForm(f => ({ ...f, priorDiagnoses: d.value }))}
                      placeholder="e.g. CVA 2022, left hemiplegia; T2DM"
                      rows={2}
                    />
                  </Field>
                </div>

                <div className={styles.fullWidth}>
                  <Field label="Functional limitations">
                    <Textarea
                      value={form.functionalLimitations}
                      onChange={(_e, d) => setForm(f => ({ ...f, functionalLimitations: d.value }))}
                      placeholder="e.g. Limited ROM right shoulder; >50% ADL dependence"
                      rows={2}
                    />
                  </Field>
                </div>

                <Text className={styles.hipaaNote}>
                  Date of birth is encrypted at rest and never echoed by the API. Only a computed age range is
                  shared with the AI for ICD-10 coding.
                </Text>
              </div>
            </DialogContent>
            <DialogActions>
              <Button
                appearance="secondary"
                onClick={() => setDialogOpen(false)}
                disabled={mutation.isPending}
              >
                Cancel
              </Button>
              <Button
                appearance="primary"
                onClick={handleSave}
                disabled={mutation.isPending}
              >
                {mutation.isPending ? 'Saving…' : 'Save'}
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}
