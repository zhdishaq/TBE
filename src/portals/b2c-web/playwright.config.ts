// Playwright config — Wave 0 E2E harness (04-VALIDATION.md Wave 0 requirement).
//
// - `mobile` project exists per B2C-05 (mobile completion target ≤5 steps).
// - `reuseExistingServer: !process.env.CI` so local dev against a
//   running `pnpm dev` doesn't double-boot Next.
// - No watch-mode flags (VALIDATION §Sign-Off "no watch" rule).

import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  retries: 0,
  fullyParallel: false,
  reporter: [['list']],
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:3000',
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'mobile',
      use: { ...devices['iPhone 12'] },
    },
  ],
  webServer: {
    command: 'pnpm dev',
    url: 'http://localhost:3000',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
