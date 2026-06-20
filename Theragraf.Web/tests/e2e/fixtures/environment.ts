/**
 * Full-Stack Test Environment Setup Helpers
 * 
 * Utilities for verifying that all required services are running
 * before E2E tests execute.
 */

/**
 * Check if a service is running by making a health check request
 */
async function checkService(url: string, name: string): Promise<boolean> {
  try {
    const response = await fetch(url, {
      method: 'GET',
      headers: { 'Accept': 'application/json' },
    });

    // Accept any response (even 404) as long as we get a response
    // 401, 403, 404 all mean the service is running, just not this exact endpoint
    const isHealthy = response.status < 500;

    if (isHealthy) {
      console.log(`✅ ${name} is running at ${url} (status: ${response.status})`);
    } else {
      console.warn(`⚠️ ${name} returned status ${response.status}`);
    }

    return isHealthy;
  } catch (error) {
    console.error(`❌ ${name} is not reachable at ${url}:`, error instanceof Error ? error.message : error);
    return false;
  }
}

/**
 * Verify all required services are running
 */
export async function verifyFullStackServices(): Promise<{
  frontend: boolean;
  backend: boolean;
  allHealthy: boolean;
}> {
  console.log('🔍 Verifying full-stack services...\n');

  const frontendUrl = process.env.TEST_BASE_URL || 'http://localhost:5173';
  const backendUrl = process.env.TEST_API_URL || 'http://localhost:7071';

  const frontend = await checkService(frontendUrl, 'Frontend (Vite)');
  // Use /api/sessions which exists and returns 401 without auth (proves service is running)
  const backend = await checkService(`${backendUrl}/api/sessions`, 'Backend (Azure Functions)');

  const allHealthy = frontend && backend;

  if (!allHealthy) {
    console.error('\n❌ Not all services are healthy. Please ensure:');
    if (!frontend) console.error('  - Frontend dev server is running: npm run dev');
    if (!backend) console.error('  - Azure Functions host is running: func start');
    console.error('  - Cosmos DB emulator is running (if testing locally)\n');
  } else {
    console.log('\n✅ All services are healthy and ready for testing\n');
  }

  return { frontend, backend, allHealthy };
}

/**
 * Wait for a service to become available
 */
export async function waitForService(
  url: string,
  name: string,
  maxAttempts: number = 30,
  delayMs: number = 1000
): Promise<boolean> {
  console.log(`⏳ Waiting for ${name} at ${url}...`);

  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    try {
      const response = await fetch(url);
      if (response.ok || response.status === 401) {
        console.log(`✅ ${name} is ready (attempt ${attempt}/${maxAttempts})`);
        return true;
      }
    } catch (error) {
      // Service not ready yet
    }

    if (attempt < maxAttempts) {
      await new Promise(resolve => setTimeout(resolve, delayMs));
    }
  }

  console.error(`❌ ${name} did not become available after ${maxAttempts} attempts`);
  return false;
}

/**
 * Get environment configuration for tests
 */
export function getTestConfig() {
  return {
    baseUrl: process.env.TEST_BASE_URL || 'http://localhost:5173',
    apiUrl: process.env.TEST_API_URL || 'http://localhost:7071',
    testUserEmail: process.env.TEST_USER_EMAIL,
    testUserPassword: process.env.TEST_USER_PASSWORD,
    clientIdPrefix: process.env.TEST_CLIENT_ID_PREFIX || 'e2e-test-client',
    cleanupEnabled: process.env.TEST_SESSION_CLEANUP_ENABLED === 'true',
    isCI: process.env.CI === 'true',
  };
}

/**
 * Validate that required environment variables are set
 */
export function validateTestEnvironment(): { valid: boolean; errors: string[]; warnings: string[] } {
  const errors: string[] = [];
  const warnings: string[] = [];
  const config = getTestConfig();

  // Check for Azure AD configuration (required)
  if (!process.env.VITE_AZURE_AD_CLIENT_ID) {
    errors.push('VITE_AZURE_AD_CLIENT_ID is not set (should be loaded from .env.development)');
  }

  if (!process.env.VITE_AZURE_AD_TENANT_ID) {
    errors.push('VITE_AZURE_AD_TENANT_ID is not set (should be loaded from .env.development)');
  }

  // Test user credentials are optional for local dev (can reuse existing session)
  if (!config.testUserEmail || !config.testUserPassword) {
    warnings.push('⚠️  TEST_USER_EMAIL/PASSWORD not set - tests will attempt to reuse existing browser session');
    warnings.push('   Make sure you are logged in to http://localhost:5173 before running tests');
  }

  const valid = errors.length === 0;

  if (!valid) {
    console.error('❌ Test environment validation failed:\n');
    errors.forEach(error => console.error(`  - ${error}`));
    console.error('\nPlease ensure .env.development contains your Azure AD configuration.\n');
  } else {
    console.log('✅ Test environment validated\n');
    if (warnings.length > 0) {
      warnings.forEach(warning => console.log(warning));
      console.log('');
    }
  }

  return { valid, errors, warnings };
}
