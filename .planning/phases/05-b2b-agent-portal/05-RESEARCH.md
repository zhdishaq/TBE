# Phase 5: B2B Agent Portal — Research

**Researched:** 2026-04-17
**Domain:** B2B multi-tenant web portal · Keycloak realm design · dual NET/GROSS pricing · atomic credit wallet integration · MassTransit saga branching
**Confidence:** HIGH (stack + infra already proven in Phases 1–4; gaps are documented honestly below)

---

## Summary

Phase 5 ships the **B2B Agent Portal** — a separate Next.js 16 customer-facing application that lets travel agencies search inventory with dual NET/GROSS pricing, book on behalf of walk-in customers using **atomic credit-wallet deduction** (no Stripe at checkout), receive ticketing-deadline + low-balance alerts, and download e-tickets / invoices.

**Critical discovery:** The entire credit-wallet data path — `payment.WalletTransactions` append-only ledger, `IWalletRepository` with `UPDLOCK + ROWLOCK + HOLDLOCK` balance reads, `WalletReserve/Commit/Release` consumers, `CreateWalletTopUpAsync` Stripe path with webhook replay dedup, `WalletLowBalance` events, and the `[Authorize(Roles="agency-admin")] /wallets/{id}/top-ups` controller — **shipped in Phase 3**. ROADMAP Plan 3 ("credit wallet service") is therefore **integration work**, not greenfield build. Similarly, the TTL monitor (Warn24H / Warn2H flags + `TicketingDeadlineApproachingConsumer`) is live. The gateway already routes `/api/b2b/*` under `B2BPolicy` against the `tbe-b2b` realm.

What's actually missing: **(1)** realm patch adding audience mapper + `tbe-b2b-admin` service client (mirror `realm-tbe-b2c.json`), **(2)** agency-pricing columns on `BookingSagaState` + B2B channel branch in `BookingSaga` that publishes `WalletReserveCommand` instead of `AuthorizePaymentCommand`, **(3)** `src/portals/b2b-web/` forked starterKit with Auth.js v5 pointed at `tbe-b2b` realm, **(4)** markup/commission rules table + pricing-service extension, and **(5)** an invoice QuestPDF generator (GROSS-only; agents re-bill their end customer).

**Primary recommendation:** Fork starterKit into `src/portals/b2b-web/` (follow Plan 04-00 recipe verbatim), layer a delta JSON patch on the existing `tbe-b2b` realm (follow Plan 04-00 Task 3 pattern), stamp `Channel = B2B` on `BookingSagaState`, and branch the existing saga so `PnrCreated` → `WalletReserveCommand` when channel is B2B (B2C continues to `AuthorizePaymentCommand`). Keep dual pricing computed server-side only — never leak NET to a B2C-authenticated client. Reuse every Phase-4 library and pattern.

<phase_requirements>
## Phase Requirements

| ID | Description (from REQUIREMENTS.md) | Research Support |
|----|-----|-----|
| B2B-01 | Travel agents log in via Keycloak tbe-b2b realm (SSO w/ backoffice if same org). | Pattern 1 (realm patch). Realm already provisioned; needs audience mapper + `tbe-b2b-admin` service client. "SSO with backoffice" = shared browser-level Keycloak session on same host; realms remain separate (HIGH). |
| B2B-02 | Agents see dual pricing (NET agent cost + GROSS suggested retail). | Pattern 3. Extend `PricingService` with markup calc; add `AgencyNet/Gross/Markup/Commission` columns to `BookingSagaState`; NET never returned from `/api/flights/*` (B2C route) — only from `/api/b2b/pricing/*`. |
| B2B-03 | Agents book on behalf of walk-in customer — capture customer name/email/phone. | Pattern 5. Reuse checkout-ref contract (`?ref=flight-{id}`) in the B2B portal; POST booking with `customer: { name, email, phone }` body + `Channel=B2B` stamped on saga. |
| B2B-04 | Booking debited from agency credit wallet (atomic — no double-spend). | Pattern 4. Wallet infra complete. Saga branch publishes `WalletReserveCommand` on `PnrCreated` for B2B; commits on `TicketIssued`; releases on any compensation. |
| B2B-05 | Low-balance alerts when wallet drops below threshold. | Already implemented. `WalletReserveConsumer` emits `WalletLowBalance` when `post_reserve < threshold`; `WalletLowBalanceConsumer` → SendGrid. Phase 5 wires the UI banner. |
| B2B-06 | Ticketing deadline alerts (24h + 2h before). | Already implemented. `TtlMonitorHostedService` writes `Warn24HSent`/`Warn2HSent` flags + publishes `TicketingDeadlineApproaching`. Phase 5 adds the agent-dashboard list view. |
| B2B-07 | Per-agency markup / commission rules. | Pattern 3 (cont.). New `pricing.AgencyMarkupRules` table (flat + percent + optional route-class override). Keep it SIMPLE — no rules engine. |
| B2B-08 | Invoice PDF + e-ticket download. | Pattern 9. New `AgencyInvoiceDocument` QuestPDF generator (GROSS-only). Reuse `BookingReceiptDocument` for the e-ticket. Stream-through proxy from the B2B portal. |
| B2B-09 | Agent dashboard — list bookings, search, filter by status. | Pattern 8. Reuse `/customers/me/bookings` pattern; new `/api/b2b/bookings/me` delegator scoped by `agency_id` claim. Server-side pagination. |
| B2B-10 | Admin agent can create sub-agents under same agency. | Pattern 6. Mirror Plan 04-01 `lib/keycloak-admin.ts` — new `tbe-b2b-admin` service client with realm-management roles `manage-users` + `view-users`; Node-runtime-only; browser-import guard; in-process 30s-skew token cache. New user stamped with same `agency_id` attribute. |
</phase_requirements>

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Agent authentication (B2B-01) | Keycloak (`tbe-b2b` realm) | YARP (JWT validation, audience mapper) | Same identity split as b2c: realm issues tokens, gateway enforces. |
| B2B portal UI (B2B-02/03/05/06/09) | Frontend Server (Next.js 16 RSC) | Browser (TanStack Query + nuqs) | Mirror Plan 04-00: RSC for auth gates, client components for live data. |
| Dual pricing (B2B-02/B2B-07) | API (`PricingService`) | DB (`pricing.AgencyMarkupRules`) | NET computation must NEVER happen in browser; markup rules server-owned. |
| Wallet reservation + commit (B2B-04) | API (`PaymentService` + `BookingService` saga) | DB (`payment.WalletTransactions` with `UPDLOCK+ROWLOCK+HOLDLOCK`) | Atomicity invariant — MUST stay in DB tier under isolation. |
| Booking-on-behalf (B2B-03) | API (`BookingService`) | DB (`BookingSagaState.CustomerName/Email/Phone + Channel=B2B`) | Saga is the single source of booking truth. |
| Sub-agent creation (B2B-10) | API-adjacent (Next.js API route → Keycloak Admin API) | Keycloak | Uses service-account client creds — node runtime only. |
| Ticketing-deadline alerts (B2B-06) | API (`BookingService` TTL monitor) + `NotificationService` | DB (idempotency via `Warn24HSent`/`Warn2HSent` columns) | Already shipped — phase 5 adds the consumer path + UI surface. |
| Low-balance email (B2B-05) | API (`NotificationService.WalletLowBalanceConsumer`) | `EmailIdempotencyLog` table | Already shipped — just needs SendGrid template review. |
| Invoice + e-ticket (B2B-08) | API (new `AgencyInvoiceDocument` / existing `BookingReceiptDocument`) | Frontend Server (stream-through proxy) | QuestPDF runs server-side only (Pitfall 11 from Phase 4). |

---

## Standard Stack

### Core (reuse verbatim from Plan 04-00)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Next.js | 16.x (App Router) | B2B portal framework | Same as b2c-web; proven edge-split pattern. |
| React | 19.x | UI runtime | Phase 4 precedent. |
| Auth.js (next-auth) | 5.0.0-beta.31 (exact pin) | Keycloak OIDC in edge-safe split | D-01/D-02 pattern; beta version must be pinned exactly. |
| TanStack Query | v5 | Server-state caching | D-12 pattern; `queryKey` criteria-only. |
| nuqs | latest | URL state sync | D-11 pattern. |
| Tailwind CSS | 4.x | Styling | Part of starterKit fork. |
| Metronic starterKit | copied to `src/portals/b2b-web/` | UI components (`.jsx` untouched) | D-03 + Pitfall 17. |

### Core (server-side — reuse verbatim from Phases 1–3)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET | 9 | Microservice runtime | Phases 1-4. |
| MassTransit | 9.1.x | Saga state machine + consumers | `BookingSaga` extends here. |
| EF Core | 9.x | Outbox/Inbox + read-side; NOT for wallet deduction | Wallet deduction uses raw Dapper per D-15. |
| Dapper | latest | Raw SQL for wallet with hints | Mandatory `UPDLOCK, ROWLOCK, HOLDLOCK` on balance read. |
| QuestPDF | 2026.2.4 | Invoice + e-ticket PDF | Phase 4 precedent; community license OK (<$1M rev). |
| RazorLight | 2.3.1 | Email templates | Phase 4 precedent. |
| SendGrid | 9.29.3 | Transactional email | Phase 4 precedent. |
| Stripe.net | 51.0.0 | Wallet top-up only (NOT checkout) | `CreateWalletTopUpAsync` already implemented. |
| Serilog | latest | Structured logging | Phases 1–4. |

### Supporting (new for Phase 5)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| — | — | No new libraries. | Phase 5 is integration + one new migration + one new QuestPDF generator. |

**Version verification:** All versions above are already pinned in existing csproj / package.json files — this phase should NOT bump them. Verify current lock-file pins before Wave 0.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Separate `src/portals/b2b-web/` fork | Role-gate inside `src/portals/b2c-web/` | ❌ Rejected — multi-realm Auth.js is fragile; CSP drifts; B2C/B2B cookies would collide. Fork is cheap (Pitfall 17 keeps `.jsx` as-is). |
| Dapper with SQL hints | EF Core `SERIALIZABLE` transaction | ❌ Rejected (already settled as D-15) — EF doesn't emit the exact hint triplet; raw SQL is safer. |
| Real-time TTL dashboard (SSE / SignalR) | TanStack Query `refetchInterval` 30s | ❌ Rejected — 2000ms-poll pattern already shipped and proven (Pitfall 6). SSE adds infra for zero real benefit at 24h/2h granularity. |
| Rules engine for markup | Flat `AgencyMarkupRules` table | ❌ Rejected — scope creep. One table: `AgencyId`, `FlatAmount`, `PercentOfNet`, optional `RouteClass`. |

---

## Architecture Patterns

### System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│  Travel Agent Browser (localhost:3001)                                   │
│  - Auth.js session (tbe-b2b realm)                                       │
│  - TanStack Query: /api/b2b/flights/search, /api/b2b/bookings/me         │
│  - TanStack Query: /api/wallets/{id}/balance (refetchInterval 30s)       │
└───────────────────────────────┬─────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  src/portals/b2b-web (Next.js 16 RSC)                                    │
│  - auth.config.ts (edge) + lib/auth.ts (Node, refresh logic)             │
│  - lib/api-client.ts  → gatewayFetch() attaches Bearer                   │
│  - lib/keycloak-b2b-admin.ts (sub-agent create)                          │
│  - middleware.ts (agent-admin gate for /admin/*)                         │
│  - /checkout/payment REPLACED BY /checkout/confirm  (wallet debit, no PI)│
└───────────────────────────────┬─────────────────────────────────────────┘
                                │ Bearer (tbe-b2b JWT)
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  YARP Gateway  (ValidateAudience=TRUE once audience mapper is live)      │
│  /api/b2b/*  →  strips prefix →  downstream /api/*                       │
│  Policies: B2BPolicy { RequireAuthenticatedUser, roles agent/agent-admin}│
└───────┬──────────────────┬───────────────────┬─────────────────┬────────┘
        │                  │                   │                 │
        ▼                  ▼                   ▼                 ▼
┌─────────────┐ ┌──────────────────┐ ┌──────────────────┐ ┌────────────┐
│ Booking     │ │ Pricing          │ │ Payment          │ │ Inventory  │
│ Service     │ │ Service          │ │ Service          │ │ Service    │
│             │ │                  │ │                  │ │ (search)   │
│ + Channel   │ │ + MarkupRules    │ │ Wallet           │ │            │
│ + AgencyNet │ │ + NET/GROSS calc │ │ (ALREADY DONE)   │ │            │
│ + B2B saga  │ │                  │ │                  │ │            │
│ branch      │ │                  │ │                  │ │            │
└──────┬──────┘ └──────────────────┘ └──────┬───────────┘ └────────────┘
       │                                    │
       │  WalletReserveCommand (B2B only)   │
       ├───────────────────────────────────►│
       │                                    ├─► Dapper UPDLOCK+ROWLOCK+HOLDLOCK
       │  WalletReserved / Failed           │   payment.WalletTransactions
       │◄───────────────────────────────────┤   (append-only ledger)
       │                                    │
       │  WalletCommitCommand (on           │
       │   TicketIssued)                    │
       ├───────────────────────────────────►│
       │                                    │
┌──────┴────────┐                      ┌────┴─────────────┐
│ NotificationSvc│ Notification via   │ Stripe webhook   │
│ • WalletLow    │◄───── RabbitMQ ────┤ StripeTopUp      │
│ • TicketingDL  │                    │ (wallet top-up)  │
│ • Invoice PDF  │                    └──────────────────┘
└────────────────┘
```

Entry point: Agent logs into `localhost:3001` → Auth.js redirects to Keycloak `tbe-b2b` → callback → session cookie → RSC gateways under `agent` role → TanStack Query hits `/api/b2b/*` → YARP strips prefix + forwards with `Authorization: Bearer` → downstream services.

### Recommended Project Structure

```
src/
├── portals/
│   ├── b2c-web/                      # exists
│   └── b2b-web/                      # NEW — fork of ui/starterKit
│       ├── app/
│       │   ├── (auth)/
│       │   │   └── login/
│       │   ├── (portal)/
│       │   │   ├── layout.tsx         # agent shell (wallet balance + TTL alerts in nav)
│       │   │   ├── dashboard/         # B2B-06 + B2B-09: bookings + TTL alerts
│       │   │   ├── search/            # B2B-02: dual-pricing flight search
│       │   │   ├── bookings/[id]/     # B2B-09 detail view + downloads
│       │   │   ├── checkout/
│       │   │   │   └── confirm/       # B2B-04: wallet debit (NO PaymentElement)
│       │   │   ├── admin/
│       │   │   │   ├── agents/        # B2B-10 sub-agent CRUD (agent-admin only)
│       │   │   │   └── wallet/        # PAY-04 top-up (PaymentElement returns here)
│       │   ├── api/
│       │   │   ├── auth/[...nextauth]/
│       │   │   ├── agents/[id]/       # B2B-10 proxy → keycloak admin
│       │   │   └── bookings/[id]/
│       │   │       ├── invoice.pdf/   # B2B-08 stream-through proxy
│       │   │       └── e-ticket.pdf/  # B2B-08 stream-through proxy
│       ├── auth.config.ts             # edge — Keycloak provider (tbe-b2b)
│       ├── lib/
│       │   ├── auth.ts                # Node — refresh logic
│       │   ├── api-client.ts          # gatewayFetch() → Bearer
│       │   ├── keycloak-b2b-admin.ts  # sub-agent creation (tbe-b2b-admin SA)
│       │   └── checkout-ref.ts        # reuse from b2c-web (ref={flight|hotel|car}-{id})
│       ├── middleware.ts              # edge gate — require agent-admin for /admin/*
│       ├── next.config.mjs            # CSP: allow js.stripe.com ONLY on /admin/wallet/*
│       ├── playwright.config.ts
│       └── vitest.config.ts
├── services/
│   ├── BookingService/
│   │   ├── BookingService.Application/Saga/
│   │   │   ├── BookingSagaState.cs        # + Channel enum (B2C/B2B)
│   │   │   │                              # + AgencyId (Guid?)
│   │   │   │                              # + AgencyNetFare/AgencyMarkup/AgencyGross/AgencyCommission
│   │   │   │                              # + CustomerName/CustomerEmail/CustomerPhone (on-behalf)
│   │   │   │                              # + WalletReservationTxId (already exists)
│   │   │   └── BookingSaga.cs             # + During PnrCreated: if Channel=B2B → WalletReserveCommand
│   │   │                                  #                      else           → AuthorizePaymentCommand
│   │   │                                  # + On TicketIssued: if Channel=B2B → WalletCommitCommand
│   │   │                                  # + Compensation: if Channel=B2B → WalletReleaseCommand
│   │   ├── BookingService.Infrastructure/Migrations/
│   │   │   └── 20260600000000_AddAgencyPricingAndChannel.cs   # NEW
│   │   └── BookingService.API/Controllers/
│   │       └── AgentBookingsController.cs  # NEW — /api/b2b/bookings/me (scoped by agency_id claim)
│   ├── PricingService/
│   │   ├── PricingService.Application/
│   │   │   ├── AgencyMarkupRules/
│   │   │   │   ├── IAgencyMarkupRepository.cs   # NEW
│   │   │   │   └── ApplyMarkup.cs               # pure: (net, rule) → gross
│   │   │   └── Consumers/
│   │   │       └── AgencyPriceRequestedConsumer.cs  # NEW
│   │   └── PricingService.Infrastructure/Migrations/
│   │       └── 20260600000100_AddAgencyMarkupRules.cs  # NEW
│   ├── PaymentService/ (wallet ALREADY complete — NO changes expected)
│   └── NotificationService/
│       ├── NotificationService.Application/
│       │   ├── Templates/
│       │   │   └── AgencyInvoice.cshtml        # NEW (GROSS-only)
│       │   └── Documents/
│       │       └── AgencyInvoiceDocument.cs    # NEW (QuestPDF)
│       └── NotificationService.API/Controllers/
│           └── InvoicesController.cs            # NEW — /api/bookings/{id}/invoice.pdf
├── gateway/TBE.Gateway/
│   ├── appsettings.json          # existing /api/b2b/* routes (no changes)
│   └── Program.cs                # ValidateAudience: false → true (after audience mapper lands)
└── shared/TBE.Contracts/
    └── Enums/Channel.cs          # NEW — B2C | B2B
infra/
└── keycloak/
    ├── realm-tbe-b2b.json        # NEW — delta patch (audience mapper + tbe-b2b-admin SA)
    └── verify-audience-smoke-b2b.sh  # NEW — mirror b2c version
```

### Pattern 1: Keycloak `tbe-b2b` Realm Patch (B2B-01)

**What:** Add audience mapper on `tbe-agent-portal` client + provision `tbe-b2b-admin` service-account client. Layered as a delta JSON on top of `infra/keycloak/realms/tbe-b2b-realm.json` — follow exact Plan 04-00 Task 3 convention.

**When to use:** Wave 0 of Phase 5.

**Example (structure, not exact config — mirror `realm-tbe-b2c.json`):**

```jsonc
// infra/keycloak/realm-tbe-b2b.json  [CITED: infra/keycloak/realm-tbe-b2c.json]
{
  "_meta": { "phase": "05-b2b-agent-portal", "plan": "05-00" },
  "realm": "tbe-b2b",
  "clients": [
    {
      "clientId": "tbe-agent-portal",
      "protocolMappers": [
        {
          "name": "tbe-api-audience",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-audience-mapper",
          "config": { "included.custom.audience": "tbe-api", "access.token.claim": "true" }
        },
        {
          "name": "agency-id-attribute",
          "protocolMapper": "oidc-usermodel-attribute-mapper",
          "config": { "user.attribute": "agency_id", "claim.name": "agency_id", "access.token.claim": "true" }
        }
      ]
    },
    {
      "clientId": "tbe-b2b-admin",
      "serviceAccountsEnabled": true,
      "standardFlowEnabled": false,
      "directAccessGrantsEnabled": false,
      "secret": "${KEYCLOAK_B2B_ADMIN_CLIENT_SECRET}",
      "serviceAccountClientRoles": {
        "realm-management": ["manage-users", "view-users"]
      }
    }
  ]
}
```

**Then:** update `src/gateway/TBE.Gateway/Program.cs` — change `ValidateAudience: false` → `true` for the `B2B` JWT scheme once the mapper is live and emits `tbe-api`.

### Pattern 2: YARP B2B Audience Validation

**What:** Gateway already has `/api/b2b/*` routes under `B2BPolicy` (verified in `src/gateway/TBE.Gateway/appsettings.json`). Program.cs has `B2B` JWT scheme but `ValidateAudience=false` (Phase 7 TODO comment).

**Phase 5 change:** After Pattern 1 lands, flip to `ValidateAudience=true` + add role-aware policies:

```csharp
// src/gateway/TBE.Gateway/Program.cs
options.AddPolicy("B2BPolicy", policy =>
{
    policy.AddAuthenticationSchemes("B2B");
    policy.RequireAuthenticatedUser();
    policy.RequireRole("agent", "agent-admin", "agent-readonly");
});

options.AddPolicy("B2BAdminPolicy", policy =>
{
    policy.AddAuthenticationSchemes("B2B");
    policy.RequireAuthenticatedUser();
    policy.RequireRole("agent-admin");
});
```

Mount `B2BAdminPolicy` on `/api/b2b/agents/*` (sub-agent CRUD) and `/api/b2b/wallet/top-up`.

### Pattern 3: Dual NET/GROSS Pricing (B2B-02, B2B-07)

**What:**
- New `pricing.AgencyMarkupRules { AgencyId PK, FlatAmount decimal(18,4), PercentOfNet decimal(9,6), RouteClass varchar(16) NULL, IsActive bit }` — ONE row per agency (plus optional per-route-class override rows).
- Pricing service exposes `/api/pricing/agency-quote` that takes `{offerId, agencyId}` and returns `{net, markup, gross, commission}`.
- `BookingSagaState` gets `Channel (enum B2C/B2B), AgencyId (Guid?), AgencyNetFare/AgencyMarkup/AgencyGrossFare/AgencyCommission (decimal(18,4) NOT NULL DEFAULT 0)` — migration naming pattern mirrors `20260500000000_AddReceiptFareBreakdown`.

**Critical — Pitfall 23 mitigation:** Markup applied **exactly once** on the server when booking is initiated. Client never reads markup rules. Client renders what server returned; does not recompute.

```csharp
// PricingService.Application/AgencyMarkupRules/ApplyMarkup.cs
public static AgencyQuote ApplyMarkup(decimal netFare, AgencyMarkupRule rule)
{
    var markup = Math.Round(netFare * rule.PercentOfNet / 100m, 2) + rule.FlatAmount;
    var gross  = Math.Round(netFare + markup, 2);
    var commission = Math.Round(markup, 2); // commission == markup at v1
    return new AgencyQuote(netFare, markup, gross, commission);
}
```

### Pattern 4: Wallet Integration into Agent Checkout (B2B-04)

**What:** Branch the **existing** `BookingSaga`:
- On `PnrCreated`: if `saga.Channel == B2B` → publish `WalletReserveCommand(walletId, bookingId, amount=saga.AgencyNetFare, currency)`; else continue with `AuthorizePaymentCommand` (existing path).
- On `TicketIssued`: if B2B → `WalletCommitCommand(walletId, bookingId, reservationTxId)`; else `CapturePaymentCommand` (existing).
- **Compensation** (any failure between PnrCreated and TicketIssued): if B2B → `WalletReleaseCommand`; else `CancelAuthorizationCommand`.

**Critical — Pitfall 20 mitigation:** Wallet repo already uses `UPDLOCK + ROWLOCK + HOLDLOCK` on the balance read ([VERIFIED: `src/services/PaymentService/PaymentService.Infrastructure/Wallet/WalletRepository.cs`]). Do NOT change this. Append-only ledger means reservations and commits are separate rows; balance is always a SUM over `SignedAmount` — no mutation race possible.

**Idempotency keys** (already implemented in wallet repo):
- Reserve: `booking-{bookingId}-reserve`
- Commit: `booking-{bookingId}-commit-{reservationTxId}`
- Release: `booking-{bookingId}-release-{reservationTxId}`

### Pattern 5: Booking-on-Behalf Flow (B2B-03)

**What:** Reuse Plan 04-04 `checkout-ref` contract (`?ref=flight-{id}` etc.), but in the B2B portal the checkout page captures the walk-in customer (name, email, phone) and POSTs to `/api/b2b/bookings` with:

```json
{ "offerId": "...", "customer": { "name": "...", "email": "...", "phone": "..." } }
```

The controller (new `AgentBookingsController`) stamps `Channel = B2B`, `AgencyId = <from claim>`, `CustomerName/Email/Phone` onto the saga state and starts it. Customer receives confirmation email at the captured address (reuses existing `BookingConfirmedConsumer` — no change).

### Pattern 6: Sub-Agent Creation (B2B-10)

**What:** Mirror the Plan 04-01 `lib/keycloak-admin.ts` recipe exactly:

```ts
// src/portals/b2b-web/lib/keycloak-b2b-admin.ts
import "server-only"; // blocks browser bundling

let cached: { token: string; expiresAt: number } | null = null;

export async function getAdminToken() {
  if (cached && Date.now() < cached.expiresAt) return cached.token;
  const res = await fetch(`${KC_BASE}/realms/tbe-b2b/protocol/openid-connect/token`, {
    method: "POST",
    headers: { "content-type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      grant_type: "client_credentials",
      client_id: process.env.KEYCLOAK_B2B_ADMIN_CLIENT_ID!,
      client_secret: process.env.KEYCLOAK_B2B_ADMIN_CLIENT_SECRET!,
    }),
  });
  const j = await res.json();
  cached = { token: j.access_token, expiresAt: Date.now() + (j.expires_in - 30) * 1000 };
  return cached.token;
}

export async function createSubAgent(agencyId: string, email: string, firstName: string, lastName: string) {
  const token = await getAdminToken();
  return fetch(`${KC_BASE}/admin/realms/tbe-b2b/users`, {
    method: "POST",
    headers: { authorization: `Bearer ${token}`, "content-type": "application/json" },
    body: JSON.stringify({
      username: email, email, firstName, lastName, enabled: true,
      attributes: { agency_id: [agencyId] }, // critical — stamps scope
      realmRoles: ["agent"],
    }),
  });
}
```

Route handler at `/app/api/agents/route.ts` — Node-runtime only; requires `agent-admin` session; reads `session.user.agency_id` (never from request body).

### Pattern 7: Ticketing-Deadline Alerts (B2B-06)

**What:** TTL monitor + consumer already live. Phase 5 adds:
1. Agent dashboard widget: `GET /api/b2b/bookings/me?urgent=true` returns bookings where `TicketingDeadlineUtc < now + 24h AND Status IN (PnrCreated, PaymentAuthorized)`.
2. SendGrid template review for agent audience (consumer already dispatches; template copy may need adjustment — a MINOR change).

### Pattern 8: Server-Side Paginated Booking List (B2B-09)

**What:** New `AgentBookingsController` parallels `/customers/me/bookings`:

```csharp
[Authorize(Policy = "B2B")]
[HttpGet("/api/b2b/bookings/me")]
public async Task<IActionResult> List(int page = 1, int pageSize = 20, string? status = null)
{
    var agencyId = User.FindFirst("agency_id")?.Value
        ?? throw new UnauthorizedAccessException("missing agency_id claim");
    var items = await _repo.ListForAgencyAsync(Guid.Parse(agencyId), page, pageSize, status);
    return Ok(items);
}
```

**Critical — Pitfall 26 mitigation:** Query MUST `WHERE AgencyId = @agencyId`. No agency_id claim → 401. No fallback to user id.

### Pattern 9: Invoice PDF + E-Ticket (B2B-08)

**What:** New `AgencyInvoiceDocument : IDocument` (QuestPDF) renders GROSS-only pricing (the agent rebills the end customer). Reuse existing `BookingReceiptDocument` for the e-ticket. Both served via stream-through proxy:

```ts
// app/api/bookings/[id]/invoice.pdf/route.ts
export async function GET(_req: Request, ctx: { params: Promise<{ id: string }> }) {
  const { id } = await ctx.params;                                  // Pitfall 14
  const upstream = await gatewayFetch(`/api/b2b/bookings/${id}/invoice.pdf`, {
    headers: { accept: "application/pdf" },
  });
  return new Response(upstream.body, {                              // Pitfall 11
    status: upstream.status,
    headers: upstream.headers,
  });
}
```

**Verification (Plan 04-01 precedent):** tests extract text with PdfPig (QuestPDF FlateDecode-compresses — naive substring search fails silently).

### Anti-Patterns to Avoid

- **Role-gating B2C portal for agents:** Fragile multi-realm Auth.js, CSP collisions, cookie aliasing risk. Fork.
- **Computing markup client-side:** Double-application risk; leaks commission logic. Server-only.
- **EF Core for wallet deduction:** Does not emit `UPDLOCK+ROWLOCK+HOLDLOCK`. Use the existing Dapper repo.
- **Buffering PDFs before return:** `arrayBuffer()` triples latency — always stream (`new Response(upstream.body, ...)`).
- **Returning NET from B2C routes:** NET must never cross the `/api/flights` boundary. Only `/api/b2b/*`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Wallet atomicity | Custom lock service / Redis mutex | Existing `WalletRepository` (Dapper + `UPDLOCK+ROWLOCK+HOLDLOCK`) [VERIFIED] | Already solved; serializable read + append-only ledger = no double-spend window. |
| Stripe wallet top-up | DIY Stripe client | Existing `CreateWalletTopUpAsync` + `StripeTopUpConsumer` [VERIFIED] | Webhook replay dedup via `StripeWebhookEvents` already in place. |
| Keycloak sub-agent CRUD UI | Custom user store | Keycloak Admin API + service account client (mirror `lib/keycloak-admin.ts`) [CITED: `src/portals/b2c-web/lib/keycloak-admin.ts`] | Already proven in Plan 04-01. |
| Email idempotency | Custom dedupe | `EmailIdempotencyLog` unique index on `(EventId, EmailType)` [VERIFIED] | Phase 4 pattern. |
| PDF generation | Headless chromium / wkhtmltopdf | QuestPDF (MIT / community < $1M rev) | Phase 4 precedent; in-process, no subprocess. |
| TTL scheduling | BackgroundService polling loop | Existing `TtlMonitorHostedService` + `Warn24HSent`/`Warn2HSent` flags [VERIFIED] | Already shipped in Phase 3. |
| Session refresh | DIY refresh token dance | Auth.js v5 edge-split (`auth.config.ts` + `lib/auth.ts`) [VERIFIED in b2c-web] | D-01/D-02 pattern. |
| Booking saga | Custom orchestrator | Existing MassTransit `BookingSaga` — add B2B branch | Already handles compensations, retries, idempotency. |

**Key insight:** Phase 5 is ~80% integration of Phase 3 + Phase 4 work, ~20% new code (realm patch, portal fork, saga branch, markup table, invoice PDF).

---

## Runtime State Inventory

This phase is **not** a rename/refactor — mostly greenfield + schema additions. Runtime-state concerns:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | `payment.WalletTransactions` (exists) — no renames; Phase 5 only adds *new* transaction rows via the saga [VERIFIED]. `BookingSagaState` — Phase 5 migration ADDS columns (no backfill needed; `DEFAULT 0` + `Channel DEFAULT 'B2C'`). | Code edit only (new migration). |
| Live service config | **Keycloak `tbe-b2b` realm live in dev/staging Keycloak — the delta patch MUST be applied to running Keycloak** (or re-import). Service client `tbe-b2b-admin` does not exist yet → populate `KEYCLOAK_B2B_ADMIN_CLIENT_ID` + `KEYCLOAK_B2B_ADMIN_CLIENT_SECRET` envs. | Manual: import patch + provision service client + set envs. Document as open human action. |
| OS-registered state | None. | — |
| Secrets / env vars | NEW env vars required: `KEYCLOAK_B2B_ADMIN_CLIENT_ID`, `KEYCLOAK_B2B_ADMIN_CLIENT_SECRET`, `KEYCLOAK_B2B_REALM_URL`, and b2b-portal `NEXTAUTH_URL=http://localhost:3001`, `NEXTAUTH_SECRET`, `KEYCLOAK_CLIENT_ID=tbe-agent-portal`. No existing key renames. | Code edit (env wiring) + secret population. |
| Build artifacts | None — new portal directory; `dotnet restore` picks up new migrations automatically. | — |

**The canonical question** (after every file in repo is updated, what runtime systems still carry old state?): for Phase 5 specifically — **Keycloak realm config** is the risk. A fresh realm import erases the patch; team must merge into `infra/keycloak/realms/tbe-b2b-realm.json` (source of truth) AND apply to live Keycloak.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| PostgreSQL | — | N/A | — | Project uses MSSQL. |
| MSSQL | `payment.*`, `BookingSagaState`, new `pricing.AgencyMarkupRules` | ✓ (Phase 1) | 2022 | — |
| RabbitMQ | MassTransit + saga | ✓ (Phase 1) | 3.x | — |
| Redis | Selection cache, IATA index | ✓ (Phases 1-2) | 7.x | — |
| Keycloak | Auth (tbe-b2b realm) | ✓ (Phase 1; realm exists, delta patch pending) | 25.x | — |
| YARP gateway | Routing | ✓ (Phase 1; `/api/b2b/*` routes already defined) | 2.3.0 | — |
| SendGrid API key | Low-balance + TTL alerts | Unknown; set in env | — | Blocked if missing. |
| Stripe test keys | Wallet top-up only | Pending (per STATE.md) | — | Top-up flow cannot run without. |
| Node.js 20+ | b2b portal build | ✓ (b2c-web uses same) | ≥20 | — |
| pnpm | b2b portal install | ✓ | ≥9 | — |

**Missing with no fallback:** Stripe test keys (already known open human action from Phase 4).
**Missing with fallback:** `tbe-b2b-admin` service client — without it, B2B-10 is blocked; all other flows work.

---

## Common Pitfalls

(Continuing Phase 4 numbering; Phase 4 shipped Pitfalls 1–18.)

### Pitfall 19: B2C session cookie leaking into B2B portal (or vice versa)
**What goes wrong:** Deploy both portals on `*.company.com`, both use `NEXTAUTH_SECRET` + Auth.js default cookie name — browsers attach the b2c cookie to b2b domain, token audience is wrong, gateway rejects, user sees inscrutable 401.
**Why it happens:** Auth.js default cookie name is realm-agnostic.
**How to avoid:** Dev: run on different ports (3000 + 3001 — already set in realm). Prod: each portal sets `cookies.sessionToken.name` explicitly (`__Secure-tbe-b2c` vs `__Secure-tbe-b2b`) and scopes `domain` to its host only.
**Warning signs:** 401 at gateway with valid token; audience claim in JWT logs shows unexpected realm.

### Pitfall 20: Wallet double-spend under concurrency
**What goes wrong:** Two simultaneous bookings each see the same pre-reservation balance, both reserve the same available funds → negative ledger.
**Why it happens:** Without `UPDLOCK+ROWLOCK+HOLDLOCK`, `READ COMMITTED` allows dirty non-repeatable reads.
**How to avoid:** Existing `WalletRepository.ReserveAsync` already uses `SELECT ISNULL(SUM(SignedAmount),0) FROM payment.WalletTransactions WITH (UPDLOCK, ROWLOCK, HOLDLOCK) WHERE WalletId = @WalletId` [VERIFIED]. **DO NOT** replace with EF Core or remove the hints.
**Warning signs:** Load-test concurrent reservations pass unit tests individually but fail integration test `Reserve_Concurrent_SameWallet_RejectsSecond`.

### Pitfall 21: Balance projection drift
**What goes wrong:** Wallet dashboard shows a cached `Balance` column that drifts from the ledger sum.
**Why it happens:** Denormalized balance separate from `WalletTransactions`.
**How to avoid:** **Never store a separate balance column.** Balance is always `SUM(SignedAmount) WHERE WalletId = @id`. Repo already does this [VERIFIED]. `PERSISTED` computed column `SignedAmount` makes it fast.
**Warning signs:** Any PR that adds a `Balance` column to a wallet-related table.

### Pitfall 22: YARP forwarding B2C token to B2B route (audience confusion)
**What goes wrong:** `ValidateAudience=false` means a valid-signature JWT from `tbe-b2c` realm would be accepted at `/api/b2b/*` if signed by a shared authority. Today the authorities differ so it fails by signature — but once audience is validated the intent becomes explicit.
**Why it happens:** Defensive depth missing.
**How to avoid:** After Pattern 1 audience mapper lands, flip `ValidateAudience=true` in the `B2B` JWT scheme in `src/gateway/TBE.Gateway/Program.cs` and set `ValidAudience = "tbe-api"`. Add `verify-audience-smoke-b2b.sh` to CI (mirror `verify-audience-smoke.sh`).
**Warning signs:** Any 200 response at `/api/b2b/*` from a token whose `aud` doesn't include `tbe-api`.

### Pitfall 23: Markup applied twice (server + client)
**What goes wrong:** Pricing service returns GROSS; client ALSO reads an `AgencyMarkup` config and adds it to display — user sees 2× markup.
**How to avoid:** Pricing service returns a sealed `AgencyQuote { net, markup, gross, commission }`. Client renders `quote.gross` verbatim. No client-side arithmetic. Markup rules table access is gated — only `PricingService` reads it.
**Warning signs:** Any `import` of markup config in `src/portals/b2b-web/`.

### Pitfall 24: Stripe top-up webhook replay credits wallet twice
**What goes wrong:** Stripe retries webhooks; without dedup the same `payment_intent.succeeded` credits twice.
**How to avoid:** Already mitigated. `StripeWebhookEvents` table has unique index on `EventId`; `StripeTopUpConsumer` idempotency key is `wallet-{walletId}-topup-{paymentIntentId}` on the `TopUpAsync` call [VERIFIED].
**Warning signs:** Duplicate `TopUp` rows for the same `payment_intent.succeeded`.

### Pitfall 25: TTL alert email storm on service restart
**What goes wrong:** `TtlMonitorHostedService` restart re-examines every open booking and sends 24h/2h alerts again.
**How to avoid:** Already mitigated. `Warn24HSent` and `Warn2HSent` boolean columns on `BookingSagaState` gate the publish [VERIFIED]. `TicketingDeadlineApproachingConsumer` uses `EmailIdempotencyLog` (EventId, EmailType) unique index as defense-in-depth.
**Warning signs:** Duplicate emails at deploy time.

### Pitfall 26: Agents seeing other agencies' bookings
**What goes wrong:** `/api/b2b/bookings/me` query filters by user id only; agent-admin at Agency A sees Agency B bookings.
**How to avoid:** `agency_id` claim is mandatory; missing → 401. Query MUST contain `WHERE AgencyId = @agencyId`. Integration test that spins up two agencies with one booking each and asserts they cannot see each other's.
**Warning signs:** Any booking list query in `AgentBookingsController` that doesn't WHERE on AgencyId.

### Pitfall 27: Starter-kit CSS/JS bundled into b2b-web conflicts with b2c-web port share
**What goes wrong:** Both Next.js apps listen on same port in dev, or share `NEXTAUTH_URL`, leading to callback loop.
**How to avoid:** Port 3001 for b2b (already in realm config). Separate `.env.local` per portal. Document in `src/portals/b2b-web/README.md`.
**Warning signs:** Auth callback infinite redirect; 400 from Keycloak `invalid_redirect_uri`.

### Pitfall 28: Sub-agent created without `agency_id` attribute
**What goes wrong:** Admin creates user; route handler forgets the `attributes: { agency_id: [...] }` payload; user logs in, gets no `agency_id` claim, every B2B API returns 401.
**How to avoid:** Route handler reads `agency_id` from the **caller's session**, not from the request body; always writes it; integration test verifies.
**Warning signs:** `createSubAgent` route that accepts `agency_id` as input.

---

## Code Examples

### Example 1: BookingSaga B2B Branch

```csharp
// src/services/BookingService/BookingService.Application/Saga/BookingSaga.cs
During(PnrCreating,
    When(PnrCreated)
        .Then(ctx =>
        {
            ctx.Saga.PnrLocator = ctx.Message.PnrLocator;
            ctx.Saga.TicketingDeadlineUtc = ctx.Message.TicketingDeadlineUtc;
        })
        .IfElse(ctx => ctx.Saga.Channel == Channel.B2B,
            b2b => b2b
                .PublishAsync(ctx => ctx.Init<WalletReserveCommand>(new
                {
                    WalletId = ctx.Saga.WalletId!.Value,
                    BookingId = ctx.Saga.CorrelationId,
                    Amount = ctx.Saga.AgencyNetFare,
                    Currency = ctx.Saga.Currency
                }))
                .TransitionTo(WalletReserving),
            b2c => b2c
                .PublishAsync(ctx => ctx.Init<AuthorizePaymentCommand>(new { /* existing */ }))
                .TransitionTo(Authorizing)));

During(WalletReserving,
    When(WalletReserved)
        .Then(ctx => ctx.Saga.WalletReservationTxId = ctx.Message.ReservationTxId)
        .PublishAsync(ctx => ctx.Init<IssueTicketCommand>(new { /* existing */ }))
        .TransitionTo(TicketIssuing),
    When(WalletReservationFailed)
        .TransitionTo(Failed));

During(TicketIssuing,
    When(TicketIssued)
        .IfElse(ctx => ctx.Saga.Channel == Channel.B2B,
            b2b => b2b.PublishAsync(ctx => ctx.Init<WalletCommitCommand>(new
            {
                WalletId = ctx.Saga.WalletId!.Value,
                BookingId = ctx.Saga.CorrelationId,
                ReservationTxId = ctx.Saga.WalletReservationTxId!.Value
            })).TransitionTo(Confirmed),
            b2c => b2c.PublishAsync(ctx => ctx.Init<CapturePaymentCommand>(new { /* existing */ }))
                      .TransitionTo(Capturing)));
```

### Example 2: Migration Skeleton

```csharp
// src/services/BookingService/BookingService.Infrastructure/Migrations/20260600000000_AddAgencyPricingAndChannel.cs
// Hand-authored (03-01 ModelSnapshot convention — do NOT scaffold).
public partial class AddAgencyPricingAndChannel : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        mb.AddColumn<int>("Channel", "BookingSagaState", nullable: false, defaultValue: 0); // 0=B2C, 1=B2B
        mb.AddColumn<Guid>("AgencyId", "BookingSagaState", nullable: true);
        mb.AddColumn<decimal>("AgencyNetFare", "BookingSagaState", type: "decimal(18,4)", nullable: false, defaultValue: 0m);
        mb.AddColumn<decimal>("AgencyMarkup", "BookingSagaState", type: "decimal(18,4)", nullable: false, defaultValue: 0m);
        mb.AddColumn<decimal>("AgencyGrossFare", "BookingSagaState", type: "decimal(18,4)", nullable: false, defaultValue: 0m);
        mb.AddColumn<decimal>("AgencyCommission", "BookingSagaState", type: "decimal(18,4)", nullable: false, defaultValue: 0m);
        mb.AddColumn<string>("CustomerName", "BookingSagaState", type: "nvarchar(200)", nullable: true);
        mb.AddColumn<string>("CustomerEmail", "BookingSagaState", type: "nvarchar(256)", nullable: true);
        mb.AddColumn<string>("CustomerPhone", "BookingSagaState", type: "nvarchar(32)", nullable: true);
        mb.CreateIndex("IX_BookingSagaState_AgencyId_StartedAtUtc", "BookingSagaState", new[] { "AgencyId", "StartedAtUtc" });
    }
}
```

### Example 3: Agent Dashboard Query (TanStack Query)

```ts
// src/portals/b2b-web/app/(portal)/dashboard/recent-bookings.tsx
"use client";
import { useQuery } from "@tanstack/react-query";
import { gatewayFetch } from "@/lib/api-client";

export function RecentBookings() {
  const { data } = useQuery({
    queryKey: ["agent-bookings", "recent"],
    queryFn: () => gatewayFetch("/api/b2b/bookings/me?page=1&pageSize=10").then(r => r.json()),
    staleTime: 60_000,
  });
  // render data.items…
}

// Wallet balance — global polling
export function WalletBalanceBadge({ walletId }: { walletId: string }) {
  const { data } = useQuery({
    queryKey: ["wallet-balance", walletId],
    queryFn: () => gatewayFetch(`/api/wallets/${walletId}/balance`).then(r => r.json()),
    refetchInterval: 30_000,
    staleTime: 25_000,
  });
  return <span>{data?.balance?.toFixed(2)} {data?.currency}</span>;
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Stripe Checkout for agent billing | Atomic credit wallet (pre-funded) | Phase 3 (D-14 + D-15) | No per-transaction Stripe fees for agent bookings. |
| EF Core with `IsolationLevel.Serializable` for wallet | Dapper + `UPDLOCK+ROWLOCK+HOLDLOCK` | Phase 3 (D-15) | Precise lock hints; avoid EF emitting wrong SQL. |
| Separate balance column on Wallet | Append-only ledger with computed `SignedAmount PERSISTED` | Phase 3 (D-14) | No drift possible; full audit trail. |
| Customer checkout page shared with agent | Separate `/checkout/confirm` in b2b-web (wallet debit, no PI) | Phase 5 (this phase) | Cleaner CSP; agents never see PaymentElement except top-up. |
| Markup rules engine | Flat `AgencyMarkupRules` table | Phase 5 (this phase) | Ships faster; good enough for v1. |

**Deprecated / outdated:**
- N/A for this phase (greenfield additions only).

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit (backend, existing) · Vitest + React Testing Library (b2b-web) · Playwright 1.48 (e2e) |
| Config file | Backend: existing `*.Tests.csproj` under `tests/`. b2b-web: `vitest.config.ts` + `playwright.config.ts` (copied from b2c-web in Wave 0). |
| Quick run command | Backend: `dotnet test --filter Category!=RedPlaceholder`. b2b-web: `pnpm --filter @tbe/b2b-web vitest run` |
| Full suite command | `dotnet test && pnpm --filter @tbe/b2b-web test && pnpm --filter @tbe/b2b-web exec playwright test` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| B2B-01 | Login to tbe-b2b realm, token carries `tbe-api` audience + `agency_id` claim | integration (shell smoke) | `bash infra/keycloak/verify-audience-smoke-b2b.sh` | ❌ Wave 0 |
| B2B-02 | Agent search returns both net + gross pricing | integration (xUnit) | `dotnet test --filter FullyQualifiedName~Pricing.AgencyQuoteTests` | ❌ Wave 0 |
| B2B-03 | Booking on behalf persists customer contact on saga | integration (xUnit) | `dotnet test --filter FullyQualifiedName~BookingSaga.B2BChannelTests` | ❌ Wave 0 |
| B2B-04 | Concurrent reservations do not double-spend | integration (xUnit) | `dotnet test --filter FullyQualifiedName~WalletRepo.ConcurrentReserveTest` | ❌ Wave 0 (will reuse existing wallet test fixture) |
| B2B-04 | B2B saga path publishes WalletReserveCommand instead of AuthorizePaymentCommand | unit (xUnit) | `dotnet test --filter FullyQualifiedName~BookingSaga.B2BBranchTests` | ❌ Wave 0 |
| B2B-05 | Low-balance event fires when post-reserve < threshold | unit (xUnit) | `dotnet test --filter FullyQualifiedName~WalletReserveConsumerTests` | ✓ exists (Phase 3) |
| B2B-06 | TTL monitor writes Warn24HSent + publishes event once | unit (xUnit) | `dotnet test --filter FullyQualifiedName~TtlMonitorTests` | ✓ exists (Phase 3) |
| B2B-07 | Markup rule applied server-side only | unit (xUnit) | `dotnet test --filter FullyQualifiedName~ApplyMarkupTests` | ❌ Wave 0 |
| B2B-08 | Invoice PDF returns valid QuestPDF bytes with GROSS values | integration | `dotnet test --filter FullyQualifiedName~AgencyInvoiceDocumentTests` | ❌ Wave 0 |
| B2B-08 | Invoice stream-through proxy does not buffer | e2e (Playwright) | `pnpm --filter @tbe/b2b-web exec playwright test tests/e2e/invoice-download.spec.ts` | ❌ Wave 0 |
| B2B-09 | `/api/b2b/bookings/me` rejects without `agency_id` claim | integration (xUnit) | `dotnet test --filter FullyQualifiedName~AgentBookingsControllerTests.AgencyScopeEnforced` | ❌ Wave 0 |
| B2B-09 | Two agencies never see each other's bookings | integration (xUnit) | `dotnet test --filter FullyQualifiedName~AgentBookingsControllerTests.CrossTenantBlocked` | ❌ Wave 0 |
| B2B-10 | Create sub-agent stamps `agency_id` attribute | integration (Vitest + MSW) | `pnpm --filter @tbe/b2b-web vitest run tests/unit/keycloak-b2b-admin.test.ts` | ❌ Wave 0 |
| B2B-10 | Admin route rejects when session is not agent-admin | e2e (Playwright) | `pnpm --filter @tbe/b2b-web exec playwright test tests/e2e/sub-agent-create.spec.ts` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test --filter Category!=RedPlaceholder` (backend) or `pnpm --filter @tbe/b2b-web vitest run` (portal)
- **Per wave merge:** full xUnit + full Vitest + smoke Playwright (`@smoke` tag)
- **Phase gate:** full suite green before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] `src/portals/b2b-web/` (entire app) — fork starterKit (follow Plan 04-00 recipe verbatim)
- [ ] `src/portals/b2b-web/vitest.config.ts` + `tests/unit/*.test.ts` placeholders
- [ ] `src/portals/b2b-web/playwright.config.ts` + `tests/e2e/*.spec.ts` placeholders
- [ ] `infra/keycloak/realm-tbe-b2b.json` (delta patch)
- [ ] `infra/keycloak/verify-audience-smoke-b2b.sh`
- [ ] Red placeholders (xUnit `Category=RedPlaceholder`) for all new B2B tests:
  - `tests/*/Pricing.AgencyQuoteTests.cs`
  - `tests/*/BookingSaga.B2BChannelTests.cs`
  - `tests/*/BookingSaga.B2BBranchTests.cs`
  - `tests/*/AgentBookingsControllerTests.cs`
  - `tests/*/AgencyInvoiceDocumentTests.cs`
  - `tests/*/ApplyMarkupTests.cs`
- [ ] `src/shared/TBE.Contracts/Enums/Channel.cs`

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes | Keycloak OIDC; Auth.js v5 edge-split; service account for admin flows; NEVER log tokens. |
| V3 Session Management | yes | Auth.js session cookies (`__Secure-*` in prod); httpOnly; sameSite=lax; 30-min idle. |
| V4 Access Control | yes (critical) | YARP `B2BPolicy` role check + **mandatory `agency_id` claim scoping on every B2B repo query** (Pitfall 26). |
| V5 Input Validation | yes | zod at portal edge; FluentValidation server-side; markup bounds (percent ∈ [0..100]). |
| V6 Cryptography | yes | TLS via Caddy (Phase 7). No hand-rolled crypto; service-account secrets in env only. |
| V7 Error Handling / Logging | yes | Serilog; never log wallet amounts with balance side-effect; correlate via `CorrelationId`. |
| V8 Data Protection | yes | Walk-in customer PII (name, email, phone) stored on saga — retention aligns with booking retention. |
| V9 Communications | yes | HTTPS; CSP on portals; Stripe.js whitelisted only under `/admin/wallet/*` in b2b-web. |
| V13 API / Web Service | yes | JWT audience validation (Pitfall 22) — mandatory after Pattern 1 lands. |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Wallet double-spend under concurrency | Tampering | `UPDLOCK+ROWLOCK+HOLDLOCK` (Pitfall 20) — already in place. |
| Cross-tenant data leak (Agency A reads Agency B) | Information Disclosure | Mandatory `WHERE AgencyId = @claim` (Pitfall 26). |
| Token reuse across B2C/B2B audiences | Spoofing | Audience mapper + `ValidateAudience=true` (Pitfall 22). |
| Sub-agent created without agency scope | Elevation of Privilege | Server-injected `agency_id` from session, never from request body (Pitfall 28). |
| Webhook replay credits wallet twice | Tampering | `StripeWebhookEvents` dedup + idempotency key (Pitfall 24) — already in place. |
| Markup rule read/write by wrong service | Tampering | Rules table owned by `PricingService` only; no cross-service access (Pitfall 23). |
| TTL alert spam on restart | Repudiation/DoS | `Warn24HSent`/`Warn2HSent` flags + EmailIdempotencyLog (Pitfall 25) — already in place. |

---

## Research-Derived Decisions (proposed — for `/gsd-discuss-phase`)

Continuing numbering from Phase 4 (last committed D-19; Phase 4 plans referenced D-21 — pick up at D-22).

| # | Decision | Status |
|---|----------|--------|
| **D-22** | **Agent portal ships as a separate Next.js app at `src/portals/b2b-web/`** — NOT role-gating inside `b2c-web`. Same reasons as D-01/D-02: edge-split, CSP, cookie isolation. Runs on port 3001 per existing realm config. | [ASSUMED — user confirm] |
| **D-23** | **Keycloak `tbe-b2b` realm patch adds an `oidc-audience-mapper` emitting `tbe-api` on the access token** of `tbe-agent-portal` client **+ an `agency_id` user-attribute mapper**. Gateway `B2B` scheme flips `ValidateAudience=false → true` once the mapper lands. | [ASSUMED — user confirm] |
| **D-24** | **`Channel` (enum B2C/B2B) stamped on `BookingSagaState` via migration `20260600000000_AddAgencyPricingAndChannel`**. Default value 0 (B2C) — existing bookings retain B2C semantics. | [ASSUMED — user confirm] |
| **D-25** | **`pricing.AgencyMarkupRules` table is intentionally simple**: `AgencyId PK, FlatAmount, PercentOfNet, RouteClass NULL, IsActive`. No rules engine. Route-class override via one extra row per agency. v1 ships with single global rule per agency. | [ASSUMED — user confirm] |
| **D-26** | **Invoice PDF renders GROSS-only** — the agent re-bills their end customer; NET is never rendered. E-ticket reuses the existing `BookingReceiptDocument` unchanged. | [ASSUMED — user confirm] |
| **D-27** | **Wallet balance badge in agent header uses TanStack Query `refetchInterval: 30_000`, `staleTime: 25_000`** against `GET /api/wallets/{id}/balance`. No SSE, no SignalR. | [ASSUMED — user confirm] |
| **D-28** | **TTL dashboard uses polling** — `refetchInterval: 60_000` against `GET /api/b2b/bookings/me?urgent=true`. Matches proven 2000ms-pattern at checkout but at lower cadence. | [ASSUMED — user confirm] |
| **D-29** | **`tbe-b2b-admin` service-account client** (realm-management `manage-users` + `view-users`) mirrors Plan 04-01's `tbe-b2c-admin`. `lib/keycloak-b2b-admin.ts` uses `server-only` import, 30s-skew token cache, Node runtime, stamps `agency_id` from session (never from request body). | [ASSUMED — user confirm] |
| **D-30** | **"SSO with backoffice if same org" (B2B-01) interpreted as "agents can log into both portals from the same browser with their respective realm credentials"** — NOT a single unified realm. Backoffice remains on `tbe-backoffice` realm. No cross-realm federation at v1. | [ASSUMED — user confirm] |
| **D-31** | **Agent booking-on-behalf reuses Plan 04-04 `checkout-ref` contract** — `?ref=flight-{id}` / `?ref=hotel-{id}` — but the B2B portal's confirm page POSTs to `/api/b2b/bookings` with `customer: { name, email, phone }` body and NO PaymentElement. | [ASSUMED — user confirm] |

---

## Cross-Phase Reuse Checklist

Every file below is already shipped and reused verbatim (or near-verbatim) in Phase 5:

**Reuse from Phase 4 b2c-web (copy to b2b-web with realm/port tweaks):**
- `src/portals/b2c-web/auth.config.ts` → `b2b-web/auth.config.ts` (change realm URL + client id)
- `src/portals/b2c-web/lib/auth.ts` → `b2b-web/lib/auth.ts`
- `src/portals/b2c-web/lib/api-client.ts` → `b2b-web/lib/api-client.ts` (verbatim)
- `src/portals/b2c-web/lib/keycloak-admin.ts` → `b2b-web/lib/keycloak-b2b-admin.ts` (swap realm + env var names)
- `src/portals/b2c-web/lib/checkout-ref.ts` → `b2b-web/lib/checkout-ref.ts` (verbatim)
- `src/portals/b2c-web/middleware.ts` → `b2b-web/middleware.ts` (swap role claims)
- `src/portals/b2c-web/next.config.mjs` → `b2b-web/next.config.mjs` (restrict Stripe CSP to `/admin/wallet/*`)
- `src/portals/b2c-web/playwright.config.ts`, `vitest.config.ts`, `tsconfig.json`, `eslint.config.mjs` → verbatim
- `src/portals/b2c-web/types/ui.d.ts` → verbatim (starterKit ambient shim)

**Reuse from existing services (NO changes):**
- `src/services/PaymentService/PaymentService.Infrastructure/Wallet/WalletRepository.cs`
- `src/services/PaymentService/PaymentService.Application/Wallet/IWalletRepository.cs`
- `src/services/PaymentService/PaymentService.Application/Consumers/WalletReserveConsumer.cs`
- `src/services/PaymentService/PaymentService.Application/Consumers/StripeTopUpConsumer.cs`
- `src/services/PaymentService/PaymentService.Application/Stripe/IStripePaymentGateway.cs` (uses `CreateWalletTopUpAsync`)
- `src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs`
- `src/services/NotificationService/NotificationService.Application/Consumers/WalletLowBalanceConsumer.cs`
- `src/services/NotificationService/NotificationService.Application/Consumers/TicketingDeadlineApproachingConsumer.cs`
- `src/services/BookingService/BookingService.Infrastructure/Ttl/TtlMonitorHostedService.cs`
- `src/shared/TBE.Contracts/Events/WalletEvents.cs`
- `src/shared/TBE.Contracts/Events/NotificationEvents.cs` (WalletLowBalance, TicketingDeadlineApproaching)
- `src/shared/TBE.Contracts/Commands/SagaCommands.cs` (WalletReserve/Commit/Release)
- `src/gateway/TBE.Gateway/appsettings.json` (B2B routes already present)
- `infra/keycloak/realms/tbe-b2b-realm.json` (base realm; delta patch applied separately)

**Reuse from Plan 04-01 (copy as pattern):**
- QuestPDF generator pattern (`BookingReceiptDocument`) → `AgencyInvoiceDocument`
- PdfPig verification pattern for tests
- Stream-through PDF proxy pattern (`new Response(upstream.body, ...)`)
- Service-account token cache pattern (`lib/keycloak-admin.ts`) — 30s skew, Node-only

**Reuse from Plan 04-00 (copy as recipe):**
- Fork starterKit → `src/portals/b2b-web/` (`tsconfig.json` with `allowJs: true`)
- Delta realm JSON pattern (`realm-tbe-b2c.json` → `realm-tbe-b2b.json`)
- `verify-audience-smoke.sh` → `verify-audience-smoke-b2b.sh`
- Red-placeholder xUnit `Trait("Category","RedPlaceholder")` staging

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Separate b2b-web fork is preferred over role-gating in b2c-web | D-22 | Re-scoping could merge portals; large refactor if wrong. |
| A2 | "SSO with backoffice" means shared browser-session experience, not unified realm | D-30 | If user wants federated realm, Phase 7 adds OIDC broker config (bigger scope). |
| A3 | `tbe-b2b` realm audience mapper must emit `tbe-api` (not `tbe-gateway`) | D-23 | Could break gateway validation if `ValidAudience` differs in Program.cs. |
| A4 | Wallet amount in `WalletTransactions.Amount` is decimal major units (not minor) | Pattern 4 | Phase 3 D-15 says decimal; need to verify vs Stripe's minor-unit convention at boundary. |
| A5 | Markup v1 = flat + percent per agency, optional route-class override row | D-25 | If user wants GDS-class + airline + date-range dimensions, schema grows considerably. |
| A6 | Invoice PDF shows GROSS only (no NET, no commission) | D-26 | If accounting requires NET breakdown, template + contract change. |
| A7 | Agent portal runs on port 3001 (already in realm redirect URIs) | Pattern 1 | If user wants different port, realm update + env changes. |
| A8 | `agency_id` is a single-value user attribute (one agency per user) | D-22, Pattern 6 | Multi-agency support would require re-modeling the claim. |
| A9 | Agent never sees PaymentElement except on `/admin/wallet/top-up` | Pattern 4 | CSP scoping assumption. |
| A10 | Walk-in customer captured on saga (CustomerName/Email/Phone) — no separate Customer entity creation | Pattern 5 | If customer must become a platform user, flow is bigger (Keycloak user creation). |
| A11 | Existing `WalletEvents.WalletReservationFailed` is published for insufficient-balance case and saga compensates accordingly | Pattern 4 | Need to verify vs consumer implementation. |
| A12 | TTL consumer email copy is generic enough for agent audience (minor template tweak only) | Pattern 7 | Copy rework could extend plan scope. |
| A13 | `KEYCLOAK_B2B_ADMIN_CLIENT_ID` + `KEYCLOAK_B2B_ADMIN_CLIENT_SECRET` env var names follow b2c naming | D-29 | Naming consistency only. |

---

## Open Questions

1. **"SSO with backoffice" semantic** (B2B-01)
   - What we know: realm `tbe-backoffice` exists separately; `tbe-b2b` is a distinct realm.
   - What's unclear: does the PM want OIDC brokering (backoffice user SSO into b2b portal seamlessly) or just "same browser cookie stays alive" experience?
   - Recommendation: D-30 interprets this as shared browser-session experience; no brokering at v1. Confirm in `/gsd-discuss-phase`.

2. **Multi-agency users**
   - What we know: realm attribute `agency_id` modeled as single value.
   - What's unclear: can one user belong to multiple agencies (e.g., an OTA group)?
   - Recommendation: v1 = single value; defer multi-agency to a future phase.

3. **Commission settlement**
   - What we know: `AgencyCommission` field added; Invoice PDF shows GROSS only (D-26).
   - What's unclear: is commission paid out to the agency periodically? If so, where does the accounting trigger live?
   - Recommendation: out of scope for Phase 5; track as deferred idea for Phase 6 (Backoffice & CRM).

4. **Agent booking refund policy**
   - What we know: compensation path releases wallet reservation; capture released via `WalletReleaseCommand`.
   - What's unclear: if a B2B booking is refunded post-ticket (customer cancel), does the wallet get credited back or does the agency eat the cost?
   - Recommendation: v1 = manual refund via backoffice (Phase 6). Document explicitly; don't auto-credit on `BookingCancelled`.

5. **Top-up limits**
   - What we know: top-up via Stripe PaymentIntent, no min/max checks visible in controller.
   - What's unclear: should there be per-day / per-transaction caps for fraud mitigation?
   - Recommendation: add `MinTopUpCents` / `MaxTopUpCents` env config in Phase 5 or defer to Phase 7 hardening.

6. **"No B2C dependency" requirement** (ROADMAP)
   - What we know: Phase 5 must not regress B2C.
   - What's unclear: does a b2b-web fork satisfy this, or should even backend changes (saga migration, new controllers) be feature-flagged for safety?
   - Recommendation: migration is additive with safe defaults; saga branch is explicit (`if Channel == B2B`); no feature flag needed. Smoke-run Phase 4 e2e after Phase 5 lands to confirm.

---

## Sources

### Primary (HIGH confidence)

- [VERIFIED: `infra/keycloak/realms/tbe-b2b-realm.json`] — realm exists with `tbe-agent-portal` + `tbe-gateway` clients, `agent`/`agent-admin`/`agent-readonly` roles, port 3001 redirect.
- [VERIFIED: `src/gateway/TBE.Gateway/appsettings.json`] — `/api/b2b/*` routes already defined under `B2BPolicy`.
- [VERIFIED: `src/gateway/TBE.Gateway/Program.cs`] — `B2B` JWT scheme against `tbe-b2b` realm; `ValidateAudience=false` (Phase 7 TODO).
- [VERIFIED: `src/services/PaymentService/PaymentService.Infrastructure/Wallet/WalletRepository.cs`] — Dapper `UPDLOCK + ROWLOCK + HOLDLOCK` balance read; idempotency keys; unique-index recovery.
- [VERIFIED: `src/services/PaymentService/PaymentService.Application/Wallet/IWalletRepository.cs`] — full interface shipped.
- [VERIFIED: `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260417000000_AddWalletAndStripe.cs`] — `SignedAmount` PERSISTED computed column; StripeWebhookEvents dedup; outbox/inbox.
- [VERIFIED: `src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs`] — endpoints + `[Authorize(Roles="agency-admin")]`.
- [VERIFIED: `src/services/PaymentService/PaymentService.Application/Consumers/WalletReserveConsumer.cs`] — emits `WalletLowBalance` below threshold.
- [VERIFIED: `src/services/PaymentService/PaymentService.Application/Consumers/StripeTopUpConsumer.cs`] — sole writer of TopUp entries; webhook-scoped idempotency.
- [VERIFIED: `src/services/PaymentService/PaymentService.Application/Stripe/IStripePaymentGateway.cs`] — `CreateWalletTopUpAsync` shipped.
- [VERIFIED: `src/services/BookingService/BookingService.Application/Saga/BookingSaga.cs`] — saga state machine (current path: PnrCreated → AuthorizePayment; no B2B branch yet).
- [VERIFIED: `src/services/BookingService/BookingService.Application/Saga/BookingSagaState.cs`] — has `WalletId`, `WalletReservationTxId`, `Warn24HSent`, `Warn2HSent`; needs `Channel` + agency pricing fields.
- [VERIFIED: `src/services/BookingService/BookingService.Infrastructure/Ttl/TtlMonitorHostedService.cs`] — TTL monitor shipped.
- [VERIFIED: `.planning/phases/04-b2c-portal-customer-facing/04-CONTEXT.md`] — Phase 4 locked decisions (D-01..D-19).
- [VERIFIED: `.planning/phases/04-b2c-portal-customer-facing/04-00-SUMMARY.md` / `04-01-SUMMARY.md` / `04-02-SUMMARY.md` / `04-03-SUMMARY.md` / `04-04-SUMMARY.md`] — reusable recipes.
- [VERIFIED: `.planning/phases/03-core-flight-booking-saga-b2c/03-CONTEXT.md`] — D-14 append-only wallet, D-15 SQL hints, D-19 idempotency.
- [VERIFIED: `.planning/STATE.md`] — current milestone progress + open human actions.
- [VERIFIED: `.planning/REQUIREMENTS.md`] — B2B-01..B2B-10 + PAY-04 + NOTF-04/05.
- [VERIFIED: `.planning/ROADMAP.md`] — Phase 5 plan breakdown + UAT.

### Secondary (MEDIUM confidence)

- Auth.js v5 edge-split pattern — [CITED: Phase 4 b2c-web implementation + Auth.js v5 beta migration docs].
- QuestPDF community licensing — [CITED: questpdf.com licensing FAQ, Phase 4 research].
- MassTransit saga optimistic concurrency (`ISagaVersion`) — [CITED: Phase 3 research].

### Tertiary (LOW confidence)

- None explicitly LOW — all claims either directly observed in codebase or cited from prior phase documents.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all dependencies already pinned and shipped in Phases 1–4.
- Architecture: HIGH — BookingSaga is explicit code; wallet infra is complete and verified.
- Pitfalls: HIGH — Pitfalls 20/21/24/25 already mitigated in code; 19/22/23/26/27/28 are preventive with concrete hooks.
- Decisions: MEDIUM — D-22..D-31 are defensible defaults but must be confirmed in `/gsd-discuss-phase`.
- Ambiguous semantic ("SSO with backoffice", multi-agency users, refund flow): LOW — listed in Open Questions.

**Research date:** 2026-04-17
**Valid until:** 2026-05-17 (30 days — stable stack; revisit if Auth.js exits beta or Stripe.net majors bump)
