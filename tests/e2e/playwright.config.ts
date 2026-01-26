import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright configuration for slskdn E2E tests.
 * 
 * Tests use a multi-peer harness that launches real slskdn instances.
 * Each test gets isolated nodes with unique app directories and ports.
 */
export default defineConfig({
  testDir: './specs',
  fullyParallel: false, // Tests share harness, run serially
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1, // One worker to avoid port conflicts
  reporter: [
    ['html'],
    ['list'],
    process.env.CI ? ['github'] : ['list']
  ],
  use: {
    baseURL: undefined, // Each test uses node-specific URLs
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: undefined, // Nodes are started by harness, not Playwright
  timeout: 60000, // 60s per test
  expect: {
    timeout: 10000, // 10s for assertions
  },
});
