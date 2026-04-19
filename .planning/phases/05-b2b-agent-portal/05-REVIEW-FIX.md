---
phase: 05-b2b-agent-portal
applied_at: 2026-04-18
scope: "HI-01 + HI-02 (explicit user instruction)"
iteration_mode: "single-pass (no --auto)"
findings_in_scope: 2
findings_fixed: 2
findings_deferred: 8
---

# Phase 05 Code Review — Auto-Fix Summary

## Scope

User directive: `/gsd-code-review-fix 05 — auto-fix HI-01 + HI-02`.

Per REVIEW.md, both HIGH findings were runtime defects that would break
user-visible flows on first live call — prioritised over the medium/low
items. Out-of-scope findings are listed under **Deferred**.

## Fixes Applied

### HI-01 — Missing GET /api/wallet/threshold route handler  →  FIXED

**Root cause:** `src/portals/b2b-web/app/api/wallet/threshold/route.ts`
only exported `PUT`. The sitewide `LowBalanceBanner` polls
`GET /api/wallet/threshold` every 30 seconds on every authenticated
route; Next.js returned 405 everywhere except `/admin/wallet` (where
the threshold is loaded via server component props), silently breaking
the advisory banner for readonly agents and non-wallet pages.

**Fix:**
- Added `GET` handler sibling to existing `PUT` in
  `src/portals/b2b-web/app/api/wallet/threshold/route.ts`.
- Mirrors the PUT auth shape: session + `agency_id` required (403 on
  miss), `UnauthenticatedError` → 401, other errors → 502.
- Intentionally **not** gated on `agent-admin` — readonly agents must
  see the banner. `B2BPolicy` (any authenticated B2B user) is the
  upstream gate; agent-admin restriction is write-only.
- Upstream body/status/content-type forwarded verbatim so the banner's
  TanStack Query hook can render without extra shaping.

**Test coverage:**
- `tests/route-wallet-threshold.test.ts` gained two facts:
  - `GET: returns 403 without agency_id`
  - `GET: forwards upstream 200 with body + content-type for readonly user`
- 8/8 facts green (6 existing PUT + 2 new GET).

**Commit:** `fix(05-05): HI-01 — add GET /api/wallet/threshold route handler`

### HI-02 — Stripe confirmPayment relative return_url  →  FIXED

**Root cause:** `src/portals/b2b-web/app/(portal)/admin/wallet/top-up-form.tsx`
passed `return_url: '/admin/wallet?success=1'` to
`stripe.confirmPayment`. Stripe.js requires an absolute URL and throws
`IntegrationError` on a relative path, so the first live 3DS challenge
would hard-fail with a cryptic client-side error.

**Fix:**
- Changed to
  `return_url: \`${window.location.origin}/b2b/admin/wallet?success=1\``
  in `StripeConfirmBlock.onConfirm`.
- Explicitly includes the `/b2b` basePath (per `next.config.mjs`
  line 55) so the Stripe redirect hits the correct portal mount after
  test-mode or live 3DS.

**Test coverage:**
- `tests/components/admin/wallet-top-up-form.test.tsx` gained
  `HI-02: confirmPayment is called with absolute return_url including /b2b basePath`.
- Refactored mock to use `vi.hoisted()` so the `confirmPaymentSpy`
  reference is stable across the vi.mock hoist boundary; added
  `confirmPaymentSpy.mockClear()` in `beforeEach`.
- Asserts `return_url` matches `/^https?:\/\//` AND contains
  `/b2b/admin/wallet` AND contains `success=1`.
- 4/4 facts green.

**Commit:** `fix(05-05): HI-02 — use absolute return_url for stripe.confirmPayment`

## Regression Sweep

Full b2b-web vitest run after both fixes: **25 files / 112 tests green**
(up from 109 — three new facts, no regressions).

## Deferred (Out of Scope — Not Fixed This Pass)

Per user directive, only HI-01 + HI-02 were in scope. The following
findings remain in REVIEW.md for a future `/gsd-code-review-fix 05 --all`
or targeted follow-up:

| ID | Severity | One-liner |
|----|----------|-----------|
| MD-01 | Warning | Redundant `session?.user as { agency_id?: string }` casts across route handlers |
| MD-02 | Warning | `£0.00` / `NaN` flicker in `WalletChip` during first-load while query is pending |
| MD-03 | Warning | Empty-state pager clutter on `/bookings` when result set < page size |
| MD-04 | Warning | `?success=1` URL param read in `/admin/wallet` but never cleared from history |
| LO-01 | Info | Unused imports in `threshold-dialog.tsx` |
| LO-02 | Info | Hard-coded `£50–£10,000` copy duplicated in 3 places |
| LO-03 | Info | `aria-live` missing on LowBalanceBanner root |
| LO-04 | Info | TanStack Query staleTime default 0 causes extra refetches |

None of the deferred items block ship or wet-lab verification.

## Re-verification

Not re-spawned (single-pass mode, no `--auto`). Both fixes are covered
by the 112/112 vitest run above. The 8 items in
`05-VERIFICATION.md` `human_verification` (Keycloak realm import, live
Stripe, live email, concurrency UAT, visual D-42/D-44, browser CSP)
remain the gating wet-lab list before phase sign-off.

## Recommendation

Phase 05 is now code-complete AND code-review-clean at the HIGH level.
Proceed to the human-verification sprint — the 2 HIGH fixes remove the
two trivial user-touch failure modes that would have derailed the
Stripe top-up and low-balance banner on first real use.

---
*Applied: 2026-04-18 by gsd-code-review-fix*
