import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  timeout: 120_000, // Increased for node startup and login flows
  expect: { timeout: 20_000 }, // Increased for slower React rendering
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 1 : 2,
  use: {
    headless: process.env.HEADLESS !== 'false', // Allow HEADLESS=false to run in headed mode
    trace: "on-first-retry",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
});
