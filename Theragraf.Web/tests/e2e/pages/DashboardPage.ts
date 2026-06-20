import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from './BasePage';

/**
 * Dashboard Page Object
 * 
 * Represents the main dashboard page with therapist statistics,
 * charts, and caseload table.
 */
export class DashboardPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  /**
   * Navigate to the dashboard
   */
  async goto() {
    await super.goto('/');
    await this.waitForChartsToLoad();
  }

  // Page Elements
  get title(): Locator {
    return this.page.getByText('Dashboard', { exact: true });
  }

  get statsCards(): Locator {
    // Stats cards contain labels like "TOTAL SESSIONS", "ACTIVE CLIENTS", etc.
    return this.page.locator(':has-text("TOTAL SESSIONS"), :has-text("ACTIVE CLIENTS"), :has-text("BILLABLE UNITS")');
  }

  get totalSessionsCard(): Locator {
    return this.page.locator(':has-text("TOTAL SESSIONS"), :has-text("Total Sessions")').first();
  }

  get activeClientsCard(): Locator {
    return this.page.locator(':has-text("ACTIVE CLIENTS"), :has-text("Active Clients")').first();
  }

  get charts(): Locator {
    return this.page.locator('[class*="chart"], svg[class*="recharts"]');
  }

  get caseloadTable(): Locator {
    return this.page.locator('table, [role="table"]');
  }

  get caseloadRows(): Locator {
    return this.page.locator('table tbody tr, [role="row"]:not([role="rowheader"])');
  }

  get searchInput(): Locator {
    return this.page.getByPlaceholder(/search/i);
  }

  get newSessionButton(): Locator {
    // Use the header button (not the table row buttons)
    // The header button has both icon and text, and is in the nav area
    return this.page.locator('nav').getByRole('button', { name: /new session/i });
  }

  get dashboardNavButton(): Locator {
    return this.page.getByRole('button', { name: /dashboard/i }).or(
      this.page.locator('a[href="/"]')
    );
  }

  get settingsNavButton(): Locator {
    return this.page.getByRole('button', { name: /settings/i }).or(
      this.page.locator('a[href="/settings"]')
    );
  }

  // Actions
  async waitForChartsToLoad() {
    await expect(this.charts.first()).toBeVisible({ timeout: 15000 });
  }

  async searchCaseload(query: string) {
    await this.searchInput.fill(query);
    // Wait for table to update
    await this.page.waitForTimeout(500);
  }

  async clickNewSession() {
    await this.newSessionButton.click();
    await this.page.waitForURL(/\/sessions\/new/);

    // Ensure testMode query parameter is added
    const currentUrl = new URL(this.page.url());
    if (!currentUrl.searchParams.has('testMode')) {
      await this.page.goto(`${currentUrl.pathname}?testMode=true`);
    }
  }

  async openClientProfile(clientId: string) {
    const clientRow = this.caseloadRows.filter({ hasText: clientId });
    const viewButton = clientRow.getByRole('button', { name: /view/i });
    await viewButton.click();
    await this.page.waitForURL(/\/sessions\/.+/);
  }

  async getStatValue(statName: string): Promise<string> {
    const statCard = this.page.locator(`:has-text("${statName}")`).first();
    await expect(statCard).toBeVisible();

    // Try to find a number in the card
    const text = await statCard.textContent();
    const match = text?.match(/\d+/);
    return match ? match[0] : '0';
  }

  async getCaseloadCount(): Promise<number> {
    await expect(this.caseloadTable).toBeVisible();
    return await this.caseloadRows.count();
  }

  // Assertions
  async assertDashboardLoaded() {
    // Wait for the page title to appear
    await expect(this.title).toBeVisible();

    // Wait for either the spinner to disappear or data to appear
    // The dashboard shows a spinner on first load, then renders stats/charts/table
    await this.page.waitForFunction(() => {
      const spinners = document.querySelectorAll('[role="progressbar"], [aria-label*="Loading"]');
      return spinners.length === 0 || document.querySelector('table, [role="table"]') !== null;
    }, { timeout: 30000 }); // Dashboard can be slow on cold start

    // Now assert that data components are visible (with generous timeout)
    await expect(this.statsCards.first()).toBeVisible({ timeout: 10000 });
    await expect(this.caseloadTable).toBeVisible();
  }

  async assertClientInCaseload(clientId: string) {
    await expect(this.caseloadRows.filter({ hasText: clientId })).toBeVisible();
  }

  async assertClientNotInCaseload(clientId: string) {
    await expect(this.caseloadRows.filter({ hasText: clientId })).not.toBeVisible();
  }
}
