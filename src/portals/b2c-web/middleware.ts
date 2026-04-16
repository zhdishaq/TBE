// Next.js middleware — runs on the edge runtime.
//
// Source: 04-RESEARCH "Middleware with email_verified check" + Pitfall 3.
//
// IMPORTANT: imports from `@/auth.config` (edge-safe), NOT `@/lib/auth`.
// Importing lib/auth here would pull Node crypto and blow up at build.

import { auth } from '@/auth.config';

export default auth((req) => {
  const { pathname } = req.nextUrl;
  const session = req.auth;

  // Never gate the NextAuth callback/sign-in endpoints themselves.
  if (pathname.startsWith('/api/auth')) {
    return;
  }

  const isProtected =
    pathname.startsWith('/bookings') || pathname.startsWith('/checkout');
  if (!isProtected) return;

  if (!session) {
    const url = req.nextUrl.clone();
    url.pathname = '/login';
    url.searchParams.set('callbackUrl', pathname);
    return Response.redirect(url);
  }

  // Checkout payment step is hard-gated on Keycloak email verification
  // (CONTEXT D-06). Any attempt to enter /checkout/payment without a
  // verified email bounces to a "verify your email" page.
  if (
    pathname.startsWith('/checkout/payment') &&
    !session.email_verified
  ) {
    const url = req.nextUrl.clone();
    url.pathname = '/checkout/verify-email';
    return Response.redirect(url);
  }
});

export const config = {
  // Explicitly exclude /api/auth/* so Auth.js callback endpoints work
  // even when the user has no session yet (sign-in flow).
  matcher: [
    '/bookings/:path*',
    '/checkout/:path*',
  ],
};
