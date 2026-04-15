---
phase: 3
slug: core-flight-booking-saga-b2c
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-15
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + MassTransit TestHarness (unit/integration); Testcontainers for SQL Server + RabbitMQ |
| **Config file** | `tests/` solution folder; per-project `.csproj` |
| **Quick run command** | `dotnet test --filter "Category=Unit" --no-restore --nologo` |
| **Full suite command** | `dotnet test --no-restore --nologo` |
| **Estimated runtime** | ~45s quick / ~3m full |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "Category=Unit"` scoped to the affected project
- **After every plan wave:** Run full `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds for quick; 3 minutes for full

---

## Per-Task Verification Map

*Populated by planner during plan generation. Every task must map to a requirement, a threat reference (if applicable), and an automated command or Wave 0 dependency.*

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 03-01-T1 | 03-01 | 1 | FLTB-01..05,08,09,10; COMP-01..04 | T-03-06,07,09,10 | Saga contracts in TBE.Contracts (events + commands, Guid BookingId first) | unit | dotnet build src/shared/TBE.Contracts/TBE.Contracts.csproj /warnaserror | ❌ W0 | ⬜ pending |
| 03-01-T2 | 03-01 | 1 | FLTB-01..05,08,09,10; COMP-01..04 | T-03-06,07,08,09,10 | BookingSaga state machine + EF persistence (Optimistic D-01) + SagaDeadLetter | unit | dotnet test --filter "Category=Unit&FullyQualifiedName~BookingSaga" --no-build --nologo | ❌ W0 | ⬜ pending |
| 03-01-T3 | 03-01 | 1 | FLTB-01,02,08; COMP-04 | T-03-04 | BookingsController [Authorize] + JwtBearer wiring + AddSagaStateMachine | integration | dotnet test --filter "Category=Integration&FullyQualifiedName~BookingsController" --nologo | ❌ W0 | ⬜ pending |
| 03-02-T1 | 03-02 | 2 | PAY-01,02,06,07,08 | T-03-01,02,03,04 | StripePaymentGateway + webhook signature verification (tolerance 300s) + idempotency keys | unit | dotnet test --filter "Category=Unit&FullyQualifiedName~Stripe" --no-build --nologo | ❌ W0 | ⬜ pending |
| 03-02-T2 | 03-02 | 2 | PAY-03,04,05 | T-03-05,06,12 | Wallet ledger Dapper UPDLOCK/ROWLOCK/HOLDLOCK; concurrency test 30/50 success on 30-cap wallet | integration | dotnet test --filter "Category=Integration&FullyQualifiedName~Wallet" --nologo | ❌ W0 | ⬜ pending |
| 03-03-T1 | 03-03 | 3 | COMP-05,06 | T-03-05,11,04,13 | AesGcmFieldEncryptor + SensitiveAttributeProcessor + secrets migration to .env | unit | dotnet test --filter "Category=Unit&(FullyQualifiedName~AesGcmFieldEncryptor\|FullyQualifiedName~SensitiveAttributeProcessor)" --no-build --nologo | ❌ W0 | ⬜ pending |
| 03-03-T2 | 03-03 | 3 | FLTB-06,07 | T-03-05,11,14 | FareRuleParser (Amadeus/Sabre/Galileo adapters) + TtlMonitorHostedService + CreatePnrConsumer 2h fallback | unit | dotnet test --filter "Category=Unit&(FullyQualifiedName~FareRuleParser\|FullyQualifiedName~TtlMonitorHostedService)" --no-build --nologo | ❌ W0 | ⬜ pending |
| 03-04-T1 | 03-04 | 2 | NOTF-01,02,03,04,05,06 | T-03-15,16 | SendGrid/RazorLight/QuestPDF infra + EmailIdempotencyLog unique (EventId,EmailType) | unit | dotnet test --filter "Category=Unit&FullyQualifiedName~NotificationService" --no-build --nologo | ❌ W0 | ⬜ pending |
| 03-04-T2 | 03-04 | 2 | NOTF-01..06 | T-03-15,16,04 | 6 lifecycle consumers + Program.cs wiring + Worker.cs removal | unit+integration | dotnet test --filter "FullyQualifiedName~BookingConfirmedConsumer\|FullyQualifiedName~EmailIdempotency" --nologo | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Booking.Saga.Tests/` — xUnit project with MassTransit.Testing harness fixtures
- [ ] `tests/Payments.Tests/` — Stripe mock client + wallet concurrency fixtures
- [ ] `tests/Notifications.Tests/` — RabbitMQ consumer + SendGrid mock fixtures
- [ ] `tests/Integration/` — Testcontainers fixtures for SQL Server + RabbitMQ
- [ ] Shared test assembly with ClockFixture, StripeTestFixture, GdsSandboxFixture

*If test projects already exist from Phase 2, extend rather than duplicate.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| End-to-end 60s SLA in production-like env | FLTB-10, NOTF-01 | Requires real Stripe test-mode webhook + GDS sandbox PNR + SMTP delivery timing | Run booking flow against staging; measure time from `POST /bookings` to inbox receipt |
| PCI SAQ-A boundary inspection | PAY-03, PAY-04 | Requires log, DB column, and OTel span inspection across services | After test booking, grep logs + query tables + inspect span attributes for card number/CVC patterns |
| GDS TTL regex correctness vs real fare rules | FLTB-07 | Requires real Amadeus/Sabre fare-rule payloads captured from phase 2 sandbox | Feed captured fixtures into parser, verify extracted deadline matches operator expectation |
| QuestPDF e-ticket rendering fidelity | NOTF-04 | Visual correctness | Open generated PDF, verify passenger name, e-ticket number, itinerary, barcode |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s for quick suite
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
