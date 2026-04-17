// Plan 05-02 Task 3 — shared money-formatter used by every B2B surface that
// renders price cells. Keeps the B2B portal single-sourced on Intl.NumberFormat
// so tabular-nums alignment + currency-symbol placement stay consistent across
// the 4-column dual-pricing grid, the DebitSummary CTA, and the InsufficientFunds
// panel (UI-SPEC §Dual-pricing Grid, §Confirm Page).

export function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat('en-GB', {
      style: 'currency',
      currency: currency || 'GBP',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(amount);
  } catch {
    // Fallback — Intl throws on unrecognised currency codes in edge tests.
    return `${currency} ${amount.toFixed(2)}`;
  }
}
