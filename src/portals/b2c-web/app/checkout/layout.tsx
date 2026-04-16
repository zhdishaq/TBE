// Checkout layout — authenticated shell + step indicator.
//
// Gates the whole /checkout/** tree behind Auth.js session. Unauth'd
// visitors bounce to /login with a callbackUrl. Verified users land on
// the appropriate sub-page; the email-verify gate still fires at
// /checkout/payment when `email_verified=false` (D-06, Pitfall 7) —
// that check is in the payment page itself AND in middleware.

import { redirect } from 'next/navigation';
import type { ReactNode } from 'react';

import { auth } from '@/lib/auth';
import { CheckoutStepper, type CheckoutStep } from '@/components/checkout/stepper';

function stepFromPath(pathname: string | undefined): CheckoutStep {
  if (!pathname) return 'details';
  if (pathname.startsWith('/checkout/payment')) return 'payment';
  if (pathname.startsWith('/checkout/processing')) return 'payment';
  if (pathname.startsWith('/checkout/success')) return 'payment';
  return 'details';
}

export default async function CheckoutLayout({ children }: { children: ReactNode }) {
  const session = await auth();
  if (!session) {
    redirect('/login?callbackUrl=/checkout/details');
  }

  // Server layouts don't receive the raw pathname; the client-side
  // Stepper reads route segment in the child pages and re-renders if
  // needed. Default to `details` here (layout renders once per route
  // segment, so step mismatches are transient).
  const currentStep: CheckoutStep = stepFromPath('/checkout/details');

  return (
    <div className="flex min-h-screen w-full flex-col">
      <CheckoutStepper currentStep={currentStep} />
      <main className="flex-1">{children}</main>
    </div>
  );
}
