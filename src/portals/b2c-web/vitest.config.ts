// Vitest config — Wave 0 harness (04-VALIDATION.md Wave 0 requirement).
//
// - jsdom environment so React Testing Library works out of the box.
// - tsconfig paths (`@/...`) mirrored here so imports resolve identically
//   in tests as they do at runtime.
// - globals: true so vitest exposes describe/it/expect without per-file
//   imports (matches starterKit convention).
// - e2e/** excluded — Playwright owns that tree.
// - No watch-mode scripts anywhere (VALIDATION §Sign-Off "no watch" rule).

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
