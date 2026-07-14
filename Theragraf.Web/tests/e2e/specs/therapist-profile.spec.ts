import { test, expect } from '../fixtures/test-fixtures';
import { TherapistProfilePage } from '../pages';
import {
  setupMockProfileAPI,
  clearMockProfileAPIs,
  MOCK_THERAPIST_PROFILE_CONFIGURED,
  MOCK_TENANT,
  MOCK_PROVIDER,
} from '../helpers/mockAPI';

/**
 * Therapist Profile E2E Tests
 *
 * Covers the /profile page including:
 * - Page load and basic content
 * - Setup banner visibility based on isConfigured flag
 * - Tenant org context and AI quota display
 * - Edit/save profile workflow
 * - Provider / practice info section (group practice)
 * - Navigation to profile from the top nav icon
 */

test.describe('Therapist Profile', () => {
  let profilePage: TherapistProfilePage;

  test.beforeEach(async ({ page }) => {
    profilePage = new TherapistProfilePage(page);
  });

  test.afterEach(async ({ page }) => {
    await clearMockProfileAPIs(page);
  });

  // ── Load ───────────────────────────────────────────────────────────────────

  test('should load profile page with therapist info', async ({ page }) => {
    await setupMockProfileAPI(page);
    await profilePage.goto();
    await profilePage.assertProfileLoaded();

    // Therapist name should be visible somewhere on the page
    await expect(
      page.getByText(MOCK_THERAPIST_PROFILE_CONFIGURED.firstName)
    ).toBeVisible();
    await expect(
      page.getByText(MOCK_THERAPIST_PROFILE_CONFIGURED.lastName)
    ).toBeVisible();

    console.log('✅ Profile page loaded with therapist info');
  });

  test('should display credentials and NPI in read mode', async ({ page }) => {
    await setupMockProfileAPI(page);
    await profilePage.goto();
    await profilePage.assertProfileLoaded();

    // Credentials and NPI displayed
    await expect(
      page.getByText(MOCK_THERAPIST_PROFILE_CONFIGURED.credentials!)
    ).toBeVisible();
    await expect(
      page.getByText(MOCK_THERAPIST_PROFILE_CONFIGURED.individualNpi!)
    ).toBeVisible();

    console.log('✅ Credentials and NPI visible in read mode');
  });

  // ── Setup Banner ───────────────────────────────────────────────────────────

  test('should NOT show setup banner when profile is configured', async ({ page }) => {
    await setupMockProfileAPI(page); // isConfigured: true
    await profilePage.goto();
    await profilePage.assertProfileLoaded();
    await profilePage.assertSetupBannerHidden();

    console.log('✅ Setup banner correctly hidden for configured profile');
  });

  test('should show setup banner when profile is not configured', async ({ page }) => {
    await setupMockProfileAPI(page, { unconfigured: true });
    await profilePage.goto();
    await profilePage.assertProfileLoaded();
    await profilePage.assertSetupBannerVisible();

    console.log('✅ Setup banner visible for unconfigured profile');
  });

  // ── Tenant / Org Section ───────────────────────────────────────────────────

  test('should display organization name and plan', async ({ page }) => {
    await setupMockProfileAPI(page);
    await profilePage.goto();
    await profilePage.assertProfileLoaded();

    await expect(page.getByText(MOCK_TENANT.organizationName)).toBeVisible();
    await expect(profilePage.planBadge).toBeVisible();

    console.log('✅ Organization name and plan badge visible');
  });

  test('should display AI quota progress bar and usage text', async ({ page }) => {
    await setupMockProfileAPI(page);
    await profilePage.goto();
    await profilePage.assertProfileLoaded();
    await profilePage.assertQuotaDisplayed();

    // Verify the usage numbers appear in the quota text
    const quotaEl = profilePage.quotaText.first();
    await expect(quotaEl).toContainText(MOCK_TENANT.aiCallsThisPeriod.toString());
    await expect(quotaEl).toContainText(MOCK_TENANT.monthlyAiCallQuota!.toString());

    console.log(
      `✅ Quota displayed: ${MOCK_TENANT.aiCallsThisPeriod}/${MOCK_TENANT.monthlyAiCallQuota} calls`
    );
  });

  // ── Edit Profile ───────────────────────────────────────────────────────────

  test('should enter and exit edit mode', async ({ page }) => {
    await setupMockProfileAPI(page);
    await profilePage.goto();
    await profilePage.assertProfileLoaded();

    // Enter edit mode
    await profilePage.startEdit();
    await expect(profilePage.saveButton).toBeVisible();
    await expect(profilePage.cancelButton).toBeVisible();

    // Cancel — return to read mode
    await profilePage.cancelEdit();
    await expect(profilePage.editProfileButton).toBeVisible();

    console.log('✅ Edit mode enter/exit works');
  });

  test('should save edited credentials and return to read mode', async ({ page }) => {
    await setupMockProfileAPI(page);
    await profilePage.goto();
    await profilePage.assertProfileLoaded();

    await profilePage.startEdit();

    // Update credentials
    const newCredentials = 'PT, DPT';
    const credInput = profilePage.credentialsInput;
    await credInput.clear();
    await credInput.fill(newCredentials);

    // Save — mock PATCH returns the updated profile
    await profilePage.saveEdit();

    // Read mode should now show updated value
    await expect(page.getByText(newCredentials)).toBeVisible();

    console.log(`✅ Credentials updated to "${newCredentials}"`);
  });

  test('should not close edit mode on save when fields are invalid (empty first name)', async ({
    page,
  }) => {
    await setupMockProfileAPI(page);
    await profilePage.goto();
    await profilePage.assertProfileLoaded();

    await profilePage.startEdit();

    // Clear required first-name field
    const firstNameInput = page.getByLabel('First Name');
    await firstNameInput.clear();

    // Save button should be disabled (form guard)
    await expect(profilePage.saveButton).toBeDisabled();

    // Cancel to clean up
    await profilePage.cancelEdit();

    console.log('✅ Save button disabled when required field is empty');
  });

  // ── Provider Section ───────────────────────────────────────────────────────

  test('should show practice section for group practice profile', async ({ page }) => {
    await setupMockProfileAPI(page, { withProvider: true });
    await profilePage.goto();
    await profilePage.assertProfileLoaded();

    await expect(profilePage.practiceNameText).toBeVisible();
    await expect(page.getByText(MOCK_PROVIDER.organizationNpi!)).toBeVisible();

    console.log(`✅ Practice section visible: ${MOCK_PROVIDER.practiceName}`);
  });

  test('should NOT show practice section for solo practitioner', async ({ page }) => {
    await setupMockProfileAPI(page); // no providerId
    await profilePage.goto();
    await profilePage.assertProfileLoaded();

    await expect(profilePage.practiceNameText).not.toBeVisible();

    console.log('✅ Practice section correctly hidden for solo practitioner');
  });

  // ── Navigation ─────────────────────────────────────────────────────────────

  test('should navigate to profile from the nav icon', async ({ page }) => {
    await setupMockProfileAPI(page);

    // Start from dashboard — suppress tour so it doesn't block nav clicks
    await page.addInitScript(() => {
      try { localStorage.setItem('theragraf:tourCompleted:v1', 'true'); } catch { /* ignore */ }
    });
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');

    // Click the Profile nav icon (aria-label "Go to My Profile")
    await page.getByRole('button', { name: /my profile/i }).click();

    await expect(page).toHaveURL(/\/profile/);
    await profilePage.assertProfileLoaded();

    console.log('✅ Profile nav icon navigates to /profile');
  });
});
