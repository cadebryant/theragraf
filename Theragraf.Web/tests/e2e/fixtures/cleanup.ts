import { Page } from '@playwright/test';
import { ApiHelpers } from './test-fixtures';

/**
 * Test Data Cleanup Manager
 * 
 * Tracks test data created during test execution and provides
 * utilities for cleanup to prevent test data accumulation.
 */
export class TestDataCleanup {
  private sessions: string[] = [];
  private clients: string[] = [];
  private goals: Array<{ clientId: string; goalId: string }> = [];

  constructor(
    private page: Page,
    private apiHelpers: ApiHelpers = new ApiHelpers()
  ) {}

  /**
   * Register a session for cleanup
   */
  trackSession(sessionId: string) {
    this.sessions.push(sessionId);
  }

  /**
   * Register a client for cleanup
   */
  trackClient(clientId: string) {
    this.clients.push(clientId);
  }

  /**
   * Register a goal for cleanup
   */
  trackGoal(clientId: string, goalId: string) {
    this.goals.push({ clientId, goalId });
  }

  /**
   * Clean up all tracked test data
   */
  async cleanupAll(): Promise<void> {
    if (!process.env.TEST_SESSION_CLEANUP_ENABLED) {
      console.log('⏭️  Test data cleanup disabled via TEST_SESSION_CLEANUP_ENABLED');
      return;
    }

    console.log(`\n🧹 Starting test data cleanup...`);
    console.log(`  Sessions: ${this.sessions.length}`);
    console.log(`  Clients: ${this.clients.length}`);
    console.log(`  Goals: ${this.goals.length}\n`);

    // Get authentication token
    const token = await ApiHelpers.getAuthToken(this.page);

    if (!token) {
      console.warn('⚠️  No auth token available for cleanup');
      return;
    }

    let successCount = 0;
    let failureCount = 0;

    // Clean up sessions
    for (const sessionId of this.sessions) {
      try {
        await this.apiHelpers.deleteSession(sessionId, token);
        successCount++;
        console.log(`  ✅ Deleted session: ${sessionId}`);
      } catch (error) {
        failureCount++;
        console.warn(`  ⚠️  Failed to delete session ${sessionId}:`, error);
      }
    }

    // Clean up goals
    for (const goal of this.goals) {
      try {
        const response = await this.apiHelpers.request(
          `/api/clients/${goal.clientId}/goals/${goal.goalId}`,
          { method: 'DELETE' },
          token
        );

        if (response.ok) {
          successCount++;
          console.log(`  ✅ Deleted goal: ${goal.clientId}/${goal.goalId}`);
        } else {
          failureCount++;
        }
      } catch (error) {
        failureCount++;
        console.warn(`  ⚠️  Failed to delete goal ${goal.goalId}:`, error);
      }
    }

    // Clean up clients (this should cascade delete sessions and goals)
    for (const clientId of this.clients) {
      try {
        await this.apiHelpers.deleteClient(clientId, token);
        successCount++;
        console.log(`  ✅ Deleted client: ${clientId}`);
      } catch (error) {
        failureCount++;
        console.warn(`  ⚠️  Failed to delete client ${clientId}:`, error);
      }
    }

    console.log(`\n✅ Cleanup complete: ${successCount} items deleted, ${failureCount} failures\n`);
  }

  /**
   * Clean up test data matching a specific prefix
   * Useful for cleaning up orphaned test data from previous runs
   */
  static async cleanupOrphanedTestData(page: Page): Promise<void> {
    if (!process.env.TEST_SESSION_CLEANUP_ENABLED) {
      return;
    }

    console.log('🧹 Checking for orphaned test data...');

    const apiHelpers = new ApiHelpers();
    const token = await ApiHelpers.getAuthToken(page);

    if (!token) {
      console.warn('⚠️  No auth token available for orphaned data cleanup');
      return;
    }

    const prefix = process.env.TEST_CLIENT_ID_PREFIX || 'e2e-test-client';

    try {
      // Fetch all sessions for this therapist
      const response = await apiHelpers.request('/api/sessions', {}, token);

      if (!response.ok) {
        console.warn('⚠️  Could not fetch sessions for cleanup');
        return;
      }

      const sessions = await response.json();

      // Filter for test sessions
      const testSessions = sessions.filter((s: any) => 
        s.clientId?.startsWith(prefix)
      );

      if (testSessions.length > 0) {
        console.log(`  Found ${testSessions.length} orphaned test sessions`);

        for (const session of testSessions) {
          try {
            await apiHelpers.deleteSession(session.id, token);
            console.log(`  ✅ Deleted orphaned session: ${session.id}`);
          } catch (error) {
            console.warn(`  ⚠️  Failed to delete orphaned session ${session.id}`);
          }
        }
      } else {
        console.log('  No orphaned test data found');
      }
    } catch (error) {
      console.warn('⚠️  Orphaned data cleanup failed:', error);
    }
  }

  /**
   * Get cleanup summary
   */
  getSummary() {
    return {
      sessions: this.sessions.length,
      clients: this.clients.length,
      goals: this.goals.length,
      total: this.sessions.length + this.clients.length + this.goals.length,
    };
  }
}

/**
 * Create a cleanup manager for a test
 */
export function createCleanupManager(page: Page): TestDataCleanup {
  return new TestDataCleanup(page);
}
