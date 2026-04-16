// /checkout/payment — unified Stripe PaymentElement step (B5 / Plan 04-04).
//
// RSC shell that branches on `?ref={kind}-{id}`:
//   - kind="flight": existing 04-02 behaviour — POST
//     /bookings/{id}/payment-intent → mount <PaymentElementWrapper>.
//   - kind="hotel": POST /hotel-bookings/{id}/payment-intent → same.
//   - kind="car":   POST /car-bookings/{id}/payment-intent → same.
//   - kind="basket": POST /baskets/{id}/payment-intents (plural) → mount
//     the single-PI <CombinedPaymentForm> (D-08). We hit the server here
//     (not the Zustand store) so the clientSecret is sourced server-side
//     with the session bearer, matching the flight/hotel contract.
//
// No query param other than `ref` is consulted; legacy booking= /
// hotelBookingId= / basketId= are eliminated (B5).

import { redirect } from 'next/navigation';

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { EmailVerifyGate } from '@/components/checkout/email-verify-gate';
import { PaymentElementWrapper } from '@/components/checkout/payment-element-wrapper';
import { CheckoutStepper } from '@/components/checkout/stepper';
import { CombinedPaymentForm } from '@/app/checkout/payment/combined-payment-form';
import { parseCheckoutRef, type CheckoutRef } from '@/lib/checkout-ref';

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

interface BasketClientSecretDto {
  clientSecret?: string;
  // For baskets the amount + currency live on the BasketDtoPublic; the
  // payment-intents endpoint returns just clientSecret, so we fetch the
  // basket body separately below to surface the total on the pay button.
}

interface BasketDtoPublic {
  basketId: string;
  status: string;
  totalAmount: number;
  currency: string;
}

async function createBookingPaymentIntent(kind: 'flight' | 'hotel' | 'car', id: string): Promise<PaymentIntentDto | null> {
  const routePrefix =
    kind === 'flight' ? '/bookings' : kind === 'hotel' ? '/hotel-bookings' : '/car-bookings';
  try {
    const res = await gatewayFetch(
      `${routePrefix}/${encodeURIComponent(id)}/payment-intent`,
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

async function initBasketPaymentIntent(
  basketId: string,
): Promise<{ clientSecret: string; amount: number; currency: string } | null> {
  try {
    const [secretRes, basketRes] = await Promise.all([
      gatewayFetch(`/baskets/${encodeURIComponent(basketId)}/payment-intents`, {
        method: 'POST',
      }),
      gatewayFetch(`/baskets/${encodeURIComponent(basketId)}`, { method: 'GET' }),
    ]);
    if (!secretRes.ok || !basketRes.ok) return null;
    const secret = (await secretRes.json()) as BasketClientSecretDto;
    const basket = (await basketRes.json()) as BasketDtoPublic;
    if (!secret.clientSecret) return null;
    return {
      clientSecret: secret.clientSecret,
      amount: basket.totalAmount,
      currency: basket.currency,
    };
  } catch (err) {
    if (err instanceof UnauthenticatedError) return null;
    return null;
  }
}

function firstParam(v: string | string[] | undefined): string | undefined {
  return Array.isArray(v) ? v[0] : v;
}

export default async function CheckoutPaymentPage({ searchParams }: PageProps) {
  const sp = await searchParams;
  const ref: CheckoutRef | null = parseCheckoutRef(firstParam(sp.ref));
  const verifyFlag = firstParam(sp.verify);

  const session = await auth();
  if (!session) {
    redirect('/login?callbackUrl=/checkout/payment');
  }

  if (!ref) {
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

  // Basket branch — single combined PaymentIntent (D-08).
  if (ref.kind === 'basket') {
    const basket = await initBasketPaymentIntent(ref.id);
    return (
      <>
        <CheckoutStepper currentStep="payment" />
        <section className="mx-auto w-full max-w-3xl px-6 py-8 md:px-10">
          <h1 className="text-2xl font-semibold">Payment</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Your card data goes directly to Stripe — we never see your PAN.
          </p>

          {!basket && (
            <div className="mt-8 rounded-md border border-dashed border-border p-4 text-sm text-muted-foreground">
              Could not initialise the payment. Please return to search and retry.
            </div>
          )}

          {basket && (
            <div className="mt-8">
              <CombinedPaymentForm
                basketId={ref.id}
                clientSecret={basket.clientSecret}
                amount={basket.amount}
                currency={basket.currency}
              />
            </div>
          )}
        </section>
      </>
    );
  }

  // Flight / hotel / car — single-booking PaymentElement.
  const intent = await createBookingPaymentIntent(ref.kind, ref.id);

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
              bookingId={ref.id}
              clientSecret={intent.client_secret}
              refKind={ref.kind}
            />
          </div>
        )}
      </section>
    </>
  );
}
