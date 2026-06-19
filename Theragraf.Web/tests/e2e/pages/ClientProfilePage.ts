import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from './BasePage';

/**
 * Client Profile Page Object
 * 
 * Represents the individual client profile page with demographics,
 * goals, session history, and statistics.
 */
export class ClientProfilePage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  /**
   * Navigate to a specific client profile
   */
  async goto(clientId: string) {
    await super.goto(`/client/${clientId}`);
  }

  // Demographics Section
  get demographicsSection(): Locator {
    return this.page.locator('[data-testid="demographics"], :has-text("Demographics")').first();
  }

  get clientIdDisplay(): Locator {
    return this.page.locator(':has-text("Client ID")');
  }

  get editDemographicsButton(): Locator {
    return this.page.getByRole('button', { name: /edit.*demographics/i });
  }

  // Goals Section
  get goalsSection(): Locator {
    return this.page.locator('[data-testid="goals"], :has-text("Goals")').first();
  }

  get addGoalButton(): Locator {
    return this.page.getByRole('button', { name: /add goal/i });
  }

  get suggestGoalsButton(): Locator {
    return this.page.getByRole('button', { name: /suggest.*goals|ai.*goals/i });
  }

  get goalsList(): Locator {
    return this.page.locator('[data-testid="goals-list"], ul, [role="list"]');
  }

  // Session History
  get sessionHistorySection(): Locator {
    return this.page.locator('[data-testid="session-history"], :has-text("Session History")').first();
  }

  get sessionsTable(): Locator {
    return this.page.locator('table, [role="table"]');
  }

  get sessionRows(): Locator {
    return this.page.locator('table tbody tr, [role="row"]:not([role="rowheader"])');
  }

  // Stats
  get statsCards(): Locator {
    return this.page.locator('[class*="statCard"], [data-testid^="stat-"]');
  }

  get totalSessionsCard(): Locator {
    return this.statsCards.filter({ hasText: /total.*sessions/i });
  }

  get lastSessionDateCard(): Locator {
    return this.statsCards.filter({ hasText: /last.*session/i });
  }

  // Actions
  async addGoal(goalData: {
    description: string;
    targetDate?: string;
  }) {
    await this.addGoalButton.click();

    // Fill in goal form
    await this.page.getByLabel(/description/i).fill(goalData.description);

    if (goalData.targetDate) {
      await this.page.getByLabel(/target date/i).fill(goalData.targetDate);
    }

    // Save goal
    await this.page.getByRole('button', { name: /^save$|add/i }).click();

    // Wait for goal to appear in list
    await this.page.locator(`:has-text("${goalData.description}")`).waitFor({ timeout: 10000 });
  }

  async editGoal(oldDescription: string, newDescription: string) {
    const goalItem = this.goalsList.locator(`:has-text("${oldDescription}")`);
    const editButton = goalItem.getByRole('button', { name: /edit/i });

    await editButton.click();

    const descriptionInput = this.page.getByLabel(/description/i);
    await descriptionInput.fill(newDescription);

    await this.page.getByRole('button', { name: /^save$/i }).click();

    // Wait for updated goal
    await this.page.locator(`:has-text("${newDescription}")`).waitFor({ timeout: 10000 });
  }

  async deleteGoal(description: string) {
    const goalItem = this.goalsList.locator(`:has-text("${description}")`);
    const deleteButton = goalItem.getByRole('button', { name: /delete|remove/i });

    await deleteButton.click();

    // Confirm deletion if modal appears
    try {
      await this.page.getByRole('button', { name: /confirm|yes|delete/i }).click({ timeout: 2000 });
    } catch {
      // No confirmation modal
    }

    // Wait for goal to disappear
    await expect(goalItem).not.toBeVisible({ timeout: 10000 });
  }

  async openSession(sessionDate: string) {
    const sessionRow = this.sessionRows.filter({ hasText: sessionDate });
    await sessionRow.click();

    await this.page.waitForURL(/\/session\/\w+/);
  }

  async requestGoalSuggestions() {
    await this.suggestGoalsButton.click();

    // Wait for suggestions to load
    await this.page.locator(':has-text("suggestion")').waitFor({ timeout: 30000 });
  }

  // Assertions
  async assertProfileLoaded(clientId: string) {
    await expect(this.clientIdDisplay).toContainText(clientId);
    await expect(this.goalsSection).toBeVisible();
    await expect(this.sessionHistorySection).toBeVisible();
  }

  async assertGoalPresent(description: string) {
    await expect(this.goalsList.locator(`:has-text("${description}")`)).toBeVisible();
  }

  async assertGoalNotPresent(description: string) {
    await expect(this.goalsList.locator(`:has-text("${description}")`)).not.toBeVisible();
  }

  async assertSessionCount(expectedCount: number) {
    await expect(this.sessionRows).toHaveCount(expectedCount);
  }

  async assertSessionInHistory(sessionDate: string) {
    await expect(this.sessionRows.filter({ hasText: sessionDate })).toBeVisible();
  }
}
