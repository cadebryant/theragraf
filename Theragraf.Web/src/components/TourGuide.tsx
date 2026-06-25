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
      title: 'Create Your First Session',
      content: (
        <p>
          Click <strong>New Session</strong> to begin documenting a therapy session. 
          You'll select a client and record your session notes.
        </p>
      ),
      skipBeacon: true,
      placement: 'bottom',
    },
    {
      target: '[data-tour="record-section"]',
      title: 'Record and Generate Your Note',
      content: (
        <p>
          Use the recording controls to capture your session, or enter text directly. 
          TheraGraf will generate a professional SOAP/DAP note with CPT and ICD-10 codes.
        </p>
      ),
      placement: 'bottom',
    },
    {
      target: '[data-tour="dashboard-link"]',
      title: 'Always Verify AI Drafts',
      content: (
        <p>
          AI-generated notes are marked as drafts until you review and approve them. 
          Navigate to your dashboard to verify notes, add your attestation, and finalize documentation.
        </p>
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
        overlayColor: 'rgba(0, 0, 0, 0.3)',
        showProgress: true,
        zIndex: 10000,
        arrowColor: tokens.colorNeutralBackground1,
        spotlightPadding: 10,
        scrollOffset: 120,
        buttons: ['back', 'primary', 'skip'],
      }}
      styles={{
        overlay: {
          mixBlendMode: 'normal',
        },
        tooltip: {
          borderRadius: tokens.borderRadiusLarge,
          fontSize: tokens.fontSizeBase300,
          padding: 0,
          boxShadow: tokens.shadow28,
          maxWidth: '420px',
          backgroundColor: tokens.colorNeutralBackground1,
          border: `1px solid ${tokens.colorNeutralStroke1}`,
        },
        tooltipContainer: {
          textAlign: 'left',
          backgroundColor: tokens.colorNeutralBackground1,
        },
        tooltipTitle: {
          fontSize: tokens.fontSizeBase400,
          fontWeight: tokens.fontWeightSemibold,
          marginTop: 0,
          marginBottom: tokens.spacingVerticalS,
          color: tokens.colorNeutralForeground1,
        },
        tooltipContent: {
          padding: `${tokens.spacingVerticalL} ${tokens.spacingHorizontalL}`,
          fontSize: tokens.fontSizeBase300,
          lineHeight: tokens.lineHeightBase300,
          color: tokens.colorNeutralForeground1,
          backgroundColor: tokens.colorNeutralBackground1,
        },
        tooltipFooter: {
          marginTop: 0,
          paddingTop: tokens.spacingVerticalM,
          paddingBottom: tokens.spacingVerticalL,
          paddingLeft: tokens.spacingHorizontalL,
          paddingRight: tokens.spacingHorizontalL,
          borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
          backgroundColor: tokens.colorNeutralBackground1,
        },
        buttonPrimary: {
          backgroundColor: tokens.colorBrandBackground,
          color: tokens.colorNeutralForegroundOnBrand,
          borderRadius: tokens.borderRadiusMedium,
          fontSize: tokens.fontSizeBase300,
          fontWeight: tokens.fontWeightSemibold,
          padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalL}`,
          border: 'none',
          cursor: 'pointer',
        },
        buttonBack: {
          color: tokens.colorNeutralForeground2,
          marginRight: tokens.spacingHorizontalM,
          fontSize: tokens.fontSizeBase300,
          padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
          backgroundColor: 'transparent',
          border: 'none',
          cursor: 'pointer',
        },
        buttonSkip: {
          color: tokens.colorNeutralForeground2,
          fontSize: tokens.fontSizeBase200,
          padding: tokens.spacingVerticalS,
          backgroundColor: 'transparent',
          border: 'none',
          cursor: 'pointer',
        },
        buttonClose: {
          color: tokens.colorNeutralForeground2,
          cursor: 'pointer',
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
