import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from './BasePage';

/**
 * New Session Page Object
 * 
 * Represents the session creation page with metadata form,
 * audio recording, and transcript submission.
 */
export class NewSessionPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  /**
   * Navigate to the new session page
   */
  async goto() {
    await super.goto('/sessions/new');

    // Wait for network to be idle after navigation
    await this.page.waitForLoadState('networkidle');

    // Give React time to mount components
    await this.page.waitForTimeout(1000);

    // Wait for the form to be ready
    await this.waitForFormReady();
  }

  /**
   * Wait for the session form to be fully loaded and interactive
   */
  async waitForFormReady() {
    console.log('⏳ Waiting for New Session form to load...');

    // First check if we're on the right page
    const currentUrl = this.page.url();
    console.log(`📍 Current URL: ${currentUrl}`);

    // Check if we got redirected away from /sessions/new
    if (!currentUrl.includes('/sessions/new')) {
      console.error(`❌ Unexpected redirect! Expected /sessions/new but got: ${currentUrl}`);

      // Check if we're on login page
      if (currentUrl.includes('login.microsoftonline.com')) {
        throw new Error('Redirected to Azure AD login - authentication may have failed');
      }

      throw new Error(`Unexpected navigation to: ${currentUrl}`);
    }

    // Wait for the page title or main heading to confirm page loaded
    try {
      await this.page.waitForSelector('h1, h2, [role="heading"]', { timeout: 5000 });
      const pageTitle = await this.page.locator('h1, h2').first().textContent().catch(() => 'Unknown');
      console.log(`📄 Page heading: "${pageTitle}"`);
    } catch {
      console.warn('⚠️  No heading found on page');
    }

    // Try to wait for the form container first
    try {
      await this.page.waitForSelector('form, [role="form"], input[type="text"], input[type="email"]', { timeout: 5000 });
      console.log('✅ Form elements detected');
    } catch {
      console.warn('⚠️  No form elements found');
    }

    // Check if Client ID input exists with multiple strategies
    console.log('🔍 Looking for Client ID input...');

    let clientIdVisible = false;

    // Strategy 1: Label-based selector (preferred)
    clientIdVisible = await this.page.getByLabel(/client id/i).isVisible({ timeout: 2000 }).catch(() => false);

    if (!clientIdVisible) {
      console.log('   Strategy 1 (label) failed, trying placeholder...');
      // Strategy 2: Placeholder-based selector
      clientIdVisible = await this.page.getByPlaceholder(/patient|client/i).isVisible({ timeout: 2000 }).catch(() => false);
    }

    if (!clientIdVisible) {
      console.log('   Strategy 2 (placeholder) failed, trying test ID...');
      // Strategy 3: Test ID or name attribute
      clientIdVisible = await this.page.locator('[name="clientId"], [data-testid*="client"]').first().isVisible({ timeout: 2000 }).catch(() => false);
    }

    console.log(`🔍 Client ID input visible: ${clientIdVisible}`);

    if (!clientIdVisible) {
      // Debug information
      console.error('❌ Form not visible after all strategies');
      console.error('📍 Current URL:', currentUrl);
      console.error('📄 Page title:', await this.page.title());

      // Check what's actually on the page
      const h1Text = await this.page.locator('h1').first().textContent().catch(() => 'No h1 found');
      console.error('📝 First h1:', h1Text);

      // List all input fields on the page
      const inputs = await this.page.locator('input').count();
      console.error(`📊 Found ${inputs} input elements on page`);

      if (inputs > 0) {
        const inputDetails = await this.page.locator('input').evaluateAll((elements) => 
          elements.slice(0, 5).map(el => ({
            type: el.getAttribute('type'),
            name: el.getAttribute('name'),
            placeholder: el.getAttribute('placeholder'),
            id: el.getAttribute('id'),
          }))
        );
        console.error('🔍 Input elements found:', JSON.stringify(inputDetails, null, 2));
      }

      // Take a screenshot
      const screenshotPath = `test-results/form-not-found-${Date.now()}.png`;
      await this.page.screenshot({ path: screenshotPath, fullPage: true });
      console.error(`📸 Screenshot saved: ${screenshotPath}`);
    }

    // Now wait with proper timeout using the primary selector
    await expect(this.clientIdInput).toBeVisible({ timeout: 10000 });
    console.log('✅ New Session form ready');
  }

  // Form Elements
  get clientIdInput(): Locator {
    return this.page.getByLabel(/client id/i);
  }

  get disciplineSelect(): Locator {
    return this.page.getByLabel('Discipline*');
  }

  get noteFormatSelect(): Locator {
    return this.page.getByLabel('Note Format*');
  }

  get settingSelect(): Locator {
    return this.page.getByLabel('Setting*');
  }

  get payerSelect(): Locator {
    return this.page.getByLabel('Payer*');
  }

  get sessionDateInput(): Locator {
    return this.page.getByLabel(/session date.*time/i);
  }

  get durationInput(): Locator {
    return this.page.getByLabel(/duration/i);
  }

  // Recording Elements
  get startRecordingButton(): Locator {
    return this.page.getByRole('button', { name: /start recording/i });
  }

  get stopRecordingButton(): Locator {
    return this.page.getByRole('button', { name: /stop recording/i });
  }

  get transcriptTextarea(): Locator {
    return this.page.locator('textarea, [contenteditable="true"]').first();
  }

  get submitButton(): Locator {
    return this.page.getByRole('button', { name: /submit|process/i });
  }

  get cancelButton(): Locator {
    return this.page.getByRole('button', { name: /cancel/i });
  }

  // Actions
  async fillSessionMetadata(data: {
    clientId: string;
    discipline?: string;
    noteFormat?: string;
    setting?: string;
    payer?: string;
    sessionDate?: string;
    duration?: number;
  }) {
    await this.clientIdInput.fill(data.clientId);

    if (data.discipline) {
      await this.disciplineSelect.selectOption(data.discipline);
    }

    if (data.noteFormat) {
      await this.noteFormatSelect.selectOption(data.noteFormat);
    }

    if (data.setting) {
      await this.settingSelect.selectOption(data.setting);
    }

    if (data.payer) {
      await this.payerSelect.selectOption(data.payer);
    }

    if (data.sessionDate) {
      await this.sessionDateInput.fill(data.sessionDate);
    }

    if (data.duration !== undefined) {
      await this.durationInput.fill(data.duration.toString());
    }
  }

  async enterTranscript(text: string) {
    await this.transcriptTextarea.fill(text);
  }

  async submitSession() {
    await this.submitButton.click();
    // Wait for navigation to review/status page
    await this.page.waitForURL(/\/(session|status)/, { timeout: 60000 });
  }

  async startRecording() {
    await this.startRecordingButton.click();
    await expect(this.stopRecordingButton).toBeVisible({ timeout: 5000 });
  }

  async stopRecording() {
    await this.stopRecordingButton.click();
    await expect(this.startRecordingButton).toBeVisible({ timeout: 5000 });
  }

  /**
   * Create a complete test session with metadata and transcript
   */
  async createSession(sessionData: {
    clientId: string;
    discipline?: string;
    noteFormat?: string;
    setting?: string;
    payer?: string;
    transcript: string;
    sessionDate?: string;
    duration?: number;
  }) {
    await this.fillSessionMetadata({
      clientId: sessionData.clientId,
      discipline: sessionData.discipline || 'OccupationalTherapy',
      noteFormat: sessionData.noteFormat || 'Soap',
      setting: sessionData.setting || 'Outpatient',
      payer: sessionData.payer || 'Medicare',
      sessionDate: sessionData.sessionDate,
      duration: sessionData.duration,
    });

    await this.enterTranscript(sessionData.transcript);
    await this.submitSession();
  }

  // Assertions
  async assertFormVisible() {
    await expect(this.clientIdInput).toBeVisible();
    await expect(this.disciplineSelect).toBeVisible();
    await expect(this.noteFormatSelect).toBeVisible();
    await expect(this.settingSelect).toBeVisible();
    await expect(this.payerSelect).toBeVisible();
  }

  async assertFieldsPopulated(data: {
    clientId?: string;
    discipline?: string;
    noteFormat?: string;
    setting?: string;
    payer?: string;
  }) {
    if (data.clientId) {
      await expect(this.clientIdInput).toHaveValue(data.clientId);
    }

    if (data.discipline) {
      await expect(this.disciplineSelect).toHaveValue(data.discipline);
    }

    if (data.noteFormat) {
      await expect(this.noteFormatSelect).toHaveValue(data.noteFormat);
    }

    if (data.setting) {
      await expect(this.settingSelect).toHaveValue(data.setting);
    }

    if (data.payer) {
      await expect(this.payerSelect).toHaveValue(data.payer);
    }
  }
}
