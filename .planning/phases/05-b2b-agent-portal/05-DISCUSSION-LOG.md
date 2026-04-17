# Phase 05 — B2B Agent Portal — Discussion Log

**Date:** 2026-04-17
**Mode:** discuss (interactive)
**Outcome:** `.planning/phases/05-b2b-agent-portal/05-CONTEXT.md` (decisions D-32..D-44)

This log is a faithful audit trail of every question asked during the
discuss-phase session, the options the user was shown, and the option
they selected. It exists so that downstream agents and future reviewers
can trace how each decision in CONTEXT.md was reached.

---

## Areas Selected

User selected **all four** gray areas surfaced by the analyzer:

1. Realm & multi-agency model
2. Markup rule schema depth
3. Post-ticket refund & top-up controls
4. B2B UI deltas + invoice content

---

## Area 1 — Realm & multi-agency model

### Q1.1 — SSO with backoffice if same org?

**Options presented:**
- (a) Shared browser session only — separate realms, separate logins, but same Keycloak instance (Recommended)
- (b) OIDC brokering / federation between `tbe-b2b` ↔ `tbe-backoffice`
- (c) Unified realm with role-based portal routing

**User selected:** (a) Shared browser session only

**Rationale captured:** Lowest complexity; Phase 5 doesn't need cross-portal SSO UX; revisit in Phase 7 if friction shows up.

**Result:** D-32 in CONTEXT.md.

---

### Q1.2 — Multi-agency users (one Keycloak user spanning multiple agencies)?

**Options presented:**
- (a) One user = one agency — single-valued `agency_id` claim (Recommended for v1)
- (b) Multi-agency from v1 — list-valued `agency_id` claim + UI context switcher

**User selected:** (a) One user = one agency

**Rationale captured:** No real-world OTA-group requirement surfaced yet; defer complexity to a future phase.

**Result:** D-33 in CONTEXT.md. Multi-agency support is explicitly a deferred idea.

---

### Q1.3 — Agent booking visibility within an agency?

**Options presented:**
- (a) Own bookings only for `agent`; agency-wide for `agent-admin` (matches ROADMAP Phase 5 UAT wording "sub-agent sees only their own bookings")
- (b) Agency-wide for all agent roles (agent, agent-admin, agent-readonly)
- (c) Own-only for agent+readonly; agency-wide for admin

**User selected:** (b) Agency-wide for all agent roles

**Rationale captured:** Deliberate override of ROADMAP UAT wording. User's operational model: all staff at a travel agency need to see the full agency book to cover holidays, handovers, and shared customers. "Own-only" is a trainee-grade mode that doesn't reflect real agency workflows.

**Result:** D-34 in CONTEXT.md — flagged with an explicit override note. Downstream MUST filter by `agency_id` only, never additionally by `sub`. Planner must add a comment block in `AgentBookingsController` citing D-34 so a future reader doesn't "fix" this back to the UAT wording.

---

### Q1.4 — What is `agent-readonly` actually for?

**Options presented:**
- (a) Agency oversight — read-only agency-wide view for finance/compliance staff (Recommended)
- (b) Trainee own-only view
- (c) Drop the role entirely

**User selected:** (a) Agency oversight

**Result:** D-35 in CONTEXT.md. Role stays, agency-wide read access, no booking or wallet mutations.

---

## Area 2 — Markup rule schema depth

### Q2.1 — Markup rule table schema?

**Options presented:**
- (a) Minimal — single row per agency: `(AgencyId, FlatAmount, PercentOfNet)` (Recommended)
- (b) Minimal + one RouteClass override row per agency: base + optional `RouteClass` override
- (c) Full priority engine — multiple rules per agency with priority ordering, route/class/market filters

**User selected:** (b) Minimal + one override row per agency

**Rationale captured:** User needs the ability to apply a different markup to long-haul vs short-haul (RouteClass) without building the full rule-priority engine. Max 2 rows per agency keeps evaluation trivial (`override ?? base`).

**Result:** D-36 in CONTEXT.md. Schema: `pricing.AgencyMarkupRules (AgencyId PK-component, FlatAmount decimal(18,4) NOT NULL DEFAULT 0, PercentOfNet decimal(5,4) NOT NULL DEFAULT 0, RouteClass varchar(16) NULL, IsActive bit NOT NULL DEFAULT 1)`. Composite PK `(AgencyId, RouteClass)` where `RouteClass IS NULL` is the base row. Evaluation: `pick row matching RouteClass else base`.

---

### Q2.2 — Keep the per-booking markup override on `/checkout/details`?

**Options presented:**
- (a) Keep — agent-admin only, per booking (Recommended)
- (b) Drop it — agency-level only, no per-booking deviation
- (c) Keep for all agent roles

**User selected:** (a) Keep — agent-admin only

**Result:** D-37 in CONTEXT.md. `BookingSagaState.AgencyMarkupOverride decimal(18,4) NULL` column. Enforced via `B2BAdminPolicy` (role `agent-admin` required) on the mutation endpoint. If null → fall through to `AgencyMarkupRules` evaluation.

---

### Q2.3 — Who manages markup rules (CRUD)?

**Options presented:**
- (a) Backoffice staff only, via Phase 6 backoffice UI (Recommended)
- (b) Agent-admin self-service in the B2B portal
- (c) Both

**User selected:** (a) Backoffice staff only

**Result:** D-38 in CONTEXT.md. Phase 5 seeds rules via EF migration or a SQL script committed with the phase; no CRUD UI is shipped in Phase 5. Phase 6 backoffice will add the management UI.

---

## Area 3 — Post-ticket refund & top-up controls

### Q3.1 — Post-ticket refund mechanism?

**Options presented:**
- (a) Manual backoffice credit in Phase 6 — saga only releases pre-ticket reservations (Recommended)
- (b) Automated refund saga in Phase 5 — GDS refund call + wallet credit transaction
- (c) Automated refund saga gated behind a feature flag

**User selected:** (a) Manual backoffice credit in Phase 6

**Rationale captured:** GDS refund semantics are airline-specific and error-prone; safer to keep humans in the loop and push the automation to a later phase.

**Result:** D-39 in CONTEXT.md. Phase 5 `BookingSaga` cancel path only releases pre-ticket reservations and writes a cancellation wallet transaction if the booking is in `Paid` but pre-`Ticketed` state. Post-ticket cancellations block at the API with `409 Conflict` + message pointing to backoffice.

---

### Q3.2 — Top-up min/max controls?

**Options presented:**
- (a) Min/max per-transaction via env config (Recommended) — `Wallet__TopUp__MinAmount` / `Wallet__TopUp__MaxAmount`
- (b) Per-agency configurable min/max (requires agency config table)
- (c) No limits at all

**User selected:** (a) Min/max per-transaction via env config

**Result:** D-40 in CONTEXT.md. Default `£10` / `£50,000`. Enforced in `WalletController.TopUp` BEFORE creating a Stripe PaymentIntent. `400 BadRequest` on violation with a structured error.

---

### Q3.3 — Commission settlement / payout to agencies?

**Options presented:**
- (a) Out of scope for Phase 5, track in Phase 6 backoffice (Recommended)
- (b) Show commission-accrued totals in Phase 5 dashboard only (no payout)
- (c) Ship full payout pipeline in Phase 5

**User selected:** (a) Out of scope for Phase 5, track in Phase 6 backoffice

**Result:** D-41 in CONTEXT.md. Phase 5 displays commission-accrued figures on the dashboard/invoice (informational only). No payout wiring. Phase 6 backoffice will own payout.

---

## Area 4 — B2B UI deltas + invoice content

### Q4.1 — Portal differentiation from B2C?

**Options presented:**
- (a) Indigo-600 accent only (swap B2C's blue for indigo) — subtle
- (b) "AGENT PORTAL" outline wordmark badge only — explicit
- (c) Keep both: indigo accent + badge (Recommended)
- (d) Completely different theme/layout

**User selected:** (c) Keep both

**Result:** D-42 in CONTEXT.md. Indigo-600 primary (WCAG AA verified against slate-50 background), outline "AGENT PORTAL" wordmark badge in the top navigation. starterKit fork pattern preserved (Pitfall 17 — `.jsx` untouched; B2B gets its own Tailwind theme extension).

---

### Q4.2 — Invoice PDF content?

**Options presented:**
- (a) GROSS only — simplest, matches most B2B invoice conventions (Recommended)
- (b) NET + markup + GROSS line-itemised
- (c) Include commission earned on each line

**User selected:** (a) GROSS only

**Rationale captured:** Standard B2B invoice practice; NET/markup are internal-only and should not appear on a document that the agency may forward to its own customer.

**Result:** D-43 in CONTEXT.md. New `AgencyInvoiceDocument` QuestPDF generator (separate from B2C `BookingReceiptDocument`). Columns: Product, Description, Passenger/Reference, Qty, GROSS. No NET, no markup, no commission. Commission accrued is displayed on the dashboard only.

---

### Q4.3 — Remaining UI-SPEC ASSUMED decisions?

**Options presented:**
- (a) Accept all UI-SPEC defaults as-is (Recommended) — compact tables, 20/50/100 page-number pager, stricter tone, 2-col dashboard, inline Stripe top-up, dark mode, Radix AlertDialog destructive confirms
- (b) Revisit each one individually

**User selected:** (a) Accept all UI-SPEC defaults as-is

**Result:** D-44 in CONTEXT.md. UI-SPEC sections that were tagged ASSUMED are promoted to LOCKED. Planner may proceed against UI-SPEC as the authoritative design contract.

---

## Decisions Skipped (Already Locked Upstream)

The following were NOT re-asked because they are already decided in prior CONTEXT files and/or research:

- Auth.js v5 edge/Node split (Phase 4 D-01/D-02)
- starterKit `.jsx` untouched rule (Phase 4 D-17, Pitfall 17)
- nuqs URL state for search/filter pages (Phase 4 D-12)
- TanStack Query staleTime pattern (Phase 4 D-12)
- Stripe CSP whitelist at portal layer (Phase 4 D-16)
- Wallet append-only ledger + UPDLOCK+ROWLOCK+HOLDLOCK (Phase 3 D-14/D-15)
- QuestPDF stream-through proxy (Phase 3 D-18, Phase 4 D-17)
- YARP gateway policy pattern (Phase 1 baseline)
- Research decisions D-22, D-23, D-24, D-27, D-28, D-29, D-31 (confirmed as-written in CONTEXT.md)

---

## Deferred Ideas

Captured for future phases:

- **OIDC brokering between tbe-backoffice and tbe-b2b realms** — revisit in Phase 7 hardening if the single-sign-in UX gap becomes an operational pain point.
- **Multi-agency users (OTA groups)** — future phase; requires list-valued `agency_id` claim + UI context switcher.
- **Automated post-ticket refund saga** — Phase 6+ once GDS refund flow is proven manually.
- **Per-agency configurable top-up min/max** — add when a tenant asks for it; requires an agency config table.
- **Commission payout pipeline** — Phase 6 backoffice.
- **Markup rule CRUD in B2B portal (agent-admin self-service)** — considered and rejected for v1; revisit only if backoffice load becomes a bottleneck.
- **Full markup priority engine** — only if (base + RouteClass override) proves insufficient.

---

*End of discussion log.*
