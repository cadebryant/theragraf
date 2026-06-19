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
    await super.goto('/session/new');
  }

  // Form Elements
  get clientIdInput(): Locator {
    return this.page.getByLabel(/client id/i);
  }

  get disciplineSelect(): Locator {
    return this.page.getByLabel(/discipline/i);
  }

  get noteFormatSelect(): Locator {
    return this.page.getByLabel(/note format/i);
  }

  get settingSelect(): Locator {
    return this.page.getByLabel(/setting/i).nth(0);
  }

  get payerSelect(): Locator {
    return this.page.getByLabel(/payer/i);
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
