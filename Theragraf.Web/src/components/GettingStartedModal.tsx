import { useState } from 'react';
import {
  makeStyles,
  tokens,
  Button,
  Text,
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogContent,
  DialogActions,
  Divider,
  Checkbox,
} from '@fluentui/react-components';
import {
  ShieldCheckmark24Regular,
  MicrophoneChat24Regular,
  DocumentBulletList24Regular,
  Target24Regular,
  Settings24Regular,
  CheckmarkCircle24Regular,
} from '@fluentui/react-icons';

const useStyles = makeStyles({
  content: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  intro: {
    color: tokens.colorNeutralForeground2,
    lineHeight: tokens.lineHeightBase400,
  },
  stepList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  step: {
    display: 'flex',
    alignItems: 'flex-start',
    gap: tokens.spacingHorizontalM,
  },
  stepIcon: {
    color: tokens.colorBrandForeground1,
    flexShrink: 0,
    marginTop: '2px',
  },
  stepText: {
    display: 'flex',
    flexDirection: 'column',
    gap: '2px',
  },
  stepTitle: {
    fontWeight: tokens.fontWeightSemibold,
  },
  stepDesc: {
    color: tokens.colorNeutralForeground2,
    fontSize: tokens.fontSizeBase200,
    lineHeight: tokens.lineHeightBase300,
  },
  hipaaBox: {
    backgroundColor: tokens.colorStatusWarningBackground1,
    border: `1px solid ${tokens.colorStatusWarningBorder1}`,
    borderRadius: tokens.borderRadiusMedium,
    padding: tokens.spacingVerticalM,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  hipaaTitle: {
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorStatusWarningForeground3,
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  hipaaList: {
    margin: 0,
    paddingLeft: tokens.spacingHorizontalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  hipaaItem: {
    fontSize: tokens.fontSizeBase200,
    lineHeight: tokens.lineHeightBase300,
    color: tokens.colorNeutralForeground1,
  },
  footer: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    flexWrap: 'wrap',
    gap: tokens.spacingHorizontalM,
  },
});

interface Props {
  open: boolean;
  /** Called when the user dismisses the modal. `alwaysShow` reflects the checkbox state. */
  onDismiss: (alwaysShow: boolean) => void;
}

export default function GettingStartedModal({ open, onDismiss }: Props) {
  const styles = useStyles();
  const [alwaysShow, setAlwaysShow] = useState(true);

  return (
    <Dialog open={open} modalType="modal">
      <DialogSurface style={{ maxWidth: '560px' }}>
        <DialogBody>
          <DialogTitle>Welcome to TheraGraf</DialogTitle>
          <DialogContent>
            <div className={styles.content}>
              <Text className={styles.intro}>
                TheraGraf uses AI to generate SOAP/DAP notes, CPT billing codes, and ICD-10
                diagnoses from recorded therapy sessions. Build client profiles, track SMART goals,
                and customize your experience — spend less time on paperwork, more time with
                patients.
              </Text>

              <div className={styles.stepList}>
                <div className={styles.step}>
                  <MicrophoneChat24Regular className={styles.stepIcon} />
                  <div className={styles.stepText}>
                    <Text className={styles.stepTitle}>1. Record a session</Text>
                    <Text className={styles.stepDesc}>
                      Click <strong>New Session</strong>, fill in details, then press Record.
                      TheraGraf transcribes with speaker labels. Assign roles (Therapist/Client)
                      after recording.
                    </Text>
                  </div>
                </div>
                <div className={styles.step}>
                  <DocumentBulletList24Regular className={styles.stepIcon} />
                  <div className={styles.stepText}>
                    <Text className={styles.stepTitle}>2. Generate &amp; review documentation</Text>
                    <Text className={styles.stepDesc}>
                      Press <strong>Generate Documentation</strong>. AI produces a SOAP or DAP note
                      (DAP auto-selected for Psychotherapy), CPT codes, and ICD-10 diagnoses.
                      Sessions are marked as <strong>AI Draft</strong> until you approve.
                    </Text>
                  </div>
                </div>
                <div className={styles.step}>
                  <CheckmarkCircle24Regular className={styles.stepIcon} />
                  <div className={styles.stepText}>
                    <Text className={styles.stepTitle}>3. Attest &amp; approve</Text>
                    <Text className={styles.stepDesc}>
                      Edit AI content as needed, check the attestation box, then click{' '}
                      <strong>Verify &amp; Approve</strong>. Editing after approval clears the
                      status, ensuring accountability.
                    </Text>
                  </div>
                </div>
                <div className={styles.step}>
                  <Target24Regular className={styles.stepIcon} />
                  <div className={styles.stepText}>
                    <Text className={styles.stepTitle}>4. Build client profiles &amp; track goals</Text>
                    <Text className={styles.stepDesc}>
                      Add intake information (age, sex, diagnoses) and manage SMART treatment goals.
                      Intake data enriches ICD-10 suggestions; DOB is encrypted and never shared
                      with AI.
                    </Text>
                  </div>
                </div>
                <div className={styles.step}>
                  <Settings24Regular className={styles.stepIcon} />
                  <div className={styles.stepText}>
                    <Text className={styles.stepTitle}>5. Customize your experience</Text>
                    <Text className={styles.stepDesc}>
                      Click the Settings icon to configure display preferences, documentation
                      defaults, notifications, accessibility options, and privacy controls.
                    </Text>
                  </div>
                </div>
              </div>

              <Divider />

              <div className={styles.hipaaBox}>
                <Text className={styles.hipaaTitle}>
                  <ShieldCheckmark24Regular />
                  Important — HIPAA Responsibilities
                </Text>
                <ul className={styles.hipaaList}>
                  <li className={styles.hipaaItem}>
                    TheraGraf is a documentation aid, not a covered entity. <strong>You</strong> are
                    responsible for ensuring your use of this tool complies with HIPAA and any
                    applicable state regulations.
                  </li>
                  <li className={styles.hipaaItem}>
                    A <strong>Business Associate Agreement (BAA)</strong> with Microsoft Azure is
                    required before using TheraGraf with real patient data. Contact your
                    organization's compliance officer if you are unsure whether one is in place.
                  </li>
                  <li className={styles.hipaaItem}>
                    <strong>Do not use real patient names or identifiers</strong> when trying the
                    app for the first time. Use the built-in demo data or anonymised test data
                    until you have confirmed your compliance obligations are met.
                  </li>
                  <li className={styles.hipaaItem}>
                    Audio is processed in real time and is not stored by TheraGraf. Only the
                    de-identified transcript and generated note are retained.
                  </li>
                </ul>
              </div>
            </div>
          </DialogContent>
          <DialogActions className={styles.footer}>
            <Checkbox
              label="Always show this message on startup"
              checked={alwaysShow}
              onChange={(_e, data) => setAlwaysShow(!!data.checked)}
            />
            <Button appearance="primary" onClick={() => onDismiss(alwaysShow)}>
              I understand — Get started
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
