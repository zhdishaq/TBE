// Playwright config — Wave 0 Backoffice E2E harness (06-VALIDATION.md Wave 0).
//
// Source: fork of src/portals/b2b-web/playwright.config.ts.
//
// Deltas vs b2b-web:
//   - testDir: harness lives under tests/e2e/ per 06-VALIDATION.md §Wave 0
//     (b2b-web stores it at the portal root; backoffice-web uses the nested
//     path so Plans 06-02/03/04 can add *.spec.ts files under one subtree).
//   - baseURL → http://localhost:3003 (backoffice-web test harness port;
//     dev port is 3002 per Plan 06-01 Task 3 package.json; 3003 avoids
//     colliding with a running dev server).
//   - projects renamed from 'chromium' → 'backoffice' per plan Task 1 §B.2.
//   - use.storageState gated behind BACKOFFICE_STORAGE_STATE env var so the
//     authenticated-user fixture file for this suite is isolated from the
//     b2b-web harness (Pitfall 19 cookie-isolation).
//   - webServer.command runs the backoffice-web dev server specifically.
//
// Pitfall 19: backoffice cookie MUST be __Host-backoffice.session /
// __Secure-tbe-backoffice.session-token — NEVER reuse b2b-web's
// __Secure-tbe-b2b.session-token cookie. The `storageState` file written by
// the auth fixture below MUST serialise the backoffice cookie name exactly.

import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  timeout: 30_000,
  retries: 0,
  fullyParallel: false,
  reporter: [['list']],
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:3003',
    trace: 'retain-on-failure',
    // Pitfall 19 pin: per-portal storageState isolation. The auth fixture
    // writes the __Secure-tbe-backoffice.session-token cookie here; spec
    // files load it via `use: { storageState: ... }`.
    storageState: process.env.BACKOFFICE_STORAGE_STATE,
    // extraHTTPHeaders: kept empty — session auth flows via cookie only.
    // Backoffice harness never piggy-backs on the b2b-web Bearer-forwarding
    // convention; every spec exercises the Auth.js session cookie path.
    extraHTTPHeaders: {},
  },
  projects: [
    {
      name: 'backoffice',
      testMatch: '**/*.spec.ts',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: {
    command: 'pnpm --filter ./src/portals/backoffice-web run dev',
    url: 'http://localhost:3003',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
