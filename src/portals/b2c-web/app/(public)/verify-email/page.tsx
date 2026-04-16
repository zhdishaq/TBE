'use client';

// Landing page after Keycloak's "verify your email" link. Keycloak flips
// `email_verified=true` on the server; we force a fresh sign-in so the
// new claim lands in the JWT on the client side. Re-signing in is
// simpler than `update()` gymnastics and is transparent for the user —
// they already have a Keycloak session cookie.

import { signIn } from 'next-auth/react';
import { Button } from '@/components/ui/button';

export default function VerifyEmailPage() {
  return (
    <main className="mx-auto flex min-h-[60vh] max-w-md flex-col justify-center gap-6 p-8 text-center">
      <h1 className="text-2xl font-semibold leading-tight">Email verified</h1>
      <p className="text-muted-foreground">
        Thanks — your email address is now verified. You can continue to your
        bookings.
      </p>
      <Button
        onClick={() => signIn('keycloak', { callbackUrl: '/bookings' })}
      >
        Continue
      </Button>
    </main>
  );
}
