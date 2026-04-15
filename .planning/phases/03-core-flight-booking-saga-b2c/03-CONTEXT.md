# Phase 3: Core Flight Booking Saga (B2C) - Context

**Gathered:** 2026-04-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver an end-to-end B2C flight booking that completes in production: PNR created, payment authorized via Stripe, ticket issued by the GDS, payment captured, and a confirmation email with e-ticket delivered within 60 seconds — with full compensation logic if any step fails. Includes the TTL monitor, PCI/PII compliance hardening, and the Notification Service. B2B credit wallet is in scope (schema + atomic deduction) but the B2B portal is not.

New capabilities (cancellation flows beyond the auto-compensation path, refund UX, customer account screens) belong in Phase 4 (B2C Portal).

</domain>

<decisions>
## Implementation Decisions

### Saga Orchestration & Error Policy
- **D-01:** Saga state persisted in a dedicated `BookingService.Saga` schema using `MassTransit.EntityFrameworkCore`. `BookingSagaState` table. Concurrency via EF optimistic concurrency token. Isolated from domain tables (`Bookings`, `Passengers`).
- **D-02:** Retry policy per saga step: **3 attempts with exponential backoff (2s / 4s / 8s)** before triggering compensation. Applies to GDS calls, Stripe calls, and email dispatch.
- **D-03:** Compensation failure policy: when a compensation action itself fails, the saga writes to a `SagaDeadLetter` table, fires an ops alert (PagerDuty/email), and marks the booking `RequiresManualReconciliation`. Never infinite-retry compensation; never silently swallow.
- **D-04:** TTL hard-timeout behavior: at ticketing-deadline minus 2 minutes, saga auto-voids the PNR via the GDS, releases any Stripe authorization, publishes `BookingExpired`, and sends a cancellation email. No ops approval required for this path.
- **D-05:** Saga step ordering (locked, matches ROADMAP): `BookingInitiated` → `PriceReconfirmed` → `PNRCreated` → `PaymentAuthorized` → `TicketIssued` → `PaymentCaptured` → `BookingConfirmed`. Any step failure fires the compensation chain for all prior steps.

### Fare-Rule / TTL Parsing
- **D-06:** Ticketing deadline extraction: primary regex parser with per-GDS override adapters (Amadeus, Sabre, Galileo) for known formatting quirks. Common pattern baseline (e.g. `TICKET BY DDMMM HH:MM`) plus vendor-specific variants.
- **D-07:** Parse-failure fallback: **default to 2-hour TTL + ops alert** when deadline cannot be determined. Aggressive safety window prevents orphaned PNRs; ops can manually override.

### Encryption, PCI, and PII
- **D-08:** Passport/document encryption uses AES-256. Key source abstracted behind `IEncryptionKeyProvider` interface in `TBE.Common`. Dev implementation reads a 256-bit key from `.env`. Prod implementation (Azure Key Vault / AWS KMS) deferred to Phase 7 hardening — interface is ready now so swap is drop-in.
- **D-09:** OpenTelemetry PCI/PII scrubbing implemented once as a `SensitiveAttributeProcessor` in `TBE.Common`, registered in every service's DI. Filters attribute keys matching `card.*`, `cvv`, `pan`, `stripe.raw_*`, `passport.*`, `document.number` before export. Never per-service ad-hoc.
- **D-10:** Stripe Elements is the only surface that touches raw card data (SAQ-A boundary — locked in ROADMAP). Server stores only `PaymentIntentId` and `CustomerId`. No PAN, CVV, or expiry anywhere server-side.

### Payment Flow
- **D-11:** Stripe Payment Intents with **authorize-before-capture** (`capture_method: manual`). Capture happens only after `TicketIssued` succeeds. Locked in ROADMAP.
- **D-12:** Payment confirmation processed exclusively via **Stripe webhook** with signature verification. Client-side redirect is NOT trusted as confirmation. Locked in ROADMAP.
- **D-13:** All Stripe API calls use idempotency keys scoped per `(BookingId, OperationType)` (e.g., `booking-{id}-authorize`, `booking-{id}-capture`, `booking-{id}-refund`). Guarantees replay safety on retries.

### B2B Credit Wallet
- **D-14:** Wallet schema uses append-only `WalletTransactions` log (entry types: `Reserve`, `Commit`, `Release`, `TopUp`). No mutable `balance` column — balance is derived from the log.
- **D-15:** Balance reads during booking use `UPDLOCK, ROWLOCK` hints to prevent concurrent agencies overspending. Reservation entry written before PNR creation; committed on `BookingConfirmed` or released on compensation.

### Notification Service
- **D-16:** Email delivery via `IEmailDelivery` interface with **SendGrid as the primary production implementation** and an SMTP fallback implementation for dev (MailHog/Papercut). Same interface — zero code changes between environments.
- **D-17:** HTML email templates rendered via **RazorLight** (Razor engine). Strongly-typed view models per template. Shared branded header/footer as Razor partials.
- **D-18:** PDF generation for e-ticket attachments and B2B vouchers via **QuestPDF** (locked in ROADMAP).
- **D-19:** Notification service consumes `BookingConfirmed`, `BookingCancelled`, `TicketIssued`, `BookingExpired`, `WalletLowBalance` events from RabbitMQ. Uses an `EmailIdempotencyLog` table keyed by `(EventId, EmailType)` to prevent duplicate sends on MassTransit redelivery.

### Data Ownership & Read Model
- **D-20:** `BookingService` owns passenger/document PII (passport, DOB, nationality) as per-booking entities. `CrmService` owns the `Customer` aggregate (name, email, account login, profile) but not document data. Clear aggregate boundaries — BookingService does not sync documents to CRM.
- **D-21:** Customer booking queries (`GET /bookings/{id}`, `GET /customers/{id}/bookings`) are served by `BookingService` directly via dedicated read endpoints, using EF Core projections into response DTOs. No separate CQRS read store for v1.

### Claude's Discretion
- **Stripe UX surface** (PaymentElement embedded vs Checkout Session hosted) — user deferred. Claude defaults to **embedded PaymentElement** for brand consistency, automatic 3DS/SCA handling, and lowest integration complexity inside Next.js.
- Exact compensation step-by-step ordering when multiple compensations must run in sequence (reverse order of forward steps is assumed default).
- Specific SendGrid template IDs / API configuration; RazorLight template file naming convention.
- Concrete schema names for `BookingSagaState`, `SagaDeadLetter`, `WalletTransactions`, `EmailIdempotencyLog` beyond what's implied above.
- Exact regex patterns for fare-rule parsing — planner/researcher works these out from actual Amadeus/Sabre/Galileo sample payloads.
- OpenTelemetry sampling strategy and exporter choice (OTLP vs Jaeger) beyond the scrubbing processor.
- Retry behavior for transient SendGrid API failures (separate from saga retry policy).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & roadmap
- `.planning/REQUIREMENTS.md` — FLTB-01..10, PAY-01..08, NOTF-01..06, COMP-01..06 define acceptance criteria for this phase
- `.planning/ROADMAP.md` §Phase 3 — Locked plan scope (4 plans, UAT criteria, dependency on Phase 2)

### Architecture & stack
- `.planning/research/ARCHITECTURE.md` — Service topology, DB ownership, saga placement in BookingService
- `.planning/research/STACK.md` — MassTransit 8.x, `MassTransit.EntityFrameworkCore`, QuestPDF confirmed
- `.planning/research/PITFALLS.md` — Known implementation traps from research phase
- `.planning/research/SUMMARY.md` — Synthesized critical rules

### Prior phase decisions (locked)
- `.planning/phases/01-infrastructure-foundation/01-CONTEXT.md` — Service layout (API/Application/Infrastructure), shared projects (`TBE.Contracts`, `TBE.Common`), RabbitMQ topology, Keycloak realm
- `.planning/phases/02-inventory-layer-gds-integration/02-RESEARCH.md` — GDS flight search adapters (Amadeus/Sabre/Galileo connector patterns); booking saga consumes these for `PriceReconfirmed` step

### Compliance
- `.planning/PROJECT.md` §Constraints — PCI-DSS applies; GDS credentials in env/vault only
- `.planning/REQUIREMENTS.md` §COMP — COMP-01..06: compensation, PCI scrubbing, encryption-at-rest, `[Authorize]` on booking/account endpoints

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/services/BookingService/{API,Application,Infrastructure}` — scaffolded projects from Phase 1, empty of domain logic. `BookingDbContext.cs` exists.
- `src/services/PaymentService/{API,Application,Infrastructure}` — scaffolded, no payment logic yet.
- `src/services/NotificationService/{API,Application,Infrastructure}` — scaffolded, no consumers yet.
- `src/shared/TBE.Common` — add `IEncryptionKeyProvider`, `SensitiveAttributeProcessor`, `IEmailDelivery` here.
- `src/shared/TBE.Contracts` — add saga event contracts (`BookingInitiated`, `PriceReconfirmed`, `PNRCreated`, `PaymentAuthorized`, `TicketIssued`, `PaymentCaptured`, `BookingConfirmed`, `BookingCancelled`, `BookingExpired`) here.
- Phase 2 GDS connector services (`FlightConnectorService`) — saga calls these via RabbitMQ/HTTP for `PriceReconfirmed` and PNR creation; contracts already defined in Phase 2.

### Established Patterns
- Three-project service layout (API / Application / Infrastructure) — follow strictly.
- MassTransit + EFCore outbox — saga consumers use this pattern; no direct RabbitMQ publishes outside outbox.
- `TBE.{ServiceName}.{Layer}` C# namespace convention.
- Cross-service communication ONLY via `TBE.Contracts` messages — no direct project references.

### Integration Points
- Phase 2 Search/Pricing feeds into `PriceReconfirmed` step — booking saga re-queries the connector before PNR creation.
- Phase 4 (B2C Portal) consumes the customer-facing booking read endpoints defined in D-21.
- Phase 5 (B2B Portal) consumes the wallet schema and `WalletLowBalance` notification defined in D-14..D-15, D-19.
- Phase 7 (Hardening) swaps `.env`-backed `IEncryptionKeyProvider` for Key Vault/KMS implementation.

</code_context>

<specifics>
## Specific Ideas

- Saga step granularity matches the ROADMAP-specified events exactly — do not collapse steps to reduce event volume. Each transition is an auditable checkpoint.
- Compensation chain runs in reverse order of forward steps. Example: ticket-issuance failure after payment authorization triggers (1) payment release (2) PNR void in that order.
- Idempotency keys for Stripe MUST be deterministic from `(BookingId, OperationType)` so that retries replay safely. Never use random GUIDs.
- `SagaDeadLetter` entries must include: saga correlation ID, last successful step, failed compensation step, raw exception, timestamp. Enough detail for ops to fix by hand.
- Email SLA is 60 seconds wall-clock from `BookingConfirmed` to inbox. Measure it in integration tests.

</specifics>

<deferred>
## Deferred Ideas

- Stripe UX choice between embedded PaymentElement and hosted Checkout — not discussed, Claude's discretion defaults to embedded PaymentElement. Revisit in Phase 4 if B2C portal UX drives a change.
- 3DS/SCA exemption strategy (low-value, trusted-beneficiary) — out of scope for v1; default 3DS flow applies to all transactions.
- Cancellation and refund UX screens — Phase 4 (B2C Portal).
- Booking modification (change date/route) — future phase; v1 only supports create + auto-cancel.
- CQRS read projections / materialized views — deferred; D-21 uses same-DB reads for v1.
- Azure Key Vault / AWS KMS production wiring — deferred to Phase 7; `IEncryptionKeyProvider` interface ready.
- CI/CD pipeline for saga deployment — Phase 7.
- Loyalty/points awarded on booking confirmation — PROJECT.md scope exclusion.

</deferred>

---

*Phase: 03-core-flight-booking-saga-b2c*
*Context gathered: 2026-04-15*
