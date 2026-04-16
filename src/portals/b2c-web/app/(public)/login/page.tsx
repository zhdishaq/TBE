'use client';

// B2C sign-in page (D-04, D-06).
//
// Thin wrapper over `signIn("keycloak")` — Auth.js handles the redirect
// to Keycloak's hosted login form and the callback back into our
// middleware. We never render a username/password field here.
//
// T-04-01-03 (STRIDE: open redirect on callbackUrl) mitigation:
//   - callbackUrl is validated same-origin before being handed to
//     `signIn`. Auth.js v5 also validates callbacks, but we defend in
//     depth because an attacker that reaches this page has already
//     bypassed middleware.

import { useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { signIn } from 'next-auth/react';
import { Button } from '@/components/ui/button';

const DEFAULT_CALLBACK = '/bookings';

function sameOriginPath(raw: string | null | undefined): string {
  if (!raw) return DEFAULT_CALLBACK;
  // Accept only paths starting with `/` that are not protocol-relative
  // (`//evil.example.com`) or backslash-tricks (`/\\evil.example.com`).
  if (!raw.startsWith('/')) return DEFAULT_CALLBACK;
  if (raw.startsWith('//')) return DEFAULT_CALLBACK;
  if (raw.startsWith('/\\')) return DEFAULT_CALLBACK;
  return raw;
}

export default function LoginPage() {
  const searchParams = useSearchParams();
  const callbackUrl = sameOriginPath(searchParams.get('callbackUrl'));

  return (
    <main className="mx-auto flex min-h-[60vh] max-w-md flex-col justify-center gap-6 p-8">
      <div className="flex flex-col gap-2">
        <h1 className="text-2xl font-semibold leading-tight">Sign in</h1>
        <p className="text-muted-foreground">
          We use Keycloak to keep your account safe. You&apos;ll confirm your
          details on the next screen.
        </p>
      </div>

      <Button
        onClick={() => signIn('keycloak', { callbackUrl })}
        className="w-full"
      >
        Sign in with Keycloak
      </Button>

      <div className="flex flex-col gap-2 text-sm text-muted-foreground">
        <Link href="/password-reset" className="underline hover:text-foreground">
          Forgot password?
        </Link>
        <span>
          Don&apos;t have an account?{' '}
          <Link href="/register" className="underline hover:text-foreground">
            Create one
          </Link>
        </span>
      </div>
    </main>
  );
}
