import { test as setup, expect } from '@playwright/test';
import * as path from 'path';

const authFile = path.join(__dirname, '.auth', 'user.json');

/**
 * Authentication Setup Test
 * 
 * This test runs before all other tests (via the 'setup' project in playwright.config.ts)
 * and performs Azure AD authentication, saving the state for reuse.
 * 
 * This is an alternative to global-setup.ts and can be used if you prefer
 * the authentication to be part of the test suite rather than a global setup script.
 */
setup('authenticate', async ({ page }) => {
  console.log('🔐 Starting authentication setup...');

  // Navigate to the application
  await page.goto('/');

  // Wait for either the login page or the dashboard (if already logged in)
  await page.waitForURL(/login\.microsoftonline\.com|localhost:5173/, { timeout: 30000 });

  const currentUrl = page.url();

  // If we're already on the app (not redirected to login), we're authenticated
  if (!currentUrl.includes('login.microsoftonline.com')) {
    console.log('✅ Already authenticated');
    await expect(page.locator('h1, nav, [data-testid="app-layout"]')).toBeVisible();
    await page.context().storageState({ path: authFile });
    return;
  }

  // Perform Azure AD login
  const testEmail = process.env.TEST_USER_EMAIL;
  const testPassword = process.env.TEST_USER_PASSWORD;

  if (!testEmail || !testPassword) {
    throw new Error(
      'TEST_USER_EMAIL and TEST_USER_PASSWORD must be set in .env.test file. ' +
      'See .env.test.template for details.'
    );
  }

  console.log(`🔑 Logging in as ${testEmail}...`);

  // Azure AD login flow
  const emailInput = page.locator('input[type="email"], input[name="loginfmt"]');
  await expect(emailInput).toBeVisible({ timeout: 10000 });
  await emailInput.fill(testEmail);

  const nextButton = page.locator('input[type="submit"], button[type="submit"]');
  await nextButton.click();

  // Password page
  const passwordInput = page.locator('input[type="password"], input[name="passwd"]');
  await expect(passwordInput).toBeVisible({ timeout: 10000 });
  await passwordInput.fill(testPassword);

  await page.locator('input[type="submit"], button[type="submit"]').click();

  // Handle "Stay signed in?" prompt (may or may not appear)
  try {
    const staySignedInButton = page.locator(
      'input[type="submit"]:has-text("Yes"), button[type="submit"]:has-text("Yes")'
    );
    await staySignedInButton.click({ timeout: 5000 });
  } catch (e) {
    // Prompt didn't appear, continue
    console.log('ℹ️ "Stay signed in" prompt did not appear');
  }

  // Wait for redirect back to application
  await page.waitForURL(/localhost:5173/, { timeout: 30000 });

  // Verify we're authenticated by checking for app UI elements
  await expect(page.locator('h1, nav, [data-testid="app-layout"]')).toBeVisible({ timeout: 15000 });

  console.log('✅ Authentication successful');

  // Save authentication state
  await page.context().storageState({ path: authFile });
});
