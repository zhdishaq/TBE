// Next.js middleware (edge runtime) — Backoffice Portal.
//
// Source: fork of src/portals/b2b-web/middleware.ts + 06-PATTERNS.md Pattern M.
//
// IMPORTANT: imports from `@/auth.config` (edge-safe), NOT `@/lib/auth`.
// Importing lib/auth here would pull Node crypto and blow up at build.
//
// Phase 6 Plan 06-01 deltas vs b2b-web/middleware.ts:
//   - Entire portal is staff-only — every non-login path requires auth.
//   - Per-surface role gates (D-46):
//       /approvals/*                     → ops-admin (queue owner)
//       /finance/wallet-credits/*        → ops-finance OR ops-admin (read)
//                                           ops-admin only for mutation routes
//       /operations/dlq/*                → ops-admin only
//       /bookings/cancellations/*        → ops-cs OR ops-admin (open/approve gated downstream)
//       /bookings/new                    → ops-cs OR ops-admin (Plan 06-02)
//       /customers/[id]/erase            → ops-admin only
//     Non-authorised role → soft-redirect to /dashboard (UX per 06-UI-SPEC).

import { auth } from '@/auth.config';

export default auth((req) => {
  const { pathname } = req.nextUrl;
  const session = req.auth;

  // Never gate the NextAuth callback/sign-in endpoints.
  if (pathname.startsWith('/api/auth')) return;
  // Login page stays public.
  if (pathname.startsWith('/login')) return;

  // Every other path requires a session.
  if (!session) {
    const url = req.nextUrl.clone();
    url.pathname = '/login';
    url.searchParams.set('callbackUrl', pathname);
    return Response.redirect(url);
  }

  const roles = (session as { roles?: string[] }).roles ?? [];
  const hasRole = (name: string) => roles.includes(name);

  // Admin-only surfaces — approvals queue, DLQ operations, customer erase.
  const adminOnly =
    pathname.startsWith('/approvals') ||
    pathname.startsWith('/operations/dlq') ||
    /^\/customers\/[^/]+\/erase$/.test(pathname);
  if (adminOnly && !hasRole('ops-admin')) {
    const url = req.nextUrl.clone();
    url.pathname = '/dashboard';
    return Response.redirect(url);
  }

  // Finance + admin surface — wallet credits. Mutation endpoints enforce
  // ops-admin separately at the controller layer (D-39 4-eyes); the RSC
  // page itself is readable by ops-finance.
  if (
    pathname.startsWith('/finance/wallet-credits') &&
    !hasRole('ops-finance') &&
    !hasRole('ops-admin')
  ) {
    const url = req.nextUrl.clone();
    url.pathname = '/dashboard';
    return Response.redirect(url);
  }

  // CS + admin surface — manual booking creation, cancellation requests.
  if (
    (pathname.startsWith('/bookings/new') ||
      pathname.startsWith('/bookings/cancellations')) &&
    !hasRole('ops-cs') &&
    !hasRole('ops-admin')
  ) {
    const url = req.nextUrl.clone();
    url.pathname = '/dashboard';
    return Response.redirect(url);
  }
});

export const config = {
  // Match everything except static assets, Next internals, and the NextAuth
  // callback surface. Login page is handled inside the body above.
  matcher: [
    '/((?!_next/static|_next/image|favicon.ico|api/auth).*)',
  ],
};
