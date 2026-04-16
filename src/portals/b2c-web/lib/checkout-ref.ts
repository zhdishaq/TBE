// Unified checkout ref contract (Plan 04-04 / Task 3b / B5).
//
// The checkout pipeline used to read a patchwork of query params:
//   /checkout/details?booking=...          (flight)
//   /checkout/details?hotelBookingId=...   (hotel)
//   /checkout/details?basketId=...         (basket)
//
// B5 replaces all three with a single `?ref={kind}-{id}` contract.
// Every checkout page now calls `parseCheckoutRef(searchParams.ref)` and
// branches on the resolved `kind`.
//
// Threat T-04-04-12 (ref tampering): `parseCheckoutRef` validates the
// shape before returning anything; each downstream handler then
// re-asserts ownership against the resolved id (basket.UserId,
// booking.UserId) so a guessed ref still hits 403.

export type CheckoutRefKind = 'flight' | 'hotel' | 'basket' | 'car';

export interface CheckoutRef {
  kind: CheckoutRefKind;
  id: string;
}

/**
 * Parse a raw ?ref= query value into a typed ref, or null when absent /
 * malformed / an unexpected array shape (Next.js returns
 * `string | string[] | undefined`).
 */
export function parseCheckoutRef(
  raw: string | string[] | undefined | null,
): CheckoutRef | null {
  if (raw === null || raw === undefined) return null;
  if (Array.isArray(raw)) return null;
  const match = /^(flight|hotel|basket|car)-([A-Za-z0-9_-]+)$/.exec(raw);
  if (!match) return null;
  return { kind: match[1] as CheckoutRefKind, id: match[2] };
}

/**
 * Serialize a CheckoutRef back to the `?ref={kind}-{id}` wire format.
 * Inverse of parseCheckoutRef so round-trips are safe.
 */
export function buildCheckoutRef(kind: CheckoutRefKind, id: string): string {
  return `${kind}-${id}`;
}
