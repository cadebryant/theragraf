import { test, expect } from '@playwright/test';

/**
 * Diagnostic test to debug auth and page loading issues
 */
test.describe('Diagnostics', () => {
  test('check auth and page loading', async ({ page }) => {
    console.log('🔍 Starting diagnostic test...');

    // Capture ALL console messages
    const consoleMessages: Array<{ type: string; text: string }> = [];
    page.on('console', (msg) => {
      consoleMessages.push({ type: msg.type(), text: msg.text() });
    });

    const pageErrors: string[] = [];
    page.on('pageerror', (error) => {
      pageErrors.push(error.message);
      console.error('❌ Page error:', error.message);
    });

    const requestFailures: string[] = [];
    page.on('requestfailed', (request) => {
      requestFailures.push(`${request.url()} - ${request.failure()?.errorText}`);
    });

    // Step 1: Check auth state
    console.log('📋 Storage state loaded from config');

    // Step 2: Navigate to homepage
    console.log('🏠 Navigating to homepage...');
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const homeUrl = page.url();
    console.log('📍 Homepage URL:', homeUrl);

    if (homeUrl.includes('login.microsoftonline.com')) {
      throw new Error('❌ Auth failed: Redirected to Azure AD login');
    }

    // Step 3: Check for modal
    console.log('🔍 Checking for Getting Started modal...');
    const modal = page.getByRole('dialog');
    const modalVisible = await modal.isVisible().catch(() => false);
    console.log('🎭 Modal visible:', modalVisible);

    if (modalVisible) {
      console.log('🖱️ Dismissing modal...');
      const button = modal.getByRole('button', { name: /understand.*get started/i });
      if (await button.isVisible({ timeout: 2000 })) {
        await button.click();
        await modal.waitFor({ state: 'hidden', timeout: 5000 });
        console.log('✅ Modal dismissed');
      }
    }

    // Step 4: Navigate to new session
    console.log('📝 Navigating to /sessions/new...');
    await page.goto('/sessions/new');
    await page.waitForLoadState('networkidle');

    const newSessionUrl = page.url();
    console.log('📍 New session URL:', newSessionUrl);

    // Step 5: Check page content
    const title = await page.title();
    console.log('📄 Page title:', title);

    // Log all console messages
    console.log(`\n📊 Console Summary (${consoleMessages.length} messages):`);
    const errorLogs = consoleMessages.filter(m => m.type === 'error');
    const warnLogs = consoleMessages.filter(m => m.type === 'warning');
    const infoLogs = consoleMessages.filter(m => m.type === 'info' || m.type === 'log');

    if (errorLogs.length > 0) {
      console.log(`\n❌ Errors (${errorLogs.length}):`);
      errorLogs.forEach((msg, idx) => console.log(`  ${idx + 1}. ${msg.text}`));
    }

    if (warnLogs.length > 0) {
      console.log(`\n⚠️  Warnings (${warnLogs.length}):`);
      warnLogs.forEach((msg, idx) => console.log(`  ${idx + 1}. ${msg.text}`));
    }

    if (pageErrors.length > 0) {
      console.log(`\n💥 Page Errors (${pageErrors.length}):`);
      pageErrors.forEach((err, idx) => console.log(`  ${idx + 1}. ${err}`));
    }

    if (requestFailures.length > 0) {
      console.log(`\n🌐 Failed Requests (${requestFailures.length}):`);
      requestFailures.forEach((req, idx) => console.log(`  ${idx + 1}. ${req}`));
    }

    // Check if page has any content at all
    const bodyText = await page.locator('body').textContent();
    console.log('\n📝 Body text length:', bodyText?.length || 0);
    console.log('📝 Body text preview:', bodyText?.substring(0, 200));

    const h1Count = await page.locator('h1').count();
    console.log('📝 H1 count:', h1Count);

    if (h1Count > 0) {
      const h1 = await page.locator('h1').first().textContent();
      console.log('📝 First h1:', h1);
    } else {
      console.log('⚠️ No h1 elements found on page');
    }

    // Step 6: Look for the form
    console.log('🔍 Looking for Client ID field...');

    // Try different selectors
    const byLabel = page.getByLabel(/client id/i);
    const byLabelVisible = await byLabel.isVisible().catch(() => false);
    console.log('  ✓ getByLabel(/client id/i) visible:', byLabelVisible);

    const byPlaceholder = page.getByPlaceholder(/patient/i);
    const byPlaceholderVisible = await byPlaceholder.isVisible().catch(() => false);
    console.log('  ✓ getByPlaceholder(/patient/i) visible:', byPlaceholderVisible);

    const byText = page.getByText('Client ID');
    const byTextVisible = await byText.isVisible().catch(() => false);
    console.log('  ✓ getByText("Client ID") visible:', byTextVisible);

    // Step 7: Take screenshot
    const screenshotPath = 'test-results/diagnostic-new-session.png';
    await page.screenshot({ path: screenshotPath, fullPage: true });
    console.log('📸 Screenshot saved:', screenshotPath);

    // Step 8: List all input fields
    const inputs = page.locator('input[type="text"], input:not([type])');
    const inputCount = await inputs.count();
    console.log(`📋 Found ${inputCount} text inputs`);

    for (let i = 0; i < Math.min(inputCount, 5); i++) {
      const input = inputs.nth(i);
      const placeholder = await input.getAttribute('placeholder');
      const ariaLabel = await input.getAttribute('aria-label');
      const id = await input.getAttribute('id');
      console.log(`  Input ${i}: id="${id}", placeholder="${placeholder}", aria-label="${ariaLabel}"`);
    }

    // Final assertion
    console.log('✅ Diagnostic complete');
    expect(byLabelVisible || byPlaceholderVisible).toBe(true);
  });
});
