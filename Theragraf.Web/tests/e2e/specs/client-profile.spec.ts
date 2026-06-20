import { test, expect, TestData } from '../fixtures/test-fixtures';
import { ClientProfilePage, NewSessionPage, DashboardPage } from '../pages';

/**
 * Client Profile E2E Tests
 * 
 * Tests for the individual client profile page including:
 * - Demographics display
 * - Goals management (add, edit, delete)
 * - AI goal suggestions
 * - Session history
 * - Statistics
 */

test.describe('Client Profile', () => {
  let clientId: string;
  let clientProfile: ClientProfilePage;

  test.beforeAll(async ({ browser }, testInfo) => {
    // Create a test client with a session for profile testing
    clientId = TestData.generateClientId();

    // Create page with auth state (must load storageState manually in beforeAll)
    const context = await browser.newContext({
      storageState: 'tests/e2e/.auth/user.json'
    });
    const page = await context.newPage();
    const newSession = new NewSessionPage(page);

    try {
      await newSession.goto();
      await newSession.createSession({
        clientId,
        discipline: 'OccupationalTherapy',
        noteFormat: 'Soap',
        setting: 'Outpatient',
        payer: 'Medicare',
        transcript: 'Initial session for client profile testing.',
      });

      // Wait for processing
      await page.waitForURL(/\/(session|status)/, { timeout: 90000 });

      console.log(`✅ Created test client: ${clientId}`);
    } catch (error) {
      console.error('❌ Failed to create test client:', error);
      throw error; // Re-throw to fail the setup
    } finally {
      await context.close();
    }
  });

  test.beforeEach(async ({ page }) => {
    clientProfile = new ClientProfilePage(page);
    await clientProfile.goto(clientId);
  });

  test('should load client profile with all sections', async () => {
    await clientProfile.assertProfileLoaded(clientId);

    // Verify main sections are visible
    await expect(clientProfile.demographicsSection).toBeVisible();
    await expect(clientProfile.goalsSection).toBeVisible();
    await expect(clientProfile.sessionHistorySection).toBeVisible();

    console.log('✅ Client profile loaded with all sections');
  });

  test('should display client demographics', async () => {
    await expect(clientProfile.clientIdDisplay).toContainText(clientId);
    await expect(clientProfile.demographicsSection).toBeVisible();

    console.log('✅ Demographics section displayed');
  });

  test('should display session history', async () => {
    await expect(clientProfile.sessionHistorySection).toBeVisible();
    await expect(clientProfile.sessionsTable).toBeVisible();

    // Should have at least 1 session (created in beforeAll)
    const sessionCount = await clientProfile.sessionRows.count();
    expect(sessionCount).toBeGreaterThanOrEqual(1);

    console.log(`📋 Session history: ${sessionCount} sessions`);
  });

  test('should add a new goal', async () => {
    const goalData = TestData.generateGoalData();

    await clientProfile.addGoal({
      description: goalData.description,
      targetDate: goalData.targetDate,
    });

    // Verify goal appears in list
    await clientProfile.assertGoalPresent(goalData.description);

    console.log('✅ Goal added successfully');
  });

  test('should edit an existing goal', async ({ page }) => {
    // First, add a goal to edit
    const originalGoal = 'E2E Test: Original goal description';
    const updatedGoal = 'E2E Test: Updated goal description';

    await clientProfile.addGoal({
      description: originalGoal,
    });

    // Edit the goal
    await clientProfile.editGoal(originalGoal, updatedGoal);

    // Verify updated goal is present
    await clientProfile.assertGoalPresent(updatedGoal);
    await clientProfile.assertGoalNotPresent(originalGoal);

    console.log('✅ Goal edited successfully');
  });

  test('should delete a goal', async () => {
    // Add a goal to delete
    const goalToDelete = 'E2E Test: Goal to be deleted';

    await clientProfile.addGoal({
      description: goalToDelete,
    });

    // Verify it's there
    await clientProfile.assertGoalPresent(goalToDelete);

    // Delete the goal
    await clientProfile.deleteGoal(goalToDelete);

    // Verify it's gone
    await clientProfile.assertGoalNotPresent(goalToDelete);

    console.log('✅ Goal deleted successfully');
  });

  test('should request AI goal suggestions', async ({ page }) => {
    // Check if suggest button is available
    const suggestButton = clientProfile.suggestGoalsButton;

    if (!await suggestButton.isVisible({ timeout: 2000 })) {
      console.log('⏭️ Goal suggestions feature not available');
      test.skip();
      return;
    }

    await clientProfile.requestGoalSuggestions();

    // Verify suggestions appear
    await expect(page.locator(':has-text("suggestion")')).toBeVisible({ timeout: 30000 });

    console.log('✅ AI goal suggestions loaded');
  });

  test('should navigate to session from history', async ({ page }) => {
    const sessionCount = await clientProfile.sessionRows.count();

    if (sessionCount === 0) {
      console.log('⏭️ No sessions in history');
      test.skip();
      return;
    }

    // Get the date of the first session
    const firstRow = clientProfile.sessionRows.first();
    const rowText = await firstRow.textContent();

    // Look for a date pattern (adjust regex based on your date format)
    const dateMatch = rowText?.match(/\d{1,2}\/\d{1,2}\/\d{4}|\d{4}-\d{2}-\d{2}/);

    if (!dateMatch) {
      console.log('⏭️ Could not extract session date');
      test.skip();
      return;
    }

    await clientProfile.openSession(dateMatch[0]);

    // Verify navigation to session detail page
    await expect(page).toHaveURL(/\/session\/[\w-]+/);

    console.log('✅ Navigated to session from history');
  });

  test('should display client statistics', async () => {
    // Verify stats cards are visible
    await expect(clientProfile.statsCards.first()).toBeVisible();

    // Try to find total sessions stat
    const totalSessionsVisible = await clientProfile.totalSessionsCard
      .isVisible({ timeout: 2000 })
      .catch(() => false);

    if (totalSessionsVisible) {
      const sessionsText = await clientProfile.totalSessionsCard.textContent();
      console.log(`📊 Client stats: ${sessionsText}`);
    }

    console.log('✅ Statistics displayed');
  });

  test('should handle empty goals list', async ({ page }) => {
    // If there are goals, this test will just verify the UI handles them
    const goalItems = await clientProfile.goalsList
      .locator('> *')
      .count()
      .catch(() => 0);

    if (goalItems === 0) {
      // Verify empty state or placeholder
      const emptyMessage = page.locator(':has-text("No goals"), :has-text("Add a goal")');
      const hasEmptyState = await emptyMessage.isVisible({ timeout: 2000 }).catch(() => false);

      if (hasEmptyState) {
        console.log('✅ Empty goals state displayed');
      } else {
        console.log('ℹ️ No explicit empty state for goals');
      }
    } else {
      console.log(`ℹ️ Client has ${goalItems} goals`);
    }
  });

  test('should persist profile data on refresh', async ({ page }) => {
    // Get current client ID
    const originalClientId = await clientProfile.clientIdDisplay.textContent();

    // Refresh page
    await page.reload();

    // Verify profile loads again
    await clientProfile.assertProfileLoaded(clientId);

    const refreshedClientId = await clientProfile.clientIdDisplay.textContent();
    expect(refreshedClientId).toBe(originalClientId);

    console.log('✅ Profile data persisted after refresh');
  });

  test('should navigate back to dashboard', async ({ page }) => {
    const dashboard = new DashboardPage(page);

    // Navigate to dashboard
    await dashboard.goto();

    // Verify we're back on dashboard
    await dashboard.assertDashboardLoaded();

    // Client should appear in caseload
    await dashboard.assertClientInCaseload(clientId);

    console.log('✅ Navigated back to dashboard');
  });
});
