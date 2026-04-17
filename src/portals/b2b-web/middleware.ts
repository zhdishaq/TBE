// Next.js middleware (edge runtime) — B2B Agent Portal.
//
// Source: fork of src/portals/b2c-web/middleware.ts + 05-PATTERNS.md §16 +
// 05-00-PLAN action step 4.
//
// IMPORTANT: imports from `@/auth.config` (edge-safe), NOT `@/lib/auth`.
// Importing lib/auth here would pull Node crypto and blow up at build.
//
// Phase 5 Plan 05-00 deltas vs b2c-web/middleware.ts:
//   - Protected-path set extended: /dashboard, /admin, /search added.
//   - The b2c "email-verified gate on /checkout/payment" block is REPLACED
//     by a /admin/* role gate that bounces non-admin agents to /dashboard
//     (never 403 — softer UX per 05-UI-SPEC).
//   - Matcher updated to match the expanded protected paths.

import { auth } from '@/auth.config';

export default auth((req) => {
  const { pathname } = req.nextUrl;
  const session = req.auth;

  // Never gate the NextAuth callback/sign-in endpoints themselves.
  if (pathname.startsWith('/api/auth')) {
    return;
  }

  const isProtected =
    pathname.startsWith('/dashboard') ||
    pathname.startsWith('/bookings') ||
    pathname.startsWith('/checkout') ||
    pathname.startsWith('/admin') ||
    pathname.startsWith('/search');
  if (!isProtected) return;

  if (!session) {
    const url = req.nextUrl.clone();
    url.pathname = '/login';
    url.searchParams.set('callbackUrl', pathname);
    return Response.redirect(url);
  }

  // D-32 / B2BAdminPolicy (05-CONTEXT.md) — /admin/* is agent-admin only.
  // Non-admin agents (role `agent` or `agent-readonly`) bounce back to the
  // dashboard rather than seeing a 403. The softer UX is mandated by
  // 05-UI-SPEC §Portal Differentiation — agents should never feel locked
  // out of their own portal, just redirected away from admin-only surfaces.
  const roles = (session as { roles?: string[] }).roles;
  if (pathname.startsWith('/admin') && !roles?.includes('agent-admin')) {
    const url = req.nextUrl.clone();
    url.pathname = '/dashboard';
    return Response.redirect(url);
  }
});

export const config = {
  // Explicitly exclude /api/auth/* so Auth.js callback endpoints work
  // even when the user has no session yet (sign-in flow).
  matcher: [
    '/dashboard/:path*',
    '/bookings/:path*',
    '/checkout/:path*',
    '/admin/:path*',
    '/search/:path*',
  ],
};
