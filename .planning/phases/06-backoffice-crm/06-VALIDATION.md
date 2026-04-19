---
phase: 6
slug: backoffice-crm
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-19
---

# Phase 6 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + Testcontainers (.NET 9) + Playwright (Next.js portals) |
| **Config file** | `tests/TBE.BackofficeService.Tests/TBE.BackofficeService.Tests.csproj` (Wave 0 creates), `tests/TBE.CrmService.Tests/TBE.CrmService.Tests.csproj` (Wave 0 creates); reuse `tests/TBE.BookingService.Tests/` and `tests/TBE.PaymentService.Tests/` |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~Phase06" --no-restore` |
| **Full suite command** | `dotnet test tests/TBE.sln --no-restore` (backend) + `pnpm --filter ./src/portals/backoffice-web test` (portal) |
| **Estimated runtime** | ~90 seconds (quick), ~8 minutes (full backend), +4 minutes portal |

---

## Sampling Rate

- **After every task commit:** Run quick filter against changed project's `Phase06` trait
- **After every plan wave:** Run full `dotnet test tests/TBE.sln` + portal Playwright suite
- **Before `/gsd-verify-work`:** Full suite must be green, DENY UPDATE/DELETE on BookingEvents verified via migration integration test, reconciliation diff round-trip verified
- **Max feedback latency:** 90 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 6-01-01 | 01 | 1 | BO-04 | T-6-01 | BookingEvents rejects UPDATE/DELETE at engine level | integration | `dotnet test --filter "FullyQualifiedName~BookingEventsAppendOnlyTests"` | ❌ W0 | ⬜ pending |
| 6-01-02 | 01 | 1 | BO-05 | — | Each event persists full Snapshot JSON + pricing breakdown | integration | `dotnet test --filter "FullyQualifiedName~BookingEventsSnapshotTests"` | ❌ W0 | ⬜ pending |
| 6-01-03 | 01 | 2 | BO-03 | T-6-02 | Staff cancel/modify logs reason, writes BookingEvents row, requires auth | integration | `dotnet test --filter "FullyQualifiedName~StaffCancelModifyTests"` | ❌ W0 | ⬜ pending |
| 6-01-04 | 01 | 2 | D-39 | T-6-03 | Manual wallet credit 4-eyes: no self-approval, atomic write | integration | `dotnet test --filter "FullyQualifiedName~ManualWalletCreditFourEyesTests"` | ❌ W0 | ⬜ pending |
| 6-01-05 | 01 | 3 | BO-09, BO-10 | T-6-04 | DLQ consumer persists `_error` envelopes; requeue preserves CorrelationId | integration | `dotnet test --filter "FullyQualifiedName~DeadLetterQueueTests"` | ❌ W0 | ⬜ pending |
| 6-01-06 | 01 | 4 | BO-01 | — | Unified booking list returns B2C+B2B+Manual channels w/ RBAC filter | integration | `dotnet test --filter "FullyQualifiedName~UnifiedBookingListTests"` | ❌ W0 | ⬜ pending |
| 6-02-01 | 02 | 1 | BO-02 | T-6-05 | Manual booking creates Channel=Manual with no GDS calls, saga bypassed | integration | `dotnet test --filter "FullyQualifiedName~ManualBookingEntryTests"` | ❌ W0 | ⬜ pending |
| 6-02-02 | 02 | 2 | BO-07 | — | Supplier contract CRUD + validity windows enforced | integration | `dotnet test --filter "FullyQualifiedName~SupplierContractTests"` | ❌ W0 | ⬜ pending |
| 6-02-03 | 02 | 3 | BO-06 | — | Reconciliation diff surfaces orphan Stripe events + ledger drift | integration | `dotnet test --filter "FullyQualifiedName~PaymentReconciliationTests"` | ❌ W0 | ⬜ pending |
| 6-03-01 | 03 | 1 | D-38 | — | Markup rule CRUD enforces bounds + writes audit row | integration | `dotnet test --filter "FullyQualifiedName~MarkupRuleAuditTests"` | ❌ W0 | ⬜ pending |
| 6-03-02 | 03 | 2 | BO-08 | — | Daily aggregates populated by nightly job; drill-down returns per-booking rows | integration | `dotnet test --filter "FullyQualifiedName~MisDailyAggregateTests"` | ❌ W0 | ⬜ pending |
| 6-03-03 | 03 | 2 | BO-08 | — | CSV + Excel (ClosedXML) export renders Summary + Details sheets | integration | `dotnet test --filter "FullyQualifiedName~MisExportTests"` | ❌ W0 | ⬜ pending |
| 6-03-04 | 03 | 3 | D-41 | T-6-06 | Monthly commission batch credits wallets + archives QuestPDF statement; idempotent | integration | `dotnet test --filter "FullyQualifiedName~CommissionPayoutTests"` | ❌ W0 | ⬜ pending |
| 6-04-01 | 04 | 1 | CRM-01 | — | Customer 360 projection rebuilt from BookingConfirmed/Cancelled events | integration | `dotnet test --filter "FullyQualifiedName~Customer360ProjectionTests"` | ❌ W0 | ⬜ pending |
| 6-04-02 | 04 | 1 | CRM-02 | T-6-07 | Credit limit enforced at WalletReserveCommand (402 + problem+json) | integration | `dotnet test --filter "FullyQualifiedName~CreditLimitEnforcementTests"` | ❌ W0 | ⬜ pending |
| 6-04-03 | 04 | 2 | CRM-03 | — | Booking search by PNR/name/email/ref returns projection rows | integration | `dotnet test --filter "FullyQualifiedName~BookingSearchTests"` | ❌ W0 | ⬜ pending |
| 6-04-04 | 04 | 2 | CRM-04 | — | Communication log writes (Customer + Agency) with RBAC | integration | `dotnet test --filter "FullyQualifiedName~CommunicationLogTests"` | ❌ W0 | ⬜ pending |
| 6-04-05 | 04 | 3 | CRM-05 | — | Upcoming trips view filters by future date + status | integration | `dotnet test --filter "FullyQualifiedName~UpcomingTripsTests"` | ❌ W0 | ⬜ pending |
| 6-04-06 | 04 | 4 | COMP-03 | T-6-08 | GDPR erasure nullifies PII, writes tombstone, projections redact | integration | `dotnet test --filter "FullyQualifiedName~GdprErasureTests"` | ❌ W0 | ⬜ pending |
| 6-04-07 | 04 | 4 | COMP-03 | — | Projection rebuild-from-archive replays events and re-applies erasure | integration | `dotnet test --filter "FullyQualifiedName~CrmRebuildReplayTests"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/TBE.BackofficeService.Tests/TBE.BackofficeService.Tests.csproj` — test project with Testcontainers-SqlServer + RabbitMQ fixtures
- [ ] `tests/TBE.CrmService.Tests/TBE.CrmService.Tests.csproj` — test project with SqlServer container for projection tests
- [ ] `tests/TBE.BackofficeService.Tests/TestFixtures/SqlServerFixture.cs` — shared SqlServer container (inherit existing pattern from Phase 5 BookingService.Tests)
- [ ] `tests/TBE.BackofficeService.Tests/TestFixtures/RabbitMqFixture.cs` — MassTransit in-memory or container harness
- [ ] `tests/TBE.CrmService.Tests/TestFixtures/MassTransitHarness.cs` — MassTransit test harness for projection consumers
- [ ] `src/portals/backoffice-web/tests/e2e/` — Playwright suite bootstrap (mirror `src/portals/b2b-web/tests/e2e/` setup)
- [ ] `[Trait("Category","Phase06")]` applied to every new xUnit test class so quick filter works

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Keycloak `tbe-backoffice` realm seeds correctly with 4 roles on container start | D-46 | Realm-file JSON must be validated inside running Keycloak; integration harness cannot fully simulate Keycloak's import step | `docker compose up keycloak` → log-in to admin console → verify `tbe-backoffice` realm exists with `ops-admin`, `ops-cs`, `ops-finance`, `ops-read` roles |
| Backoffice portal slate-900 palette + CSP excludes Stripe origins | D-47 | CSP + palette are visual/browser-header concerns | Load `/backoffice` in headed Playwright → screenshot diff against UI-SPEC palette tokens; `curl -I` the index → verify `Content-Security-Policy` header omits `js.stripe.com` |
| QuestPDF `AgencyStatement.pdf` renders legibly with line items + totals | D-54 | PDF visual fidelity requires human eyeballs | Run monthly batch in staging against a seeded agency → open generated PDF → confirm header, line items, totals, approval metadata |
| 4-eyes UI flow (ops-finance opens, ops-admin approves) | D-48 | UI inter-role handoff | Manual Playwright scripted UAT with two test users, documented in UAT.md |
| GDPR erasure visible across portals (anonymized label in backoffice, removed from B2C account) | COMP-03 | Cross-portal visual verification | Manual UAT: erase a customer, verify backoffice shows "Anonymized Customer" label, B2C portal returns 404 for that account |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (4 test projects, 3 fixtures, Playwright bootstrap)
- [ ] No watch-mode flags
- [ ] Feedback latency < 90s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
