# End-to-End Testing Guide

This document explains how to run and maintain the E2E test suite for Theragraf.

## Overview

The E2E tests use [Playwright](https://playwright.dev/) to test the full application stack:
- **Frontend**: React app (Vite dev server)
- **Backend**: Azure Functions
- **Database**: Cosmos DB (emulator or cloud)
- **Authentication**: Azure AD (real authentication with test user)

## Prerequisites

### 1. Services Running

Before running E2E tests, ensure all services are started:

```powershell
# Terminal 1: Start Cosmos DB Emulator (if testing locally)
# The emulator should auto-start, or run it manually from Windows Start Menu

# Terminal 2: Start Azure Functions backend
cd Theragraf.Functions
func start

# Terminal 3: Start Frontend dev server
cd Theragraf.Web
npm run dev
```

### 2. Test Environment Configuration

1. Copy the environment template:
   ```powershell
   cp Theragraf.Web/.env.test.template Theragraf.Web/.env.test
   ```

2. Edit `.env.test` and configure:

   **Required Settings:**
   ```env
   # Test user credentials (create a dedicated test user in Azure AD)
   TEST_USER_EMAIL=test-therapist@yourdomain.com
   TEST_USER_PASSWORD=YourSecureTestPassword123!

   # Azure AD application settings (from Azure Portal app registration)
   VITE_AZURE_CLIENT_ID=your-client-id-here
   VITE_AZURE_TENANT_ID=your-tenant-id-here
   VITE_AZURE_REDIRECT_URI=http://localhost:5173
   ```

   **Optional Settings:**
   ```env
   # Service URLs (defaults shown)
   TEST_BASE_URL=http://localhost:5173
   TEST_API_URL=http://localhost:7071

   # Test data configuration
   TEST_CLIENT_ID_PREFIX=e2e-test-client
   TEST_SESSION_CLEANUP_ENABLED=true
   ```

### 3. Install Playwright Browsers

First time only:
```powershell
cd Theragraf.Web
npm install
npx playwright install
```

## Running Tests

### Run All Tests

```powershell
cd Theragraf.Web
npm run test:e2e
```

This will:
1. Validate environment configuration
2. Check that all services are running
3. Authenticate once (results cached for all tests)
4. Run tests in parallel across Chromium, Firefox, and WebKit
5. Generate an HTML report

### Run Tests in UI Mode (Interactive)

```powershell
npm run test:e2e:ui
```

Benefits:
- Watch tests run in real-time
- Time-travel through test steps
- Inspect element locators
- Debug failures interactively

### Run Tests in Debug Mode

```powershell
npm run test:e2e:debug
```

This opens Playwright Inspector for step-by-step debugging.

### Run Specific Test Files

```powershell
npx playwright test session-creation
npx playwright test dashboard
npx playwright test client-profile
```

### Run in a Single Browser

```powershell
npx playwright test --project=chromium
npx playwright test --project=firefox
npx playwright test --project=webkit
```

### Run Tests in Headed Mode (See Browser)

```powershell
npx playwright test --headed
```

## Test Results

### HTML Report

After tests complete, view the HTML report:

```powershell
npm run test:e2e:report
```

Or open manually:
```powershell
open playwright-report/index.html
```

The report includes:
- Test results with pass/fail status
- Test duration
- Screenshots of failures
- Video recordings (on failure)
- Network activity logs
- Detailed error messages

### Test Artifacts

Test artifacts are saved in:
- `test-results/` - Screenshots, videos, traces
- `playwright-report/` - HTML report
- `tests/e2e/.auth/` - Authentication state (reused across tests)

**Note:** These directories are gitignored and safe to delete.

## Test Organization

```
Theragraf.Web/tests/e2e/
├── auth.setup.ts              # Authentication setup (runs first)
├── global-setup.ts            # Environment validation
├── fixtures/
│   ├── test-fixtures.ts       # Custom test fixtures and helpers
│   ├── environment.ts         # Service health checks
│   └── cleanup.ts             # Test data cleanup utilities
├── pages/                     # Page Object Models
│   ├── BasePage.ts
│   ├── DashboardPage.ts
│   ├── NewSessionPage.ts
│   ├── SessionReviewPage.ts
│   └── ClientProfilePage.ts
└── specs/                     # Test specifications
	├── session-creation.spec.ts
	├── dashboard.spec.ts
	└── client-profile.spec.ts
```

## Writing New Tests

### 1. Use Page Objects

```typescript
import { test, expect } from '../fixtures/test-fixtures';
import { DashboardPage, NewSessionPage } from '../pages';

test('my new test', async ({ page }) => {
  const dashboard = new DashboardPage(page);
  await dashboard.goto();
  await dashboard.assertDashboardLoaded();

  // ... test logic
});
```

### 2. Use Test Data Helpers

```typescript
import { TestData } from '../fixtures/test-fixtures';

test('create session with test data', async ({ page }) => {
  const clientId = TestData.generateClientId();
  const sessionData = TestData.generateSessionData(clientId);

  // ... use test data
});
```

### 3. Clean Up Test Data

```typescript
import { createCleanupManager } from '../fixtures/cleanup';

test('test with cleanup', async ({ page }) => {
  const cleanup = createCleanupManager(page);

  const clientId = TestData.generateClientId();
  cleanup.trackClient(clientId);

  // ... create test data

  // Cleanup runs automatically after test
  await cleanup.cleanupAll();
});
```

## Troubleshooting

### Authentication Fails

**Problem:** Tests fail at login step

**Solutions:**
1. Verify `TEST_USER_EMAIL` and `TEST_USER_PASSWORD` in `.env.test`
2. Ensure test user exists in Azure AD tenant
3. Check that user has required permissions
4. Delete cached auth: `rm -rf tests/e2e/.auth/user.json` and retry

### Service Not Running

**Problem:** "Service is not running" error

**Solutions:**
1. Start all required services (see Prerequisites)
2. Check URLs in `.env.test` match running services
3. Verify ports are not in use by other applications

### Tests Are Slow

**Problem:** Tests take too long

**Solutions:**
1. Run fewer browsers: `npx playwright test --project=chromium`
2. Run tests serially: `npx playwright test --workers=1`
3. Run specific test files instead of the full suite
4. Check that authentication state is being reused (should login once)

### Test Data Not Cleaned Up

**Problem:** Test clients/sessions accumulating

**Solutions:**
1. Ensure `TEST_SESSION_CLEANUP_ENABLED=true` in `.env.test`
2. Run cleanup manually:
   ```typescript
   import { TestDataCleanup } from './fixtures/cleanup';
   await TestDataCleanup.cleanupOrphanedTestData(page);
   ```

### Flaky Tests

**Problem:** Tests pass sometimes, fail other times

**Solutions:**
1. Increase timeouts for slow operations:
   ```typescript
   await expect(element).toBeVisible({ timeout: 30000 });
   ```
2. Wait for network idle:
   ```typescript
   await page.waitForLoadState('networkidle');
   ```
3. Use built-in auto-wait (Playwright waits for elements automatically)
4. Check for race conditions in test logic

## CI/CD Integration

### GitHub Actions Example

```yaml
name: E2E Tests

on: [push, pull_request]

jobs:
  e2e-tests:
	runs-on: windows-latest

	steps:
	  - uses: actions/checkout@v3

	  - uses: actions/setup-node@v3
		with:
		  node-version: '20'

	  - name: Install dependencies
		run: |
		  cd Theragraf.Web
		  npm ci
		  npx playwright install --with-deps

	  - name: Start Cosmos DB Emulator
		run: |
		  Import-Module "$env:ProgramFiles\Azure Cosmos DB Emulator\PSModules\Microsoft.Azure.CosmosDB.Emulator"
		  Start-CosmosDbEmulator

	  - name: Start Azure Functions
		run: |
		  cd Theragraf.Functions
		  func start &

	  - name: Start Frontend
		run: |
		  cd Theragraf.Web
		  npm run dev &

	  - name: Run E2E tests
		run: |
		  cd Theragraf.Web
		  npm run test:e2e
		env:
		  CI: true
		  TEST_USER_EMAIL: ${{ secrets.TEST_USER_EMAIL }}
		  TEST_USER_PASSWORD: ${{ secrets.TEST_USER_PASSWORD }}
		  VITE_AZURE_CLIENT_ID: ${{ secrets.AZURE_CLIENT_ID }}
		  VITE_AZURE_TENANT_ID: ${{ secrets.AZURE_TENANT_ID }}

	  - uses: actions/upload-artifact@v3
		if: always()
		with:
		  name: playwright-report
		  path: Theragraf.Web/playwright-report/
```

## Best Practices

1. **Use Page Objects** - Keep selectors and actions in page objects, not test files
2. **Avoid Hard Waits** - Use `waitFor*` methods instead of `setTimeout`
3. **Generate Unique Test Data** - Use `TestData.generateClientId()` to avoid conflicts
4. **Clean Up After Tests** - Enable cleanup or tests will pollute the database
5. **Test Realistic Scenarios** - Tests should match real user workflows
6. **Keep Tests Independent** - Each test should be able to run in isolation
7. **Use Meaningful Assertions** - Assert specific expected outcomes
8. **Handle Timeouts** - AI processing can take time, use appropriate timeouts

## Additional Resources

- [Playwright Documentation](https://playwright.dev/)
- [Playwright Best Practices](https://playwright.dev/docs/best-practices)
- [Azure AD Testing Guide](https://learn.microsoft.com/en-us/azure/active-directory/develop/test-setup-environment)
