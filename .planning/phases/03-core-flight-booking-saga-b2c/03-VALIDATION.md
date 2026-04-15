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
| TBD | — | — | — | — | — | — | — | ❌ W0 | ⬜ pending |

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
