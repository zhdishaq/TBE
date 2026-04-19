// Auth.js v5 route handler — B2B Agent Portal.
//
// Re-exports GET/POST from the `handlers` tuple exported by the full
// (Node-runtime) Auth.js config in lib/auth.ts. The edge-safe subset in
// auth.config.ts is used only by middleware.ts — do NOT import from it
// here.
//
// Source: Auth.js v5 docs; verbatim copy of b2c-web equivalent.

import { handlers } from '@/lib/auth';

export const { GET, POST } = handlers;
