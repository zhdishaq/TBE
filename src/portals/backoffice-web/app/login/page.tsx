// Plan 06-01 Task 3 — backoffice sign-in page.
//
// UI-SPEC §1 Login: centred 384px card with BackofficePortalBadge,
// heading `Backoffice portal`, body `Sign in with your TBE staff
// credentials.`, primary CTA `Sign in`.
//
// callbackUrl sanitisation inherited from b2b-web (T-04-01-03 mitigation):
// only same-origin relative paths are forwarded to signIn; anything else
// falls back to /dashboard.

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { BackofficePortalBadge } from '@/components/layout/BackofficePortalBadge';
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
    redirect('/dashboard');
  }

  const { callbackUrl } = await searchParams;
  const safeCallbackUrl = sameOriginPath(callbackUrl);

  return (
    <main className="mx-auto flex min-h-screen max-w-[384px] flex-col items-stretch justify-center gap-6 px-6 py-14">
      <div className="flex justify-center">
        <BackofficePortalBadge />
      </div>
      <div className="rounded-lg border border-border bg-background p-6 shadow-sm">
        <div className="flex flex-col gap-2">
          <h1 className="text-2xl font-semibold leading-tight text-foreground">
            Backoffice portal
          </h1>
          <p className="text-sm text-muted-foreground">
            Sign in with your TBE staff credentials.
          </p>
        </div>
        <div className="mt-6">
          <SignInButton callbackUrl={safeCallbackUrl} />
        </div>
      </div>
      <p className="text-center text-xs text-muted-foreground">
        All actions are audited. Four-eyes approval is required for
        wallet credits and booking cancellations.
      </p>
    </main>
  );
}
