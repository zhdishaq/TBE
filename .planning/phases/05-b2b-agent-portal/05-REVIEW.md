---
phase: 05-b2b-agent-portal
plan_scope: 05-05 (commit range ed0a7a5..HEAD on master)
reviewed: 2026-04-18T20:22:56Z
depth: standard
files_reviewed: 29
files_reviewed_list:
  - src/portals/b2b-web/app/(portal)/admin/wallet/page.tsx
  - src/portals/b2b-web/app/(portal)/admin/wallet/threshold-dialog.tsx
  - src/portals/b2b-web/app/(portal)/admin/wallet/top-up-form.tsx
  - src/portals/b2b-web/app/(portal)/admin/wallet/transactions-table.tsx
  - src/portals/b2b-web/app/(portal)/layout.tsx
  - src/portals/b2b-web/app/api/wallet/threshold/route.ts
  - src/portals/b2b-web/app/api/wallet/top-up/intent/route.ts
  - src/portals/b2b-web/app/api/wallet/transactions/route.ts
  - src/portals/b2b-web/components/checkout/insufficient-funds-panel.tsx
  - src/portals/b2b-web/components/wallet/low-balance-banner.tsx
  - src/portals/b2b-web/components/wallet/request-top-up-link.tsx
  - src/portals/b2b-web/components/wallet/wallet-chip.tsx
  - src/portals/b2b-web/components/wallet/wallet-payment-element-wrapper.tsx
  - src/portals/b2b-web/lib/stripe.ts
  - src/portals/b2b-web/next.config.mjs
  - src/portals/b2b-web/tests/components/admin/threshold-dialog.test.tsx
  - src/portals/b2b-web/tests/components/admin/transactions-table.test.tsx
  - src/portals/b2b-web/tests/components/admin/wallet-top-up-form.test.tsx
  - src/portals/b2b-web/tests/components/checkout/insufficient-funds-panel.test.tsx
  - src/portals/b2b-web/tests/components/wallet/low-balance-banner.test.tsx
  - src/portals/b2b-web/tests/components/wallet/request-top-up-link.test.tsx
  - src/portals/b2b-web/tests/components/wallet/wallet-payment-element-wrapper.test.tsx
  - src/portals/b2b-web/tests/csp-route-scoping.test.ts
  - src/portals/b2b-web/tests/route-wallet-threshold.test.ts
  - src/portals/b2b-web/tests/route-wallet-top-up-intent.test.ts
  - src/portals/b2b-web/tests/route-wallet-transactions.test.ts
  - src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs
  - tests/Payments.Tests/B2BWalletControllerThresholdTests.cs
  - tests/Payments.Tests/WalletControllerTopUpTests.cs
findings:
  critical: 0
  high: 2
  medium: 4
  low: 4
  total: 10
status: issues_found
advisory: true
---

# Phase 05 / Plan 05-05: Code Review Report

**Reviewed:** 2026-04-18T20:22:56Z
**Depth:** standard
**Scope:** Commits `ed0a7a5..HEAD` (Plan 05-05 `/admin/wallet` portal surface + route handlers + `UpdateThresholdAsync` backend + sitewide LowBalanceBanner + InsufficientFundsPanel retrofit).
**Files Reviewed:** 29 (11 feat/doc + 11 Vitest + 2 xUnit + 5 RSC pages/layouts + next.config.mjs).
**Status:** issues_found (advisory — no commits blocked)

## Summary

Plan 05-05 ships the full `/admin/wallet` surface plus a sitewide low-balance banner and is on solid ground on the security-critical contracts this plan was written to preserve:

- **Pitfall 28 is honoured end-to-end.** `UpdateThresholdRequest` deliberately omits any agency identifier (record has only `ThresholdAmount` + `Currency`), both route handlers (`top-up/intent`, `threshold`) additionally strip `agencyId`/`AgencyId` from the forwarded body, and the xUnit fact `UpdateThreshold_ignores_body_agency_id_Pitfall_28` asserts the JWT agency wins. Defence-in-depth is present at DTO, portal, and controller layers.
- **D-40 parity is pinned.** Both `CreateTopUpIntent` (05-03 legacy) and the new `UpdateThresholdAsync` use `ContentResult { ContentType = "application/problem+json", StatusCode = 400 }` — NOT `ObjectResult<ProblemDetails>` — and the response shape includes `{type, title, status, detail, allowedRange:{min,max,currency}, requested}` verbatim per the constraint.
- **Pitfall 5 (Stripe isolation) is structurally enforced.** `lib/stripe.ts` exports a module-scope `loadStripe` promise; `<Elements>` is only mounted under `/admin/wallet` via `wallet-payment-element-wrapper.tsx`; `next.config.mjs` serves `walletCsp` (Stripe-allowed) for `/admin/wallet/:path*` only and a Stripe-free `standardCsp` everywhere else. The new Vitest guard `csp-route-scoping.test.ts` also locks out `'unsafe-eval'` on every matcher.
- **T-05-03-09 / T-05-05-02 mailto hardening is clean.** `RequestTopUpLink` emits `mailto:{email}?subject={encodeURIComponent('Top-up request')}` with zero interpolation; tests assert no `body=`, `agency_id`, `token`, `balance`, `threshold`, or `session` substrings in the href.
- **Sitewide banner semantics match UI-SPEC §11.** `role="status"` + `aria-live="polite"` (not `alert`), sessionStorage-only dismiss key `lowBalanceDismissed`, localStorage explicitly avoided. The WalletChip cache-key migration from `['wallet-balance']` → `['wallet','balance']` is correctly paired between the chip and the banner.
- **No hardcoded secrets in new source.** All `client_secret` references are env-var interpolations (`process.env.KEYCLOAK_B2B_CLIENT_SECRET`). No `pk_live` / `sk_live` / literal `Bearer` tokens.
- **No `any` / `as any` casts in Plan 05-05 code.** The few `session as { roles?: string[] } | undefined` casts are type-narrowing over an already-typed shape (redundant but not `any`).

The two **high-severity findings** both affect runtime correctness of the sitewide banner and the Stripe top-up redirect respectively; neither is a security hole but both will produce a user-visible defect on the first real production call. The **medium** items cluster around redundant type casts, a missed `GET` handler, and a small UX wart in the top-up button label. All findings are advisory for this phase.

---

## High

### HI-01: `LowBalanceBanner` will fail its threshold fetch on every non-`/admin/wallet` page (no `GET /api/wallet/threshold` handler)

**File:** `src/portals/b2b-web/components/wallet/low-balance-banner.tsx:69-76` and `src/portals/b2b-web/app/api/wallet/threshold/route.ts:16`
**Issue:** The banner queries the portal-level threshold URL:

```ts
const r = await fetch('/api/wallet/threshold');
```

but `app/api/wallet/threshold/route.ts` only exports `PUT`. Next.js returns `405 Method Not Allowed` for unimplemented methods. On `/admin/wallet` the banner works because the RSC page prefetches `['wallet','threshold']` via `gatewayFetch('/api/b2b/wallet/threshold')` server-side and hydrates the cache. On **every other authenticated portal page** (dashboard, bookings, search) the banner mounts cold, fires the client-side `GET /api/wallet/threshold`, hits a 405, and silently short-circuits via the `if (!thresholdData) return null` guard on line 83 — meaning the sitewide "low-balance warning" never actually renders anywhere outside `/admin/wallet`, which defeats the purpose of Plan 05-05 Task 5.

**Fix:** Add a `GET` sibling to the same route handler that forwards to the backend `GET /api/b2b/wallet/threshold` endpoint (which already exists — see `B2BWalletController.GetThreshold`):

```ts
export async function GET(): Promise<Response> {
  const session = await auth();
  const agencyId = (session?.user as { agency_id?: string } | undefined)?.agency_id;
  if (!session || !agencyId) return new Response(null, { status: 403 });
  try {
    const upstream = await gatewayFetch('/api/b2b/wallet/threshold');
    return new Response(upstream.body, {
      status: upstream.status,
      headers: {
        'content-type': upstream.headers.get('content-type') ?? 'application/json',
      },
    });
  } catch (err) {
    if (err instanceof UnauthenticatedError) return new Response(null, { status: 401 });
    return new Response(null, { status: 502 });
  }
}
```

The backend already treats `GET /api/wallet/threshold` as readable by any authenticated B2B user (no `B2BAdminPolicy`), so non-admin agents will also see their banner hydrate.

---

### HI-02: `stripe.confirmPayment` passes a relative `return_url` — Stripe throws `IntegrationError`

**File:** `src/portals/b2b-web/app/(portal)/admin/wallet/top-up-form.tsx:151`
**Issue:**

```ts
await stripe.confirmPayment({
  elements,
  confirmParams: { return_url: '/admin/wallet?success=1' },
});
```

Stripe.js requires `return_url` to be an absolute URL. A relative path fails fast with `IntegrationError: "return_url" must be a full URL`. Compare the sibling B2C form (`src/portals/b2c-web/components/checkout/payment-element-wrapper.tsx:41-48`) which builds `${window.location.origin}/checkout/processing?ref=...` — identical concern, resolved there. There is an additional basePath pitfall: the B2B portal serves at `basePath: '/b2b'`, so even if Stripe accepted the relative URL the redirect would land on `https://host/admin/wallet` (missing `/b2b`).

**Fix:**

```ts
const origin =
  typeof window !== 'undefined' && window.location?.origin ? window.location.origin : '';
await stripe.confirmPayment({
  elements,
  confirmParams: { return_url: `${origin}/b2b/admin/wallet?success=1` },
});
```

Or, prefer reading the basePath from `process.env.NEXT_PUBLIC_BASE_PATH` (if exposed) rather than hardcoding `/b2b`. Add a Vitest fact that asserts `call.confirmParams.return_url` starts with `http` and contains `/b2b/admin/wallet`.

---

## Medium

### MD-01: Redundant `session` casts fragment typing and invite drift

**Files:**
- `src/portals/b2b-web/app/(portal)/admin/wallet/page.tsx:65`
- `src/portals/b2b-web/app/api/wallet/threshold/route.ts:18, 23`
- `src/portals/b2b-web/app/api/wallet/top-up/intent/route.ts:19, 25`
- `src/portals/b2b-web/app/api/wallet/transactions/route.ts:17, 22`

**Issue:** `types/auth.d.ts` already augments the `Session` interface so that `session.roles: string[]` and `session.user.agency_id?: string` are first-class fields. The new code nonetheless uses:

```ts
const agencyId = (session?.user as { agency_id?: string } | undefined)?.agency_id;
const roles = (session as { roles?: string[] } | undefined)?.roles ?? [];
```

These casts paper over the existing type augmentation — if a future edit of `types/auth.d.ts` renames or drops one of these properties, TypeScript will no longer complain in the four affected files because the cast overrides the narrower declared type. This silently defeats the module augmentation at the three security-critical IDOR call sites.

**Fix:** Drop the ad-hoc casts and let the declared types do their job:

```ts
const agencyId = session?.user?.agency_id;
const roles = session?.roles ?? [];
```

---

### MD-02: `TopUpForm` button label renders `£0.00` before the user types — also handles `NaN` inputs loosely

**File:** `src/portals/b2b-web/app/(portal)/admin/wallet/top-up-form.tsx:46, 126, 164`
**Issue:** `const watchedAmount = watch('amount') || 0;` combined with `Number(watchedAmount || 0).toFixed(2)` produces `Pay £0.00 to top up` on first render (before the user has typed anything) and, if the input is cleared after typing, can briefly pass `NaN` through `Number(...).toFixed(2)` yielding the string `"NaN"` in the label. The zod validator will catch `NaN` on submit, but the label flicker is user-visible.

**Fix:** Format defensively:

```ts
const watchedAmount = Number(watch('amount'));
const label = Number.isFinite(watchedAmount) && watchedAmount > 0
  ? `Pay £${watchedAmount.toFixed(2)} to top up`
  : 'Enter an amount to top up';
```

---

### MD-03: `transactions-table` renders empty page-controls that become unreachable when the list is empty

**File:** `src/portals/b2b-web/app/(portal)/admin/wallet/transactions-table.tsx:44-92`
**Issue:** `data?.totalPages ?? 1` is used as the `totalPages` fallback while loading OR on the empty state. Combined with the `items.length === 0` branch that renders "No transactions yet", the pager shows `Page 1 of 1` with both Previous and Next disabled — functionally fine, but the `Rows per page` selector remains actionable, so a user can change `size` on an empty ledger and trigger additional zero-result fetches. Minor, but the empty-state render could hide the whole control strip.

**Fix:** When `data?.total === 0`, render only the empty-state card (drop the pager strip). Alternatively, keep pager but disable `size` select when `total === 0`.

---

### MD-04: `return_url` in B2B top-up lands back on `/admin/wallet?success=1` but `page.tsx` never reads `success`

**File:** `src/portals/b2b-web/app/(portal)/admin/wallet/page.tsx:63-110`
**Issue:** Once HI-02 is fixed and Stripe redirects the user back with `?success=1`, the RSC page never branches on that param — it re-renders the top-up form unchanged, which will re-show the Phase-2 `Pay £{amount} to top up` button because `clientSecret` is back to `null` (RSC re-render), but any background `?payment_intent=…` query params from Stripe stay in the URL. No success toast, no cache invalidation, and the low-balance banner will lag 30 s until the next poll cycle.

**Fix:** On success, explicitly invalidate `['wallet','balance']` + `['wallet','transactions']` and surface a confirmation. Either:

1. Read `?success=1` in `TopUpForm` (`'use client'`, use `useSearchParams`) and on mount dispatch `queryClient.invalidateQueries({ queryKey: ['wallet'] })` + show a `role="status"` inline message.
2. Or deploy a small client wrapper around `TransactionsTable` that calls `invalidateQueries` on the same condition.

---

## Low

### LO-01: `page.tsx` re-fetches balance/threshold/transactions via three sequential in-module functions — duplication with the route handlers

**File:** `src/portals/b2b-web/app/(portal)/admin/wallet/page.tsx:33-61`
**Issue:** Three `fetchX()` helpers inline the same try/parse/fallback pattern. The route handlers (`/api/wallet/*`) already produce identical shapes. Inline helpers aren't wrong but duplicate the fallback logic.

**Fix:** Extract a single `async function prefetch(path, fallback)` helper, or move these to `lib/wallet-prefetch.ts` and reuse across any future wallet-adjacent RSC page.

---

### LO-02: Default-threshold fallback (`500`) and default-currency fallback (`'GBP'`) are hardcoded across four files

**Files:**
- `src/portals/b2b-web/app/(portal)/admin/wallet/page.tsx:46`
- `src/portals/b2b-web/app/(portal)/admin/wallet/threshold-dialog.tsx:48`
- `src/portals/b2b-web/components/wallet/low-balance-banner.tsx:88`
- `src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs:169`

**Issue:** The 500 GBP "default threshold" is duplicated in portal + backend. If admins ever decide to change the baseline (say, £750), four call sites need syncing. Similarly, every 'GBP' literal assumes the agency wallet is GBP.

**Fix:** Move the constants into a shared `lib/wallet-defaults.ts` (portal side) and a shared constant in the backend; or make the RSC fallback read a single env var. Acceptable to defer — this is style.

---

### LO-03: `threshold-dialog.tsx` `cached` object is reconstructed every render (ok, but fragile)

**File:** `src/portals/b2b-web/app/(portal)/admin/wallet/threshold-dialog.tsx:46-50`
**Issue:**

```ts
const cached =
  qc.getQueryData<ThresholdCacheEntry>(['wallet', 'threshold']) ?? {
    threshold: 500,
    currency: 'GBP',
  };
```

The `?? {...}` fallback allocates a fresh object each render. `useEffect([open, cached.threshold, reset])` depends on a primitive (`cached.threshold`) so the effect is stable today, but if a future refactor adds `cached` itself to the dep array, the effect will run every render and re-run `reset`, potentially wiping user input while typing.

**Fix:** Memoise the fallback object or split the fallback into discrete primitives:

```ts
const cachedData = qc.getQueryData<ThresholdCacheEntry>(['wallet', 'threshold']);
const cachedThreshold = cachedData?.threshold ?? 500;
const cachedCurrency = cachedData?.currency ?? 'GBP';
```

---

### LO-04: `low-balance-banner.tsx` polls `/api/wallet/balance` every 30 s even when the banner is dismissed

**File:** `src/portals/b2b-web/components/wallet/low-balance-banner.tsx:57-66`
**Issue:** `useQuery` runs unconditionally; the dismiss only hides the DOM, it doesn't pause the 30 s poll. Because WalletChip ALSO polls the same key at 30 s, the shared cache means zero duplicate fetches — so the real cost is exactly one extra fetch-per-30-s on the chip's schedule either way. Not a perf concern per phase-scope rules, just a minor lifecycle tidy.

**Fix:** Gate the `refetchInterval` on the dismissed flag, or drop the interval from the banner and rely entirely on the chip's poll (which is already the "canonical" one per the shared-cache design):

```ts
refetchInterval: dismissed ? false : 30_000,
```

---

## Out-of-scope (noted for awareness)

- **Route-handler fallback error messages are empty bodies.** The three new route handlers return `new Response(null, { status: 403 })` on auth failure. This is fine, but downstream UI (threshold-dialog, top-up-form) only branches on `content-type === 'application/problem+json'` — a 403 with no body leaves the UI on a generic "Top-up failed. Please try again." message. Acceptable per plan, note for future UX polish.
- **Stripe publishable-key fallback.** `lib/stripe.ts:32-37` resolves `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` OR `NEXT_PUBLIC_STRIPE_PK` OR `''`. An empty string is passed to `loadStripe('')` at module load; Stripe.js logs a console error but the promise resolves to `null`, and `<Elements stripe={null}>` renders nothing. Not a runtime crash, but CI without the env var will see a silent Stripe Elements skip. Fine for now.
- **Page-level `session` null-branch elided.** `page.tsx:63-68` dereferences `(session as ...).roles` without checking `!session` first. The effect is correct (`roles` falls to `[]` → redirect to `/forbidden`) but it would be cleaner to `if (!session) redirect('/login')` first.

---

_Reviewed: 2026-04-18T20:22:56Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
