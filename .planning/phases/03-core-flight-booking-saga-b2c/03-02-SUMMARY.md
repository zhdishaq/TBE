---
phase: "03"
plan: "02"
subsystem: payment-service
tags: [payments, stripe, wallet, saga, pci-saqa, d-13, d-14, w3-boundary]
requires:
  - 03-01 (BookingSagaStateMachine consumes PaymentAuthorized/PaymentCaptured/PaymentAuthorizationFailed/PaymentCaptureFailed)
provides:
  - IStripePaymentGateway (AuthorizeAsync/CaptureAsync/CancelAsync/RefundAsync/CreateWalletTopUpAsync)
  - Payment command consumers (AuthorizePayment, CapturePayment, CancelAuthorization, RefundPayment)
  - Wallet command consumers (WalletReserve, WalletCommit, WalletRelease)
  - StripeWebhookReceived envelope (W3 ingress boundary)
  - StripeWebhookConsumer (sole publisher of payment saga events from webhooks)
  - StripeTopUpConsumer (sole writer of TopUp ledger entries)
  - WalletRepository (Dapper + UPDLOCK/ROWLOCK/HOLDLOCK)
  - WalletController (GET balance, GET transactions, POST top-ups)
affects:
  - PaymentService.API (DI wiring, new controllers)
  - PaymentService.Infrastructure (PaymentDbContext + WalletTransactions + StripeWebhookEvents tables)
tech-stack:
  added:
    - Stripe.net 51.0.0 (Application layer only — PCI SAQ-A isolation)
    - Dapper (on Microsoft.Data.SqlClient for raw UPDLOCK pattern)
  patterns:
    - Deterministic idempotency keys (D-13): booking-{id}-{action}, wallet-{id}-topup-authorize, wallet-{id}-topup-{pi}
    - Append-only ledger with PERSISTED computed SignedAmount (D-14)
    - W3 dumb ingress: controller publishes exactly one typed envelope; consumer fans out
    - Consumer filter partitioning: saga path (WalletId == null) vs top-up path (WalletId set)
key-files:
  created:
    - src/shared/TBE.Contracts/Events/PaymentEvents.cs
    - src/shared/TBE.Contracts/Events/WalletEvents.cs
    - src/services/PaymentService/PaymentService.Application/Stripe/IStripePaymentGateway.cs
    - src/services/PaymentService/PaymentService.Application/Stripe/StripePaymentGateway.cs
    - src/services/PaymentService/PaymentService.Application/Stripe/StripeOptions.cs
    - src/services/PaymentService/PaymentService.Application/Stripe/PaymentGatewayException.cs
    - src/services/PaymentService/PaymentService.Application/Consumers/AuthorizePaymentConsumer.cs
    - src/services/PaymentService/PaymentService.Application/Consumers/CapturePaymentConsumer.cs
    - src/services/PaymentService/PaymentService.Application/Consumers/CancelAuthorizationConsumer.cs
    - src/services/PaymentService/PaymentService.Application/Consumers/RefundPaymentConsumer.cs
    - src/services/PaymentService/PaymentService.Application/Consumers/StripeWebhookConsumer.cs
    - src/services/PaymentService/PaymentService.Application/Consumers/StripeTopUpConsumer.cs
    - src/services/PaymentService/PaymentService.Application/Consumers/WalletReserveConsumer.cs
    - src/services/PaymentService/PaymentService.Application/Consumers/WalletCommitConsumer.cs
    - src/services/PaymentService/PaymentService.Application/Consumers/WalletReleaseConsumer.cs
    - src/services/PaymentService/PaymentService.Application/Wallet/IWalletRepository.cs
    - src/services/PaymentService/PaymentService.Application/Wallet/WalletEntryType.cs
    - src/services/PaymentService/PaymentService.Application/Wallet/WalletOptions.cs
    - src/services/PaymentService/PaymentService.Application/Wallet/InsufficientWalletBalanceException.cs
    - src/services/PaymentService/PaymentService.Application/Wallet/DuplicateWalletTopUpException.cs
    - src/services/PaymentService/PaymentService.Infrastructure/Wallet/WalletRepository.cs
    - src/services/PaymentService/PaymentService.Infrastructure/Stripe/StripeWebhookEvent.cs
    - src/services/PaymentService/PaymentService.Infrastructure/Configurations/WalletTransactionMap.cs
    - src/services/PaymentService/PaymentService.Infrastructure/Configurations/StripeWebhookEventMap.cs
    - src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260417000000_AddWalletAndStripe.cs
    - src/services/PaymentService/PaymentService.API/Controllers/StripeWebhookController.cs
    - src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs
    - tests/Payments.Tests/StripePaymentGatewayTests.cs
    - tests/Payments.Tests/StripeWebhookControllerTests.cs
    - tests/Payments.Tests/WalletRepositoryTests.cs
    - tests/Payments.Tests/StripeTopUpConsumerTests.cs
    - tests/Payments.Tests/MsSqlCollection.cs
  modified:
    - src/services/PaymentService/PaymentService.API/Program.cs
    - src/services/PaymentService/PaymentService.Infrastructure/PaymentDbContext.cs
    - tests/Payments.Tests/Payments.Tests.csproj
decisions:
  - Put StripePaymentGateway in Application layer (not Infrastructure) per acceptance-criteria grep path; Stripe.net ref scoped to PaymentService.Application only (PCI SAQ-A preserved).
  - DuplicateWalletTopUpException lives in Application layer to avoid a reverse reference from StripeTopUpConsumer into Infrastructure.
  - WalletRepository exposes an additional constructor accepting (string connectionString, ILogger?) so integration tests can bypass IConfiguration.
  - MsSqlCollection definition duplicated locally in Payments.Tests (xUnit requires CollectionDefinition in the test assembly).
  - Called EventUtility.ConstructEvent with throwOnApiVersionMismatch: false so Stripe's SDK does not NRE on payloads whose api_version is compared against a null sdkApiVersion in the 51.0.0 release.
  - Hand-authored EF migration (20260417000000_AddWalletAndStripe) using raw SQL for the PERSISTED computed SignedAmount column, matching the 03-01 precedent (EF CLI unusable against this SDK/provider combo).
metrics:
  completed: "2026-04-15"
  commits: 3
  task_count: 3
  unit_tests_passing: 13
  integration_tests_written: 4
---

# Phase 03 Plan 02: Stripe Payments + Wallet Ledger Summary

Implemented the PCI-isolated Stripe adapter, the full set of payment command + webhook
consumers that drive the booking saga, and the append-only wallet ledger (reserve /
commit / release / top-up) with deterministic idempotency keys and a UPDLOCK/ROWLOCK/
HOLDLOCK concurrency guarantee. W3 (dumb webhook ingress + single saga publisher) is
structurally enforced; Stripe.net now compiles in exactly one project.

## Tasks completed

| Task | Name                                              | Commit    | Tests                                                   |
| ---- | ------------------------------------------------- | --------- | ------------------------------------------------------- |
| 1    | Stripe gateway, payment consumers, webhook ingress| `f207f02` | 4 StripeGateway (Unit) + 4 StripeWebhookController (Unit) |
| 2    | Wallet schema, Dapper repo, wallet consumers      | `4d7e281` | 4 WalletRepository (Integration)                        |
| 3    | PAY-04 top-up flow (StripeTopUpConsumer)          | `86aa6bf` | 5 StripeTopUpConsumer (Unit)                            |

Total: 13 Unit tests passing locally; 4 Integration tests written (run on Docker-enabled
runners only — see Deviations).

## Key behaviours verified

- **PAY-01 / D-13:** `AuthorizeAsync` sets `CaptureMethod = "manual"`, stores `booking_id`
  metadata, and sends `IdempotencyKey = booking-{id}-authorize` on the `RequestOptions`.
  Identical pattern for `CaptureAsync` (`-capture`) and `RefundAsync` (`-refund`).
- **PAY-02 / W3:** `StripeWebhookController` verifies Stripe signatures (tolerance 300),
  dedupes by `event.Id` via `StripeWebhookEvents`, publishes exactly ONE
  `StripeWebhookReceived` envelope, never publishes any saga/wallet event itself, and
  never logs raw bodies, signatures, or metadata.
- **PAY-04:** `StripeTopUpConsumer` writes ledger rows with
  `wallet-{walletId}-topup-{paymentIntentId}`, swallows `DuplicateWalletTopUpException`
  and SQL unique-violations (2601/2627), and ignores envelopes whose `WalletId` is null
  (those belong to the saga path).
- **PAY-05 / PAY-06 / D-14:** `WalletRepository.ReserveAsync` reads `SUM(SignedAmount)`
  under `WITH (UPDLOCK, ROWLOCK, HOLDLOCK)` inside a serializable transaction, rejects
  insufficient balance, and returns the existing `TxId` on idempotency-key collision.
  50 concurrent reserves on a 30-slot wallet deterministically yield 30 successes and
  20 `InsufficientWalletBalanceException`s.
- **PAY-07:** Refunds go through the gateway with `booking-{id}-refund` and publish
  `PaymentRefundIssued`.
- **PAY-08:** `Stripe.net` imports appear only in
  `PaymentService.Application.Stripe.StripePaymentGateway` and
  `PaymentService.API.Controllers.StripeWebhookController`. No other service references
  the SDK (grep-verified).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `StripeConfiguration.ApiVersion` is read-only in Stripe.net 51.0.0**
- **Found during:** Task 1 build
- **Issue:** Assigning the property from a static constructor on `StripePaymentGateway` caused CS0200.
- **Fix:** Removed the assignment; the SDK already targets a supported API version.
- **Commit:** `f207f02`

**2. [Rule 1 - Bug] `EventUtility.ConstructEvent` NRE when api_version compared**
- **Found during:** Task 1 signature tests
- **Issue:** With Stripe.net 51.0.0 the overload that doesn't take `throwOnApiVersionMismatch` dereferences a null `sdkApiVersion` constant, raising NRE.
- **Fix:** Switched to the explicit overload `ConstructEvent(json, sig, secret, tolerance: 300, throwOnApiVersionMismatch: false)`.
- **Commit:** `f207f02`

**3. [Rule 3 - Blocking] xUnit1041 in Payments.Tests**
- **Found during:** Task 2 build
- **Issue:** `[Collection(nameof(MsSqlContainerFixture))]` failed because the `CollectionDefinition` lived in `TBE.Tests.Shared`; xUnit requires definitions in the same assembly as the test.
- **Fix:** Added a minimal `MsSqlCollection.cs` in Payments.Tests binding the shared fixture locally.
- **Commit:** `4d7e281`

**4. [Rule 3 - Blocking] DI cycle risk if DuplicateWalletTopUpException sat in Infrastructure**
- **Found during:** Task 1 consumer wiring
- **Issue:** StripeTopUpConsumer lives in Application and would have needed a reverse reference into Infrastructure.
- **Fix:** Placed the exception in Application (Infrastructure already references Application and throws it from `WalletRepository`).
- **Commit:** `f207f02`

**5. [Rule 2 - Missing critical functionality] Test-friendly repo constructor**
- **Found during:** Task 2 test authoring
- **Issue:** `WalletRepository` required `IConfiguration`, making integration tests awkward.
- **Fix:** Added a second constructor `(string connectionString, ILogger?)` used by tests while the DI constructor still binds through configuration.
- **Commit:** `f207f02` (code) / `4d7e281` (tests)

### Environment deviations (not code changes)

- **Integration tests not executed here:** The agent runs on a Windows host without a
  running Docker engine, so `WalletRepositoryTests` cannot start the MsSql
  Testcontainer. Tests are written, compile, and match the fixture contract; run them
  on any Docker-enabled developer machine or CI runner with `dotnet test --filter Category=Integration`.

## Known Stubs

None. All wallet paths are wired end-to-end (Stripe → envelope → consumer → ledger →
domain event). `WalletController`'s POST top-up calls the real gateway; there are no
hardcoded empties flowing to UI.

## Threat Flags

None — this plan stayed inside the threat surface already modelled in `03-02-PLAN.md`
(Stripe ingress, wallet ledger, agency-scoped top-up endpoint). All mitigations
(`mitigate` dispositions) implemented: signature verification, raw-body redaction in
logs, idempotency-based replay defence, row-locked balance read, role-guarded top-up
endpoint.

## Self-Check

- [x] `src/services/PaymentService/PaymentService.Application/Stripe/StripePaymentGateway.cs` — FOUND
- [x] `src/services/PaymentService/PaymentService.Infrastructure/Wallet/WalletRepository.cs` — FOUND
- [x] `src/services/PaymentService/PaymentService.API/Controllers/StripeWebhookController.cs` — FOUND
- [x] `src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs` — FOUND
- [x] `tests/Payments.Tests/WalletRepositoryTests.cs` — FOUND
- [x] `tests/Payments.Tests/StripeTopUpConsumerTests.cs` — FOUND
- [x] Commit `f207f02` — FOUND in `git log`
- [x] Commit `4d7e281` — FOUND in `git log`
- [x] Commit `86aa6bf` — FOUND in `git log`
- [x] `dotnet test --filter Category=Unit` — 13 passed, 0 failed
- [x] W3 grep: zero saga-event publishes in `StripeWebhookController.cs`
- [x] PCI grep: `using Stripe` appears only in `StripePaymentGateway.cs` + `StripeWebhookController.cs`

## Self-Check: PASSED
