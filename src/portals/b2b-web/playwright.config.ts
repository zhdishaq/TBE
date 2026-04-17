// Playwright config — Wave 0 B2B E2E harness (05-VALIDATION.md Wave 0).
//
// Source: fork of src/portals/b2c-web/playwright.config.ts.
//
// Deltas vs b2c-web:
//   - baseURL → http://localhost:3001 (B2B portal port per D-22)
//   - webServer.command stays `pnpm dev` (runs `next dev -p 3001` per
//     package.json scripts), webServer.url → http://localhost:3001
//   - mobile project dropped — B2C-05 "≤5 step mobile" constraint does NOT
//     apply to agents; the B2B portal is desktop-first per 05-UI-SPEC.

import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  retries: 0,
  fullyParallel: false,
  reporter: [['list']],
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:3001',
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: {
    command: 'pnpm dev',
    url: 'http://localhost:3001',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
