import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from './BasePage';

/**
 * Session Review Page Object
 * 
 * Represents the session review/edit page where therapists can:
 * - View AI-generated SOAP/DAP notes
 * - Edit note sections
 * - Modify CPT and ICD-10 codes
 * - Approve or reject the session
 */
export class SessionReviewPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  /**
   * Navigate to a specific session review page
   */
  async goto(sessionId: string) {
    await super.goto(`/session/${sessionId}/review`);
  }

  // Status Elements
  get statusBadge(): Locator {
    return this.page.locator('[data-testid="status-badge"], [class*="badge"]');
  }

  get aiDraftBanner(): Locator {
    return this.page.getByRole('group').filter({ hasText: /AI-generated/i }).first();
  }

  // SOAP/DAP Note Elements
  get subjectiveSection(): Locator {
    return this.page.getByLabel(/subjective|data/i);
  }

  get objectiveSection(): Locator {
    return this.page.getByLabel(/objective/i);
  }

  get assessmentSection(): Locator {
    return this.page.getByLabel(/assessment/i);
  }

  get planSection(): Locator {
    return this.page.getByLabel(/plan/i);
  }

  // CPT Codes
  get cptCodesSection(): Locator {
    // Find the card/section that contains "CPT Codes" text, then find its table
    return this.page.locator(':has-text("CPT Codes")').locator('..').getByRole('table').first();
  }

  get addCptCodeButton(): Locator {
    return this.page.getByRole('button', { name: /add.*code|add cpt/i });
  }

  // ICD-10 Codes
  get icd10CodesSection(): Locator {
    // Find the card/section that contains "ICD-10 Codes" text, then find its table
    return this.page.locator(':has-text("ICD-10 Codes")').locator('..').getByRole('table').first();
  }

  get addIcd10CodeButton(): Locator {
    return this.page.getByRole('button', { name: /add.*icd/i });
  }

  // Action Buttons
  get attestationCheckbox(): Locator {
    return this.page.locator('input[type="checkbox"]').filter({ 
      hasText: /reviewed.*draft.*accept.*responsibility|clinical accuracy/i 
    }).or(
      this.page.locator('label:has-text("reviewed")').locator('input[type="checkbox"]')
    );
  }

  get verifyAndApproveButton(): Locator {
    return this.page.getByRole('button', { name: /verify.*approve|approve/i });
  }

  get saveButton(): Locator {
    return this.page.getByRole('button', { name: /save.*draft|save/i });
  }

  get rejectButton(): Locator {
    return this.page.getByRole('button', { name: /reject/i });
  }

  get backButton(): Locator {
    return this.page.getByRole('button', { name: /back|cancel/i });
  }

  // Actions
  async editSoapNote(sections: {
    subjective?: string;
    objective?: string;
    assessment?: string;
    plan?: string;
  }) {
    if (sections.subjective) {
      await this.subjectiveSection.fill(sections.subjective);
    }

    if (sections.objective) {
      await this.objectiveSection.fill(sections.objective);
    }

    if (sections.assessment) {
      await this.assessmentSection.fill(sections.assessment);
    }

    if (sections.plan) {
      await this.planSection.fill(sections.plan);
    }
  }

  async addCptCode(code: string, description: string, units: number = 1) {
    await this.addCptCodeButton.click();

    // Fill in the new CPT code form
    await this.page.getByLabel(/^code$/i).last().fill(code);
    await this.page.getByLabel(/description/i).last().fill(description);
    await this.page.getByLabel(/units/i).last().fill(units.toString());

    // Click add/save button
    await this.page.getByRole('button', { name: /^add$/i }).last().click();
  }

  async addIcd10Code(code: string, description: string) {
    await this.addIcd10CodeButton.click();

    // Fill in the new ICD-10 code form
    await this.page.getByLabel(/^code$/i).last().fill(code);
    await this.page.getByLabel(/description/i).last().fill(description);

    // Click add/save button
    await this.page.getByRole('button', { name: /^add$/i }).last().click();
  }

  async removeCptCode(code: string) {
    const row = this.cptCodesSection.locator(`tr:has-text("${code}")`);
    const deleteButton = row.getByRole('button', { name: /delete|remove/i });
    await deleteButton.click();
  }

  async removeIcd10Code(code: string) {
    const row = this.icd10CodesSection.locator(`tr:has-text("${code}")`);
    const deleteButton = row.getByRole('button', { name: /delete|remove/i });
    await deleteButton.click();
  }

  async approveSession() {
    // First, check the attestation checkbox
    const checkbox = this.page.locator('input[type="checkbox"]').first();
    await checkbox.check();

    // Wait a moment for the button to become enabled
    await this.page.waitForTimeout(500);

    // Then click the approve button
    await this.verifyAndApproveButton.click();

    // Wait for navigation to the session detail page or for approval confirmation
    await Promise.race([
      this.page.waitForURL(/\/sessions\/.+\/.+/, { timeout: 15000 }),
      this.page.locator(':has-text("approved"), :has-text("Approved")').waitFor({ timeout: 15000 }),
    ]);
  }

  async saveChanges() {
    const currentUrl = this.page.url();
    await this.saveButton.click();

    // Save causes a navigation to the session detail page (same URL)
    // Wait for the saving state to complete
    await this.page.waitForTimeout(1000);
  }

  // Assertions
  async assertReviewPageLoaded() {
    await expect(this.subjectiveSection).toBeVisible({ timeout: 30000 });
    await expect(this.assessmentSection).toBeVisible();
    await expect(this.planSection).toBeVisible();
  }

  async assertAiDraftBannerVisible() {
    await expect(this.aiDraftBanner).toBeVisible();
  }

  async assertCptCodePresent(code: string) {
    await expect(this.cptCodesSection.locator(`:has-text("${code}")`)).toBeVisible();
  }

  async assertIcd10CodePresent(code: string) {
    await expect(this.icd10CodesSection.locator(`:has-text("${code}")`)).toBeVisible();
  }

  async assertStatus(status: string) {
    await expect(this.statusBadge).toContainText(status, { ignoreCase: true });
  }
}
