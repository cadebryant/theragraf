import { test, expect, TestData } from '../fixtures/test-fixtures';
import { DashboardPage, NewSessionPage, SessionReviewPage } from '../pages';
import { setupMockSessionAPI, setupMockSessionAPIWithCustomNote, clearMockAPIs } from '../helpers/mockAPI';

/**
 * Critical Path E2E Tests: Session Creation Flow
 * 
 * These tests cover the main user journey:
 * 1. Navigate to New Session page
 * 2. Fill in session metadata
 * 3. Submit transcript (mock mode)
 * 4. Mock AI processing with realistic responses
 * 5. Review mock SOAP note
 * 6. Edit and approve session
 * 
 * This is the most important flow in the application.
 */

test.describe('Session Creation Flow', () => {
  let clientId: string;

  test.beforeEach(async ({ page }) => {
    // Generate unique client ID for each test
    clientId = TestData.generateClientId();

    // Set up mock API responses
    await setupMockSessionAPI(page);
  });

  test.afterEach(async ({ page }) => {
    // Clean up mock routes
    await clearMockAPIs(page);
  });

  test('should create a new session with SOAP note', async ({ page }) => {
    const dashboard = new DashboardPage(page);
    const newSession = new NewSessionPage(page);
    const sessionReview = new SessionReviewPage(page);

    // Start from dashboard
    await dashboard.goto();
    await dashboard.assertDashboardLoaded();

    // Navigate to new session
    await dashboard.clickNewSession();
    await newSession.assertFormVisible();

    // Fill in session metadata
    const sessionData = {
      clientId,
      discipline: 'OccupationalTherapy',
      noteFormat: 'Soap',
      setting: 'Outpatient',
      payer: 'Medicare',
      transcript: `
Therapist: Hello, how are you feeling today?
Client: I'm doing okay. My hand is still stiff.
Therapist: Let's work on some range of motion exercises for your wrist.
Client: Sounds good.
Therapist: Can you try making a fist for me?
Client: I can get about halfway.
Therapist: That's better than last week. Let's do some gentle stretches.
      `.trim(),
    };

    await newSession.createSession(sessionData);

    // Mock backend will respond immediately, so we should navigate to review page quickly
    await page.waitForURL(/\/session/, { timeout: 30000 });

    // Verify we're on the session review page
    await sessionReview.assertReviewPageLoaded();

    // Verify mock SOAP note content is present
    await expect(sessionReview.subjectiveSection).toBeVisible();
    await expect(sessionReview.objectiveSection).toBeVisible();
    await expect(sessionReview.assessmentSection).toBeVisible();
    await expect(sessionReview.planSection).toBeVisible();

    // Verify billing codes sections are visible
    await expect(sessionReview.cptCodesSection).toBeVisible();
    await expect(sessionReview.icd10CodesSection).toBeVisible();

    console.log('✅ Session created with mocked AI-generated SOAP note');
  });

  test('should allow editing SOAP note before approval', async ({ page }) => {
    const newSession = new NewSessionPage(page);
    const sessionReview = new SessionReviewPage(page);

    // Create a session
    await newSession.goto();

    const sessionData = {
      clientId,
      discipline: 'PhysicalTherapy',
      noteFormat: 'Soap',
      setting: 'Outpatient',
      payer: 'Commercial',
      transcript: 'Brief test session transcript for editing test.',
    };

    await newSession.createSession(sessionData);

    // Mock backend responds immediately
    await page.waitForURL(/\/session/, { timeout: 30000 });

    await sessionReview.assertReviewPageLoaded();

    // Edit SOAP sections
    const customText = 'E2E Test: Edited by automated test';
    await sessionReview.editSoapNote({
      subjective: `${customText} - Subjective`,
      objective: `${customText} - Objective`,
      assessment: `${customText} - Assessment`,
      plan: `${customText} - Plan`,
    });

    // Save changes
    await sessionReview.saveChanges();

    // Verify edits persisted
    await expect(sessionReview.subjectiveSection).toContainText(customText);
    await expect(sessionReview.objectiveSection).toContainText(customText);
    await expect(sessionReview.assessmentSection).toContainText(customText);
    await expect(sessionReview.planSection).toContainText(customText);

    console.log('✅ SOAP note edited successfully');
  });

  test('should add and remove CPT codes', async ({ page }) => {
    const newSession = new NewSessionPage(page);
    const sessionReview = new SessionReviewPage(page);

    // Create a minimal session
    await newSession.goto();
    await newSession.createSession({
      clientId,
      discipline: 'OccupationalTherapy',
      noteFormat: 'Soap',
      setting: 'Outpatient',
      payer: 'Medicare',
      transcript: 'Short test transcript for CPT code testing.',
    });

    // Mock backend responds immediately
    await page.waitForURL(/\/session/, { timeout: 30000 });

    await sessionReview.assertReviewPageLoaded();

    // Add a custom CPT code
    const testCptCode = '97110';
    const testCptDescription = 'Therapeutic exercises';

    await sessionReview.addCptCode(testCptCode, testCptDescription, 2);
    await sessionReview.assertCptCodePresent(testCptCode);

    // Remove the CPT code
    await sessionReview.removeCptCode(testCptCode);

    // Verify it's removed
    await expect(
      sessionReview.cptCodesSection.locator(`:has-text("${testCptCode}")`)
    ).not.toBeVisible();

    console.log('✅ CPT code add/remove successful');
  });

  test('should approve session and navigate to dashboard', async ({ page }) => {
    const newSession = new NewSessionPage(page);
    const sessionReview = new SessionReviewPage(page);
    const dashboard = new DashboardPage(page);

    // Create a session
    await newSession.goto();
    await newSession.createSession({
      clientId,
      discipline: 'SpeechLanguagePathology',
      noteFormat: 'Soap',
      setting: 'Outpatient',
      payer: 'Medicaid',
      transcript: 'Test transcript for approval workflow.',
    });

    // Mock backend responds immediately
    await page.waitForURL(/\/session/, { timeout: 30000 });

    await sessionReview.assertReviewPageLoaded();

    // Approve the session
    await sessionReview.approveSession();

    // Verify status changed to approved
    await sessionReview.assertStatus('Approved');

    // Navigate back to dashboard
    await dashboard.goto();
    await dashboard.assertDashboardLoaded();

    // Verify the new client appears in caseload
    await dashboard.assertClientInCaseload(clientId);

    console.log('✅ Session approved successfully');
  });

  test('should handle different note formats (DAP)', async ({ page }) => {
    const newSession = new NewSessionPage(page);
    const sessionReview = new SessionReviewPage(page);

    // Create a session with DAP format
    await newSession.goto();
    await newSession.createSession({
      clientId,
      discipline: 'Psychotherapy',
      noteFormat: 'Dap',
      setting: 'Outpatient',
      payer: 'Commercial',
      transcript: 'Psychotherapy session for DAP note format testing.',
    });

    // Mock backend responds immediately
    await page.waitForURL(/\/session/, { timeout: 30000 });

    await sessionReview.assertReviewPageLoaded();

    // For DAP notes, we should see Data, Assessment, Plan (not Subjective/Objective)
    // The actual field labels depend on the implementation
    await expect(
      page.locator('[data-testid="note-section"], textarea, [contenteditable="true"]')
    ).toHaveCount(3, { timeout: 10000 });

    console.log('✅ DAP note format handled correctly');
  });
});
