import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  makeStyles,
  tokens,
  Text,
  Button,
  Badge,
  Spinner,
  MessageBar,
  MessageBarBody,
  Input,
  Label,
  Select,
  Field,
  Divider,
  ProgressBar,
} from '@fluentui/react-components';
import { Edit24Regular, Save24Regular, Dismiss24Regular, Warning24Regular } from '@fluentui/react-icons';
import {
  getTenant,
  getTherapistProfile,
  getProvider,
  updateTherapistProfile,
} from '@/api/profile';
import { formatErrorMessage } from '@/utils/errorMessages';
import type { TherapyDiscipline, TherapistProfileUpdateRequest } from '@/types';

const useStyles = makeStyles({
  page: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXL,
    maxWidth: '680px',
  },
  title: {
    fontSize: tokens.fontSizeBase600,
    fontWeight: tokens.fontWeightSemibold,
  },
  section: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    padding: tokens.spacingVerticalL,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  sectionTitle: {
    fontSize: tokens.fontSizeBase400,
    fontWeight: tokens.fontWeightSemibold,
  },
  fieldRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    flexWrap: 'wrap',
  },
  fieldItem: {
    flex: '1 1 200px',
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  readValue: {
    fontSize: tokens.fontSizeBase300,
    color: tokens.colorNeutralForeground1,
    padding: `${tokens.spacingVerticalXS} 0`,
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    justifyContent: 'flex-end',
  },
  quotaRow: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  metaRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    flexWrap: 'wrap',
    alignItems: 'center',
  },
});

const DISCIPLINES: TherapyDiscipline[] = [
  'OccupationalTherapy',
  'PhysicalTherapy',
  'SpeechLanguagePathology',
  'Psychotherapy',
];

function disciplineLabel(d: TherapyDiscipline): string {
  switch (d) {
    case 'OccupationalTherapy': return 'Occupational Therapy';
    case 'PhysicalTherapy': return 'Physical Therapy';
    case 'SpeechLanguagePathology': return 'Speech-Language Pathology';
    case 'Psychotherapy': return 'Psychotherapy';
  }
}

export default function TherapistProfile() {
  const styles = useStyles();
  const queryClient = useQueryClient();

  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState<TherapistProfileUpdateRequest>({});

  const { data: profile, isLoading: profileLoading, error: profileError } = useQuery({
    queryKey: ['therapistProfile'],
    queryFn: getTherapistProfile,
  });

  const { data: tenant, isLoading: tenantLoading, error: tenantError } = useQuery({
    queryKey: ['tenant'],
    queryFn: getTenant,
  });

  const { data: provider } = useQuery({
    queryKey: ['provider', profile?.providerId],
    queryFn: () => getProvider(profile!.providerId!),
    enabled: !!profile?.providerId,
  });

  const saveMutation = useMutation({
    mutationFn: () => updateTherapistProfile(draft),
    onSuccess: (updated) => {
      queryClient.setQueryData(['therapistProfile'], updated);
      setEditing(false);
      setDraft({});
    },
  });

  function startEdit() {
    if (!profile) return;
    setDraft({
      firstName: profile.firstName,
      lastName: profile.lastName,
      credentials: profile.credentials ?? '',
      discipline: profile.discipline,
      individualNpi: profile.individualNpi ?? '',
    });
    setEditing(true);
  }

  function cancelEdit() {
    setEditing(false);
    setDraft({});
  }

  if (profileLoading || tenantLoading)
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: '4rem' }}>
        <Spinner label="Loading profile…" size="large" />
      </div>
    );

  if (profileError || !profile)
    return (
      <MessageBar intent="error">
        <MessageBarBody>
          {profileError ? formatErrorMessage(profileError, 'loading profile') : 'Profile not found.'}
        </MessageBarBody>
      </MessageBar>
    );

  // AI quota calculation
  const quotaUsed = tenant?.aiCallsThisPeriod ?? 0;
  const quotaMax = tenant?.monthlyAiCallQuota ?? null;
  const quotaPercent = quotaMax ? Math.min(quotaUsed / quotaMax, 1) : null;
  const quotaColor = quotaPercent !== null && quotaPercent >= 0.9 ? 'error' : quotaPercent !== null && quotaPercent >= 0.75 ? 'warning' : 'brand';

  return (
    <div className={styles.page}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Text className={styles.title}>My Profile</Text>
        {!editing && (
          <Button appearance="secondary" icon={<Edit24Regular />} onClick={startEdit}>
            Edit Profile
          </Button>
        )}
      </div>

      {/* Setup prompt for unconfigured profiles */}
      {!profile.isConfigured && (
        <MessageBar intent="warning" icon={<Warning24Regular />}>
          <MessageBarBody>
            Your profile was created automatically from your login. Fill in your credentials, NPI, and discipline to complete setup.
          </MessageBarBody>
        </MessageBar>
      )}

      {saveMutation.error && (
        <MessageBar intent="error">
          <MessageBarBody>{formatErrorMessage(saveMutation.error, 'saving profile')}</MessageBarBody>
        </MessageBar>
      )}

      {/* Profile section */}
      <div className={styles.section}>
        <Text className={styles.sectionTitle}>Therapist Information</Text>
        <div className={styles.fieldRow}>
          <div className={styles.fieldItem}>
            {editing ? (
              <Field label="First Name" required>
                <Input
                  value={draft.firstName ?? ''}
                  onChange={(_, d) => setDraft((p) => ({ ...p, firstName: d.value }))}
                />
              </Field>
            ) : (
              <>
                <Label>First Name</Label>
                <Text className={styles.readValue}>{profile.firstName}</Text>
              </>
            )}
          </div>
          <div className={styles.fieldItem}>
            {editing ? (
              <Field label="Last Name" required>
                <Input
                  value={draft.lastName ?? ''}
                  onChange={(_, d) => setDraft((p) => ({ ...p, lastName: d.value }))}
                />
              </Field>
            ) : (
              <>
                <Label>Last Name</Label>
                <Text className={styles.readValue}>{profile.lastName}</Text>
              </>
            )}
          </div>
        </div>

        <div className={styles.fieldRow}>
          <div className={styles.fieldItem}>
            {editing ? (
              <Field label="Credentials" hint="e.g. OTR/L, PT DPT, CCC-SLP">
                <Input
                  value={draft.credentials ?? ''}
                  onChange={(_, d) => setDraft((p) => ({ ...p, credentials: d.value }))}
                  placeholder="e.g. OTR/L"
                />
              </Field>
            ) : (
              <>
                <Label>Credentials</Label>
                <Text className={styles.readValue}>{profile.credentials ?? '—'}</Text>
              </>
            )}
          </div>
          <div className={styles.fieldItem}>
            {editing ? (
              <Field label="Discipline" required>
                <Select
                  value={draft.discipline ?? ''}
                  onChange={(_, d) => setDraft((p) => ({ ...p, discipline: d.value as TherapyDiscipline }))}
                >
                  <option value="" disabled>Select…</option>
                  {DISCIPLINES.map((d) => (
                    <option key={d} value={d}>{disciplineLabel(d)}</option>
                  ))}
                </Select>
              </Field>
            ) : (
              <>
                <Label>Discipline</Label>
                <Text className={styles.readValue}>{disciplineLabel(profile.discipline)}</Text>
              </>
            )}
          </div>
        </div>

        <div className={styles.fieldRow}>
          <div className={styles.fieldItem}>
            {editing ? (
              <Field label="Individual NPI" hint="10-digit Type 1 NPI">
                <Input
                  value={draft.individualNpi ?? ''}
                  onChange={(_, d) => setDraft((p) => ({ ...p, individualNpi: d.value || null }))}
                  placeholder="1234567890"
                  maxLength={10}
                />
              </Field>
            ) : (
              <>
                <Label>Individual NPI</Label>
                <Text className={styles.readValue}>{profile.individualNpi ?? '—'}</Text>
              </>
            )}
          </div>
        </div>

        {editing && (
          <div className={styles.actions}>
            <Button appearance="subtle" icon={<Dismiss24Regular />} onClick={cancelEdit}>
              Cancel
            </Button>
            <Button
              appearance="primary"
              icon={saveMutation.isPending ? <Spinner size="tiny" /> : <Save24Regular />}
              onClick={() => saveMutation.mutate()}
              disabled={saveMutation.isPending || !draft.firstName || !draft.lastName}
            >
              Save
            </Button>
          </div>
        )}
      </div>

      {/* Organization / tenant section */}
      {tenant && (
        <div className={styles.section}>
          <Text className={styles.sectionTitle}>Organization</Text>

          {tenantError && (
            <MessageBar intent="warning">
              <MessageBarBody>{formatErrorMessage(tenantError, 'loading organization info')}</MessageBarBody>
            </MessageBar>
          )}

          <div className={styles.metaRow}>
            <Text style={{ fontWeight: tokens.fontWeightSemibold }}>{tenant.organizationName}</Text>
            <Badge appearance="tint">{tenant.organizationType.replace(/([A-Z])/g, ' $1').trim()}</Badge>
            <Badge appearance="filled" color={tenant.status === 'Active' ? 'success' : 'danger'}>{tenant.status}</Badge>
            <Badge appearance="outline">{tenant.plan} plan</Badge>
            {tenant.isSynthetic && <Badge appearance="tint" color="informative">Self-hosted</Badge>}
          </div>

          <Divider />

          <div className={styles.quotaRow}>
            <Text style={{ fontWeight: tokens.fontWeightSemibold }}>AI Usage This Period</Text>
            {quotaMax !== null ? (
              <>
                <ProgressBar value={quotaPercent!} color={quotaColor} thickness="large" />
                <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
                  {quotaUsed.toLocaleString()} / {quotaMax.toLocaleString()} AI calls
                  {quotaPercent !== null && quotaPercent >= 0.9 && ' — nearing limit'}
                </Text>
              </>
            ) : (
              <Text style={{ fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 }}>
                {quotaUsed.toLocaleString()} AI calls (unlimited)
              </Text>
            )}
          </div>
        </div>
      )}

      {/* Provider / practice section (group practice only) */}
      {provider && (
        <div className={styles.section}>
          <Text className={styles.sectionTitle}>Practice</Text>
          <div className={styles.fieldRow}>
            <div className={styles.fieldItem}>
              <Label>Practice Name</Label>
              <Text className={styles.readValue}>{provider.practiceName}</Text>
            </div>
            <div className={styles.fieldItem}>
              <Label>Organization NPI</Label>
              <Text className={styles.readValue}>{provider.organizationNpi ?? '—'}</Text>
            </div>
          </div>
          {provider.addressLine1 && (
            <div className={styles.fieldRow}>
              <div className={styles.fieldItem}>
                <Label>Address</Label>
                <Text className={styles.readValue}>
                  {provider.addressLine1}
                  {provider.addressLine2 ? `, ${provider.addressLine2}` : ''}
                  {provider.city ? `, ${provider.city}` : ''}
                  {provider.state ? `, ${provider.state}` : ''}
                  {provider.zip ? ` ${provider.zip}` : ''}
                </Text>
              </div>
              {provider.phone && (
                <div className={styles.fieldItem}>
                  <Label>Phone</Label>
                  <Text className={styles.readValue}>{provider.phone}</Text>
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
