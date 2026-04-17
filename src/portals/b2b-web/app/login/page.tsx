// Plan 05-01 Task 1 — B2B Agent Portal sign-in page.
//
// UI-SPEC §1 Login: single centred 384px card with AgentPortalBadge,
// heading `Agent portal`, body `Sign in with your agency credentials.`,
// primary CTA `Sign in`, and footer `Bookings are governed by your
// agency's wallet balance and markup rules.`
//
// T-04-01-03 mitigation inherited from b2c-web: callbackUrl is sanitised
// same-origin before being handed to signIn. Defence in depth — Auth.js
// v5 also validates callback URLs, but anyone who reaches this page has
// already bypassed middleware.
//
// The redirect to Keycloak is triggered by the client-side `<SignInButton />`
// below; this RSC page is responsible only for the chrome and for
// redirecting authenticated visitors away to /dashboard.

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { AgentPortalBadge } from '@/components/layout/agent-portal-badge';
import { SignInButton } from '@/app/login/sign-in-button';

interface LoginPageProps {
  // Next.js 16 — searchParams is a Promise (Pitfall 11).
  searchParams: Promise<{ callbackUrl?: string }>;
}

function sameOriginPath(raw: string | undefined): string {
  const DEFAULT = '/dashboard';
  if (!raw) return DEFAULT;
  if (!raw.startsWith('/')) return DEFAULT;
  if (raw.startsWith('//')) return DEFAULT;
  if (raw.startsWith('/\\')) return DEFAULT;
  return raw;
}

export default async function LoginPage({ searchParams }: LoginPageProps) {
  const session = await auth();
  if (session) {
    // Already signed in — bounce straight to the dashboard (never render
    // the sign-in card for an authenticated visitor).
    redirect('/dashboard');
  }

  const { callbackUrl } = await searchParams;
  const safeCallbackUrl = sameOriginPath(callbackUrl);

  return (
    <main className="mx-auto flex min-h-screen max-w-[384px] flex-col items-stretch justify-center gap-6 px-6 py-14">
      <div className="flex justify-center">
        <AgentPortalBadge />
      </div>
      <div className="rounded-lg border border-border bg-background p-6 shadow-sm">
        <div className="flex flex-col gap-2">
          <h1 className="text-2xl font-semibold leading-tight text-foreground">
            Agent portal
          </h1>
          <p className="text-sm text-muted-foreground">
            Sign in with your agency credentials.
          </p>
        </div>
        <div className="mt-6">
          <SignInButton callbackUrl={safeCallbackUrl} />
        </div>
      </div>
      <p className="text-center text-xs text-muted-foreground">
        Bookings are governed by your agency&rsquo;s wallet balance and markup rules.
      </p>
    </main>
  );
}
