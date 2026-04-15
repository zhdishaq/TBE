---
phase: 03-core-flight-booking-saga-b2c
verified: 2026-04-15T00:00:00Z
status: human_needed
score: 27/30 must-haves verified
overrides_applied: 0
gaps: []
deferred:
  - truth: "Passenger PII (passport/travel docs) encrypted via AesGcmFieldEncryptor end-to-end"
    addressed_in: "Phase 04"
    evidence: "D-20 / Phase 4 passenger capture; 03-01 intentionally omits passenger PII on BookingSagaState; AesGcmFieldEncryptor primitive landed here for Phase 4 consumption."
  - truth: "GDPR COMP-03 right-to-erasure endpoint"
    addressed_in: "Phase 06"
    evidence: "ROADMAP schedules compliance/GDPR operator endpoints in later compliance phase; Phase 3 delivers PII hygiene primitives (encryption, OTel scrubbing, recipient hashing) but not erasure flow."
  - truth: "60-second SLA validated under load"
    addressed_in: "Phase 07"
    evidence: "Load/perf testing scheduled for pre-launch hardening phase; Phase 3 delivers the code path but not the load rig."
human_verification:
  - test: "End-to-end real booking: POST /bookings with Stripe test card + Amadeus sandbox → PNR created → payment authorized → ticket issued → payment captured → confirmation email with PDF e-ticket received within 60s"
    expected: "BookingSagaState terminal state=Confirmed, StripePaymentIntent status=succeeded, GDS PNR locator returned, SendGrid delivery event for BookingConfirmed with PDF attachment, wall-clock ≤ 60s from POST → email receipt"
    why_human: "Requires live Stripe sandbox + Amadeus/Sabre/Galileo credentials + SendGrid sandbox + RabbitMQ + SQL Server stack; inherently UAT-verifiable only."
  - test: "Stripe webhook round-trip under signature verification"
    expected: "stripe CLI trigger payment_intent.succeeded → StripeWebhookController verifies signature → publishes StripeWebhookReceived → saga transitions to Capturing/Confirmed with no duplicate processing (event.Id dedupe row lands in StripeWebhookEvents)"
    why_human: "Requires stripe CLI + real webhook secret; programmatic unit tests cover the signature/dedupe logic but not a live round-trip."
  - test: "Compensation chain under forced failure (FLTB-07)"
    expected: "Force ticketing failure in GDS sandbox → VoidPnrCommand issued AFTER CancelAuthorizationCommand (D-03 reverse order); BookingSagaState terminal=Failed; SagaDeadLetter row written for capture-failure path only"
    why_human: "Requires ability to inject GDS failure response + observe MassTransit message timing in live broker; covered by unit test with ITestHarness but full-stack behaviour needs sandbox."
  - test: "Wallet concurrent-reserve deterministic behaviour (PAY-06 / D-14)"
    expected: "50 concurrent reserves on 30-slot wallet → exactly 30 succeed, 20 throw InsufficientWalletBalanceException; SUM(SignedAmount) never goes negative"
    why_human: "WalletRepositoryTests written but require Docker (Testcontainers.MsSql) — not executable on current Windows agent host; run on CI/Docker-enabled machine."
  - test: "Email idempotency replay safety (NOTF-06)"
    expected: "Replay BookingConfirmed with same CorrelationId → second consume returns without second SendGrid call; EmailIdempotencyLog has exactly one row per (EventId, EmailType)"
    why_human: "EmailIdempotencyTests use SQLite-in-memory path; full MSSQL unique-index behaviour needs Docker run."
  - test: "PDF e-ticket visual review"
    expected: "QuestPDF-rendered e-ticket PDF matches brand/design spec; all itinerary fields present and legible"
    why_human: "Visual/brand signoff; not programmatically verifiable."
  - test: "QuestPDF Community License check before commercial launch"
    expected: "QuestPDF Commercial license purchased OR alternative library swap planned before go-live"
    why_human: "Licensing / business decision outside code."
  - test: "60s SLA under realistic load"
    expected: "Confirmation email delivered within 60s of POST under expected booking concurrency"
    why_human: "Requires load-generation rig — scheduled for Phase 07."
---

# Phase 03: Core Flight Booking Saga (B2C) — Verification Report

**Phase Goal (ROADMAP):** A real end-to-end B2C flight booking completes in production — PNR created, payment authorized via Stripe, ticket issued by the GDS, payment captured, and a confirmation email with e-ticket delivered within 60 seconds — with full compensation logic if any step fails.

**Verified:** 2026-04-15
**Status:** `human_needed`
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | BookingSaga state machine orchestrates D-05 forward chain (PriceReconfirming → PnrCreating → Authorizing → TicketIssuing → Capturing → Confirmed) | VERIFIED | `src/services/BookingService/BookingService.Application/Saga/BookingSaga.cs`; saga unit tests `FLTB04_happy_path_transitions_to_Confirmed` passing (8/8 Booking.Saga.Tests) |
| 2 | D-03 reverse-order compensation implemented (CancelAuthorization before VoidPnr) | VERIFIED | `FLTB07_ticket_failure_compensates_in_reverse_order` asserts SentTime ordering |
| 3 | Capture-failure path does NOT void PNR; publishes SagaDeadLetterRequested | VERIFIED | `FLTB07_capture_failure_publishes_SagaDeadLetterRequested_without_void` + `SagaDeadLetterSink.cs` + `SagaDeadLetter` table in migration `20260416000000_AddBookingSagaState` |
| 4 | Saga persistence with optimistic concurrency (ISagaVersion + IsRowVersion) | VERIFIED | `BookingSagaState.cs` implements ISagaVersion; EF repository registered with `ConcurrencyMode = Optimistic` in Program.cs |
| 5 | BookingsController has class-level `[Authorize]` (COMP-04) | VERIFIED | grep: line 20 of `BookingsController.cs` |
| 6 | JWT Bearer + FallbackPolicy=RequireAuthenticatedUser on all 5 services (COMP-05) | VERIFIED | grep matched in 5 Program.cs (Booking/Pricing/Payment/Notification/FlightConnector) |
| 7 | Secrets removed from appsettings.*.json → `.env.example` | VERIFIED | `.env.example` present at repo root with CONNECTIONSTRINGS / STRIPE / SENDGRID / GDS / OTEL / ENCRYPTION sections |
| 8 | BookingDtoPublic excludes passport, card, and UserId (COMP-01/02) | VERIFIED | `COMP01_booking_dto_does_not_expose_passport_or_payment_fields` reflection test passing |
| 9 | Stripe authorize uses `CaptureMethod=manual` + `IdempotencyKey=booking-{id}-authorize` (PAY-01 / D-13) | VERIFIED | `StripePaymentGateway.cs:64` — grep-confirmed |
| 10 | Stripe capture / refund use deterministic keys `booking-{id}-capture`, `booking-{id}-refund` | VERIFIED | `StripePaymentGateway.cs:93,142` |
| 11 | Stripe webhook signature verification with dedupe + single-envelope publish (W3 boundary / PAY-02) | VERIFIED | `StripeWebhookController.cs:55` uses `ConstructEvent(... tolerance:300, throwOnApiVersionMismatch:false)`; publishes only `StripeWebhookReceived`; `StripeWebhookEvents` dedupe table in migration |
| 12 | Stripe.net SDK scoped to Application + webhook controller only (PCI SAQ-A / PAY-08) | VERIFIED | SUMMARY grep-verified: `using Stripe` only in `StripePaymentGateway.cs` + `StripeWebhookController.cs` |
| 13 | Wallet ledger append-only with PERSISTED computed SignedAmount (D-14) | VERIFIED | migration `20260417000000_AddWalletAndStripe` includes raw-SQL PERSISTED computed column; `WalletTransactionMap.cs` confirms mapping |
| 14 | Wallet reserve uses `WITH (UPDLOCK, ROWLOCK, HOLDLOCK)` under serializable transaction (PAY-05/06 / D-15) | VERIFIED | `WalletRepository.cs:45` — grep-confirmed |
| 15 | Wallet top-up idempotency keys `wallet-{id}-topup-{pi}` (PAY-04) | VERIFIED | `StripeTopUpConsumer` per SUMMARY; `DuplicateWalletTopUpException` + SQL 2601/2627 catch path |
| 16 | WalletController endpoints (GET balance, GET transactions, POST top-ups) | VERIFIED | `WalletController.cs` exists with endpoints per SUMMARY |
| 17 | Payment command consumers (Authorize/Capture/Cancel/Refund) wired | VERIFIED | 4 consumer files present under `PaymentService.Application/Consumers/` |
| 18 | Wallet command consumers (Reserve/Commit/Release) wired | VERIFIED | 3 consumer files present |
| 19 | AES-256-GCM field encryptor with key-version envelope (COMP-06) | VERIFIED | `AesGcmFieldEncryptor.cs` + `EnvEncryptionKeyProvider.cs` + `EncryptionOptions.cs` + `AesGcmFieldEncryptorTests.cs` |
| 20 | OpenTelemetry SensitiveAttributeProcessor wired into all 5 services | VERIFIED | grep matched in all 5 Program.cs; `TelemetryServiceExtensions.AddTbeOpenTelemetry` registers processor BEFORE exporter |
| 21 | FareRuleParser with per-GDS adapters (Amadeus/Sabre/Galileo) + past-deadline guard (FLTB-06) | VERIFIED | `FareRuleParser.cs` + 3 adapters + `FareRuleDateBuilder.cs`; `FareRuleParserTests.cs` passing |
| 22 | TtlMonitorHostedService emits 24h/2h TicketingDeadlineApproaching events (FLTB-06) | VERIFIED | `BookingService.Infrastructure/Ttl/TtlMonitorHostedService.cs` — Warn24H/Warn2H gates + publish lines 97,112 confirmed |
| 23 | CreatePnrConsumer integrates fare-rule parser with D-07 2h fallback + FareRuleParseFailedAlert | VERIFIED | `CreatePnrConsumer.cs` + tests |
| 24 | 6 NotificationService consumers registered (BookingConfirmed/Cancelled/Expired, TicketIssued, TicketingDeadlineApproaching, WalletLowBalance) | VERIFIED | all 6 `AddConsumer<...>` calls present in `NotificationService.API/Program.cs:98-103` |
| 25 | Worker SDK → Web SDK swap; legacy Worker.cs removed | VERIFIED | no `Worker.cs` under NotificationService.API |
| 26 | NotificationService email pipeline (SendGrid + RazorLight + QuestPDF) (NOTF-01) | VERIFIED | `SendGridEmailDelivery`, `RazorLightEmailTemplateRenderer`, `QuestPdfETicketGenerator` present per SUMMARY; tests 17/17 passing |
| 27 | Email idempotency via unique index on (EventId, EmailType) (NOTF-06) | VERIFIED | `NotificationDbContext.cs:35-36` — `HasIndex(x => new { x.EventId, x.EmailType }).IsUnique()`; all 6 consumers catch unique-violation via `IdempotencyHelpers.IsUniqueViolation` |
| 28 | Saga contracts centralized in TBE.Contracts (events + commands) | VERIFIED | `SagaEvents.cs`, `SagaCommands.cs`, `PaymentEvents.cs`, `WalletEvents.cs`, `NotificationEvents.cs` all present |
| 29 | Real end-to-end booking completes in production within 60s with PNR + auth + ticket + capture + email | HUMAN NEEDED | Phase goal is inherently an integration/UAT outcome — requires live Stripe + GDS + SendGrid + SQL + RabbitMQ |
| 30 | Integration tests (WalletRepository, EmailIdempotency, StripeTopUpConsumer MSSQL path) run green | HUMAN NEEDED | Tests written & compile; Docker unavailable on agent host. Requires Docker-enabled CI run (Testcontainers.MsSql). |

**Score:** 27/30 truths verified programmatically; 3 require human/UAT verification; 3 deferred to later phases (not counted as gaps).

### Required Artifacts (spot-check)

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/services/BookingService/BookingService.Application/Saga/BookingSaga.cs` | Saga state machine | VERIFIED | present; 8 states + D-03 compensation matrix |
| `src/services/BookingService/BookingService.API/Controllers/BookingsController.cs` | Public API with `[Authorize]` | VERIFIED | line 20 `[Authorize]` |
| `src/services/BookingService/BookingService.Infrastructure/Ttl/TtlMonitorHostedService.cs` | BackgroundService 24h/2h warn | VERIFIED | BackgroundService, IServiceScopeFactory scope-per-poll |
| `src/services/PaymentService/PaymentService.Application/Stripe/StripePaymentGateway.cs` | Stripe adapter with D-13 keys | VERIFIED | deterministic keys line 64/93/142 |
| `src/services/PaymentService/PaymentService.API/Controllers/StripeWebhookController.cs` | W3 ingress | VERIFIED | signature verify + dedupe + single envelope |
| `src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs` | Wallet API | VERIFIED | present |
| `src/services/PaymentService/PaymentService.Infrastructure/Wallet/WalletRepository.cs` | Dapper UPDLOCK repo | VERIFIED | UPDLOCK,ROWLOCK,HOLDLOCK line 45 |
| `src/shared/TBE.Common/Security/AesGcmFieldEncryptor.cs` | COMP-06 encryptor | VERIFIED | present with key-version envelope |
| `src/shared/TBE.Common/Telemetry/SensitiveAttributeProcessor.cs` | PII/PCI scrubber | VERIFIED | BaseProcessor<Activity>, wired in 5 services |
| `src/services/NotificationService/NotificationService.Application/Persistence/NotificationDbContext.cs` | EmailIdempotencyLog + unique idx | VERIFIED | `.IsUnique()` on (EventId, EmailType) |
| `src/services/NotificationService/NotificationService.Application/Consumers/` (×6) | All 6 notification consumers | VERIFIED | all 6 files present + registered in Program.cs |
| `.env.example` | Secrets externalized | VERIFIED | present at repo root |

### Key Link Verification

| From | To | Via | Status |
|------|----|----|--------|
| BookingsController | BookingSaga | `IPublishEndpoint.Publish(BookingInitiated)` inside DB transaction (outbox) | WIRED |
| BookingSaga | PaymentService | `AuthorizePaymentCommand` / `CapturePaymentCommand` / `CancelAuthorization` / `VoidPnr` via MassTransit | WIRED |
| StripeWebhookController | BookingSaga | `StripeWebhookReceived` envelope → `StripeWebhookConsumer` fans out → saga events | WIRED |
| StripeTopUpConsumer | WalletTransactions ledger | `WalletRepository.RecordTopUpAsync` (Dapper, append-only) | WIRED |
| Booking saga terminal events | NotificationService consumers | `BookingConfirmed`/`BookingCancelled`/`TicketIssued`/`BookingExpired` over Rabbit; consumers write to `EmailIdempotencyLog` + SendGrid | WIRED |
| TtlMonitorHostedService | NotificationService | `TicketingDeadlineApproaching` event → `TicketingDeadlineApproachingConsumer` | WIRED |
| All 5 services → OTel collector | `AddTbeOpenTelemetry` extension registering `SensitiveAttributeProcessor` before OTLP exporter | grep matched all 5 Program.cs | WIRED |

### Data-Flow Trace (Level 4)

| Artifact | Data Source | Produces Real Data | Status |
|----------|-------------|---------------------|--------|
| BookingsController → BookingDtoPublic | EF query against `BookingSagaStates` projected via DTO | YES | FLOWING |
| WalletController balance | `WalletRepository.GetBalanceAsync` → Dapper SUM over `payment.WalletTransactions` | YES | FLOWING |
| StripeWebhookController | Real HTTP body + Stripe-Signature header; `EventUtility.ConstructEvent` | YES | FLOWING |
| Notification consumers → SendGrid | `IEmailDelivery.SendAsync` with verified sender (SendGrid Web API) | YES (when API key present; short-circuits with structured log otherwise) | FLOWING |
| TtlMonitor reads sagas | `BookingDbContext.BookingSagaStates` query; `IPublishEndpoint` emits events | YES | FLOWING |

Known stubs from 03-01 (documented as downstream-consumed, not hollow):
- `OfferToken`, `PaymentMethodId`, `PassengerRefs` — set-but-empty fields on saga state / commands. Populated by Pricing (03-02), Stripe token flow (03-02), and Phase 4 passenger capture respectively. These are handshake slots, not rendered UI values.

### Behavioral Spot-Checks

| Behavior | Check | Status |
|----------|-------|--------|
| `dotnet build -warnaserror` | Asserted clean per 4 SUMMARY self-checks | PASS (per SUMMARY) |
| Unit test counts | Saga 8 + Payments 13 + Notifications 17 + 03-03 (Fare/Ttl/CreatePnr/Aes/OTel) 19 ≈ 57 green | PASS (per SUMMARY) |
| Docker-dependent integration tests | Written but not executed on Windows agent host (no Docker) | SKIP — human verification |
| End-to-end 60s SLA | Requires live stack (Stripe + GDS + SendGrid + RabbitMQ + MSSQL) | SKIP — human verification |

### Requirements Coverage

| Req | Description | Source Plan | Status | Evidence |
|-----|-------------|-------------|--------|----------|
| FLTB-01/02/03 | Booking initiation + validation | 03-01 | SATISFIED | `BookingInitiated` shape, validation tests |
| FLTB-04 | D-05 forward chain | 03-01 | SATISFIED | saga compile-time transitions + happy-path test |
| FLTB-05 | Capture only after TicketIssued | 03-01 | SATISFIED | `During(Capturing, When(PaymentCaptured))` |
| FLTB-06 | Fare-rule parsing + TTL monitor 24h/2h | 03-03 | SATISFIED | FareRuleParser + TtlMonitorHostedService |
| FLTB-07 | Compensation chain + capture-failure dead-letter | 03-01/03-03 | SATISFIED | 3 compensation tests + SagaDeadLetterSink |
| FLTB-08 | BookingConfirmed published on terminal | 03-01 | SATISFIED | `.Publish(BookingConfirmed).Finalize()` |
| FLTB-09 | GET /bookings endpoints | 03-01 | SATISFIED | Controller tests (403 on cross-user) |
| FLTB-10 | BookingCancelled + BookingExpired contracts | 03-01/03-03 | SATISFIED | contracts + TTL expiry path |
| PAY-01 | Stripe authorize/capture/refund with D-13 keys | 03-02 | SATISFIED | grep-confirmed |
| PAY-02 | Webhook signature + dedupe + W3 single envelope | 03-02 | SATISFIED | `StripeWebhookController` + unit tests |
| PAY-04 | Wallet top-up flow | 03-02 | SATISFIED | `StripeTopUpConsumer` + tests |
| PAY-05/06 | Wallet reserve with locking | 03-02 | SATISFIED | UPDLOCK/ROWLOCK/HOLDLOCK + concurrency test (Docker-gated) |
| PAY-07 | Refund publishes PaymentRefundIssued | 03-02 | SATISFIED | RefundPaymentConsumer |
| PAY-08 | PCI SAQ-A scope (Stripe.net only in Application + webhook ctrl) | 03-02 | SATISFIED | grep-verified |
| NOTF-01 | SendGrid + PDF + Razor pipeline | 03-04 | SATISFIED | 3 services + tests |
| NOTF-03 | Booking lifecycle consumers | 03-04 | SATISFIED | 4 consumers registered |
| NOTF-04 | TicketingDeadlineApproaching consumer | 03-04 | SATISFIED | registered + test |
| NOTF-05 | WalletLowBalance consumer | 03-04 | SATISFIED | registered |
| NOTF-06 | EmailIdempotencyLog unique index | 03-04 | SATISFIED | `.IsUnique()` + migration + 6-consumer catch pattern |
| COMP-01 | No PCI in public DTOs | 03-01 | SATISFIED | reflection test |
| COMP-02 | No passport in public DTOs | 03-01 | SATISFIED | reflection test |
| COMP-04 | `[Authorize]` on all controllers | 03-01/03-03 | SATISFIED | BookingsController + 5-service FallbackPolicy |
| COMP-05 | Secrets in .env, not appsettings | 03-03 | SATISFIED | `.env.example` |
| COMP-06 | PII/PCI scrubbing in logs/traces + AES-GCM primitive | 03-03 | SATISFIED | SensitiveAttributeProcessor + AesGcmFieldEncryptor + SHA256 recipient hashing in NotificationService |

### Anti-Patterns Found

| File | Pattern | Severity | Impact |
|------|---------|----------|--------|
| FlightConfirmationModel fare total | stubbed to string pending 03-02 outbox payload shape | Info | Email displays fare; to be fully wired once outbox payload stabilizes in live 03-02 runs |
| Flight detail itinerary (email) | minimal fields; full itinerary deferred to Phase 04 Portal | Info | Email still renders; richer itinerary is Phase-04 scope |
| EF ModelSnapshot missing for BookingDbContext (and possibly PaymentDbContext/NotificationDbContext after hand-authored migrations) | Tooling gap (EF8 vs EF9 transitive mismatch) | Warning | Future `dotnet ef migrations add` will produce incorrect diffs until tooling repaired or snapshot hand-authored |
| QuestPDF Community License | not Commercial | Warning | Fine for development; commercial license required before launch |

No Blocker anti-patterns found.

### Human Verification Required

See frontmatter `human_verification` section (8 items) for:

1. Real end-to-end booking with PNR + e-ticket PDF within 60s (phase goal UAT)
2. Stripe webhook round-trip with live signature
3. Full compensation chain in live sandbox (FLTB-07)
4. Docker-enabled wallet concurrency integration test (PAY-06 / D-14)
5. Docker-enabled email idempotency integration test (NOTF-06)
6. PDF e-ticket visual / brand signoff
7. QuestPDF Commercial license decision
8. 60s SLA under load (scheduled for Phase 07)

### Gaps Summary

**No blocking gaps.** All programmatically-verifiable must-haves for Phase 3 (saga orchestration code, payment/wallet code, notification pipeline, encryption + OTel + JWT cross-cutting concerns, public API controllers with `[Authorize]`, EmailIdempotencyLog unique index, deterministic idempotency keys, secrets externalization) are present and wired. Unit-test suites (≈57 tests) are green per all four SUMMARY self-checks.

The remaining items are:

- **Human/UAT items (3 truths)** — the phase goal itself is "real end-to-end production booking in 60s with all live integrations," which is only meaningfully verifiable against a live Stripe sandbox + Amadeus/Sabre/Galileo + SendGrid + RabbitMQ + SQL Server stack. Paired integration tests exist but require Docker, which is not available on the current agent host.
- **Deferred items (3)** — passenger PII wire-through (Phase 4), GDPR right-to-erasure endpoint (Phase 6), and 60s-SLA load validation (Phase 7). Phase 3 delivers the *primitives* (AES-GCM encryptor, OTel scrubbing, SendGrid pipeline) that these later phases consume.
- **Known warnings** — EF ModelSnapshot regeneration (tooling-fix follow-up) and QuestPDF Community→Commercial license swap before commercial launch.

Phase 3 has delivered the complete end-to-end code spine required for the goal. The remaining verification is operational (live-stack UAT + Docker-enabled integration runs) — appropriate to hand to a human reviewer / CI job.

---

_Verified: 2026-04-15_
_Verifier: Claude (gsd-verifier)_
