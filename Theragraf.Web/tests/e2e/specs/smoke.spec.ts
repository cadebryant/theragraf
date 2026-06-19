import { test, expect } from '@playwright/test';

/**
 * Smoke Test
 * 
 * Basic test to verify E2E test infrastructure is working.
 * This test should pass even without authentication or a running backend.
 */

test.describe('E2E Infrastructure Smoke Test', () => {
  test('Playwright is configured correctly', async ({ page }) => {
    // This test just verifies Playwright can create a page
    await page.goto('about:blank');
    expect(page).toBeDefined();

    console.log('✅ Playwright configuration is working');
  });

  test('Page objects can be imported', async () => {
    // Verify TypeScript compilation of page objects
    const { DashboardPage } = await import('../pages');
    expect(DashboardPage).toBeDefined();

    console.log('✅ Page objects compile successfully');
  });

  test('Test fixtures can be imported', async () => {
    // Verify TypeScript compilation of test fixtures
    const { TestData } = await import('../fixtures/test-fixtures');
    expect(TestData).toBeDefined();

    const clientId = TestData.generateClientId();
    expect(clientId).toContain('e2e-test-client');

    console.log('✅ Test fixtures work correctly');
  });
});
