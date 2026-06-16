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
  ArrowRight24Regular,
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
                TheraGraf uses AI to generate SOAP notes, CPT billing codes, and ICD-10 diagnoses
                from recorded therapy session transcripts. Build client profiles with intake data
                and track SMART treatment goals — so you can spend less time on paperwork and more
                time with patients.
              </Text>

              <div className={styles.stepList}>
                <div className={styles.step}>
                  <MicrophoneChat24Regular className={styles.stepIcon} />
                  <div className={styles.stepText}>
                    <Text className={styles.stepTitle}>1. Record a session</Text>
                    <Text className={styles.stepDesc}>
                      Click <strong>New Session</strong>, fill in the session details, then press
                      Record. TheraGraf transcribes the conversation in real time with speaker
                      labels.
                    </Text>
                  </div>
                </div>
                <div className={styles.step}>
                  <DocumentBulletList24Regular className={styles.stepIcon} />
                  <div className={styles.stepText}>
                    <Text className={styles.stepTitle}>2. Generate &amp; review documentation</Text>
                    <Text className={styles.stepDesc}>
                      Press <strong>Generate Documentation</strong>. The AI produces a SOAP or DAP
                      note (DAP is selected automatically for Psychotherapy; you can override in
                      Session Details), suggested CPT codes, and ICD-10 diagnoses. Review and edit
                      before saving.
                    </Text>
                  </div>
                </div>
                <div className={styles.step}>
                  <Target24Regular className={styles.stepIcon} />
                  <div className={styles.stepText}>
                    <Text className={styles.stepTitle}>3. Build client profiles &amp; track goals</Text>
                    <Text className={styles.stepDesc}>
                      Open a client's profile to add intake information (age, sex, prior diagnoses)
                      and manage SMART treatment goals. Intake data enriches ICD-10 suggestions;
                      date of birth is encrypted and never shared with the AI.
                    </Text>
                  </div>
                </div>
                <div className={styles.step}>
                  <ArrowRight24Regular className={styles.stepIcon} />
                  <div className={styles.stepText}>
                    <Text className={styles.stepTitle}>4. Export or save</Text>
                    <Text className={styles.stepDesc}>
                      Save the session to your caseload, export as PDF for your EMR, or download an
                      X12 837P file for direct billing submission.
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
