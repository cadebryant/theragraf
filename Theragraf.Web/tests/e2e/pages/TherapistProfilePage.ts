import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from './BasePage';

/**
 * Therapist Profile Page Object
 *
 * Represents the /profile page where a therapist can:
 * - View and edit their personal information (name, credentials, discipline, NPI)
 * - See the profile-setup banner when isConfigured = false
 * - View org/tenant context (org name, plan, AI quota progress bar)
 * - View practice/provider info for group practices
 */
export class TherapistProfilePage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  async goto() {
    await super.goto('/profile');
  }

  // ── Header ─────────────────────────────────────────────────────────────────

  get pageTitle(): Locator {
    return this.page.getByRole('heading', { name: /my profile/i }).or(
      this.page.getByText('My Profile', { exact: true })
    );
  }

  // ── Setup banner (shown when isConfigured === false) ───────────────────────

  get setupBanner(): Locator {
    return this.page.getByRole('group').filter({
      hasText: /profile was created automatically|complete setup/i,
    });
  }

  // ── Therapist info fields (read mode) ─────────────────────────────────────

  get firstNameValue(): Locator {
    return this.page.getByLabel('First Name').or(
      this.page.locator('label:has-text("First Name") ~ *')
    );
  }

  get lastNameValue(): Locator {
    return this.page.getByLabel('Last Name').or(
      this.page.locator('label:has-text("Last Name") ~ *')
    );
  }

  get credentialsValue(): Locator {
    return this.page.getByLabel('Credentials').or(
      this.page.locator('label:has-text("Credentials") ~ *')
    );
  }

  get disciplineValue(): Locator {
    return this.page.getByLabel('Discipline').or(
      this.page.locator('label:has-text("Discipline") ~ *')
    );
  }

  get npiValue(): Locator {
    return this.page.getByLabel(/individual npi/i).or(
      this.page.locator('label:has-text("Individual NPI") ~ *')
    );
  }

  // ── Edit controls ──────────────────────────────────────────────────────────

  get editProfileButton(): Locator {
    return this.page.getByRole('button', { name: /edit profile/i });
  }

  get saveButton(): Locator {
    return this.page.getByRole('button', { name: /^save$/i });
  }

  get cancelButton(): Locator {
    return this.page.getByRole('button', { name: /cancel/i });
  }

  // Input fields visible only while editing
  get firstNameInput(): Locator {
    return this.page.getByLabel('First Name');
  }

  get credentialsInput(): Locator {
    return this.page.getByLabel('Credentials');
  }

  get disciplineSelect(): Locator {
    return this.page.getByLabel('Discipline');
  }

  get npiInput(): Locator {
    return this.page.getByLabel(/individual npi/i);
  }

  // ── Tenant section ─────────────────────────────────────────────────────────

  get organizationSection(): Locator {
    return this.page.locator(':has-text("Organization")').filter({
      hasNot: this.page.locator('button'),
    }).first();
  }

  get orgNameText(): Locator {
    return this.page.getByText('E2E Test Practice');
  }

  get planBadge(): Locator {
    return this.page.getByRole('status').filter({ hasText: /Professional plan/i }).or(
      this.page.locator('.fui-Badge').filter({ hasText: /Professional plan/i })
    );
  }

  get statusBadge(): Locator {
    return this.page.locator(':has-text("Active")').first();
  }

  get quotaProgressBar(): Locator {
    return this.page.locator('[role="progressbar"]');
  }

  get quotaText(): Locator {
    return this.page.getByText(/AI calls/i);
  }

  // ── Provider / Practice section ────────────────────────────────────────────

  get practiceSection(): Locator {
    return this.page.locator(':has-text("Practice")').first();
  }

  get practiceNameText(): Locator {
    return this.page.getByText('E2E Group Practice');
  }

  // ── Assertions ─────────────────────────────────────────────────────────────

  async assertProfileLoaded() {
    await expect(this.pageTitle).toBeVisible({ timeout: 10000 });
  }

  async assertSetupBannerVisible() {
    await expect(this.setupBanner).toBeVisible();
  }

  async assertSetupBannerHidden() {
    await expect(this.setupBanner).not.toBeVisible();
  }

  async assertQuotaDisplayed() {
    await expect(this.quotaProgressBar).toBeVisible();
    await expect(this.quotaText).toBeVisible();
  }

  // ── Actions ────────────────────────────────────────────────────────────────

  async startEdit() {
    await this.editProfileButton.click();
    // Wait for edit form to appear
    await expect(this.saveButton).toBeVisible({ timeout: 3000 });
  }

  async saveEdit() {
    await this.saveButton.click();
    // Wait for read mode to return
    await expect(this.editProfileButton).toBeVisible({ timeout: 5000 });
  }

  async cancelEdit() {
    await this.cancelButton.click();
    await expect(this.editProfileButton).toBeVisible({ timeout: 3000 });
  }
}
