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
    return this.page.locator('[data-testid="ai-draft-banner"], :has-text("AI-generated")');
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
    return this.page.locator(':has-text("CPT Codes")').locator('..').locator('table, [role="table"]');
  }

  get addCptCodeButton(): Locator {
    return this.page.getByRole('button', { name: /add.*cpt/i });
  }

  // ICD-10 Codes
  get icd10CodesSection(): Locator {
    return this.page.locator(':has-text("ICD-10")').locator('..').locator('table, [role="table"]');
  }

  get addIcd10CodeButton(): Locator {
    return this.page.getByRole('button', { name: /add.*icd/i });
  }

  // Action Buttons
  get verifyAndApproveButton(): Locator {
    return this.page.getByRole('button', { name: /verify.*approve|approve/i });
  }

  get saveButton(): Locator {
    return this.page.getByRole('button', { name: /^save$/i });
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
    await this.verifyAndApproveButton.click();

    // Wait for navigation or success message
    await Promise.race([
      this.page.waitForURL(/\/session\/\w+$/, { timeout: 15000 }),
      this.page.locator(':has-text("approved")').waitFor({ timeout: 15000 }),
    ]);
  }

  async saveChanges() {
    await this.saveButton.click();

    // Wait for save confirmation
    await this.page.locator(':has-text("saved")').waitFor({ timeout: 10000 });
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
