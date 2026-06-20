import { test as setup, expect, chromium } from '@playwright/test';
import * as path from 'path';
import * as fs from 'fs';
import { fileURLToPath } from 'url';

// ES module equivalent of __dirname
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const authFile = path.join(__dirname, '.auth', 'user.json');

// Ensure .auth directory exists
const authDir = path.dirname(authFile);
if (!fs.existsSync(authDir)) {
  fs.mkdirSync(authDir, { recursive: true });
}

/**
 * Authentication Setup Test
 * 
 * Supports two authentication modes:
 * 1. Automated (CI/CD): Uses TEST_USER_EMAIL/PASSWORD for password-based login
 * 2. Manual (Local Dev): Opens headed browser for manual passkey authentication
 * 
 * This test runs before all other tests (via the 'setup' project in playwright.config.ts)
 * and saves the authenticated state for reuse across all test runs.
 */
setup('authenticate', async ({ browser }) => {
  console.log('🔐 Starting authentication setup...');

  const testEmail = process.env.TEST_USER_EMAIL;
  const testPassword = process.env.TEST_USER_PASSWORD;
  const hasCredentials = !!(testEmail && testPassword);

  // Determine if we should use headed mode for manual login
  const useHeadedMode = !hasCredentials;

  // Create context with appropriate settings
  let context;
  let page;

  if (useHeadedMode) {
    console.log('\n' + '='.repeat(70));
    console.log('🖱️  MANUAL LOGIN REQUIRED');
    console.log('='.repeat(70));
    console.log('No test credentials found in .env.test');
    console.log('Opening browser for manual passkey authentication...');
    console.log('');
    console.log('Instructions:');
    console.log('  1. Browser will open and navigate to login page');
    console.log('  2. Complete your passkey/biometric authentication');
    console.log('  3. Session will be saved automatically');
    console.log('  4. Timeout: 3 minutes');
    console.log('='.repeat(70) + '\n');

    // Launch headed browser for manual login
    const headedBrowser = await chromium.launch({ headless: false });
    context = await headedBrowser.newContext();
    page = await context.newPage();
  } else {
    console.log(`🔑 Using automated login with ${testEmail}...`);
    context = await browser.newContext();
    page = await context.newPage();
  }

  try {
    // Navigate to the application
    await page.goto('/');

    // Wait a moment for any immediate MSAL redirects
    await page.waitForLoadState('networkidle', { timeout: 10000 }).catch(() => {});
    await page.waitForTimeout(2000); // Give MSAL time to check tokens and potentially redirect

    const currentUrl = page.url();
    console.log(`📍 Current URL after navigation: ${currentUrl}`);

    // Check if we're on the login page
    if (currentUrl.includes('login.microsoftonline.com')) {
      console.log('🔐 Not authenticated, proceeding with login...');

      // We're on the login page - perform authentication
      if (hasCredentials) {
        // Automated login with credentials
        await performAutomatedLogin(page, testEmail, testPassword);
      } else {
        // Manual login - just wait for user to complete
        await performManualLogin(page);
      }
    } else {
      console.log('✅ Already authenticated, capturing session state...');

      // Verify the page has content
      await expect(page.locator('body')).not.toBeEmpty({ timeout: 5000 });
    }

    // Capture the authenticated state (whether we just logged in or were already logged in)
    await captureAuthState(page, context);

  } catch (error) {
    console.error('❌ Authentication failed:', error);
    const screenshotPath = path.join(__dirname, '.auth', 'auth-failure.png');
    await page.screenshot({ path: screenshotPath, fullPage: true });
    console.error(`📸 Screenshot saved: ${screenshotPath}`);
    throw error;
  } finally {
    await context.close();
    if (useHeadedMode) {
      await page.context().browser()?.close();
    }
  }
});

/**
 * Perform automated login with email and password
 */
async function performAutomatedLogin(page: any, email: string, password: string) {
  console.log('🤖 Performing automated login...');

  // Fill email
  const emailInput = page.locator('input[type="email"], input[name="loginfmt"]');
  await expect(emailInput).toBeVisible({ timeout: 10000 });
  await emailInput.fill(email);
  await page.locator('input[type="submit"], button[type="submit"]').click();

  // Check for passkey prompt and switch to password
  try {
    const signInAnotherWay = page.getByRole('link', { name: /sign.?in another way|other ways to sign in/i });
    if (await signInAnotherWay.isVisible({ timeout: 3000 })) {
      console.log('🔐 Passkey prompt detected, switching to password...');
      await signInAnotherWay.click();
      const usePasswordLink = page.getByRole('link', { name: /password/i });
      await usePasswordLink.click({ timeout: 5000 });
    }
  } catch {
    // No passkey prompt, continue to password
  }

  // Fill password
  const passwordInput = page.locator('input[type="password"], input[name="passwd"]');
  await expect(passwordInput).toBeVisible({ timeout: 10000 });
  await passwordInput.fill(password);
  await page.locator('input[type="submit"], button[type="submit"]').click();

  // Handle "Stay signed in?" prompt
  try {
    const staySignedInButton = page.locator(
      'input[type="submit"]:has-text("Yes"), button[type="submit"]:has-text("Yes")'
    );
    await staySignedInButton.click({ timeout: 5000 });
  } catch {
    console.log('ℹ️ "Stay signed in" prompt did not appear');
  }

  // Wait for redirect back to application
  await page.waitForURL(/localhost:5173/, { timeout: 30000 });
  console.log('✅ Automated login successful');
}

/**
 * Wait for manual login completion
 */
async function performManualLogin(page: any) {
  console.log('⏳ Waiting for you to complete authentication...');
  console.log('   (You have 3 minutes)');

  try {
    // Set up continuous monitoring for the "Stay signed in?" dialog
    // This runs in parallel with waiting for the user to authenticate
    let dialogHandled = false;
    const monitorDialog = async () => {
      console.log('🔍 Starting dialog monitor...');
      let checkCount = 0;
      while (!dialogHandled) {
        try {
          checkCount++;

          // Check multiple possible selectors for the "Yes" button
          const yesButton = page.locator(
            'input[type="submit"][value="Yes"],' +
            'button:has-text("Yes"),' +
            'input[value="Yes"]'
          ).first();

          const isVisible = await yesButton.isVisible({ timeout: 1000 }).catch(() => false);

          if (isVisible) {
            console.log('🔘 "Stay signed in?" dialog detected, auto-clicking "Yes"...');
            await yesButton.click();
            dialogHandled = true;
            console.log('✅ Dialog dismissed');
            break;
          }

          // Log every 10 checks (every 5 seconds)
          if (checkCount % 10 === 0) {
            console.log(`   Still monitoring for dialog (${checkCount} checks)...`);
          }
        } catch (err) {
          // Continue monitoring
        }

        // Small delay before checking again
        await page.waitForTimeout(500);
      }
      console.log('🛑 Dialog monitor stopped');
    };

    // Start monitoring in the background
    const dialogMonitor = monitorDialog();

    // Wait for user to complete login and redirect back to app
    await page.waitForURL(/localhost:5173/, { timeout: 180000 }); // 3 minutes

    // Stop monitoring
    dialogHandled = true;
    await dialogMonitor.catch(() => {}); // Ignore any errors from background monitor

    console.log('✅ Manual login completed');
  } catch (error) {
    throw new Error(
      'Manual login timed out after 3 minutes.\n' +
      'Please complete the authentication process more quickly, or use automated login by setting:\n' +
      '  TEST_USER_EMAIL and TEST_USER_PASSWORD in .env.test'
    );
  }
}

/**
 * Capture and save authentication state with MSAL tokens
 */
async function captureAuthState(page: any, context: any) {
  console.log('💾 Capturing authentication state...');

  // Verify we're back on the app (check we're not on Azure AD)
  const currentUrl = page.url();
  if (currentUrl.includes('login.microsoftonline.com')) {
    throw new Error('Still on Azure AD login page, authentication may have failed');
  }

  console.log(`📍 Capturing state from: ${currentUrl}`);

  // Wait for the page body to have content (more lenient than checking for specific elements)
  await expect(page.locator('body')).not.toBeEmpty({ timeout: 10000 });

  // Wait for MSAL to initialize and store tokens
  console.log('⏳ Waiting for MSAL tokens...');
  await page.waitForTimeout(3000);

  // Check for MSAL tokens in localStorage
  try {
    await page.waitForFunction(
      () => {
        const keys = Object.keys(localStorage);
        const hasMsalTokens = keys.some(key => 
          key.startsWith('msal.') && (key.includes('idtoken') || key.includes('accesstoken'))
        );
        return hasMsalTokens;
      },
      { timeout: 15000 }
    );
    console.log('✅ MSAL tokens detected');
  } catch (error) {
    console.warn('⚠️  MSAL tokens not detected (this may cause authentication issues)');
    const keys = await page.evaluate(() => Object.keys(localStorage));
    console.log(`   Found ${keys.length} localStorage keys:`, keys.join(', '));
  }

  // Dismiss the Getting Started modal (both by clicking and setting localStorage)
  console.log('🎯 Dismissing Getting Started modal...');

  // First, try to click the modal button if visible
  try {
    const modal = page.getByRole('dialog');
    if (await modal.isVisible({ timeout: 2000 })) {
      console.log('   Modal detected, clicking dismiss button...');
      // Try multiple selectors to ensure we find the button
      const dismissButton = modal.getByRole('button', { name: /understand.*get started|got it|dismiss|close/i })
        .or(page.getByText(/I understand.*Get started/i))
        .or(page.getByRole('button').filter({ hasText: /understand/i }));
      await dismissButton.click({ timeout: 5000 });
      console.log('   ✅ Modal dismissed');
      await page.waitForTimeout(500); // Wait for modal to close
    }
  } catch {
    console.log('   No modal visible or already dismissed');
  }

  // Also set the localStorage flag to prevent it from showing again
  await page.evaluate(() => {
    localStorage.setItem('theragraf:onboardingSeen:v2', 'true');
  });
  console.log('   ✅ localStorage flag set');

  // Give MSAL a final moment to settle
  await page.waitForTimeout(2000);

  // Save authentication state
  await context.storageState({ path: authFile });

  const storageKeys = await page.evaluate(() => Object.keys(localStorage));
  console.log(`✅ Auth state saved to ${authFile}`);
  console.log(`   Captured ${storageKeys.length} localStorage entries`);

  if (storageKeys.length > 0) {
    console.log('   Keys:', storageKeys.slice(0, 5).join(', ') + (storageKeys.length > 5 ? '...' : ''));
  }
}
