import { Joyride, Step, STATUS, ACTIONS } from 'react-joyride';
import type { EventHandler } from 'react-joyride';
import { tokens } from '@fluentui/react-components';

interface TourGuideProps {
  run: boolean;
  onComplete: () => void;
}

/**
 * TourGuide component providing a brief 3-step walkthrough of key workflows.
 * Fully accessible with keyboard navigation and screen reader support.
 */
export default function TourGuide({ run, onComplete }: TourGuideProps) {
  const steps: Step[] = [
    {
      target: '[data-tour="new-session-button"]',
      content: (
        <>
          <h3>Create Your First Session</h3>
          <p>
            Click <strong>New Session</strong> to begin documenting a therapy session. 
            You'll select a client and record your session notes.
          </p>
        </>
      ),
      skipBeacon: true,
      placement: 'bottom',
    },
    {
      target: '[data-tour="record-section"]',
      content: (
        <>
          <h3>Record and Generate Your Note</h3>
          <p>
            Use the recording controls to capture your session, or enter text directly. 
            TheraGraf will generate a professional SOAP/DAP note with CPT and ICD-10 codes.
          </p>
        </>
      ),
      placement: 'bottom',
    },
    {
      target: '[data-tour="dashboard-link"]',
      content: (
        <>
          <h3>Always Verify AI Drafts</h3>
          <p>
            AI-generated notes are marked as drafts until you review and approve them. 
            Navigate to your dashboard to verify notes, add your attestation, and finalize documentation.
          </p>
        </>
      ),
      placement: 'bottom',
    },
  ];

  const handleJoyrideCallback: EventHandler = (data) => {
    const { status, action } = data;
    const finishedStatuses: string[] = [STATUS.FINISHED, STATUS.SKIPPED];

    // User completed or skipped the tour
    if (finishedStatuses.includes(status)) {
      onComplete();
    }

    // User clicked close button (X) or pressed ESC
    if (action === ACTIONS.CLOSE) {
      onComplete();
    }
  };

  return (
    <Joyride
      steps={steps}
      run={run}
      continuous
      onEvent={handleJoyrideCallback}
      options={{
        primaryColor: tokens.colorBrandBackground,
        textColor: tokens.colorNeutralForeground1,
        backgroundColor: tokens.colorNeutralBackground1,
        overlayColor: 'rgba(0, 0, 0, 0.5)',
        showProgress: true,
        zIndex: 10000,
        arrowColor: tokens.colorNeutralBackground1,
        spotlightPadding: 8,
        scrollOffset: 100,
        buttons: ['back', 'primary', 'skip'],
      }}
      styles={{
        tooltip: {
          borderRadius: tokens.borderRadiusMedium,
          fontSize: tokens.fontSizeBase300,
        },
        tooltipContent: {
          padding: tokens.spacingVerticalM,
        },
        buttonPrimary: {
          backgroundColor: tokens.colorBrandBackground,
          color: tokens.colorNeutralForegroundOnBrand,
          borderRadius: tokens.borderRadiusMedium,
          fontSize: tokens.fontSizeBase300,
          fontWeight: tokens.fontWeightSemibold,
          padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
        },
        buttonBack: {
          color: tokens.colorNeutralForeground2,
          marginRight: tokens.spacingHorizontalS,
        },
        buttonSkip: {
          color: tokens.colorNeutralForeground2,
        },
      }}
      locale={{
        back: 'Back',
        close: 'Close',
        last: 'Finish',
        next: 'Next',
        skip: 'Skip tour',
      }}
    />
  );
}
