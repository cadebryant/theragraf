import { Page, Locator, expect } from '@playwright/test';

/**
 * Base Page Object
 * 
 * Provides common functionality for all page objects
 */
export class BasePage {
  constructor(protected page: Page) {}

  /**
   * Navigate to a specific path
   */
  async goto(path: string = '/') {
    console.log(`🔗 Navigating to: ${path}`);

    // Suppress the product tour so it never blocks clicks in tests
    await this.page.addInitScript(() => {
      try {
        localStorage.setItem('theragraf:tourCompleted:v1', 'true');
      } catch {
        // ignore
      }
    });

    await this.page.goto(path, { waitUntil: 'domcontentloaded' });

    // Wait for network to settle
    await this.page.waitForLoadState('networkidle').catch(() => {
      console.warn('⚠️  Network did not go idle, continuing anyway');
    });

    // Check if we got redirected (e.g., to login)
    const currentUrl = this.page.url();
    if (currentUrl.includes('login.microsoftonline.com')) {
      throw new Error('❌ Redirected to Azure AD login - auth state not working!');
    }

    console.log(`✅ Loaded: ${currentUrl}`);

    // Dismiss modal if it appears
    await this.dismissGettingStartedModal();
  }

  /**
   * Dismiss the "Getting Started" modal if it appears
   */
  async dismissGettingStartedModal() {
    try {
      // Wait briefly for modal to appear
      const modal = this.page.getByRole('dialog');
      const closeButton = modal.getByRole('button', { name: /understand.*get started|got it|close|dismiss/i });

      if (await closeButton.isVisible({ timeout: 2000 })) {
        await closeButton.click();
        await modal.waitFor({ state: 'hidden', timeout: 3000 });
      }
    } catch {
      // Modal didn't appear or already dismissed - that's fine
    }
  }

  /**
   * Wait for page to be fully loaded
   */
  async waitForLoad() {
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Get the app header/navigation
   */
  get header(): Locator {
    return this.page.locator('header, nav, [role="banner"]');
  }

  /**
   * Check if user is authenticated
   */
  async isAuthenticated(): Promise<boolean> {
    try {
      await expect(this.header).toBeVisible({ timeout: 5000 });
      return true;
    } catch {
      return false;
    }
  }

  /**
   * Sign out (if sign-out button is available)
   */
  async signOut() {
    const signOutButton = this.page.getByRole('button', { name: /sign out|log out/i });
    if (await signOutButton.isVisible({ timeout: 1000 })) {
      await signOutButton.click();
    }
  }

  /**
   * Take a screenshot for debugging
   */
  async screenshot(name: string) {
    await this.page.screenshot({ path: `test-results/screenshots/${name}.png`, fullPage: true });
  }
}
