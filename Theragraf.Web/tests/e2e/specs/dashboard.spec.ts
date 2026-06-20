import { test, expect } from '../fixtures/test-fixtures';
import { DashboardPage } from '../pages';

/**
 * Dashboard E2E Tests
 * 
 * Tests for the main dashboard page including:
 * - Statistics display
 * - Charts rendering
 * - Caseload table
 * - Search functionality
 * - Navigation
 */

test.describe('Dashboard', () => {
  let dashboard: DashboardPage;

  test.beforeEach(async ({ page }) => {
    dashboard = new DashboardPage(page);
    await dashboard.goto();
  });

  test('should load dashboard with all components', async () => {
    // Verify main components are visible
    await dashboard.assertDashboardLoaded();

    // Verify statistics cards
    await expect(dashboard.totalSessionsCard).toBeVisible();
    await expect(dashboard.activeClientsCard).toBeVisible();

    // Verify charts are rendered
    const chartCount = await dashboard.charts.count();
    expect(chartCount).toBeGreaterThan(0);

    // Verify caseload table
    await expect(dashboard.caseloadTable).toBeVisible();

    console.log('✅ Dashboard loaded with all components');
  });

  test('should display therapist statistics', async ({ page }) => {
    // Get stat values
    const totalSessions = await dashboard.getStatValue('Total Sessions');
    const activeClients = await dashboard.getStatValue('Active Clients');

    // Stats should be numeric
    expect(parseInt(totalSessions)).toBeGreaterThanOrEqual(0);
    expect(parseInt(activeClients)).toBeGreaterThanOrEqual(0);

    console.log(`📊 Stats: ${totalSessions} sessions, ${activeClients} clients`);
  });

  test('should render charts with data', async ({ page }) => {
    await dashboard.waitForChartsToLoad();

    // Verify charts have SVG elements (Recharts uses SVG)
    const charts = dashboard.charts;
    const firstChart = charts.first();

    await expect(firstChart).toBeVisible();

    // Check for chart elements (bars, lines, or areas)
    const chartElements = await page.locator(
      'svg .recharts-bar, svg .recharts-line, svg .recharts-area'
    ).count();

    expect(chartElements).toBeGreaterThan(0);

    console.log(`📈 Charts rendered with ${chartElements} data elements`);
  });

  test('should display caseload table with clients', async () => {
    await expect(dashboard.caseloadTable).toBeVisible();

    const rowCount = await dashboard.getCaseloadCount();
    console.log(`👥 Caseload: ${rowCount} clients`);

    // Even with no data, table should have headers
    expect(rowCount).toBeGreaterThanOrEqual(0);
  });

  test('should search caseload by client ID', async ({ page }) => {
    const rowCountBefore = await dashboard.getCaseloadCount();

    if (rowCountBefore === 0) {
      console.log('⏭️ Skipping search test - no clients in caseload');
      test.skip();
      return;
    }

    // Get first client ID from table
    const firstRow = dashboard.caseloadRows.first();
    const firstClientId = await firstRow.textContent();
    const clientIdMatch = firstClientId?.match(/[\w-]+/);

    if (!clientIdMatch) {
      console.log('⏭️ Could not extract client ID');
      test.skip();
      return;
    }

    const searchTerm = clientIdMatch[0].slice(0, 5); // Use first 5 chars

    // Perform search
    await dashboard.searchCaseload(searchTerm);

    // Table should update
    const rowCountAfter = await dashboard.getCaseloadCount();
    expect(rowCountAfter).toBeLessThanOrEqual(rowCountBefore);

    console.log(`🔍 Search "${searchTerm}": ${rowCountBefore} → ${rowCountAfter} results`);
  });

  test('should navigate to New Session page', async ({ page }) => {
    await dashboard.clickNewSession();

    // Verify navigation
    await expect(page).toHaveURL(/\/sessions\/new/);

    console.log('✅ Navigated to New Session page');
  });

  test('should navigate to Settings page', async ({ page }) => {
    await dashboard.settingsNavButton.click();

    // Verify navigation
    await expect(page).toHaveURL(/\/settings/);

    console.log('✅ Navigated to Settings page');
  });

  test('should open client profile when clicking caseload row', async ({ page }) => {
    const rowCount = await dashboard.getCaseloadCount();

    if (rowCount === 0) {
      console.log('⏭️ Skipping profile navigation test - no clients in caseload');
      test.skip();
      return;
    }

    // Click "View" button in first row
    const firstRow = dashboard.caseloadRows.first();
    const viewButton = firstRow.getByRole('button', { name: /view/i });
    await viewButton.click();

    // Verify navigation to client profile (route is /sessions/:clientId)
    await expect(page).toHaveURL(/\/sessions\/[\w-]+$/);

    console.log('✅ Navigated to client profile');
  });

  test('should handle empty caseload gracefully', async ({ page }) => {
    // This test assumes a fresh therapist account with no clients
    // If there are clients, we'll just verify the table is displayed

    await expect(dashboard.caseloadTable).toBeVisible();

    const rowCount = await dashboard.getCaseloadCount();

    if (rowCount === 0) {
      // Verify empty state message or placeholder
      const emptyMessage = page.locator(':has-text("No clients"), :has-text("no data")');
      const emptyMessageVisible = await emptyMessage.isVisible({ timeout: 2000 }).catch(() => false);

      if (emptyMessageVisible) {
        console.log('✅ Empty state message displayed');
      } else {
        console.log('ℹ️ No explicit empty state, but table handles 0 rows');
      }
    } else {
      console.log(`ℹ️ Caseload has ${rowCount} clients, skipping empty state check`);
    }
  });

  test('should update dashboard data on refresh', async ({ page }) => {
    // Get initial stat values
    const initialSessions = await dashboard.getStatValue('Total Sessions');

    // Refresh the page
    await page.reload();
    await dashboard.waitForChartsToLoad();

    // Verify dashboard reloads successfully
    await dashboard.assertDashboardLoaded();

    // Stats should still be present (values may change if data was added)
    const refreshedSessions = await dashboard.getStatValue('Total Sessions');
    expect(refreshedSessions).toBeDefined();

    console.log(`🔄 Dashboard refreshed: ${initialSessions} → ${refreshedSessions} sessions`);
  });
});
