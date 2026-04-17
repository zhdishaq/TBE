# Phase 5: B2B Agent Portal - Context

**Gathered:** 2026-04-17
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver a B2B agent portal: travel agents authenticate against Keycloak `tbe-b2b`, search inventory with dual NET/GROSS/markup/commission pricing displayed simultaneously, book on behalf of walk-in customers using atomic credit-wallet debit (no Stripe at booking checkout), receive TTL + low-balance alerts, and download invoice + e-ticket PDFs. Agency admins can create/deactivate sub-agents and top up the wallet via Stripe.

**Out of scope for Phase 5** — deferred to later phases:
- Backoffice/CRM surfaces (Phase 6) — including markup-rule CRUD UI, post-ticket manual wallet credits, commission payout logic, cross-agency reporting.
- Production hardening: multi-agency users, OIDC brokering between realms, daily/velocity top-up caps, Stripe Radar integration (Phase 7).
- Multi-dimensional markup rules (airline × GDS class × date range) — v2.
- Agent self-service markup rule editing — explicitly disallowed to prevent agencies awarding themselves unbounded margin.

</domain>

<decisions>
## Implementation Decisions

### Realm & Tenancy Model
- **D-32:** "SSO with backoffice if same org" (B2B-01) = **shared-browser-session experience only**. `tbe-b2b` and `tbe-backoffice` remain separate realms. No OIDC brokering, no unified realm. A user with credentials in both realms signs into each portal independently and the live Keycloak browser session keeps them from re-typing. (Confirms research D-30.)
- **D-33:** **One Keycloak user → one agency** in v1. `agency_id` remains a single-valued user attribute on the Keycloak user. Multi-agency users (OTA groups) deferred. (Confirms research A8.)
- **D-34:** **Agency-wide booking visibility for all three agent roles** (`agent`, `agent-admin`, `agent-readonly`). Every B2B booking query filters by `agency_id` claim only — never additionally by `sub`. **This deliberately overrides the ROADMAP §Phase 5 UAT wording "the sub-agent sees only their own bookings in the list."** Downstream agents MUST NOT implement per-user booking scoping.
- **D-35:** **`agent-readonly` role = agency oversight.** Read-only agency-wide access (bookings, wallet, sub-agent list). Cannot book, cannot top up, cannot create sub-agents. Intended for finance/compliance users. Role stays in the realm patch.

### Markup & Pricing
- **D-36:** **`pricing.AgencyMarkupRules` schema is intentionally minimal.** Columns: `AgencyId, FlatAmount, PercentOfNet, RouteClass NULL, IsActive`. **Maximum two active rows per agency** — one base rule (RouteClass NULL) + one optional RouteClass override row (e.g., long-haul). No priority engine; `PricingService` resolver picks the RouteClass row if present for that query, else the base row. (Refines research D-25.)
- **D-37:** **Per-booking markup override is agent-admin only**, via the numeric input on `/checkout/details` (UI-SPEC screen 6). Server enforces `B2BAdminPolicy` on the `POST /api/b2b/bookings` path and rejects `override` from any non-admin session. Override applies to this booking only and does **not** write back to `AgencyMarkupRules`.
- **D-38:** **Markup rule CRUD is backoffice-only, deferred to Phase 6.** Phase 5 ships the schema + resolver + seed script only. Agent-admin must not edit their own agency's rules through the B2B portal in v1. Seeded rules loaded via EF Core migration or hand-authored SQL script at deployment time.

### Post-Ticket Money Flows
- **D-39:** **Post-ticket B2B refund → manual Phase 6 backoffice credit.** Phase 5 saga compensation only releases pre-ticket wallet reservations (`WalletReleaseCommand` on any pre-`TicketIssued` failure). For a ticketed B2B booking that is later cancelled (customer changes mind), the saga does **not** auto-credit the wallet. Backoffice staff write a manual wallet credit entry in Phase 6 (e.g., `TopUp`-shaped row with reason = `RefundedBooking`). No fare-rule refund calculator in Phase 5.
- **D-40:** **Top-up hard caps via env config.** `MinTopUpAmount` (default £10) and `MaxTopUpAmount` (default £50,000) read by `WalletController` before creating the PaymentIntent; requests outside the range fail with `400 Bad Request` and a specific error code. Per-day caps, velocity checks, Stripe Radar integration deferred to Phase 7.
- **D-41:** **Commission settlement is out of scope for Phase 5.** `AgencyCommission` is stamped on `BookingSagaState`, displayed in the dual-pricing grid, and printed on internal agency statements, but Phase 5 ships no payout logic (no monthly payout job, no offset against top-up, no wallet auto-credit for commission). Payout mechanics belong in Phase 6 backoffice.

### Portal UX
- **D-42:** **Portal differentiation = both indigo-600 accent AND "AGENT PORTAL" outline wordmark badge.** Locks UI-SPEC assumption 1 & 2. Redundancy is intentional — accent is colorblind-safe with badge + badge is glance-resistant with accent. WCAG-AA verified on light + dark. Badge renders via `<AgentPortalBadge />` on every authenticated route; accent is the only Tailwind palette swap between `b2c-web` and `b2b-web`.
- **D-43:** **Invoice PDF is GROSS-only, customer-facing.** `AgencyInvoiceDocument` (new QuestPDF generator in `NotificationService.Application/Documents/`) renders itinerary, GROSS fare breakdown (fare / YQ-YR surcharges / taxes — follows FLTB-03 rules), agency letterhead, VAT line if applicable. **Never** renders NET, markup, or commission. Agent hands this PDF directly to the walk-in customer. E-ticket uses the existing Phase 3 `BookingReceiptDocument` unchanged. (Confirms research D-26.)
- **D-44:** **All remaining UI-SPEC ASSUMED decisions lock as written.** Compact row density on tables (`h-11` = 44px, also the mobile touch-target minimum); comfortable padding on forms; page-number pagination on booking list (20/50/100 rows, default 20); stricter transactional tone with actionable-not-empathetic error copy; 2-column dashboard grid on `lg`+; inline wallet top-up (amount + Stripe PaymentElement + pay CTA, no multi-step); dark mode carried over from Phase 4; Radix `AlertDialog` destructive confirms with no typed confirmation. Checker already approved UI-SPEC — no revisit needed.

### Research Decisions Confirmed (no change)
- **D-22, D-23, D-24, D-27, D-28, D-29, D-31** from `05-RESEARCH.md` **confirmed as written.** Separate `src/portals/b2b-web/` fork on port 3001; `tbe-b2b` realm delta patch with audience mapper flipping `ValidateAudience=true`; `Channel` enum (B2C=0 / B2B=1) on `BookingSagaState` via migration `20260600000000_AddAgencyPricingAndChannel`; wallet chip poll at `refetchInterval: 30_000` / `staleTime: 25_000`; TTL dashboard poll at `refetchInterval: 60_000`; `tbe-b2b-admin` service-account client mirroring Plan 04-01's `tbe-b2c-admin` (30s-skew cache, Node-runtime-only, server-injected `agency_id`); booking-on-behalf reuses Plan 04-04 `checkout-ref` contract with `?ref=flight-{id}` / `?ref=hotel-{id}`.

### Claude's Discretion
- Specific column types + constraints on `AgencyMarkupRules` (decimal precision, `IsActive` default, index on `AgencyId`).
- Seed strategy for D-38 (EF Core `HasData`, SQL script in `infra/seed-data/`, or migration-embedded `INSERT`).
- Exact error codes + `problem+json` shape for D-40 top-up cap rejections.
- Concrete mailto template when non-admin agent clicks "Request top-up from your admin" (recipient = agency admin email from Keycloak; subject/body wording).
- Folder + file naming for `AgencyInvoiceDocument` + `AgencyInvoice.cshtml` inside `NotificationService`.
- Choice of tokens in the invoice QuestPDF document beyond the ones UI-SPEC already mandates.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & roadmap
- `.planning/REQUIREMENTS.md` — B2B-01..B2B-10 acceptance criteria
- `.planning/ROADMAP.md` §Phase 5 — plan scope + UAT (note: D-34 deliberately overrides the "sub-agent sees only their own bookings" UAT line)

### This phase (already authored)
- `.planning/phases/05-b2b-agent-portal/05-RESEARCH.md` — architecture, patterns, pitfalls, D-22..D-31 proposed decisions, open questions
- `.planning/phases/05-b2b-agent-portal/05-UI-SPEC.md` — full visual & interaction contract (approved by checker 2026-04-17)
- `.planning/phases/05-b2b-agent-portal/05-VALIDATION.md` — test strategy

### Prior phase decisions (locked — must not be reversed)
- `.planning/phases/01-infrastructure-foundation/01-CONTEXT.md` — service layout, shared projects, RabbitMQ topology, Keycloak realm scaffolding
- `.planning/phases/03-core-flight-booking-saga-b2c/03-CONTEXT.md` — saga step ordering (D-05), wallet append-only ledger (D-14), `UPDLOCK+ROWLOCK+HOLDLOCK` (D-15), RazorLight + QuestPDF pattern (D-17/D-18), email idempotency (D-19)
- `.planning/phases/04-b2c-portal-customer-facing/04-CONTEXT.md` — starterKit fork pattern (D-01..D-03), Auth.js v5 edge-split (D-04), gatewayFetch Bearer forwarding (D-05), nuqs URL state (D-11), server-search/client-filter (D-12), IATA typeahead (D-18), Pitfall 17 (.jsx untouched)

### Architecture & stack
- `.planning/research/ARCHITECTURE.md` — service topology, gateway routing
- `.planning/research/STACK.md` — pinned library versions
- `.planning/research/PITFALLS.md` — known traps (Pitfalls 19..28 apply directly to Phase 5)
- `.planning/research/SUMMARY.md` — synthesized critical rules

### UI baseline
- `src/portals/b2c-web/` (full tree) — byte-for-byte source for `components/ui/*.jsx`, `types/ui.d.ts`, `lib/auth.ts`, `lib/api-client.ts`, `lib/checkout-ref.ts`, `lib/stripe-client.ts`, `auth.config.ts`, `middleware.ts`, `next.config.mjs` — fork with minimal diffs per UI-SPEC "Reuse vs New Ledger"
- `ui/starterKit/` — pristine reference
- `ui/starterKit/package.json` — version pins (Next.js 16, React 19, Tailwind v4, ReUI/Radix, TanStack Query v5, react-hook-form, zod, sonner)

### Realm & gateway (infrastructure)
- `infra/keycloak/realms/tbe-b2b-realm.json` — base realm (exists from Phase 1); Phase 5 layers `infra/keycloak/realm-tbe-b2b.json` as a delta patch
- `infra/keycloak/realms/realm-tbe-b2c.json` — delta-patch template to mirror
- `src/gateway/TBE.Gateway/appsettings.json` — `/api/b2b/*` routes already present under `B2BPolicy`
- `src/gateway/TBE.Gateway/Program.cs` — `ValidateAudience=false` flips to `true` after audience mapper lands

### Wallet & saga (already shipped in Phase 3 — reuse verbatim)
- `src/services/PaymentService/PaymentService.Infrastructure/Wallet/WalletRepository.cs` — `UPDLOCK+ROWLOCK+HOLDLOCK` balance reads
- `src/services/PaymentService/PaymentService.Application/Wallet/IWalletRepository.cs`
- `src/services/PaymentService/PaymentService.Application/Consumers/WalletReserveConsumer.cs` — emits `WalletLowBalance` below threshold
- `src/services/PaymentService/PaymentService.Application/Consumers/StripeTopUpConsumer.cs` — sole writer of TopUp entries
- `src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs` — top-up endpoints (D-40 caps enforced here)
- `src/services/BookingService/BookingService.Application/Saga/BookingSaga.cs` — add B2B branch (D-24)
- `src/services/BookingService/BookingService.Application/Saga/BookingSagaState.cs` — add Channel + agency pricing columns (D-24)
- `src/services/BookingService/BookingService.Infrastructure/Ttl/TtlMonitorHostedService.cs` — already emits 24h/2h warnings
- `src/services/NotificationService/NotificationService.Application/Consumers/WalletLowBalanceConsumer.cs` — SendGrid path shipped
- `src/services/NotificationService/NotificationService.Application/Consumers/TicketingDeadlineApproachingConsumer.cs`
- `src/shared/TBE.Contracts/Commands/SagaCommands.cs` — `WalletReserveCommand` / `WalletCommitCommand` / `WalletReleaseCommand` already defined

### Compliance
- `.planning/PROJECT.md` §Constraints — PCI-DSS SAQ-A boundary (Stripe Elements only on `/admin/wallet/*`), GDPR
- `.planning/REQUIREMENTS.md` §COMP — COMP-01..06

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets (copy verbatim / near-verbatim)
- `src/portals/b2c-web/` — fork entire tree to `src/portals/b2c-web/` → `src/portals/b2b-web/` with realm URL / client-id / CSP-scope / role-claim deltas. UI-SPEC "Reuse vs New Ledger" table enumerates every file and the exact delta.
- `src/services/PaymentService/**` — wallet path complete; Phase 5 adds **no** code here beyond D-40 top-up cap enforcement (2-line check in `WalletController`).
- `src/services/NotificationService/**` — `WalletLowBalanceConsumer` + `TicketingDeadlineApproachingConsumer` already consume. Phase 5 adds `AgencyInvoice.cshtml` template + `AgencyInvoiceDocument` QuestPDF generator + `InvoicesController` for `/api/bookings/{id}/invoice.pdf`.
- `src/services/BookingService/**` — saga exists; Phase 5 adds `Channel` enum + agency-pricing columns on `BookingSagaState`, the B2B branch in `BookingSaga.cs` (PnrCreated → `WalletReserveCommand` when Channel=B2B), and `AgentBookingsController` for `/api/b2b/bookings/me` (scoped by `agency_id` claim per D-34 — agency-wide filter only, no `sub` filter).
- `src/services/PricingService/**` — new `AgencyMarkupRules` table + `ApplyMarkup` pure function + `AgencyPriceRequestedConsumer` (D-36).
- `src/shared/TBE.Contracts/Enums/Channel.cs` — new (B2C=0 / B2B=1).
- `infra/keycloak/realms/realm-tbe-b2c.json` — delta-patch recipe to mirror for `realm-tbe-b2b.json`.

### Established Patterns (follow strictly)
- Three-project service layout (API / Application / Infrastructure).
- Append-only wallet ledger with `SignedAmount` PERSISTED computed column — never introduce a mutable balance field (D-14 from Phase 3).
- Cross-service communication via `TBE.Contracts` messages only.
- MassTransit + EF Core outbox for all publishes.
- Deterministic idempotency keys on every Stripe call (`booking-{id}-{operation}` or `topup-{walletId}-{amount}-{timestamp-bucket}`).
- `loadStripe` memoised at module scope, mounted only on `/admin/wallet/*` (Pitfall 5 inherited).
- Red-placeholder xUnit tests via `Trait("Category","RedPlaceholder")` staged in Wave 0 (Phase 4 pattern).
- `.jsx` starterKit components copied byte-for-byte; TypeScript friction resolved via `types/ui.d.ts` ambient shim, never by converting to `.tsx` (Pitfall 17).
- Stream-through PDF proxy from portal to service: `new Response(upstream.body, ...)` with awaited `params` Promise — never `upstream.arrayBuffer()` (Pitfall 11/14 inherited from Plan 04-01).

### Integration Points
- Browser (`localhost:3001`) → Next.js RSC `b2b-web` → `gatewayFetch` (Bearer from Auth.js session) → YARP `/api/b2b/*` → strip prefix → downstream `/api/*` → `BookingService` / `PricingService` / `PaymentService` / `InventoryService` / `NotificationService`.
- `PricingService` owns `AgencyMarkupRules` table + `ApplyMarkup` function — no other service reads or writes it (Pitfall 23).
- `BookingService` saga: on `PnrCreated`, branches by `Channel`: B2B → `WalletReserveCommand`; B2C → existing `AuthorizePaymentCommand`.
- Sub-agent create (B2B-10): `POST /api/b2b/agents` → `lib/keycloak-b2b-admin.ts` → Keycloak Admin API with server-injected `agency_id` (Pitfall 28) — client never sends `agency_id` in body.
- Phase 6 will consume `AgencyCommission` field for payout logic + the `WalletTransactions` table for manual credit entries (D-39, D-41).
- Phase 7 swaps `ValidateAudience=false → true` on the gateway's B2B JWT scheme once the audience mapper is verified in prod.

</code_context>

<specifics>
## Specific Ideas

- **Agency-wide visibility (D-34) is the single biggest deviation from ROADMAP.** The planner must call this out explicitly in `AgentBookingsController` — a comment block citing D-34 + this CONTEXT file so a future reader doesn't "fix" the query to include `sub`. `gsd-verify-work` should verify by running a multi-agent fixture and asserting both agents in the same agency see each other's bookings.
- **Two-row markup schema (D-36)** means the resolver is trivial: `SELECT TOP 1 * WHERE AgencyId = @id AND (RouteClass = @class OR RouteClass IS NULL) ORDER BY RouteClass DESC` — returns the RouteClass-specific row if it exists, else the base row. No priority engine, no rules DSL.
- **Per-booking override (D-37) goes on `BookingSagaState`**, not a separate table. Column name `AgencyMarkupOverride decimal(18,4) NULL`. If non-null, it overrides the computed markup for this booking only. Audit log captures it.
- **D-39's "no auto-credit for ticketed cancellations" is a hard contract.** The B2B branch of the saga MUST NOT listen for `BookingCancelled` events on ticketed bookings — only pre-ticket compensation triggers `WalletReleaseCommand`. Phase 6 will introduce a new event type (`WalletRefundRequested` or similar) that backoffice staff raise manually.
- **D-40 caps are enforced server-side only.** The UI may soft-validate (disable the "Pay" button if amount > max), but the `WalletController` is the source of truth. Env vars: `Wallet__TopUp__MinAmount` / `Wallet__TopUp__MaxAmount` (Microsoft.Extensions.Configuration naming).
- **Invoice PDF (D-43) never fetches NET from the saga state at render time** — the template only binds to `{gross, yq_yr_surcharge, taxes, vat, total_gross}`. Even if the view model accidentally contains NET, the Razor template doesn't render it.

</specifics>

<deferred>
## Deferred Ideas

- OIDC brokering between `tbe-backoffice` and `tbe-b2b` realms for same-org staff (cross-realm identity federation). Track for Phase 7 hardening or a post-v1 phase.
- Multi-agency users (OTA groups with one agent working across several agencies). Requires list-valued `agency_id` claim + UI agency-switcher + query rework. Post-v1.
- Multi-dimensional markup rules engine (airline × GDS class × origin region × date range × product). Post-v1 when a pricing team actually needs it.
- Agent-facing markup-rule self-service CRUD. Explicitly disallowed in v1 to prevent agencies awarding themselves unbounded margin.
- Auto-wallet-credit on ticketed B2B booking cancellations (with fare-rule refund calculator). Phase 6 backoffice — ticketed cancellations route through manual credit entries.
- Commission payout mechanics (monthly bank transfer, offset-against-next-top-up, wallet auto-credit). Phase 6 backoffice.
- Per-day / velocity top-up caps + Stripe Radar integration + manual-review queue for fraud. Phase 7.
- Agency logo rendering on `/login` and invoice PDF (currently uses Metronic default brand). v2 when brand assets exist.
- Real-time TTL dashboard via SSE/SignalR. Not needed — 60s polling proven sufficient (D-28).

</deferred>

---

*Phase: 05-b2b-agent-portal*
*Context gathered: 2026-04-17*
