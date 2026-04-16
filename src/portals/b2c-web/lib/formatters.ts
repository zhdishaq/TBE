// Display formatters for money, date and duration.
//
// Pitfall 13: all money is rendered via `Intl.NumberFormat` using the
// currency code supplied by the backend — never a hardcoded `£`. Pricing
// stays correct if we later flip the portal to EUR/USD/AED markets.
//
// Source: 04-RESEARCH Pitfall 13 + 04-UI-SPEC §Typography (tabular
// numerals required on every money amount so columns stay aligned).

/**
 * Format a decimal amount using the browser/server Intl table for the
 * supplied ISO-4217 currency code. Locale is fixed to `en-GB` to match
 * the voice/date conventions in UI-SPEC §Copywriting Contract.
 *
 * @example formatMoney(150, "GBP") → "£150.00"
 * @example formatMoney(150, "USD") → "US$150.00"
 * @example formatMoney(150, "EUR") → "€150.00"
 */
export function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat('en-GB', {
      style: 'currency',
      currency,
    }).format(amount);
  } catch {
    // Invalid currency code (should never happen; backend is the source
    // of truth) — degrade to "XXX 123.45" instead of throwing through
    // into a React render.
    return `${currency} ${amount.toFixed(2)}`;
  }
}

/**
 * Format an ISO-8601 date string as a user-friendly date. Uses the
 * medium format, e.g. "16 Apr 2026".
 */
export function formatDate(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(d);
}

/**
 * Format a duration in minutes as "Xh Ym" (compact for flight legs).
 *
 * @example formatDuration(85) → "1h 25m"
 */
export function formatDuration(minutes: number): string {
  if (!Number.isFinite(minutes) || minutes < 0) return '';
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  if (h === 0) return `${m}m`;
  if (m === 0) return `${h}h`;
  return `${h}h ${m}m`;
}
