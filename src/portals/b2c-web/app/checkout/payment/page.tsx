// /checkout/payment — Stripe PaymentElement step (B2C-06).
//
// RSC shell:
//   1. auth() → redirect to /login if unauth'd (middleware already does
//      this; belt-and-braces for direct RSC hits).
//   2. If session.email_verified is false (or verify=1 flag present
//      from middleware) — render <EmailVerifyGate> and DO NOT mount
//      Stripe <Elements> (Pitfall 5).
//   3. Else: POST /bookings/{id}/payment-intent → get { client_secret,
//      amount, currency } → mount <PaymentElementWrapper>.
//
// Pitfall 5: Elements is confined to this single route so Stripe.js
// isn't bundled into unrelated pages.

import { redirect } from 'next/navigation';

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { EmailVerifyGate } from '@/components/checkout/email-verify-gate';
import { PaymentElementWrapper } from '@/components/checkout/payment-element-wrapper';
import { CheckoutStepper } from '@/components/checkout/stepper';

export const dynamic = 'force-dynamic';
export const runtime = 'nodejs';

interface PageProps {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}

interface PaymentIntentDto {
  client_secret: string;
  amount: number;
  currency: string;
}

async function createPaymentIntent(bookingId: string): Promise<PaymentIntentDto | null> {
  try {
    const res = await gatewayFetch(
      `/bookings/${encodeURIComponent(bookingId)}/payment-intent`,
      { method: 'POST' },
    );
    if (!res.ok) return null;
    return (await res.json()) as PaymentIntentDto;
  } catch (err) {
    if (err instanceof UnauthenticatedError) {
      return null;
    }
    return null;
  }
}

export default async function CheckoutPaymentPage({ searchParams }: PageProps) {
  const sp = await searchParams;
  const bookingParam = Array.isArray(sp.booking) ? sp.booking[0] : sp.booking;
  const verifyFlag = Array.isArray(sp.verify) ? sp.verify[0] : sp.verify;

  const session = await auth();
  if (!session) {
    redirect('/login?callbackUrl=/checkout/payment');
  }

  if (!bookingParam) {
    redirect('/flights');
  }

  const email = session.user?.email ?? 'your email address';
  const emailVerified = Boolean(session.email_verified) && verifyFlag !== '1';

  // Pitfall 7 / T-04-02-03: if email not verified, surface the gate and
  // do NOT mount <Elements> — Stripe.js should not even load here.
  if (!emailVerified) {
    return (
      <>
        <CheckoutStepper currentStep="payment" />
        <section className="mx-auto w-full max-w-3xl px-6 py-8 md:px-10">
          <h1 className="text-2xl font-semibold">Payment</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Please verify your email before you can pay.
          </p>
          <EmailVerifyGate email={email} verified={false} />
        </section>
      </>
    );
  }

  const intent = await createPaymentIntent(bookingParam);

  return (
    <>
      <CheckoutStepper currentStep="payment" />
      <section className="mx-auto w-full max-w-3xl px-6 py-8 md:px-10">
        <h1 className="text-2xl font-semibold">Payment</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Your card data goes directly to Stripe — we never see your PAN.
        </p>

        {!intent && (
          <div className="mt-8 rounded-md border border-dashed border-border p-4 text-sm text-muted-foreground">
            Could not initialise the payment. Please return to search and retry.
          </div>
        )}

        {intent && (
          <div className="mt-8">
            <PaymentElementWrapper
              amount={intent.amount}
              currency={intent.currency}
              bookingId={bookingParam}
              clientSecret={intent.client_secret}
            />
          </div>
        )}
      </section>
    </>
  );
}
