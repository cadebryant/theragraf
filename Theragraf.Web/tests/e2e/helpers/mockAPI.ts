/**
 * Mock API responses for E2E tests
 * 
 * Provides realistic responses for backend endpoints so tests can run
 * without depending on Azure Functions or OpenAI processing.
 */

import { Page } from '@playwright/test';
import type {
  OrchestrationStartResponse,
  OrchestrationStatus,
  SessionResponse,
  CptCode,
  IcdCode,
} from '../../../src/types';

// ── Mock Data Templates ───────────────────────────────────────────────────────

const MOCK_SOAP_NOTE = {
  subjective: `Client reported feeling motivated and engaged during today's session. They demonstrated improved attention span compared to previous sessions and expressed satisfaction with their progress on functional communication goals. Client stated, "I feel like I'm getting better at getting my words out."`,
  objective: `Client participated in structured language activities targeting word retrieval and sentence formulation. Completed 15/20 trials with 75% accuracy using semantic cues. Required moderate cueing for complex sentence construction. Demonstrated improved self-monitoring skills with 3-4 self-corrections noted during conversational exchanges.`,
  assessment: `Client demonstrates steady progress toward functional communication goals. Current performance indicates readiness to advance to more complex linguistic targets. Continued therapeutic intervention is medically necessary to address residual language deficits and optimize functional communication outcomes.`,
  plan: `Continue skilled speech-language therapy 2x/week for 45-minute sessions focusing on advanced language formulation and pragmatic language skills. Will introduce higher-level inferencing tasks and complex sentence structures. Recommend reassessment in 4 weeks to evaluate progress toward long-term goals.`,
};

const MOCK_CPT_CODES: CptCode[] = [
  {
    code: '92507',
    description: 'Treatment of speech, language, voice, communication, and/or auditory processing disorder; individual',
    rationale: 'Individual speech-language therapy session addressing expressive language deficits with structured therapeutic activities.',
    billableUnits: 3,
  },
];

const MOCK_ICD_CODES: IcdCode[] = [
  {
    code: 'R47.82',
    description: 'Fluency disorder in conditions classified elsewhere',
    rationale: 'Client presents with word-finding difficulties and reduced fluency in expressive language.',
  },
  {
    code: 'R47.89',
    description: 'Other speech disturbances',
    rationale: 'Additional language processing deficits affecting functional communication.',
  },
];

// ── Mock API Setup ────────────────────────────────────────────────────────────

/**
 * Sets up mock API routes for session creation workflow.
 * Call this in test beforeEach or per-test as needed.
 */
export async function setupMockSessionAPI(page: Page) {
  // Mock the documentation orchestration start endpoint
  await page.route('**/api/DocumentationStart', async (route) => {
    const request = route.request();
    const body = JSON.parse(request.postData() || '{}');

    const mockResponse: OrchestrationStartResponse = {
      instanceId: `mock-${Date.now()}`,
      clientId: body.clientId || 'test-client',
      statusQueryGetUri: '/api/status/mock-instance',
      sendEventPostUri: '/api/sendEvent/mock-instance',
      terminatePostUri: '/api/terminate/mock-instance',
      purgeHistoryDeleteUri: '/api/purge/mock-instance',
    };

    await route.fulfill({
      status: 202,
      contentType: 'application/json',
      body: JSON.stringify(mockResponse),
    });
  });

  // Mock the orchestration status endpoint (initially running, then completed)
  let statusCallCount = 0;
  await page.route('**/api/status/**', async (route) => {
    statusCallCount++;

    // First call: Running
    // Second call: Completed with results
    const isComplete = statusCallCount >= 2;

    const mockStatus: OrchestrationStatus = isComplete
      ? {
          instanceId: 'mock-instance',
          runtimeStatus: 'Completed',
          output: {
            restoredNote: MOCK_SOAP_NOTE,
            noteFormat: 'SOAP',
            suggestedCptCodes: MOCK_CPT_CODES,
            suggestedIcdCodes: MOCK_ICD_CODES,
          },
        }
      : {
          instanceId: 'mock-instance',
          runtimeStatus: 'Running',
        };

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mockStatus),
    });
  });

  // Mock PATCH /api/sessions/{clientId}/{sessionDate} (for updates/approval)
  await page.route('**/api/sessions/**', async (route) => {
    if (route.request().method() === 'PATCH') {
      const url = new URL(route.request().url());
      const pathParts = url.pathname.split('/');
      const clientId = pathParts[pathParts.length - 2];
      const sessionDate = pathParts[pathParts.length - 1];

      const requestBody = JSON.parse(route.request().postData() || '{}');

      const mockSession: SessionResponse = {
        clientId: decodeURIComponent(clientId),
        sessionDate: decodeURIComponent(sessionDate),
        therapistName: 'Test Therapist',
        discipline: 'Speech Therapy',
        noteFormat: 'SOAP',
        setting: 'Outpatient',
        payer: 'Commercial',
        sessionDurationMinutes: 45,
        soapNote: requestBody.soapNote || MOCK_SOAP_NOTE,
        suggestedCptCodes: requestBody.suggestedCptCodes || MOCK_CPT_CODES,
        suggestedIcdCodes: requestBody.suggestedIcdCodes || MOCK_ICD_CODES,
        createdAt: new Date().toISOString(),
        isApproved: requestBody.approval?.verifyAndApprove || false,
        approvedBy: requestBody.approval?.verifyAndApprove ? 'Test Therapist' : null,
        approvedAt: requestBody.approval?.verifyAndApprove ? new Date().toISOString() : null,
      };

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockSession),
      });
    } else {
      // Let GET requests pass through to real backend
      await route.continue();
    }
  });
}

/**
 * Sets up mock API with custom SOAP note content.
 * Useful for testing different note formats (DAP, PIE, etc.)
 */
export async function setupMockSessionAPIWithCustomNote(
  page: Page,
  noteContent: {
    subjective: string;
    objective: string;
    assessment: string;
    plan: string;
  },
  noteFormat: string = 'SOAP',
) {
  await page.route('**/api/DocumentationStart', async (route) => {
    const request = route.request();
    const body = JSON.parse(request.postData() || '{}');

    const mockResponse: OrchestrationStartResponse = {
      instanceId: `mock-${Date.now()}`,
      clientId: body.clientId || 'test-client',
      statusQueryGetUri: '/api/status/mock-instance',
      sendEventPostUri: '/api/sendEvent/mock-instance',
      terminatePostUri: '/api/terminate/mock-instance',
      purgeHistoryDeleteUri: '/api/purge/mock-instance',
    };

    await route.fulfill({
      status: 202,
      contentType: 'application/json',
      body: JSON.stringify(mockResponse),
    });
  });

  let statusCallCount = 0;
  await page.route('**/api/status/**', async (route) => {
    statusCallCount++;
    const isComplete = statusCallCount >= 2;

    const mockStatus: OrchestrationStatus = isComplete
      ? {
          instanceId: 'mock-instance',
          runtimeStatus: 'Completed',
          output: {
            restoredNote: noteContent,
            noteFormat,
            suggestedCptCodes: MOCK_CPT_CODES,
            suggestedIcdCodes: MOCK_ICD_CODES,
          },
        }
      : {
          instanceId: 'mock-instance',
          runtimeStatus: 'Running',
        };

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mockStatus),
    });
  });

  await page.route('**/api/sessions/**', async (route) => {
    if (route.request().method() === 'PATCH') {
      const url = new URL(route.request().url());
      const pathParts = url.pathname.split('/');
      const clientId = pathParts[pathParts.length - 2];
      const sessionDate = pathParts[pathParts.length - 1];

      const requestBody = JSON.parse(route.request().postData() || '{}');

      const mockSession: SessionResponse = {
        clientId: decodeURIComponent(clientId),
        sessionDate: decodeURIComponent(sessionDate),
        therapistName: 'Test Therapist',
        discipline: 'Speech Therapy',
        noteFormat,
        setting: 'Outpatient',
        payer: 'Commercial',
        sessionDurationMinutes: 45,
        soapNote: requestBody.soapNote || noteContent,
        suggestedCptCodes: requestBody.suggestedCptCodes || MOCK_CPT_CODES,
        suggestedIcdCodes: requestBody.suggestedIcdCodes || MOCK_ICD_CODES,
        createdAt: new Date().toISOString(),
        isApproved: requestBody.approval?.verifyAndApprove || false,
        approvedBy: requestBody.approval?.verifyAndApprove ? 'Test Therapist' : null,
        approvedAt: requestBody.approval?.verifyAndApprove ? new Date().toISOString() : null,
      };

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockSession),
      });
    } else {
      await route.continue();
    }
  });
}

/**
 * Clears all mock routes. Call this in afterEach if needed.
 */
export async function clearMockAPIs(page: Page) {
  await page.unroute('**/api/DocumentationStart');
  await page.unroute('**/api/status/**');
  await page.unroute('**/api/sessions/**');
}
