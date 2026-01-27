import { defineConfig } from '@playwright/test';

export default defineConfig({
  // Increased for node startup and login flows
  expect: { timeout: 20_000 },

  // Increased for slower React rendering
  retries: process.env.CI ? 1 : 0,

  testDir: './e2e',
  timeout: 120_000,
  use: {
    headless: process.env.HEADLESS !== 'false',
    screenshot: 'only-on-failure',
    // Allow HEADLESS=false to run in headed mode
    trace: 'on-first-retry',
    video: 'retain-on-failure',
  },
  workers: process.env.CI ? 1 : 2,
});
