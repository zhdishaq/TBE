// Email verification gate (D-06, Pitfall 7, T-04-02-03).
//
// Renders a non-dismissable modal over /checkout/payment when the
// Keycloak `email_verified` claim is false. Two actions:
//   • Resend email           — POST /api/auth/resend-verification
//   • I have verified, refresh — force re-auth to pick up refreshed claim.
//
// If `verified` is true the component returns `null` — it's safe to
// render unconditionally from the payment page.
//
// The modal intentionally has no close affordance (`X`, backdrop click,
// Esc) — the user cannot pay until email is verified (D-06).

'use client';

import { useCallback, useState, useTransition } from 'react';

interface EmailVerifyGateProps {
  email: string;
  verified: boolean;
}

type ResendState =
  | { kind: 'idle' }
  | { kind: 'sending' }
  | { kind: 'sent' }
  | { kind: 'error'; message: string };

export function EmailVerifyGate({ email, verified }: EmailVerifyGateProps) {
  const [resend, setResend] = useState<ResendState>({ kind: 'idle' });
  const [, startTransition] = useTransition();

  const handleResend = useCallback(async () => {
    setResend({ kind: 'sending' });
    try {
      const response = await fetch('/api/auth/resend-verification', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
      });
      if (!response.ok) {
        setResend({ kind: 'error', message: 'Could not resend. Please try again.' });
        return;
      }
      setResend({ kind: 'sent' });
    } catch {
      setResend({ kind: 'error', message: 'Network error. Please try again.' });
    }
  }, []);

  const handleRefresh = useCallback(() => {
    startTransition(() => {
      // Full-page reload picks up any updated `email_verified` claim
      // from the Auth.js session after the user clicks the verification
      // link in their inbox.
      if (typeof window !== 'undefined') {
        window.location.reload();
      }
    });
  }, []);

  if (verified) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="verify-email-heading"
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
    >
      <div className="mx-4 w-full max-w-md rounded-lg bg-background p-6 shadow-xl">
        <h2 id="verify-email-heading" className="text-lg font-semibold">
          Verify your email first
        </h2>
        <p className="mt-2 text-sm text-muted-foreground">
          We need to confirm your email before you can pay. We sent a link to{' '}
          <span className="font-medium text-foreground">{email}</span>.
        </p>

        <div className="mt-6 flex flex-col gap-2">
          <button
            type="button"
            onClick={handleResend}
            disabled={resend.kind === 'sending'}
            className="inline-flex items-center justify-center rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-60"
          >
            {resend.kind === 'sending' ? 'Sending…' : 'Resend email'}
          </button>
          <button
            type="button"
            onClick={handleRefresh}
            className="inline-flex items-center justify-center rounded-md border border-border px-4 py-2 text-sm font-medium hover:bg-muted"
          >
            I have verified, refresh
          </button>
        </div>

        {resend.kind === 'sent' && (
          <p role="status" className="mt-3 text-xs text-green-700">
            Verification email sent to {email}. It may take a minute to arrive.
          </p>
        )}
        {resend.kind === 'error' && (
          <p role="alert" className="mt-3 text-xs text-red-600">
            {resend.message}
          </p>
        )}
      </div>
    </div>
  );
}
