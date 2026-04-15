---
phase: 3
slug: core-flight-booking-saga-b2c
status: approved
nyquist_compliant: true
wave_0_complete: true
created: 2026-04-15
revised: 2026-04-15
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Revised 2026-04-15 to resolve plan-checker blockers B1/B2/B3/B4/B5/B6/B7 and warnings W1..W6.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + MassTransit TestHarness (unit/integration); Testcontainers for SQL Server + RabbitMQ |
| **Config file** | `tests/` solution folder; per-project `.csproj` |
| **Quick run command** | `dotnet test --filter "Category=Unit" --no-restore --nologo` |
| **Full suite command** | `dotnet test --no-restore --nologo` |
| **Estimated runtime** | ≤60s quick / ≤3m full |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "Category=Unit"` scoped to the affected project
- **After every plan wave:** Run full `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** ≤60 seconds for quick; ≤3 minutes for full

---

## Per-Task Verification Map

*Populated and revised by planner 2026-04-15. Every task maps to a requirement, a threat reference (if applicable), and an automated command or Wave 0 dependency.*

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 03-01-T0 | 03-01 | 1 | (Wave 0 infra) | — | Wave 0: xUnit projects + Testcontainers fixtures (ClockFixture, StripeTestFixture, GdsSandboxFixture, MsSqlContainerFixture, RabbitMqContainerFixture) | infra | dotnet build tests/TBE.Tests.Shared/TBE.Tests.Shared.csproj /warnaserror | ✅ W0-provided | ⬜ pending |
| 03-01-T1 | 03-01 | 1 | FLTB-01,02,03,04,05,08,09,10; COMP-01,02,04 | T-03-06,07,09,10 | Saga contracts in TBE.Contracts (events + commands, Guid BookingId first) | unit | dotnet build src/shared/TBE.Contracts/TBE.Contracts.csproj /warnaserror | ✅ W0-provided | ⬜ pending |
| 03-01-T2 | 03-01 | 1 | FLTB-01,02,03,04,05,08,09,10; COMP-01,02 | T-03-06,07,08,09,10 | BookingSaga state machine + EF persistence (Optimistic D-01, ISagaVersion) + SagaDeadLetterRequested + Warn24HSent/Warn2HSent columns + initial migration | unit | dotnet test --filter "Category=Unit&FullyQualifiedName~BookingSaga" --no-build --nologo | ✅ W0-provided | ⬜ pending |
| 03-01-T3 | 03-01 | 1 | FLTB-01,02,08; COMP-04 | T-03-04 | BookingsController [Authorize] + JwtBearer wiring + AddSagaStateMachine + BookingDbContext registration | integration | dotnet test --filter "Category=Integration&FullyQualifiedName~BookingsController" --nologo | ✅ W0-provided | ⬜ pending |
| 03-02-T1 | 03-02 | 2 | PAY-01,02,06,07,08 | T-03-01,02,03,04 | StripePaymentGateway (authorize-then-capture, deterministic idempotency keys) + StripeWebhookController publishes only StripeWebhookReceived envelope (W3 boundary) + signature verification (tolerance 300s) | unit | dotnet test --filter "Category=Unit&FullyQualifiedName~Stripe" --no-build --nologo | ✅ W0-provided | ⬜ pending |
| 03-02-T2 | 03-02 | 2 | PAY-03,05 | T-03-05,06,12 | Wallet ledger Dapper UPDLOCK/ROWLOCK/HOLDLOCK; append-only entries with computed SignedAmount; concurrency test 30/50 success on 30-cap wallet | integration | dotnet test --filter "Category=Integration&FullyQualifiedName~Wallet" --nologo | ✅ W0-provided | ⬜ pending |
| 03-02-T3 | 03-02 | 2 | PAY-04 | T-03-01,05 | Wallet top-up via Stripe PaymentIntent (metadata wallet_id/topup_amount/agency_id) + StripeTopUpConsumer + POST /wallets/{id}/top-ups + idempotency key wallet-{walletId}-topup-{paymentIntentId} | unit+integration | dotnet test --filter "FullyQualifiedName~StripeTopUpConsumer\|FullyQualifiedName~WalletTopUp" --nologo | ✅ W0-provided | ⬜ pending |
| 03-03-T1 | 03-03 | 3 | COMP-05,06 | T-03-05,11,04,13 | AesGcmFieldEncryptor (AES-256-GCM + key-version envelope) + SensitiveAttributeProcessor (OTel PCI/PII redaction) + secrets migration to .env | unit | dotnet test --filter "Category=Unit&(FullyQualifiedName~AesGcmFieldEncryptor\|FullyQualifiedName~SensitiveAttributeProcessor)" --no-build --nologo | ✅ W0-provided | ⬜ pending |
| 03-03-T2a | 03-03 | 3 | FLTB-06 | T-03-05,11 | FareRuleParser + keyed DI adapters (Amadeus/Sabre/Galileo) + per-GDS fixtures (amadeus_sample1.json, sabre_sample1.xml, galileo_sample1.txt) | unit | dotnet test --filter "Category=Unit&FullyQualifiedName~FareRuleParser" --no-build --nologo | ✅ W0-provided | ⬜ pending |
| 03-03-T2b | 03-03 | 3 | FLTB-07 | T-03-14 | TtlMonitorHostedService (polling BookingSagaStates, reads/writes Warn24HSent/Warn2HSent owned by 03-01) + CreatePnrConsumer 2h fallback | unit | dotnet test --filter "Category=Unit&(FullyQualifiedName~TtlMonitorHostedService\|FullyQualifiedName~CreatePnrConsumer)" --no-build --nologo | ✅ W0-provided | ⬜ pending |
| 03-04-T1a | 03-04 | 2 | NOTF-01,03,04,05,06 | T-03-15,16 | RazorLight renderer + QuestPdfETicketGenerator + typed template models + _Header/_Footer branded partials + no `@Html.Raw` (grep guard) | unit | dotnet test --filter "Category=Unit&(FullyQualifiedName~RazorLightEmailTemplateRenderer\|FullyQualifiedName~QuestPdfETicketGenerator)" --no-build --nologo | ✅ W0-provided | ⬜ pending |
| 03-04-T1b | 03-04 | 2 | NOTF-06 | T-03-15,16 | SendGridEmailDelivery + EmailIdempotencyLog unique index (EventId, EmailType) + NotificationDbContext + migration 20260418000000_AddNotificationTables | unit | dotnet test --filter "Category=Unit&(FullyQualifiedName~SendGridEmailDelivery\|FullyQualifiedName~EmailIdempotency)" --no-build --nologo | ✅ W0-provided | ⬜ pending |
| 03-04-T2 | 03-04 | 2 | NOTF-01,03,04,05 | T-03-15,16,04 | 5 lifecycle consumers (BookingConfirmed w/ PDF attachment for NOTF-01, BookingCancelled, TicketingDeadlineApproaching 24h+2h, WalletLowBalance, TicketIssued as part of NOTF-01 flow) + Program.cs wiring + Worker.cs removal | unit+integration | dotnet test --filter "FullyQualifiedName~BookingConfirmedConsumer\|FullyQualifiedName~EmailIdempotency" --nologo | ✅ W0-provided | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

**Requirement coverage (28 IDs):** FLTB-01..10, PAY-01..08, NOTF-01, NOTF-03..06, COMP-01, COMP-02, COMP-04, COMP-05, COMP-06 — each appears in ≥1 task row above. COMP-03 (Phase 6) and NOTF-02 (Phase 4) are intentionally absent.

---

## Wave 0 Requirements

- [x] `tests/Booking.Saga.Tests/` — xUnit project with MassTransit.Testing harness fixtures (provided by 03-01-T0)
- [x] `tests/Payments.Tests/` — Stripe mock client + wallet concurrency fixtures (provided by 03-01-T0)
- [x] `tests/Notifications.Tests/` — RabbitMQ consumer + SendGrid mock fixtures (provided by 03-01-T0)
- [x] `tests/TBE.Tests.Integration/` — Testcontainers fixtures for SQL Server + RabbitMQ (provided by 03-01-T0)
- [x] `tests/TBE.Tests.Shared/` — ClockFixture, StripeTestFixture, GdsSandboxFixture, MsSqlContainerFixture, RabbitMqContainerFixture (provided by 03-01-T0)

*All Wave 0 test-infrastructure scaffolding is owned by Plan 03-01 Task 0 and runs before any other task in the phase.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| End-to-end 60s SLA in production-like env | FLTB-10, NOTF-01 | Requires real Stripe test-mode webhook + GDS sandbox PNR + SMTP delivery timing | Run booking flow against staging; measure time from `POST /bookings` to inbox receipt |
| PCI SAQ-A boundary inspection | PAY-03, COMP-05 | Requires log, DB column, and OTel span inspection across services | After test booking, grep logs + query tables + inspect span attributes for card number/CVC patterns |
| GDS TTL regex correctness vs real fare rules | FLTB-07 | Requires real Amadeus/Sabre fare-rule payloads captured from phase 2 sandbox | Feed captured fixtures into parser, verify extracted deadline matches operator expectation |
| QuestPDF e-ticket rendering fidelity | NOTF-01 | Visual correctness of attached PDF | Open generated e-ticket PDF attached to BookingConfirmed email; verify passenger name, e-ticket number, itinerary, barcode |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency ≤60s for quick suite
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-04-15
