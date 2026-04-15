# Phase 3: Core Flight Booking Saga (B2C) - Research

**Researched:** 2026-04-15
**Domain:** Distributed saga orchestration, Stripe authorize/capture, B2B wallet concurrency, PCI/PII hardening, notification delivery — .NET 8 / ASP.NET Core
**Confidence:** HIGH (core NuGet versions verified against nuget.org; Stripe flow verified against docs.stripe.com; MassTransit saga pattern verified against official docs; prior-phase stack decisions locked)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Saga Orchestration & Error Policy:**
- **D-01:** Saga state persisted in dedicated `BookingService.Saga` schema using `MassTransit.EntityFrameworkCore`. `BookingSagaState` table. Concurrency via EF optimistic concurrency token. Isolated from domain tables (`Bookings`, `Passengers`).
- **D-02:** Retry policy per saga step: **3 attempts with exponential backoff (2s / 4s / 8s)** before triggering compensation. Applies to GDS, Stripe, and email dispatch.
- **D-03:** Compensation failure policy: write to `SagaDeadLetter` table, fire ops alert (PagerDuty/email), mark booking `RequiresManualReconciliation`. Never infinite-retry compensation; never silently swallow.
- **D-04:** TTL hard-timeout at ticketing-deadline minus 2 minutes. Saga auto-voids PNR, releases Stripe auth, publishes `BookingExpired`, sends cancellation email.
- **D-05:** Saga step ordering (locked): `BookingInitiated` → `PriceReconfirmed` → `PNRCreated` → `PaymentAuthorized` → `TicketIssued` → `PaymentCaptured` → `BookingConfirmed`.

**Fare-Rule / TTL Parsing:**
- **D-06:** Primary regex parser + per-GDS override adapters (Amadeus / Sabre / Galileo).
- **D-07:** Parse-failure fallback: default to 2-hour TTL + ops alert.

**Encryption, PCI, PII:**
- **D-08:** AES-256 for passport/document. `IEncryptionKeyProvider` interface in `TBE.Common`. Dev = `.env` 256-bit key. Prod (Key Vault / KMS) deferred to Phase 7.
- **D-09:** OpenTelemetry `SensitiveAttributeProcessor` in `TBE.Common`, registered in every service DI. Filters `card.*`, `cvv`, `pan`, `stripe.raw_*`, `passport.*`, `document.number`.
- **D-10:** Stripe Elements is the only surface touching raw card data (SAQ-A). Server stores only `PaymentIntentId` and `CustomerId`.

**Payment Flow:**
- **D-11:** Stripe Payment Intents with `capture_method: manual`. Capture only after `TicketIssued`.
- **D-12:** Payment confirmation only via Stripe webhook with signature verification. Client redirect never trusted.
- **D-13:** Idempotency keys deterministic from `(BookingId, OperationType)` — e.g., `booking-{id}-authorize`, `booking-{id}-capture`, `booking-{id}-refund`.

**B2B Credit Wallet:**
- **D-14:** Append-only `WalletTransactions` log (entry types: `Reserve`, `Commit`, `Release`, `TopUp`). No mutable balance column — balance derived from log.
- **D-15:** Balance reads use `UPDLOCK, ROWLOCK`. Reservation before PNR creation; committed on `BookingConfirmed` or released on compensation.

**Notification Service:**
- **D-16:** `IEmailDelivery` interface. SendGrid primary prod. SMTP fallback (MailHog/Papercut) for dev.
- **D-17:** HTML via **RazorLight** (Razor engine), strongly-typed view models, shared Razor partial header/footer.
- **D-18:** PDF via **QuestPDF**.
- **D-19:** Consumes `BookingConfirmed`, `BookingCancelled`, `TicketIssued`, `BookingExpired`, `WalletLowBalance`. `EmailIdempotencyLog` keyed by `(EventId, EmailType)`.

**Data Ownership:**
- **D-20:** `BookingService` owns passenger/document PII. `CrmService` owns `Customer` aggregate (profile only, no documents).
- **D-21:** Customer booking queries served by `BookingService` EF projections. No separate CQRS read store for v1.

### Claude's Discretion
- Stripe UX surface: **embedded PaymentElement** (default).
- Compensation ordering: reverse of forward steps (default).
- SendGrid template IDs, RazorLight file naming convention.
- Concrete schema names (within D-01/D-03/D-14/D-19 shape constraints).
- Exact fare-rule regex patterns (researcher/planner derives from Amadeus/Sabre/Galileo samples).
- OpenTelemetry sampling and exporter choice (beyond scrubbing processor).
- SendGrid transient-retry policy (separate from saga retry).

### Deferred Ideas (OUT OF SCOPE)
- Hosted Checkout as alternative to PaymentElement — revisit Phase 4 if portal UX drives it.
- 3DS/SCA exemption strategy — v1 uses default 3DS.
- Cancellation / refund UX screens — Phase 4.
- Booking modification — future phase (v1 = create + auto-cancel).
- CQRS read projections — same-DB reads for v1 per D-21.
- Azure Key Vault / AWS KMS — Phase 7.
- CI/CD pipeline for saga — Phase 7.
- Loyalty points on confirmation — PROJECT.md exclusion.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| FLTB-01 | One-way/return/multi-city with pax types | Phase 2 search consumed by `PriceReconfirmed`; saga-state holds pax breakdown |
| FLTB-02 | Fare rules displayed before booking | Phase 2 delivers fare rules; saga snapshots on `BookingInitiated` |
| FLTB-03 | All-in pricing shown at search | Delivered by Phase 2; saga preserves snapshot |
| FLTB-04 | MassTransit saga: PNR → authorize → ticket → capture → confirm | § Saga State Machine; MassTransit 9.1.0 |
| FLTB-05 | PNR created and held before payment captured | Step ordering D-05; `capture_method=manual` |
| FLTB-06 | TTL extracted, saga hard-timeout at TTL-2min | § TTL Monitor Service; `Schedule` API |
| FLTB-07 | Compensation per step | § Compensation Chain |
| FLTB-08 | Confirmation email + e-ticket within 60s | § Notification Service § 60-Second SLA Budget |
| FLTB-09 | Customer can view booking + itinerary | D-21 EF projections; `BookingsController.GetById` |
| FLTB-10 | Customer can request cancellation | Auto-compensation path in v1; UX in Phase 4 |
| PAY-01 | Stripe Payment Element with 3DS | § Stripe Payment Intents |
| PAY-02 | Webhook-only confirmation | § Webhook Signature Verification |
| PAY-03 | B2B wallet balance + transaction history | Append-only log derivation |
| PAY-04 | Wallet top-up via Stripe (SAQ-A) | Separate PI flow; `TopUp` entry type |
| PAY-05 | Every wallet movement = immutable record | D-14 append-only log |
| PAY-06 | UPDLOCK/ROWLOCK on balance read | § Wallet Concurrency |
| PAY-07 | Refunds to original method; wallet credits back to wallet | § Stripe Refund Flow |
| PAY-08 | Payment Service isolated (PCI scope control) | Microservice boundary |
| NOTF-01..03 | Confirm/voucher/cancellation emails ≤ 60s | § Notification Service |
| NOTF-04 | B2B TTL alert emails (24h, 2h) | TTL monitor publishes `TicketingDeadlineApproaching` |
| NOTF-05 | Wallet low-balance alert | `WalletLowBalance` publisher after wallet mutation |
| NOTF-06 | Branded HTML templates | RazorLight + shared partials |
| COMP-01 | No card data server-side | SAQ-A D-10 |
| COMP-02 | AES-256 document encryption | `IEncryptionKeyProvider` |
| COMP-03 | GDPR erasure without destroying audit | Tombstone pattern on passengers, keep booking events |
| COMP-04 | JWT required on all endpoints | `[Authorize]` attribute blanket |
| COMP-05 | GDS creds via vault/env | `IConfiguration` + `.env`; Phase 7 KMS |
| COMP-06 | OTel span scrubbing | `SensitiveAttributeProcessor` in `TBE.Common` |
</phase_requirements>

---

## Summary

Phase 3 builds the distributed saga that turns a Phase 2 search result into a real e-ticket, money captured, and a confirmation email delivered in 60 seconds — with correct compensation on every failure path. Four plans are already locked by CONTEXT.md and ROADMAP: (1) MassTransit state-machine saga, (2) Stripe Payment Service + B2B wallet, (3) TTL monitor + compliance hardening, (4) Notification Service.

The **biggest correctness risk** is any path where Stripe captures money before a GDS ticket exists — forbidden by FLTB-05/PAY-01 and enforced by `capture_method: manual` plus the strict D-05 step ordering. The **biggest performance risk** is the 60-second email SLA — realistic only if (a) email dispatch is fully async off the saga critical path, (b) PDF generation happens in the notification consumer not the saga, and (c) the saga terminates on `PaymentCaptured` → `BookingConfirmed` without waiting for the email send receipt. The **biggest concurrency risk** is wallet double-spend across concurrent B2B bookings, handled by `UPDLOCK, ROWLOCK` + append-only log + saga reservation step.

Versions verified on nuget.org 2026-04-15: **MassTransit 9.1.0** (released 2026-03-30), **MassTransit.EntityFrameworkCore 9.1.0**, **Stripe.net 51.0.0** (released 2026-03-26), **QuestPDF 2026.2.4** (dual-license — under the $1M revenue threshold so community license applies in dev/test; commercial license required for production). `RazorLight 2.3.1` is stable but older (2023-01-16); it still works on .NET 8 but note the lack of recent maintenance as a tertiary risk. `SendGrid 9.29.3` (2024-04-02) is stable.

**Primary recommendation:** Build the saga as a single `MassTransitStateMachine<BookingSagaState>` in `BookingService.Application`, persist via `EntityFrameworkRepository` with **Optimistic** concurrency (per D-01 — matches `RowVersion` token, scales better than Pessimistic at RabbitMQ delivery rates, and works correctly because MassTransit retries on concurrency conflict). Use MassTransit's `Schedule` for the TTL-minus-2-minute hard timeout. Payment Service receives commands from the saga (`AuthorizePayment`, `CapturePayment`, `CancelAuthorization`, `RefundPayment`) via RabbitMQ — no synchronous HTTP. Webhook ingress validated via `EventUtility.ConstructEvent` then published onto the bus as a `StripeWebhookReceived` event that triggers a saga event. Notification Service is a pure event consumer — it never blocks the saga.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Saga state & orchestration | BookingService (Application) | — | Owns booking lifecycle per ARCHITECTURE.md; saga state DB-local |
| PNR create/void/ticket-issue | FlightConnectorService (Phase 2) | BookingService (caller) | Phase 2 owns GDS adapters; saga sends commands |
| Stripe authorize/capture/refund | PaymentService | — | PCI scope isolation (PAY-08); only service touching Stripe SDK |
| Stripe webhook ingress | PaymentService.API | — | Signature verification at edge; webhook body never crosses service boundary in raw form |
| B2B wallet log + balance derivation | PaymentService (Infrastructure) | — | Payment operations clustered; wallet is payment-adjacent |
| TTL monitor (5-min poll) | BookingService (`IHostedService`) | — | Owns PNR state; reads saga state directly |
| TTL deadline extraction (regex parser) | BookingService.Application (`IFareRuleParser`) | FlightConnectorService (supplies raw text) | Parsing is booking-domain logic, not GDS-adapter logic |
| Email delivery + PDF | NotificationService | — | Isolated consumer; never blocks saga |
| Email idempotency log | NotificationService.Infrastructure | — | Local to consumer |
| Encryption key access | TBE.Common (`IEncryptionKeyProvider`) | Every service that reads/writes PII | Shared infrastructure concern |
| OTel attribute scrubbing | TBE.Common (`SensitiveAttributeProcessor`) | Registered in every service DI | Shared concern, never per-service ad-hoc (D-09) |
| Saga dead-letter alerting | BookingService + ops-alerts integration | Backoffice UI (Phase 6) | BookingService writes row; alert hook side-effects |
| Customer booking read endpoints | BookingService.API | — | D-21: EF projection, no separate read store |

---

## Standard Stack

### Core (BookingService — saga)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| MassTransit | 9.1.0 | State-machine saga, outbox, retry, schedule | De facto .NET messaging framework; 9.1.0 released 2026-03-30 [VERIFIED: nuget.org] |
| MassTransit.RabbitMQ | 9.1.0 | RabbitMQ transport | Matches phase-1 broker [VERIFIED: nuget.org] |
| MassTransit.EntityFrameworkCore | 9.1.0 | Saga state persistence with EF Core | Official MT-maintained repository provider [VERIFIED: nuget.org] |
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.x | DB provider for saga and domain tables | Aligns with phase-1 stack |
| Polly / Microsoft.Extensions.Http.Resilience | 10.4.0 | Retry/circuit-breaker for GDS outbound HTTP within a step | Already in Phase 2; reuse |

### Core (PaymentService)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Stripe.net | 51.0.0 | Stripe SDK (Payment Intents, webhooks, refunds) | Official Stripe-maintained SDK; 51.0.0 released 2026-03-26, supports .NET 8/9 [VERIFIED: nuget.org] |
| MassTransit (+ RabbitMQ + EF Core outbox) | 9.1.0 | Consume saga commands, publish events atomically with payment-row writes | Outbox pattern locked at phase-1 |
| Dapper | 2.1.x | Raw SQL for `WalletTransactions` reservation (UPDLOCK hint via `SqlCommand`/`FromSqlRaw` raw) | EF Core does not emit lock hints directly; Dapper or `FromSqlRaw` required per PITFALLS §18 |

### Core (NotificationService)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| SendGrid | 9.29.3 | Email delivery via SendGrid REST API | Official Twilio SendGrid SDK; 9.29.3 released 2024-04-02 [VERIFIED: nuget.org] |
| RazorLight | 2.3.1 | Razor template engine outside MVC (HTML email bodies) | D-17 locked choice; 2.3.1 released 2023-01-16 [VERIFIED: nuget.org]. Note: infrequent updates — [MEDIUM confidence] still works on .NET 8 per community reports, but watch for AOT/trimming issues |
| QuestPDF | 2026.2.4 | PDF generation (e-tickets, hotel vouchers) | D-18 locked; 2026.2.4 released 2026-03-20 [VERIFIED: nuget.org]. **License:** Community OK for individuals/non-profits/FOSS/orgs < $1M revenue. **Commercial license required for production** at real travel-business revenue — flag as a cost item |

### Shared (TBE.Common)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| OpenTelemetry | 1.9.x | Tracing + metrics pipeline | Already in phase-1; add `SensitiveAttributeProcessor` here |
| OpenTelemetry.Extensions.Hosting | 1.9.x | DI registration | Standard pairing |
| System.Security.Cryptography (in-box) | .NET 8 | AES-256 via `AesGcm` for authenticated encryption | Prefer `AesGcm` over `Aes` CBC — built-in IV + authentication tag prevents malleability [CITED: learn.microsoft.com/AesGcm] |

### Alternatives Considered
| Instead of | Could Use | Tradeoff / Why Not |
|------------|-----------|--------------------|
| MassTransit saga | Choreography via domain events | Locked: orchestration per CONTEXT; choreography scatters compensation logic across services, impossible to audit |
| `capture_method: manual` | Separate auth+capture Charges API | Charges API is legacy per STACK.md; Payment Intents handles 3DS/SCA automatically |
| RazorLight | Scriban / Liquid / raw StringBuilder | D-17 locked; RazorLight gives strongly-typed view models. If RazorLight compatibility fails on .NET 8, **Fluid** (@sebastienros) is the highest-confidence fallback |
| QuestPDF | IronPDF / Syncfusion PDF | IronPDF has commercial licensing with per-developer fees; Syncfusion free tier has seat limits. QuestPDF's $1M threshold is the most generous |
| Dapper for wallet | EF Core raw SQL (`FromSqlRaw`) | Dapper preferred for explicit transaction scope + lock hint control; `FromSqlRaw` works but ties lock-sensitive SQL to DbContext lifetime |
| Stripe webhook in HTTP handler | Publish to bus, process in consumer | Locked by ARCHITECTURE.md: HTTP handler validates signature and idempotent-stores raw event, background consumer advances saga — avoids holding HTTP connection during saga step |

### Installation
```bash
# BookingService
dotnet add src/services/BookingService/BookingService.Infrastructure package MassTransit --version 9.1.0
dotnet add src/services/BookingService/BookingService.Infrastructure package MassTransit.RabbitMQ --version 9.1.0
dotnet add src/services/BookingService/BookingService.Infrastructure package MassTransit.EntityFrameworkCore --version 9.1.0

# PaymentService
dotnet add src/services/PaymentService/PaymentService.Infrastructure package Stripe.net --version 51.0.0
dotnet add src/services/PaymentService/PaymentService.Infrastructure package Dapper --version 2.1.35

# NotificationService
dotnet add src/services/NotificationService/NotificationService.Infrastructure package SendGrid --version 9.29.3
dotnet add src/services/NotificationService/NotificationService.Infrastructure package RazorLight --version 2.3.1
dotnet add src/services/NotificationService/NotificationService.Infrastructure package QuestPDF --version 2026.2.4
```

**Version verification:** All versions above queried from nuget.org on 2026-04-15. [VERIFIED: nuget.org]

---

## Architecture Patterns

### System Architecture Diagram

```
┌──────────┐  POST /bookings    ┌─────────────────┐
│ B2C UI   │──────────────────▶ │ BookingService  │
│ (Next.js)│                    │  .API           │
└──────────┘                    └────────┬────────┘
                                         │ publishes InitiateBookingCommand
                                         ▼
                              ┌─────────────────────┐
                              │  RabbitMQ (outbox)  │
                              └──────────┬──────────┘
                                         │
                          ┌──────────────┴──────────────────┐
                          ▼                                 ▼
        ┌────────────────────────────┐       ┌──────────────────────────┐
        │ BookingSagaStateMachine    │       │ FlightConnectorService   │
        │  (BookingService.App)      │       │  (Phase 2 — GDS adapters)│
        │                            │◀─────▶│                          │
        │ Initially ─▶ BookingInit.  │  cmd  └──────────────────────────┘
        │  During(PriceReconfirming) │
        │  During(PNRCreating)       │       ┌──────────────────────────┐
        │  During(Authorizing)       │◀─────▶│ PaymentService           │
        │  During(TicketIssuing)     │  cmd  │  Stripe.net SDK          │
        │  During(Capturing)         │       │  WalletTransactions log  │
        │  → BookingConfirmed        │       └───────────┬──────────────┘
        │                            │                   │ webhook ingress
        │ ScheduledTimeout @ TTL-2m  │                   ▼
        └──────────┬─────────────────┘       ┌──────────────────┐
                   │ publishes events        │  Stripe (external)│
                   ▼                         └──────────────────┘
        ┌──────────────────────────┐
        │ RabbitMQ booking.events  │
        └──────────┬───────────────┘
                   │
                   ▼
        ┌──────────────────────────┐       ┌──────────────────┐
        │ NotificationService      │──────▶│ SendGrid (email) │
        │  RazorLight HTML         │       └──────────────────┘
        │  QuestPDF attachment     │
        │  EmailIdempotencyLog     │
        └──────────────────────────┘

                   ▲
                   │ 5-min poll
                   │
        ┌──────────────────────────┐
        │ TtlMonitorHostedService  │  reads open-PNR saga states
        │  (BookingService)        │  fires BookingTimeoutRequested
        └──────────────────────────┘
```

### Recommended Project Structure
```
src/services/BookingService/
├── BookingService.API/
│   ├── Controllers/BookingsController.cs      # POST /bookings, GET /bookings/{id}
│   └── Controllers/StripeWebhookController.cs # NO — lives in PaymentService
├── BookingService.Application/
│   ├── Sagas/
│   │   ├── BookingSagaState.cs                # SagaStateMachineInstance + RowVersion
│   │   ├── BookingSagaStateMachine.cs         # MassTransitStateMachine<BookingSagaState>
│   │   └── BookingSagaDefinition.cs           # retry/redelivery policy
│   ├── Consumers/
│   │   ├── PnrCreationConsumer.cs             # commands GDS adapter, emits PNRCreated/PNRFailed
│   │   └── CompensationConsumers/             # VoidPnrConsumer, ReleaseWalletConsumer
│   ├── Services/
│   │   ├── IFareRuleParser.cs                 # ticketing deadline extraction
│   │   └── FareRuleParser.cs                  # regex + per-GDS adapters
│   └── HostedServices/
│       └── TtlMonitorHostedService.cs         # 5-min poll
├── BookingService.Infrastructure/
│   ├── Persistence/
│   │   ├── BookingDbContext.cs                # domain tables
│   │   ├── BookingSagaDbContext.cs            # isolated schema (D-01)
│   │   ├── SagaClassMaps/BookingSagaStateMap.cs
│   │   └── Migrations/
│   └── MassTransitConfig.cs

src/services/PaymentService/
├── PaymentService.API/
│   └── Controllers/StripeWebhookController.cs # signature verify → publish StripeWebhookReceived
├── PaymentService.Application/
│   ├── Consumers/
│   │   ├── AuthorizePaymentConsumer.cs
│   │   ├── CapturePaymentConsumer.cs
│   │   ├── CancelAuthorizationConsumer.cs
│   │   ├── RefundPaymentConsumer.cs
│   │   ├── WalletReserveConsumer.cs
│   │   ├── WalletCommitConsumer.cs
│   │   ├── WalletReleaseConsumer.cs
│   │   └── StripeWebhookConsumer.cs           # bridges webhook → saga events
│   └── Services/
│       ├── IStripeGateway.cs
│       └── StripeGateway.cs
├── PaymentService.Infrastructure/
│   ├── Persistence/
│   │   ├── PaymentDbContext.cs
│   │   └── WalletRepository.cs                # Dapper, UPDLOCK/ROWLOCK
│   └── Stripe/IdempotencyKeyFactory.cs

src/services/NotificationService/
├── NotificationService.Application/
│   ├── Consumers/
│   │   ├── BookingConfirmedConsumer.cs
│   │   ├── BookingCancelledConsumer.cs
│   │   ├── BookingExpiredConsumer.cs
│   │   ├── TicketingDeadlineConsumer.cs       # 24h / 2h alerts
│   │   └── WalletLowBalanceConsumer.cs
│   └── Templates/                             # RazorLight .cshtml files
│       ├── _Layout.cshtml                     # shared header/footer partial
│       ├── FlightConfirmation.cshtml
│       ├── HotelVoucher.cshtml
│       └── Cancellation.cshtml
├── NotificationService.Infrastructure/
│   ├── Email/
│   │   ├── IEmailDelivery.cs                  # in TBE.Common (D-16)
│   │   ├── SendGridEmailDelivery.cs
│   │   └── SmtpEmailDelivery.cs
│   ├── Pdf/
│   │   ├── ETicketPdfGenerator.cs             # QuestPDF
│   │   └── HotelVoucherPdfGenerator.cs
│   └── Persistence/
│       ├── NotificationDbContext.cs
│       └── EmailIdempotencyLog.cs

src/shared/TBE.Common/
├── Security/
│   ├── IEncryptionKeyProvider.cs
│   ├── EnvEncryptionKeyProvider.cs            # dev
│   └── AesGcmFieldEncryptor.cs                # AES-256-GCM
├── Email/
│   └── IEmailDelivery.cs                      # shared interface
└── Observability/
    └── SensitiveAttributeProcessor.cs         # OTel processor

src/shared/TBE.Contracts/
├── Commands/                                  # saga → service commands
│   ├── AuthorizePaymentCommand.cs
│   ├── CapturePaymentCommand.cs
│   ├── CancelAuthorizationCommand.cs
│   ├── RefundPaymentCommand.cs
│   ├── CreatePnrCommand.cs
│   ├── VoidPnrCommand.cs
│   ├── IssueTicketCommand.cs
│   ├── WalletReserveCommand.cs
│   ├── WalletCommitCommand.cs
│   └── WalletReleaseCommand.cs
└── Events/                                    # saga outputs + callbacks
    ├── BookingInitiated.cs
    ├── PriceReconfirmed.cs
    ├── PnrCreated.cs
    ├── PaymentAuthorized.cs
    ├── TicketIssued.cs
    ├── PaymentCaptured.cs
    ├── BookingConfirmed.cs
    ├── BookingCancelled.cs
    ├── BookingExpired.cs
    ├── BookingFailed.cs                       # carries cause
    └── WalletLowBalance.cs
```

### Pattern 1: MassTransit State Machine Saga

**What:** A `MassTransitStateMachine<T>` defines states, events, and transitions in a fluent DSL. Saga state (one row per booking, correlated by `CorrelationId`) persists via `EntityFrameworkRepository`.

**When to use:** Any multi-step workflow with compensations. This is the locked choice (D-01/D-05).

**Example (minimal, illustrative):**
```csharp
// Source: masstransit.io/documentation/patterns/saga/state-machine [CITED]
public class BookingSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public int CurrentState { get; set; }        // int mapping more compact than string
    public int Version { get; set; }             // EF optimistic concurrency (D-01)

    // Snapshot fields (locked at BookingInitiated, never recalculated mid-saga — PITFALLS)
    public string BookingReference { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; }
    public string PaymentMethod { get; set; }    // "card" | "wallet"

    // External refs (filled in as steps complete)
    public string GdsPnr { get; set; }
    public string StripePaymentIntentId { get; set; }
    public string TicketNumber { get; set; }
    public string WalletReservationId { get; set; }

    // TTL
    public DateTime TicketingDeadlineUtc { get; set; }
    public Guid? TimeoutTokenId { get; set; }    // MT Schedule token for cancellation
}

public class BookingSagaStateMachine : MassTransitStateMachine<BookingSagaState>
{
    public State PriceReconfirming { get; private set; }
    public State PnrCreating { get; private set; }
    public State Authorizing { get; private set; }
    public State TicketIssuing { get; private set; }
    public State Capturing { get; private set; }
    public State Confirmed { get; private set; }
    public State Compensating { get; private set; }
    public State Failed { get; private set; }

    public Event<BookingInitiated> BookingInitiated { get; private set; }
    public Event<PriceReconfirmed> PriceReconfirmed { get; private set; }
    public Event<PnrCreated> PnrCreated { get; private set; }
    public Event<PaymentAuthorized> PaymentAuthorized { get; private set; }
    public Event<TicketIssued> TicketIssued { get; private set; }
    public Event<PaymentCaptured> PaymentCaptured { get; private set; }
    public Event<BookingTimeoutExpired> TimeoutExpired { get; private set; }
    // ... failure events and schedule

    public Schedule<BookingSagaState, BookingTimeoutExpired> HardTimeout { get; private set; }

    public BookingSagaStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => BookingInitiated, x => x.CorrelateById(m => m.Message.BookingId));
        // ... wire other events by BookingId

        Schedule(() => HardTimeout, x => x.TimeoutTokenId, s =>
        {
            s.Delay = TimeSpan.FromMinutes(60);  // default; overridden per saga
            s.Received = r => r.CorrelateById(m => m.Message.BookingId);
        });

        Initially(
            When(BookingInitiated)
                .Then(ctx => { /* copy snapshot fields */ })
                .Schedule(HardTimeout, ctx => new BookingTimeoutExpired(ctx.Saga.CorrelationId),
                          ctx => ctx.Saga.TicketingDeadlineUtc.AddMinutes(-2) - DateTime.UtcNow)
                .Publish(ctx => new ReconfirmPriceCommand(ctx.Saga.CorrelationId))
                .TransitionTo(PriceReconfirming));

        During(PriceReconfirming,
            When(PriceReconfirmed)
                .Publish(ctx => new CreatePnrCommand(ctx.Saga.CorrelationId, /* ... */))
                .TransitionTo(PnrCreating));

        During(PnrCreating,
            When(PnrCreated)
                .Then(ctx => ctx.Saga.GdsPnr = ctx.Message.Pnr)
                .Publish(ctx => new AuthorizePaymentCommand(/* idempotency key booking-{id}-authorize */))
                .TransitionTo(Authorizing));

        During(Authorizing,
            When(PaymentAuthorized)
                .Then(ctx => ctx.Saga.StripePaymentIntentId = ctx.Message.PaymentIntentId)
                .Publish(ctx => new IssueTicketCommand(/* ... */))
                .TransitionTo(TicketIssuing));

        During(TicketIssuing,
            When(TicketIssued)
                .Then(ctx => ctx.Saga.TicketNumber = ctx.Message.TicketNumber)
                .Publish(ctx => new CapturePaymentCommand(/* idempotency key booking-{id}-capture */))
                .TransitionTo(Capturing));

        During(Capturing,
            When(PaymentCaptured)
                .Unschedule(HardTimeout)
                .Publish(ctx => new BookingConfirmed(ctx.Saga.CorrelationId, /* ... */))
                .TransitionTo(Confirmed)
                .Finalize());

        // TTL hard-timeout from any state
        DuringAny(
            When(TimeoutExpired)
                .TransitionTo(Compensating)
                /* trigger compensation chain */);

        // Compensation chain declared DuringAny for each failure event type ...

        SetCompletedWhenFinalized();
    }
}
```

**EF persistence wiring:**
```csharp
// Source: masstransit.io/documentation/configuration/sagas/entity-framework [CITED]
services.AddMassTransit(x =>
{
    x.AddSagaStateMachine<BookingSagaStateMachine, BookingSagaState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;   // D-01 — uses ISagaVersion
            r.ExistingDbContext<BookingSagaDbContext>();
            r.UseSqlServer();
        });

    x.AddEntityFrameworkOutbox<BookingSagaDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.UseMessageRetry(r => r.Exponential(
            retryLimit: 3,
            minInterval: TimeSpan.FromSeconds(2),
            maxInterval: TimeSpan.FromSeconds(8),
            intervalDelta: TimeSpan.FromSeconds(2))); // D-02
        cfg.UseMessageScheduler(new Uri("queue:scheduler")); // for Schedule API
        cfg.ConfigureEndpoints(ctx);
    });
});
```

> **Concurrency note (D-01 vs ARCHITECTURE.md):** CONTEXT.md D-01 locks **Optimistic** concurrency. ARCHITECTURE.md §Critical Implementation Notes recommended **Pessimistic**. Resolution: **Optimistic wins per D-01** — optimistic with `ISagaVersion` + RowVersion token is the modern MassTransit default and correctly handles concurrent event delivery (MassTransit retries on concurrency conflict). Pessimistic was a pre-MT-8 recommendation when optimistic support was weaker. This is a deliberate CONTEXT override of older architecture guidance.

### Pattern 2: Compensation Chain (Reverse Order)

**What:** On any unrecoverable step failure, emit compensation commands in **reverse order** of completed forward steps.

**Compensation matrix:**

| Failed Step | Forward Steps Already Completed | Compensation Sequence (reverse) |
|-------------|--------------------------------|---------------------------------|
| `PriceReconfirmed` fails | — | Publish `BookingFailed(cause)`; transition to `Failed`. |
| `PNRCreated` fails | Price reconfirmed | Publish `BookingFailed`. Price reconfirm has no side effect to undo. |
| `PaymentAuthorized` fails | PNR created | `VoidPnrCommand` → `BookingFailed`. |
| `TicketIssued` fails | PNR created + auth | `CancelAuthorizationCommand` → `VoidPnrCommand` → `BookingFailed`. |
| `PaymentCaptured` fails | PNR + auth + ticket | Edge case: ticket issued but capture failed. **Retry capture up to 3 times** (per D-02), then escalate to `SagaDeadLetter` — a ticket exists but money isn't captured, requires human resolution. Do NOT void PNR (ticket is real). Do NOT auto-refund (nothing captured yet). |
| TTL timeout hit | Variable | Run full compensation chain for whatever steps completed. |

**Wallet path (B2B) compensation:**

| Failed Step | Wallet Compensation |
|-------------|--------------------| 
| Any step after `WalletReserve` but before `BookingConfirmed` | Publish `WalletReleaseCommand` with the reservation ID — appends a `Release` entry to `WalletTransactions`. |
| `BookingConfirmed` | Publish `WalletCommitCommand` — appends `Commit` entry. |

### Pattern 3: Stripe Authorize-then-Capture (manual capture)

**What:** Create PaymentIntent with `capture_method=manual` → client confirms via PaymentElement → webhook `payment_intent.amount_capturable_updated` signals auth complete → saga captures after ticket issuance.

**Authorization hold duration (from docs.stripe.com):** Visa CIT 7 days, Mastercard/Amex/Discover 7 days card-not-present, JPY up to 30 days. Well beyond any travel booking TTL — no risk of expiry mid-saga. [CITED: docs.stripe.com/payments/place-a-hold-on-a-payment-method]

**Authorize (saga → PaymentService consumer):**
```csharp
// Source: docs.stripe.com/api/payment_intents/create [CITED]
var options = new PaymentIntentCreateOptions
{
    Amount = (long)(cmd.AmountCents),
    Currency = cmd.Currency,
    CaptureMethod = "manual",
    Customer = cmd.StripeCustomerId,
    PaymentMethod = cmd.PaymentMethodId,       // from PaymentElement
    Confirm = true,
    AutomaticPaymentMethods = new() { Enabled = true, AllowRedirects = "always" },
    Metadata = new Dictionary<string, string>
    {
        ["booking_id"] = cmd.BookingId.ToString()
    }
};

var requestOptions = new RequestOptions
{
    IdempotencyKey = $"booking-{cmd.BookingId}-authorize"  // D-13
};

var intent = await _stripe.PaymentIntents.CreateAsync(options, requestOptions);
```

**Capture (after TicketIssued):**
```csharp
var captureOptions = new PaymentIntentCaptureOptions
{
    AmountToCapture = (long)(cmd.AmountCents)
};
var requestOptions = new RequestOptions
{
    IdempotencyKey = $"booking-{cmd.BookingId}-capture"   // D-13
};
var captured = await _stripe.PaymentIntents.CaptureAsync(cmd.PaymentIntentId, captureOptions, requestOptions);
```

**Cancel authorization (void):**
```csharp
var cancelOptions = new PaymentIntentCancelOptions { CancellationReason = "requested_by_customer" };
var requestOptions = new RequestOptions { IdempotencyKey = $"booking-{cmd.BookingId}-cancel" };
await _stripe.PaymentIntents.CancelAsync(cmd.PaymentIntentId, cancelOptions, requestOptions);
```

**Refund (after capture, compensation path only):**
```csharp
var refundOptions = new RefundCreateOptions { PaymentIntent = cmd.PaymentIntentId };
var requestOptions = new RequestOptions { IdempotencyKey = $"booking-{cmd.BookingId}-refund" };
await _stripe.Refunds.CreateAsync(refundOptions, requestOptions);
```

### Pattern 4: Stripe Webhook Ingress (PaymentService.API)

**What:** HTTP handler validates signature, stores raw event (idempotent by `event.Id`), publishes domain event to RabbitMQ. A saga-bridging consumer translates Stripe events into saga events.

```csharp
// Source: docs.stripe.com/webhooks/signatures + Stripe.net README [CITED]
[HttpPost("/webhooks/stripe")]
public async Task<IActionResult> Handle()
{
    using var reader = new StreamReader(Request.Body);
    var json = await reader.ReadToEndAsync();
    var sig = Request.Headers["Stripe-Signature"];

    Event stripeEvent;
    try
    {
        stripeEvent = EventUtility.ConstructEvent(
            json, sig, _webhookSecret,
            tolerance: 300);   // 5-min default per docs.stripe.com — do NOT set 0
    }
    catch (StripeException) { return BadRequest(); }

    // Idempotency: persist event.Id → return 200 if already processed
    if (await _processedEvents.ExistsAsync(stripeEvent.Id))
        return Ok();

    await _publishEndpoint.Publish(new StripeWebhookReceived
    {
        EventId = stripeEvent.Id,
        EventType = stripeEvent.Type,
        PaymentIntentId = (stripeEvent.Data.Object as PaymentIntent)?.Id,
        RawPayload = json  // encrypted at rest if retained; SCRUBBED in OTel spans (D-09)
    });

    await _processedEvents.RecordAsync(stripeEvent.Id);
    return Ok();
}
```

**Key Stripe events the saga cares about:**
- `payment_intent.amount_capturable_updated` → saga event `PaymentAuthorized`
- `payment_intent.succeeded` (post-capture) → saga event `PaymentCaptured`
- `payment_intent.payment_failed` → saga event `PaymentFailed`
- `payment_intent.canceled` → saga event `PaymentCancelled`
- `charge.refunded` → informational, logged

### Pattern 5: Wallet Concurrency (UPDLOCK + append-only)

**What:** Every balance read inside a reservation transaction takes a lock that serializes concurrent bookings for the same wallet. Append `Reserve` / `Commit` / `Release` / `TopUp` entries. Balance is `SUM(signed amounts)` over the log.

```sql
-- WalletTransactions schema (append-only)
CREATE TABLE payment.WalletTransactions (
    TxId             UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    WalletId         UNIQUEIDENTIFIER NOT NULL,
    BookingId        UNIQUEIDENTIFIER NULL,           -- null for TopUp
    EntryType        TINYINT NOT NULL,                 -- 1=Reserve 2=Commit 3=Release 4=TopUp
    Amount           DECIMAL(18,4) NOT NULL,           -- always positive
    SignedAmount     AS (CASE WHEN EntryType IN (1,2) THEN -Amount ELSE Amount END) PERSISTED,
    Currency         CHAR(3) NOT NULL,
    IdempotencyKey   NVARCHAR(100) NOT NULL UNIQUE,    -- e.g., "booking-{id}-reserve"
    CreatedAtUtc     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CorrelatesWithTx UNIQUEIDENTIFIER NULL             -- Commit/Release points at original Reserve
);

CREATE INDEX IX_WalletTransactions_Wallet_Created
    ON payment.WalletTransactions(WalletId, CreatedAtUtc);
```

**Reservation (Dapper, explicit transaction, UPDLOCK):**
```csharp
// Source: PITFALLS.md Pitfall 18 + STACK.md B2B wallet section [CITED]
using var conn = new SqlConnection(_cs);
await conn.OpenAsync(ct);
using var tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

// UPDLOCK prevents any other session from reading balance until this tx commits/rolls back
var available = await conn.ExecuteScalarAsync<decimal>(
    @"SELECT ISNULL(SUM(SignedAmount), 0)
      FROM payment.WalletTransactions WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
      WHERE WalletId = @WalletId",
    new { cmd.WalletId }, tx);

if (available < cmd.Amount)
    throw new InsufficientWalletBalanceException(cmd.WalletId, available, cmd.Amount);

await conn.ExecuteAsync(
    @"INSERT INTO payment.WalletTransactions
        (WalletId, BookingId, EntryType, Amount, Currency, IdempotencyKey)
      VALUES (@WalletId, @BookingId, 1 /*Reserve*/, @Amount, @Currency, @IdemKey)",
    new { cmd.WalletId, cmd.BookingId, cmd.Amount, cmd.Currency,
          IdemKey = $"booking-{cmd.BookingId}-reserve" }, tx);

await tx.CommitAsync(ct);
```

**Why `UPDLOCK, ROWLOCK, HOLDLOCK`:**
- `UPDLOCK` — takes an update-intent lock (blocks other UPDLOCK readers + writers, permits shared reads elsewhere).
- `ROWLOCK` — hint to engine to lock at row granularity (avoid page-level escalation).
- `HOLDLOCK` — equivalent to SERIALIZABLE for that statement; prevents phantom inserts into the wallet's ledger window.

`Commit` / `Release` on the same wallet also use this pattern; `TopUp` does not need to read balance first.

### Pattern 6: TTL Monitor (IHostedService, 5-min poll)

```csharp
public class TtlMonitorHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BookingSagaDbContext>();
            var bus = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            var soonThreshold = DateTime.UtcNow.AddHours(24);
            var urgentThreshold = DateTime.UtcNow.AddHours(2);

            // Emit 24h warning
            var warnings = await db.BookingSagaStates
                .Where(s => s.CurrentState != (int)BookingStates.Confirmed
                         && s.CurrentState != (int)BookingStates.Failed
                         && s.TicketingDeadlineUtc <= soonThreshold
                         && !s.Warn24HSent)
                .ToListAsync(ct);

            foreach (var s in warnings)
            {
                await bus.Publish(new TicketingDeadlineApproaching(s.CorrelationId, "24h"), ct);
                s.Warn24HSent = true;
            }
            // Same pattern for 2h warnings.

            await db.SaveChangesAsync(ct);
            await Task.Delay(PollInterval, ct);
        }
    }
}
```

The hard-timeout at TTL minus 2 minutes is handled by **MassTransit `Schedule`** scheduled at `BookingInitiated`, not by this poll loop. The poll loop only emits the advisory 24h/2h warning notifications.

### Pattern 7: Fare Rule Parser (TTL extraction)

**What:** A chain-of-responsibility parser: primary regex tries common IATA formats; per-GDS adapters handle vendor quirks; fallback returns `parseResult = null` which the saga converts to 2-hour default + ops alert (D-07).

**Known IATA-style formats (to build regex fixtures from real Phase 2 payloads):**

| Source | Typical free-text format | Example |
|--------|--------------------------|---------|
| Amadeus (TST) | `TICKETING DEADLINE ...` line in FareRules response + structured `lastTicketingDate` field in flight-offer JSON | `"lastTicketingDate": "2026-04-18"` |
| Amadeus (free-text rules) | `TICKET BY DDMMMM HH:MM` | `TICKET BY 18APR 23:59` |
| Sabre | `TKT TL DD-MMM-YY` | `TKT TL 18-APR-26` |
| Galileo / Travelport | `T.TAU/DDMMM` | `T.TAU/18APR` |

**Strategy:**
1. Prefer the **structured** field whenever the GDS provides one (Amadeus `lastTicketingDate`, Sabre `PricingInfo/TicketingAgency/TimeLimit`). Regex is only for free-text.
2. Per-GDS adapters live in `BookingService.Application/Services/FareRuleParsers/{Amadeus,Sabre,Galileo}Parser.cs`. Register as keyed DI services.
3. Primary regex:
   ```csharp
   // TICKET BY 18APR 23:59 or TICKET BY 18 APR 2026 23:59
   private static readonly Regex TicketByRegex = new(
       @"TICKET\s+BY\s+(?<day>\d{1,2})\s*(?<month>[A-Z]{3,})\s*(?<year>\d{2,4})?\s+(?<hour>\d{2}):(?<minute>\d{2})",
       RegexOptions.IgnoreCase | RegexOptions.Compiled);
   ```
4. **Parse failure path (D-07):** return `TicketingDeadline = now + 2h` and publish `FareRuleParseFailedAlert` (ops email).

**[MEDIUM confidence]** — Phase 2 research confirmed Amadeus provides structured `lastTicketingDate`; exact Sabre/Galileo formats need verification against real Phase 2 adapter responses. Phase 3 planning task should budget a spike to capture 5-10 real fare-rule payloads per GDS for regex tuning.

### Pattern 8: OTel SensitiveAttributeProcessor

```csharp
public class SensitiveAttributeProcessor : BaseProcessor<Activity>
{
    private static readonly HashSet<string> ForbiddenKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "card.number", "card.cvv", "card.expiry", "cvv", "pan",
        "stripe.raw_payment_method", "stripe.raw_body",
        "passport.number", "passport.raw", "document.number"
    };
    private static readonly Regex ForbiddenKeyPattern =
        new(@"^(card\.|stripe\.raw_|passport\.)", RegexOptions.IgnoreCase);

    public override void OnEnd(Activity activity)
    {
        foreach (var tag in activity.TagObjects.ToList())
        {
            if (ForbiddenKeys.Contains(tag.Key) || ForbiddenKeyPattern.IsMatch(tag.Key))
                activity.SetTag(tag.Key, "[REDACTED]");
        }
    }
}
```

Registered once in `TBE.Common` and added by every service:
```csharp
services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource("*")
        .AddProcessor<SensitiveAttributeProcessor>()   // MUST be before exporter
        .AddOtlpExporter());
```

### Pattern 9: AES-256-GCM Field Encryption

```csharp
// Use AesGcm (authenticated encryption) not Aes CBC [CITED: learn.microsoft.com/dotnet/api/System.Security.Cryptography.AesGcm]
public class AesGcmFieldEncryptor
{
    private readonly IEncryptionKeyProvider _keyProvider;

    public byte[] Encrypt(string plaintext)
    {
        var key = _keyProvider.GetActiveKey();       // 32 bytes for AES-256
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, tagSizeInBytes: 16);
        aes.Encrypt(nonce, plain, cipher, tag);

        // Output format: [1-byte keyVersion][12-byte nonce][16-byte tag][N-byte cipher]
        var result = new byte[1 + 12 + 16 + cipher.Length];
        result[0] = _keyProvider.ActiveKeyVersion;
        Buffer.BlockCopy(nonce, 0, result, 1, 12);
        Buffer.BlockCopy(tag, 0, result, 13, 16);
        Buffer.BlockCopy(cipher, 0, result, 29, cipher.Length);
        return result;
    }
    // Decrypt mirrors.
}
```

**Key versioning** (byte 0): enables zero-downtime rotation in Phase 7 when KMS/Key Vault replaces `EnvEncryptionKeyProvider`.

### Pattern 10: Notification Consumer + Idempotency

```csharp
public class BookingConfirmedConsumer : IConsumer<BookingConfirmed>
{
    public async Task Consume(ConsumeContext<BookingConfirmed> ctx)
    {
        var key = new EmailIdempotencyKey(ctx.Message.EventId, "FlightConfirmation");
        if (await _idemLog.AlreadySent(key, ctx.CancellationToken))
            return;

        var vm = await _viewModelBuilder.ForFlightConfirmation(ctx.Message);
        var html = await _razor.CompileRenderAsync("FlightConfirmation", vm);
        var pdf = _eTicketPdf.Generate(vm);       // QuestPDF bytes

        await _email.SendAsync(new EmailMessage
        {
            To = vm.CustomerEmail,
            Subject = $"Your booking {vm.BookingReference} is confirmed",
            HtmlBody = html,
            Attachments = [new("e-ticket.pdf", "application/pdf", pdf)]
        }, ctx.CancellationToken);

        await _idemLog.RecordSent(key, ctx.CancellationToken);
    }
}
```

**EmailIdempotencyLog schema:**
```sql
CREATE TABLE notification.EmailIdempotencyLog (
    Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    EventId       UNIQUEIDENTIFIER NOT NULL,
    EmailType     NVARCHAR(100) NOT NULL,
    SentAtUtc     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    MessageId     NVARCHAR(200) NULL,                -- SendGrid X-Message-Id
    CONSTRAINT UX_EmailIdem UNIQUE(EventId, EmailType)
);
```

### Anti-Patterns to Avoid

- **Capturing Stripe before ticket number returned.** Breaks FLTB-05. Enforced by `capture_method: manual` at Authorize time — capture call only issued from `TicketIssued` state transition.
- **Trusting client redirect as payment confirmation.** Breaks PAY-02. All saga transitions driven by webhook events only.
- **Random/GUID Stripe idempotency keys.** Breaks D-13 replay safety. Always `booking-{id}-{op}`.
- **EF Core `SaveChanges` for wallet deduction.** EF Core cannot emit `UPDLOCK, ROWLOCK, HOLDLOCK` reliably. Use Dapper in a `SqlTransaction`.
- **Mutable `balance` column on Wallet.** Breaks D-14 / PAY-05.
- **Per-service ad-hoc log/span scrubbing.** D-09 locks this in `TBE.Common`; never reinvent.
- **Email send on the saga critical path.** Breaks 60s SLA and couples two failure modes. Notifications are always async consumers of events.
- **Pessimistic saga concurrency.** CONTEXT D-01 locks Optimistic; do not revert to Pessimistic even though older ARCHITECTURE.md suggested it.
- **Storing full Stripe webhook JSON in OTel spans or logs.** Breaks COMP-06; scrubbing processor handles it but don't rely on that — treat webhook body as sensitive from the first line of code that receives it.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Distributed saga orchestration | Bespoke state machine over RabbitMQ | **MassTransit state machine** | Compensation, correlation, timeout scheduling, outbox — all baked in. DIY gets concurrency wrong. |
| Saga state persistence | Hand-rolled JSON-in-column | **MassTransit.EntityFrameworkCore repository** | Handles optimistic concurrency, retries, migrations. Official MT-maintained. |
| Stripe HTTP client | Raw `HttpClient` calls | **Stripe.net SDK** | Auto-handles idempotency headers, webhook signature, enum evolution, API versioning. |
| Stripe webhook signature | Custom HMAC | `EventUtility.ConstructEvent` | Correct timing-safe compare + 5-min tolerance default. Do NOT use tolerance=0. |
| PDF generation | `iTextSharp` / manual PDF bytes | **QuestPDF** | Fluent API, PDF/A compliance. Mind the commercial license threshold. |
| HTML email templating | `string.Format` / raw StringBuilder | **RazorLight** | D-17 locked; strongly-typed view models. |
| AES encryption | `Aes` CBC + manual HMAC | `AesGcm` (built-in .NET 8) | Authenticated encryption in one primitive. Prevents padding-oracle and malleability. |
| SQL row locking | EF Core tracking | Dapper + explicit `UPDLOCK, ROWLOCK, HOLDLOCK` | EF does not emit table hints reliably. |
| Retry/backoff | Manual `Task.Delay` loop | MassTransit `UseMessageRetry(r => r.Exponential(...))` | Per-consumer retry config, respects cancellation, integrates with outbox redelivery. |
| OpenTelemetry span filter | Custom exporter middleware | `BaseProcessor<Activity>` | Standard OTel extension point; runs before any exporter. |
| Scheduled saga timeout | Polling loop over state table | MassTransit `Schedule<,>` + Quartz or RabbitMQ delayed exchange | Cancellable via `Unschedule`; survives service restart. |
| Cryptographic key storage (dev) | Hard-coded in appsettings | `.env` + `IConfiguration` + `IEncryptionKeyProvider` abstraction | Swap to Key Vault in Phase 7 without code change (D-08). |
| Idempotent event consumer | Bare `try/catch` | Idempotency-log pattern keyed on `(EventId, EmailType)` | RabbitMQ is at-least-once; consumers MUST be idempotent. |

**Key insight:** Phase 3 is the most distributed-systems-heavy phase in the project. Every custom solution is a hidden concurrency bug. The listed libraries are the only acceptable primitives; every "we'll just write this ourselves" is a rewrite waiting to happen.

---

## Runtime State Inventory

Phase 3 is greenfield — not a rename/refactor/migration. **Section omitted per template guidance.**

---

## Common Pitfalls

### Pitfall 1: Capturing before ticket number exists
**What goes wrong:** A developer moves capture into the `Authorizing` state transition for "simplicity," breaks FLTB-05.
**Root cause:** Not internalizing that `capture_method: manual` is the ONLY reason this requirement is satisfiable safely.
**Avoidance:** Capture call appears exclusively in the `TicketIssued` → `Capturing` transition. Code review rule: grep for `PaymentIntents.CaptureAsync` must show exactly one call site, in `CapturePaymentConsumer.cs`.
**Warning signs:** Test scenario where GDS ticketing fails and customer is charged anyway.

### Pitfall 2: Saga state corruption from optimistic concurrency retry storm
**What goes wrong:** Under high event load, multiple events for the same booking arrive simultaneously; optimistic concurrency rejects one, MT retries, but the retry sees stale state.
**Root cause:** `ISagaVersion` not implemented, or `RowVersion` not configured in EF mapping.
**Avoidance:** `BookingSagaState : SagaStateMachineInstance, ISagaVersion`; EF `Property(s => s.Version).IsRowVersion()` in `SagaClassMap`. MT then retries cleanly on conflict.
**Warning signs:** Saga ends in impossible state combinations in production; logs show "Saga version conflict" with no retry recovery.

### Pitfall 3: Wallet double-spend under concurrent agent bookings
**What goes wrong:** Two agents at one agency click "Book" within ms of each other; both reads see the same balance.
**Root cause:** Using EF Core `SELECT` without UPDLOCK, or isolation level too low.
**Avoidance:** Dapper + `UPDLOCK, ROWLOCK, HOLDLOCK` in a `BeginTransaction` block. Integration test with concurrent 50× booking attempts on a wallet with 30× capacity should yield exactly 30 successes.
**Warning signs:** Wallet balance goes negative in UAT.

### Pitfall 4: Stripe webhook replay / double-processing
**What goes wrong:** Stripe retries webhook delivery (their retry policy is aggressive); saga advances twice.
**Root cause:** No `event.Id` idempotency check before publishing to bus.
**Avoidance:** Persist `event.Id` to a dedicated `ProcessedStripeEvents` table with a unique constraint; check before publish; HTTP 200 if already recorded. Plus saga consumers themselves idempotent (MT outbox helps).
**Warning signs:** Duplicate `PaymentCaptured` entries in saga event log.

### Pitfall 5: TTL parse silently produces `DateTime.MinValue`
**What goes wrong:** Parser regex doesn't match, returns default struct, saga schedules timeout in the far past, fires immediately, voids a perfectly good PNR.
**Root cause:** Using `DateTime` (struct, non-null default) instead of `DateTime?` from the parser; no explicit fallback path.
**Avoidance:** Parser signature `bool TryParse(string raw, out DateTime deadline)`. On `false`, fall back to D-07 (now + 2h). Never allow a deadline in the past.
**Warning signs:** PNRs auto-voided within seconds of creation.

### Pitfall 6: Email sent twice on consumer redelivery
**What goes wrong:** Consumer crashes after SendGrid call but before ack; RabbitMQ redelivers; customer gets two confirmation emails.
**Root cause:** Idempotency log check missing or not transactionally aligned with the email send.
**Avoidance:** Record intent BEFORE send (with status `InFlight`), update on success; if redelivered and log shows `InFlight`, treat as sent (accept small chance of dropped email over high chance of double email). Unique `(EventId, EmailType)` constraint.
**Warning signs:** Customers reporting duplicate emails; SendGrid dashboard showing double sends.

### Pitfall 7: PCI scope creep via generic logging middleware
**What goes wrong:** A Phase 1 logging middleware logs request bodies for all incoming HTTP. PaymentElement confirmation uses Stripe.js client-side so no raw card hits server — BUT the webhook body contains payment_method details.
**Root cause:** Blanket request logging; webhook body not considered sensitive.
**Avoidance:** Explicit allowlist of routes with body logging enabled; `/webhooks/stripe` explicitly excluded. `SensitiveAttributeProcessor` on OTel as second line of defence.
**Warning signs:** PCI SAQ-A self-assessment fails at the logging question.

### Pitfall 8: QuestPDF commercial license gap at launch
**What goes wrong:** Dev team uses Community license throughout build; go-live happens; company is at $3M revenue; technically non-compliant.
**Root cause:** License threshold ($1M ARR) not surfaced as a cost/procurement item.
**Avoidance:** Plan task: "Procure QuestPDF Professional/Business license before production deployment." Phase 3 code doesn't change but the license key does get registered in startup: `QuestPDF.Settings.License = LicenseType.Community;` → `LicenseType.Professional;`.
**Warning signs:** Legal/procurement reviews hit QuestPDF in dependency scan.

### Pitfall 9: Saga `Schedule` token lost across service restart
**What goes wrong:** `HardTimeout` is scheduled via RabbitMQ delayed exchange; service restart; delayed message still delivered but saga state has the wrong `TimeoutTokenId` or was advanced already.
**Root cause:** Not using `Unschedule` in success paths or on state transitions.
**Avoidance:** Every transition OUT of an in-flight state that reaches `Confirmed` or `Failed` calls `.Unschedule(HardTimeout)`. Also store `TimeoutTokenId` in state so `Unschedule` works across restarts.
**Warning signs:** Confirmed bookings auto-cancel 2 hours later.

### Pitfall 10: 60-second email SLA broken by synchronous PDF in consumer
**What goes wrong:** QuestPDF generation of a 4-page itinerary takes 1-2s under load; SendGrid API call takes 500-1500ms; if consumer is single-threaded per queue, backlog builds and SLA slips.
**Avoidance:** Notification consumer has `PrefetchCount` tuned so that multiple emails process concurrently; PDF generation happens in-consumer (not on saga thread); SendGrid client configured with HTTP/2 and connection reuse. Integration test measures wall-clock `BookingConfirmed → email-sent` end-to-end.
**Warning signs:** UAT measurement above 60s at concurrency 10.

---

## 60-Second SLA Budget (FLTB-08 / NOTF-01)

Where time actually goes, measured against the 60-second wall-clock budget from `PaymentCaptured` (webhook) to email-delivered-to-inbox:

| Stage | Typical | Budget | Notes |
|-------|---------|--------|-------|
| Stripe webhook → bus publish | ~50ms | 200ms | In-process |
| RabbitMQ hop (webhook consumer → saga) | ~20ms | 500ms | Local broker |
| Saga transitions `Capturing → Confirmed` + outbox publish `BookingConfirmed` | ~100ms | 1s | Optimistic concurrency retry if any |
| RabbitMQ hop (saga → NotificationService) | ~20ms | 500ms | |
| View-model build (DB read for passenger + itinerary) | ~200ms | 2s | EF projection |
| RazorLight render | ~100ms | 1s | First render slower (compile) — pre-warm at startup |
| QuestPDF e-ticket gen | ~500ms | 3s | Single-page e-ticket is lightweight |
| SendGrid API call | ~800ms | 5s | Includes 3DS-signed TLS handshake |
| SendGrid → ESP → mailbox delivery | 2-30s | 45s | **Longest and least predictable** — outside our control |
| **Total** | ~4-35s | ~58s | Leaves ~2s margin |

**Enforcement strategy:**
- Saga critical path timeouts: Stripe auth call `CancellationTokenSource(TimeSpan.FromSeconds(15))`; GDS ticket issue `TimeSpan.FromSeconds(30)` (GDS can be slow).
- Notification consumer pre-warms RazorLight templates at startup (compile each template once).
- QuestPDF `Settings.EnableCaching = true`.
- SendGrid `SandboxMode = false` in prod; `HttpClient` reused via `IHttpClientFactory`.
- Metric: emit `booking.email.latency_ms` histogram from `BookingConfirmedConsumer` — alert on p95 > 45000ms.

---

## Testing Strategy

### MassTransit `TestHarness` for saga
```csharp
// Source: masstransit.io/documentation/concepts/testing [CITED]
[Test]
public async Task Should_complete_happy_path()
{
    await using var provider = new ServiceCollection()
        .AddMassTransitTestHarness(x =>
        {
            x.AddSagaStateMachine<BookingSagaStateMachine, BookingSagaState>()
                .InMemoryRepository();   // swap EF for tests
        })
        .BuildServiceProvider(true);

    var harness = provider.GetRequiredService<ITestHarness>();
    await harness.Start();

    var bookingId = NewId.NextGuid();
    await harness.Bus.Publish(new BookingInitiated(bookingId, /*...*/));

    Assert.That(await harness.Consumed.Any<BookingInitiated>(), Is.True);

    // Simulate downstream events
    await harness.Bus.Publish(new PriceReconfirmed(bookingId));
    await harness.Bus.Publish(new PnrCreated(bookingId, "ABC123"));
    // etc.

    var sagaHarness = harness.GetSagaStateMachineHarness<BookingSagaStateMachine, BookingSagaState>();
    Assert.That(await sagaHarness.Exists(bookingId, m => m.Confirmed), Is.Not.Null);
}
```

**Test matrix:**
1. Happy path end-to-end.
2. Each step's failure → correct compensation chain.
3. Concurrent event delivery (two `PnrCreated` events) → one processed, one ignored.
4. TTL timeout fires → compensation chain + `BookingExpired`.
5. Stripe idempotency key reuse → no double-charge.
6. Wallet: 50 concurrent reservations for a wallet with N capacity → exactly N succeed.
7. Webhook replay → saga advances once.

### Stripe test mode
- Use `pk_test_*` / `sk_test_*` keys; `stripe listen --forward-to localhost:5000/webhooks/stripe` for local webhook routing.
- Test cards for scenarios: `4242 4242 4242 4242` (success), `4000 0027 6000 3184` (3DS challenge), `4000 0000 0000 9995` (insufficient funds).
- Stripe CLI `stripe trigger payment_intent.amount_capturable_updated` to simulate webhook.

### GDS sandbox
- Reuse Phase 2's Amadeus test endpoint credentials. Phase 2 research confirms the sandbox returns structured `lastTicketingDate` — valid for TTL testing.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 8 SDK | All services | Assumed (Phase 1) | 8.0.x | — |
| MSSQL | Saga state, wallet, payment, notification | Assumed (Phase 1 Docker Compose) | 2022 | — |
| RabbitMQ | MassTransit transport | Assumed (Phase 1) | 3.13 | — |
| Redis | Idempotency cache, Phase 2 carry-over | Assumed (Phase 1) | 7.2 | — |
| Stripe test account | Dev/test PaymentIntents | Must be created in Phase 3 task | n/a | Stripe CLI local mode for unit tests |
| SendGrid account | Email delivery | Must be created; API key in `.env` | n/a | MailHog/Papercut SMTP fallback per D-16 |
| QuestPDF Community license | PDF gen dev | In-code opt-in | 2026.2.4 | — |
| QuestPDF Professional license | PDF gen prod | **Procurement task** — revenue-dependent | Commercial | Dev can proceed on Community |
| Stripe webhook signing secret | Webhook validation | Obtained from Stripe dashboard; `.env` | n/a | — |

**Missing dependencies with no fallback:** None that block Phase 3 development; procurement of SendGrid + QuestPDF licenses happens in parallel.

**Missing dependencies with fallback:** SendGrid vs. MailHog — dev ships with SMTP fallback per D-16, no blocker.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | `xunit` + `FluentAssertions` (assumed from phase-1/2 convention — verify at Wave 0) |
| Saga harness | `MassTransit.Testing` 9.1.0 |
| Integration tests | `Microsoft.AspNetCore.Mvc.Testing` + `Testcontainers.MsSql` + `Testcontainers.RabbitMq` |
| Quick run | `dotnet test src/services/BookingService/BookingService.Tests -v minimal` |
| Full suite | `dotnet test TBE.sln -v minimal` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | Exists? |
|--------|----------|-----------|-------------------|---------|
| FLTB-04 | Saga happy path | integration (MT TestHarness) | `dotnet test --filter FullyQualifiedName~BookingSagaHappyPathTests` | ❌ Wave 0 |
| FLTB-05 | PNR created before capture | integration | `dotnet test --filter CaptureOnlyAfterTicket` | ❌ Wave 0 |
| FLTB-06 | TTL hard-timeout fires | integration (MT harness + scheduler) | `dotnet test --filter TtlTimeoutTests` | ❌ Wave 0 |
| FLTB-07 | Each step's compensation chain | unit + integration | `dotnet test --filter Compensation` | ❌ Wave 0 |
| FLTB-08 | Email delivered within 60s | integration (wall-clock) | `dotnet test --filter Sla60sTests` | ❌ Wave 0 |
| PAY-02 | Webhook-only confirmation | integration (simulated webhook) | `dotnet test --filter WebhookDrivenTests` | ❌ Wave 0 |
| PAY-06 | Wallet concurrency | integration (parallel tasks) | `dotnet test --filter WalletConcurrencyTests` | ❌ Wave 0 |
| COMP-01 | No card data in logs | static scan + integration (log assertion) | `dotnet test --filter NoCardDataInLogs` | ❌ Wave 0 |
| COMP-02 | Passport encrypted at rest | integration (DB blob inspection) | `dotnet test --filter PassportEncryptionTests` | ❌ Wave 0 |
| COMP-06 | OTel span scrubbing | unit on processor | `dotnet test --filter SensitiveAttributeProcessorTests` | ❌ Wave 0 |
| D-13 | Idempotent Stripe calls | unit (mocked Stripe client asserts header) | `dotnet test --filter StripeIdempotencyKeyTests` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test src/services/BookingService/BookingService.Tests -v minimal` (fast — unit only).
- **Per wave merge:** `dotnet test TBE.sln -v minimal` (includes Testcontainers integration tests, ~2-5 min).
- **Phase gate:** Full suite green + manual UAT per ROADMAP Phase 3 criteria.

### Wave 0 Gaps
- [ ] `src/services/BookingService/BookingService.Tests/` project — does not exist; create with xUnit + MT.Testing references.
- [ ] `src/services/PaymentService/PaymentService.Tests/` project.
- [ ] `src/services/NotificationService/NotificationService.Tests/` project.
- [ ] `tests/TBE.Integration.Tests/` project for cross-service Testcontainers scenarios.
- [ ] Shared `WebApplicationFactory` fixtures per service.
- [ ] CI pipeline stage invoking `dotnet test` with test-result publishing (deferred to Phase 7 per ROADMAP, but local `dotnet test` must be green at every phase gate).

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|------------------|
| V2 Authentication | yes | Keycloak OIDC (Phase 1); `[Authorize]` on every Phase 3 endpoint (COMP-04) |
| V3 Session Management | yes | Stateless JWT; short access-token TTL (15 min, per ARCHITECTURE.md) |
| V4 Access Control | yes | `[Authorize(Roles = "customer")]` / role checks; customer can only read own bookings — enforce via `customerId == JwtClaim("sub")` in `BookingsController.GetById` |
| V5 Input Validation | yes | **FluentValidation** on every command DTO; server-side validation of pax counts, dates, IATA codes |
| V6 Cryptography | yes | AES-256-GCM via `AesGcm` (D-08); never hand-roll; keys via `IEncryptionKeyProvider` |
| V7 Error Handling | yes | Problem Details (`ProblemDetails` built-in .NET 8); never leak stack traces or saga internals to client |
| V8 Data Protection | yes | PII encrypted at rest (passports); PCI boundary strictly Stripe-side (D-10) |
| V9 Communication | yes | TLS enforced at gateway (Phase 1); webhook signature verification (D-12) |
| V10 Malicious Code | partial | Dependabot/npm-audit/nuget-audit in CI (deferred to Phase 7) |
| V11 Business Logic | yes | Wallet overdraft prevention; idempotent saga steps; no double-capture |
| V12 Files & Resources | yes | PDF attachments served from in-memory bytes only; never write to disk |
| V13 API & Web Service | yes | Rate limiting at YARP (Phase 1); webhook IP allowlist optional |

### Known Threat Patterns for .NET / MassTransit / Stripe stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Webhook spoofing | Spoofing | `EventUtility.ConstructEvent` signature check + 5-min tolerance |
| Card-data logging | Information Disclosure | `SensitiveAttributeProcessor` + explicit log-body allowlist (D-09) |
| Wallet race condition → negative balance | Tampering | `UPDLOCK, ROWLOCK, HOLDLOCK` + append-only log (D-14/15) |
| Replay of booking command | Repudiation/Tampering | Saga `CorrelateById`; a second `BookingInitiated` with same id is rejected |
| Saga state tampering | Tampering | DB-level access control; saga schema isolated (D-01); audit via `BookingEvents` |
| SQL injection via raw Dapper | Tampering | Parameterized queries only; no string concat |
| JWT forgery | Spoofing | JWKS validation at gateway; token signature verified at every service |
| GDPR deletion corrupting audit | Compliance | Tombstone passenger record (null out PII, keep row); preserve `BookingEvents` (COMP-03) |
| Unencrypted passport in DB | Information Disclosure | AES-GCM byte[] column; pre-save hook in `BookingDbContext` (COMP-02) |
| OTel exports leak PII to observability backend | Information Disclosure | Processor redacts before exporter (COMP-06) |

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Stripe Charges API | **Payment Intents** (with `capture_method`) | 2019+, firmly settled by 2023 | Only API with native 3DS/SCA; legacy Charges deprecated for new builds |
| Hand-rolled webhook HMAC | `EventUtility.ConstructEvent` | Stripe.net 40+ | Built-in tolerance, constant-time compare |
| MassTransit Pessimistic saga concurrency | **Optimistic + `ISagaVersion`** | MassTransit 8+ | Better throughput; correct retry on conflict |
| Raw `Aes` CBC + HMAC | `AesGcm` | .NET 5+ | One primitive for authenticated encryption |
| IdentityServer4 | **Keycloak** (already locked) | 2022 (Duende licensing) | Avoids $1500+/yr license |
| Ocelot | **YARP** (already locked) | 2022 | Actively maintained |

**Deprecated / outdated:**
- `Polly.Extensions.Http` 3.x — superseded by `Microsoft.Extensions.Http.Resilience`.
- Stripe Charges API for new integrations — use Payment Intents.
- Stripe Redirect-based 3DS flows — Payment Element handles SCA natively.
- `System.Security.Cryptography.Aes` with manual padding — use `AesGcm`.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | xUnit is the project test framework | Validation Architecture | LOW — swap framework at Wave 0 is a one-line change per csproj |
| A2 | Sabre/Galileo TTL formats match the examples shown (`TKT TL DD-MMM-YY`, `T.TAU/DDMMM`) | Pattern 7 | MEDIUM — Phase 3 plan should include a spike to capture real payloads from Phase 2 adapters |
| A3 | The 60s SLA budget numbers approximate real production timing | 60-Second SLA Budget | MEDIUM — must validate with load test before UAT sign-off |
| A4 | Optimistic saga concurrency + MT auto-retry handles concurrent event delivery cleanly | Pattern 1 note | LOW — official MT-documented pattern, widely used in production |
| A5 | RazorLight 2.3.1 (2023) still works on .NET 8 without issue | Standard Stack | MEDIUM — last release is 3+ years old; fallback to Fluid if runtime issues surface |
| A6 | QuestPDF Community license is acceptable for the org during Phase 3 dev | Standard Stack | LOW — procurement is in parallel; Community covers dev/test phase |
| A7 | Stripe idempotency keys scoped to `booking-{id}-{operation}` are unique across retries (one capture per booking) | Pattern 3 | LOW — CONTEXT D-13 locked; Stripe's idempotency contract is 24h cache |
| A8 | `IFareRuleParser` chain-of-responsibility is the right abstraction for multi-GDS quirks | Pattern 7 | LOW — direct application of Phase 2's keyed-DI adapter pattern |
| A9 | Notification consumer prefetch count tuning is sufficient to hit 60s SLA at concurrency 10 | 60-Second SLA Budget | MEDIUM — validate with load test |
| A10 | Phase 2 adapter events can be re-used for `PriceReconfirmed` step (no new adapter contracts needed) | Architectural Responsibility Map | LOW — phase-2 RESEARCH explicitly calls out price-reconfirmation endpoint as in-scope downstream |

---

## Open Questions

1. **Exact Sabre/Galileo ticketing-deadline formats**
   - What we know: Amadeus has structured `lastTicketingDate`; Sabre and Galileo also have structured fields in some response types but free-text in others.
   - What's unclear: Which real response payloads Phase 2 produces for the flight-offer shape used here.
   - Recommendation: First task of the TTL-monitor plan should capture 5-10 real fare-rule payloads per GDS into test fixtures; regex tuning is driven by those fixtures, not speculation.

2. **SendGrid template IDs vs. fully RazorLight-rendered HTML**
   - What we know: D-17 mandates RazorLight; D-16 mandates SendGrid/SMTP.
   - What's unclear: Whether SendGrid's Dynamic Templates feature is used (reducing payload size, deferring HTML to SendGrid) or only its raw SMTP-like send API (sending full RazorLight-rendered HTML in the API body).
   - Recommendation: Use RazorLight for the HTML body and SendGrid's send-mail API (`MailHelper.CreateSingleEmail`) — avoids split template ownership. SendGrid Dynamic Templates becomes a v2 optimization.

3. **OTel exporter choice (OTLP vs Jaeger vs Zipkin)**
   - What we know: CONTEXT marks this as Claude's discretion; Phase 7 installs the collector.
   - What's unclear: Whether the collector is OTel-native (prefer OTLP) or a specific backend (Datadog/Jaeger-native).
   - Recommendation: Ship OTLP exporter — it's the vendor-neutral standard; Phase 7 can reroute via collector config without service changes.

4. **QuestPDF commercial license procurement timing**
   - What we know: Community license threshold is $1M ARR.
   - What's unclear: Current org revenue / whether the org is already above threshold.
   - Recommendation: Raise procurement ticket with license owner in parallel with Phase 3 dev. Code-side change at go-live is one line; business-side change is a PO.

5. **GDPR erasure details for customer-initiated data deletion (COMP-03)**
   - What we know: Must erase PII without destroying audit trail.
   - What's unclear: Whether passport number + DOB on `Passengers` is considered PII that must be erased, and whether encryption-at-rest already satisfies EU regulators' "pseudonymization" standard.
   - Recommendation: Implement tombstone pattern (null-out columns in `Passengers`; preserve `BookingEvents` which reference only booking_id and event metadata); legal review before go-live.

---

## Sources

### Primary (HIGH confidence)
- [NuGet.org — Stripe.net 51.0.0](https://www.nuget.org/packages/Stripe.net) — version + release date verified 2026-04-15
- [NuGet.org — MassTransit 9.1.0](https://www.nuget.org/packages/MassTransit) — verified 2026-04-15
- [NuGet.org — MassTransit.EntityFrameworkCore 9.1.0](https://www.nuget.org/packages/MassTransit.EntityFrameworkCore) — verified 2026-04-15
- [NuGet.org — QuestPDF 2026.2.4](https://www.nuget.org/packages/QuestPDF) — verified 2026-04-15; license terms captured
- [NuGet.org — SendGrid 9.29.3](https://www.nuget.org/packages/SendGrid) — verified 2026-04-15
- [NuGet.org — RazorLight 2.3.1](https://www.nuget.org/packages/RazorLight) — verified 2026-04-15
- [docs.stripe.com — Place a hold on a payment method](https://docs.stripe.com/payments/place-a-hold-on-a-payment-method) — authorize/capture/void flow, auth hold durations, webhook events
- [docs.stripe.com — Webhook signatures](https://docs.stripe.com/webhooks/signatures) — tolerance parameter, HMAC-SHA256 structure
- [masstransit.io — State machine saga](https://masstransit.io/documentation/patterns/saga/state-machine) — core saga DSL
- `.planning/phases/03-core-flight-booking-saga-b2c/03-CONTEXT.md` — all locked decisions
- `.planning/ROADMAP.md` §Phase 3 — plan boundaries and UAT criteria
- `.planning/REQUIREMENTS.md` — FLTB/PAY/NOTF/COMP requirement IDs
- `.planning/research/ARCHITECTURE.md` — service topology, saga placement, DB ownership
- `.planning/research/STACK.md` — stack baseline
- `.planning/research/PITFALLS.md` — domain-level gotchas (PNR TTL, wallet race, saga compensation)
- `.planning/phases/02-inventory-layer-gds-integration/02-RESEARCH.md` — upstream adapter patterns

### Secondary (MEDIUM confidence)
- MassTransit EF Core persistence details (Optimistic vs Pessimistic) — inferred from MT 9.x release notes + CONTEXT D-01; exact docs page returned 404 at research time, so pattern relies on prior MT-8 knowledge carried forward
- IATA ticketing deadline free-text formats — general pattern known; exact per-GDS variants must be verified against Phase 2 live payloads

### Tertiary (LOW confidence — flagged for validation)
- RazorLight 2.3.1 compatibility with .NET 8 at runtime — version is 3+ years old; monitor for AOT/trimming issues in Wave 0 test run
- 60-second SLA budget numbers — ballpark based on typical Stripe/SendGrid latencies; **must be validated with a Phase 3 load test** before UAT sign-off

---

## Metadata

**Confidence breakdown:**
- Saga state machine pattern: HIGH — official MassTransit, verified against docs
- Stripe authorize/capture/webhook: HIGH — verified against docs.stripe.com
- Wallet UPDLOCK pattern: HIGH — PITFALLS.md + SQL Server semantics well-established
- TTL regex patterns: MEDIUM — per-GDS variants need real-payload verification
- 60s SLA budget: MEDIUM — plausible numbers; requires load test validation
- QuestPDF license model: HIGH — verified on questpdf.com
- Version pins: HIGH — all six verified on nuget.org 2026-04-15
- AES-GCM encryption: HIGH — in-box .NET primitive
- Compensation chain ordering: HIGH — D-05 locked + reverse order convention
- Notification idempotency: HIGH — standard consumer pattern

**Research date:** 2026-04-15
**Valid until:** 2026-05-15 (30 days — stack is stable, but re-verify nuget versions before Phase 3 kickoff if delayed)
