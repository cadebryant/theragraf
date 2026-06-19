import { test as base, expect, Page } from '@playwright/test';

/**
 * Custom test fixtures for Theragraf E2E tests
 * 
 * Fixtures provide a way to set up and tear down test prerequisites.
 * They run before/after each test and can be composed together.
 */

// Extend the base test with custom fixtures
export const test = base.extend<{
  /**
   * Authenticated page that's already logged in via the setup project
   */
  authenticatedPage: Page;

  /**
   * Helper to generate unique test data IDs
   */
  testDataId: string;

  /**
   * Helper to clean up test data after tests
   */
  cleanupTestData: () => Promise<void>;
}>({
  authenticatedPage: async ({ page }, use) => {
    // Page is already authenticated via storageState in playwright.config.ts
    await use(page);
  },

  testDataId: async ({}, use) => {
    // Generate a unique ID for this test run
    const timestamp = Date.now();
    const random = Math.random().toString(36).substring(2, 9);
    const testId = `e2e-test-${timestamp}-${random}`;
    await use(testId);
  },

  cleanupTestData: async ({ page }, use) => {
    const itemsToCleanup: string[] = [];

    // Provide a cleanup function that tests can use
    const cleanup = async () => {
      if (!process.env.TEST_SESSION_CLEANUP_ENABLED) {
        console.log('⏭️ Test data cleanup disabled');
        return;
      }

      console.log(`🧹 Cleaning up ${itemsToCleanup.length} test items...`);

      // TODO: Implement cleanup logic via API calls
      // For now, we'll just log what would be cleaned up
      for (const item of itemsToCleanup) {
        console.log(`  - Would delete: ${item}`);
      }
    };

    // Let the test use the cleanup function
    await use(cleanup);

    // After the test finishes, perform cleanup
    await cleanup();
  },
});

export { expect };

/**
 * Test data helpers
 */
export class TestData {
  /**
   * Generate a unique client ID for testing
   */
  static generateClientId(): string {
    const prefix = process.env.TEST_CLIENT_ID_PREFIX || 'e2e-test-client';
    const timestamp = Date.now();
    const random = Math.random().toString(36).substring(2, 7);
    return `${prefix}-${timestamp}-${random}`;
  }

  /**
   * Generate realistic test session data
   */
  static generateSessionData(clientId: string) {
    return {
      clientId,
      discipline: 'OccupationalTherapy' as const,
      noteFormat: 'Soap' as const,
      setting: 'Outpatient' as const,
      payer: 'Medicare' as const,
      sessionDate: new Date().toISOString().slice(0, 16), // YYYY-MM-DDTHH:mm format
      transcript: 'Test transcript for E2E testing purposes.',
    };
  }

  /**
   * Generate test goal data
   */
  static generateGoalData() {
    return {
      description: 'E2E test goal: Improve fine motor skills',
      targetDate: new Date(Date.now() + 90 * 24 * 60 * 60 * 1000).toISOString().split('T')[0],
      status: 'Active' as const,
    };
  }
}

/**
 * API helpers for direct backend interactions
 */
export class ApiHelpers {
  constructor(private baseUrl: string = process.env.TEST_API_URL || 'http://localhost:7071') {}

  /**
   * Get authentication token from the page context
   */
  static async getAuthToken(page: Page): Promise<string | null> {
    // Extract MSAL token from localStorage
    const token = await page.evaluate(() => {
      const msalKey = Object.keys(localStorage).find(k => k.includes('msal') && k.includes('accessToken'));
      if (!msalKey) return null;

      const tokenData = localStorage.getItem(msalKey);
      if (!tokenData) return null;

      try {
        const parsed = JSON.parse(tokenData);
        return parsed.secret || parsed.accessToken || null;
      } catch {
        return null;
      }
    });

    return token;
  }

  /**
   * Make an authenticated API request
   */
  async request(endpoint: string, options: RequestInit = {}, token?: string): Promise<Response> {
    const url = `${this.baseUrl}${endpoint}`;
    const headers = {
      'Content-Type': 'application/json',
      ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
      ...options.headers,
    };

    return fetch(url, {
      ...options,
      headers,
    });
  }

  /**
   * Delete a test session
   */
  async deleteSession(sessionId: string, token: string): Promise<void> {
    const response = await this.request(
      `/api/sessions/${sessionId}`,
      { method: 'DELETE' },
      token
    );

    if (!response.ok) {
      console.warn(`Failed to delete test session ${sessionId}: ${response.status}`);
    }
  }

  /**
   * Delete a test client and all associated data
   */
  async deleteClient(clientId: string, token: string): Promise<void> {
    const response = await this.request(
      `/api/clients/${clientId}`,
      { method: 'DELETE' },
      token
    );

    if (!response.ok) {
      console.warn(`Failed to delete test client ${clientId}: ${response.status}`);
    }
  }
}
