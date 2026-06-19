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
    return this.page.getByRole('heading', { name: /dashboard/i });
  }

  get statsCards(): Locator {
    return this.page.locator('[class*="statCard"], [data-testid^="stat-"]');
  }

  get totalSessionsCard(): Locator {
    return this.page.locator('[data-testid="stat-total-sessions"], :has-text("Total Sessions")').first();
  }

  get activeClientsCard(): Locator {
    return this.page.locator('[data-testid="stat-active-clients"], :has-text("Active Clients")').first();
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
    return this.page.getByRole('button', { name: /new session/i });
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
    await this.page.waitForURL(/\/session\/new/);
  }

  async openClientProfile(clientId: string) {
    const clientRow = this.caseloadRows.filter({ hasText: clientId });
    await clientRow.click();
    await this.page.waitForURL(/\/client\//);
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
    await expect(this.title).toBeVisible();
    await expect(this.statsCards.first()).toBeVisible();
    await expect(this.charts.first()).toBeVisible();
    await expect(this.caseloadTable).toBeVisible();
  }

  async assertClientInCaseload(clientId: string) {
    await expect(this.caseloadRows.filter({ hasText: clientId })).toBeVisible();
  }

  async assertClientNotInCaseload(clientId: string) {
    await expect(this.caseloadRows.filter({ hasText: clientId })).not.toBeVisible();
  }
}
