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
  TenantSummaryResponse,
  TherapistProfileResponse,
  ProviderResponse,
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
  // Mock the caseload endpoint for dashboard
  await page.route('**/api/caseload', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        therapistName: 'Test Therapist',
        totalSessions: 0,
        totalClients: 0,
        clients: [],
      }),
    });
  });

  // Mock the stats endpoint for dashboard
  await page.route('**/api/stats/**', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        totalSessions: 0,
        totalClients: 0,
        billableUnits: 0,
        sessionsByDate: [],
        sessionsByDiscipline: [],
      }),
    });
  });

  // Mock the sessions list endpoint for dashboard
  await page.route('**/api/sessions', async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          sessions: [],
          totalCount: 0,
        }),
      });
    } else {
      // Let POST requests through to the orchestration mock
      await route.continue();
    }
  });

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
    } else if (route.request().method() === 'GET') {
      // Mock GET requests for dashboard/caseload
      const url = new URL(route.request().url());

      // Check if it's a caseload request (no client ID in path)
      if (url.pathname.endsWith('/sessions') || url.pathname.match(/\/sessions$/)) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            clients: [],
            total: 0,
          }),
        });
      } else {
        // Let other GET requests pass through or mock as needed
        // Mock individual client retrieval
        if (route.request().url().includes('/api/clients/')) {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              id: 'test-client',
              name: 'Test Client',
              dateOfBirth: '1990-01-01',
              therapistId: 'test-therapist',
            }),
          });
        } else {
          await route.continue();
        }
      }
    } else {
      // Let other methods pass through
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

// ── Profile / Multi-tenancy Mock Data ────────────────────────────────────────

export const MOCK_TENANT: TenantSummaryResponse = {
  tenantId: 'tenant-e2e-test',
  organizationName: 'E2E Test Practice',
  organizationType: 'SoloPractitioner',
  plan: 'Professional',
  aiCallsThisPeriod: 42,
  monthlyAiCallQuota: 500,
  status: 'Active',
  isSynthetic: false,
};

export const MOCK_THERAPIST_PROFILE_CONFIGURED: TherapistProfileResponse = {
  therapistId: 'therapist-e2e-test',
  tenantId: 'tenant-e2e-test',
  firstName: 'Playwright',
  lastName: 'E2EUser',
  credentials: 'OTR/L',
  discipline: 'OccupationalTherapy',
  individualNpi: '1234567890',
  providerId: null,
  isConfigured: true,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
};

export const MOCK_THERAPIST_PROFILE_UNCONFIGURED: TherapistProfileResponse = {
  ...MOCK_THERAPIST_PROFILE_CONFIGURED,
  credentials: null,
  individualNpi: null,
  isConfigured: false,
};

export const MOCK_PROVIDER: ProviderResponse = {
  providerId: 'provider-e2e-test',
  tenantId: 'tenant-e2e-test',
  practiceName: 'E2E Group Practice',
  organizationNpi: '9876543210',
  addressLine1: '123 Test St',
  addressLine2: null,
  city: 'Testville',
  state: 'TX',
  zip: '75001',
  phone: '555-0100',
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
};

/**
 * Sets up mock routes for the Therapist Profile page.
 * Defaults to a fully configured profile. Pass `unconfigured: true` to test
 * the setup banner state.
 */
export async function setupMockProfileAPI(
  page: Page,
  options: { unconfigured?: boolean; withProvider?: boolean } = {},
) {
  const profile = options.unconfigured
    ? MOCK_THERAPIST_PROFILE_UNCONFIGURED
    : options.withProvider
      ? { ...MOCK_THERAPIST_PROFILE_CONFIGURED, providerId: MOCK_PROVIDER.providerId }
      : MOCK_THERAPIST_PROFILE_CONFIGURED;

  // GET /api/tenant
  await page.route('**/api/tenant', async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_TENANT),
      });
    } else {
      await route.continue();
    }
  });

  // GET and PATCH /api/therapists/me
  await page.route('**/api/therapists/me', async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(profile),
      });
    } else if (route.request().method() === 'PATCH') {
      const body = JSON.parse(route.request().postData() || '{}');
      const updated: TherapistProfileResponse = {
        ...profile,
        ...body,
        isConfigured: true,
        updatedAt: new Date().toISOString(),
      };
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(updated),
      });
    } else {
      await route.continue();
    }
  });

  // GET /api/providers/{providerId}
  if (options.withProvider) {
    await page.route(`**/api/providers/${MOCK_PROVIDER.providerId}`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_PROVIDER),
      });
    });
  }
}

/**
 * Clears mock routes added by setupMockProfileAPI.
 */
export async function clearMockProfileAPIs(page: Page) {
  await page.unroute('**/api/tenant');
  await page.unroute('**/api/therapists/me');
  await page.unroute(`**/api/providers/${MOCK_PROVIDER.providerId}`);
}
