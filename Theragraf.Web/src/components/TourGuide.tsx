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
  console.log('TourGuide render:', { run });

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
    {
      target: '[data-tour="settings-link"]',
      title: 'Customize Your Experience',
      content: (
        <p>
          Access <strong>Settings</strong> to customize your preferences, set documentation defaults, 
          and configure accessibility options. You can also restart this tour anytime from Settings.
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
          mixBlendMode: 'normal' as const,
        },
        tooltip: {
          borderRadius: '8px',
          fontSize: '14px',
          padding: '0',
          boxShadow: '0 8px 16px rgba(0,0,0,0.14), 0 0 2px rgba(0,0,0,0.12)',
          maxWidth: '420px',
          minWidth: '290px',
          backgroundColor: '#ffffff',
          border: '1px solid #e1e1e1',
          position: 'relative' as const,
          width: '100%',
        },
        tooltipContainer: {
          textAlign: 'left' as const,
          backgroundColor: '#ffffff',
          lineHeight: 1.4,
        },
        tooltipTitle: {
          fontSize: '16px',
          fontWeight: 600,
          margin: '0 0 8px 0',
          padding: '20px 20px 0 20px',
          color: '#242424',
        },
        tooltipContent: {
          padding: '12px 20px 20px 20px',
          fontSize: '14px',
          lineHeight: '1.4',
          color: '#242424',
          backgroundColor: '#ffffff',
        },
        tooltipFooter: {
          margin: '0',
          padding: '12px 20px 20px 20px',
          borderTop: '1px solid #e1e1e1',
          backgroundColor: '#ffffff',
          display: 'flex' as const,
          justifyContent: 'space-between' as const,
          alignItems: 'center' as const,
        },
        buttonPrimary: {
          backgroundColor: '#0078d4',
          color: '#ffffff',
          borderRadius: '4px',
          fontSize: '14px',
          fontWeight: 600,
          padding: '8px 16px',
          border: 'none',
          cursor: 'pointer',
          outline: 'none',
        },
        buttonBack: {
          color: '#605e5c',
          marginRight: '8px',
          fontSize: '14px',
          padding: '8px 12px',
          backgroundColor: 'transparent',
          border: 'none',
          cursor: 'pointer',
          outline: 'none',
        },
        buttonSkip: {
          color: '#605e5c',
          fontSize: '12px',
          padding: '8px',
          backgroundColor: 'transparent',
          border: 'none',
          cursor: 'pointer',
          outline: 'none',
          textDecoration: 'underline',
        },
        buttonClose: {
          color: '#605e5c',
          cursor: 'pointer',
          width: '12px',
          height: '12px',
          outline: 'none',
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
