import { chromium, FullConfig } from '@playwright/test';
import * as path from 'path';
import * as fs from 'fs';
import { validateTestEnvironment, verifyFullStackServices } from './fixtures/environment';

/**
 * Global setup runs once before all tests.
 * 
 * This performs the Azure AD authentication flow and saves the authentication
 * state to a file that can be reused across all test files.
 * 
 * This approach is much faster than logging in before each test.
 */
async function globalSetup(config: FullConfig) {
  console.log('\n🚀 Starting E2E Test Suite Setup\n');
  console.log('='.repeat(60) + '\n');

  // Validate environment configuration
  const { valid, errors } = validateTestEnvironment();
  if (!valid) {
    throw new Error(`Test environment validation failed:\n${errors.join('\n')}`);
  }

  // Verify all services are running
  const services = await verifyFullStackServices();
  if (!services.allHealthy) {
    throw new Error('Not all required services are healthy. Please start all services before running E2E tests.');
  }

  const { baseURL } = config.projects[0].use;
  const authFile = path.join(__dirname, '.auth', 'user.json');

  // Ensure .auth directory exists
  const authDir = path.dirname(authFile);
  if (!fs.existsSync(authDir)) {
    fs.mkdirSync(authDir, { recursive: true });
  }

  console.log('🔐 Setting up authentication...');

  const browser = await chromium.launch();
  const context = await browser.newContext();
  const page = await context.newPage();

  try {
    // Navigate to the application
    await page.goto(baseURL || 'http://localhost:5173');

    // Wait for redirect to Azure AD login page or dashboard
    await page.waitForURL(/login\.microsoftonline\.com|localhost:5173/, { timeout: 30000 });

    // Check if already authenticated (e.g., from previous run)
    const currentUrl = page.url();
    if (!currentUrl.includes('login.microsoftonline.com')) {
      console.log('✅ Already authenticated, skipping login');
      await context.storageState({ path: authFile });
      await browser.close();
      return;
    }

    // Perform Azure AD login
    const testEmail = process.env.TEST_USER_EMAIL;
    const testPassword = process.env.TEST_USER_PASSWORD;

    if (!testEmail || !testPassword) {
      throw new Error(
        'TEST_USER_EMAIL and TEST_USER_PASSWORD must be set in .env.test file. ' +
        'See .env.test.template for configuration details.'
      );
    }

    console.log(`🔑 Logging in as ${testEmail}...`);

    // Enter email
    await page.fill('input[type="email"], input[name="loginfmt"]', testEmail);
    await page.click('input[type="submit"], button[type="submit"]');

    // Wait for password page
    await page.waitForSelector('input[type="password"], input[name="passwd"]', { timeout: 10000 });

    // Enter password
    await page.fill('input[type="password"], input[name="passwd"]', testPassword);
    await page.click('input[type="submit"], button[type="submit"]');

    // Handle "Stay signed in?" prompt
    try {
      await page.waitForSelector('input[type="submit"], button[type="submit"]', { timeout: 5000 });
      // Click "Yes" to stay signed in (faster for subsequent test runs)
      await page.click('input[type="submit"]:has-text("Yes"), button[type="submit"]:has-text("Yes")');
    } catch (e) {
      // Prompt might not appear, continue
    }

    // Wait for redirect back to the application
    await page.waitForURL(/localhost:5173/, { timeout: 30000 });

    // Wait for app to fully load
    await page.waitForSelector('[data-testid="app-layout"], h1, nav', { timeout: 15000 });

    console.log('✅ Authentication successful');

    // Save authentication state
    await context.storageState({ path: authFile });

  } catch (error) {
    console.error('❌ Authentication failed:', error);

    // Take a screenshot for debugging
    const screenshotPath = path.join(__dirname, '.auth', 'auth-failure.png');
    await page.screenshot({ path: screenshotPath, fullPage: true });
    console.error(`Screenshot saved to: ${screenshotPath}`);

    throw error;
  } finally {
    await browser.close();
  }
}

export default globalSetup;
