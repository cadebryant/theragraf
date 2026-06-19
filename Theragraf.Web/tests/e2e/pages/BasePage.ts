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
    await this.page.goto(path);
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
