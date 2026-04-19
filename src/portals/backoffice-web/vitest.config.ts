// Vitest config — Wave 0 B2B harness (05-VALIDATION.md Wave 0 requirement).
//
// Source: fork of src/portals/b2c-web/vitest.config.ts. No deltas at this
// stage — jsdom + React Testing Library + tsconfig path alias `@/...`.
//
// - globals: true so describe/it/expect are available without per-file imports.
// - e2e/** excluded — Playwright owns that tree.
// - No watch-mode scripts (05-VALIDATION §Sampling Rate "no watch" rule).

import { defineConfig } from 'vitest/config';
import path from 'node:path';

export default defineConfig({
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./tests/setup.ts'],
    include: [
      'tests/**/*.test.ts',
      'tests/**/*.test.tsx',
      '**/*.test.ts',
      '**/*.test.tsx',
    ],
    exclude: [
      'e2e/**',
      'node_modules/**',
      '.next/**',
    ],
    css: false,
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname),
    },
  },
});
