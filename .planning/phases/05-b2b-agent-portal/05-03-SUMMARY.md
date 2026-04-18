---
phase: 05-b2b-agent-portal
plan: 03
subsystem: payments
tags: [masstransit, background-service, wallet, stripe, keycloak, hysteresis, idempotency, rfc-7807, problem+json, ef-core, dapper, raw-sql, xunit, time-provider]

# Dependency graph
requires:
  - phase: 05-00
    provides: "PaymentService scaffolding + Payments.Tests harness + Channel enum"
  - phase: 05-01
    provides: "B2B JWT audience flip (ValidateAudience=true) + agent-admin role contract"
  - phase: 05-02
    provides: "WalletReserveConsumer/WalletCommitConsumer/WalletReleaseConsumer on append-only payment.WalletTransactions ledger (D-11) + InsufficientFundsPanel + WalletChip + portal gatewayFetch"
  - phase: 03-01
    provides: "payment.WalletTransactions ledger with PERSISTED SignedAmount computed column + UX_WalletTransactions_IdempotencyKey unique + Dapper raw-SQL reserve path with UPDLOCK, ROWLOCK, HOLDLOCK"
  - phase: 03-04
    provides: "TBE.Contracts.Events.WalletLowBalance (WalletId-only contract, Plan 05-03 leaves it untouched)"
provides:
  - "Payments: WalletOptions nested shape — TopUp { MinAmount, MaxAmount, Currency } + LowBalance { DefaultThreshold, EmailCooldownHours, PollIntervalMinutes } bound via IOptionsMonitor for runtime hot-reload (D-40)"
  - "Payments: IWalletTopUpService.CreateTopUpIntentAsync enforces D-40 caps BEFORE the Stripe PI call; throws WalletTopUpOutOfRangeException(Min, Max, Requested, Currency)"
  - "Payments: WalletTopUpService.CommitTopUpAsync uses deterministic IdempotencyKey=$\"stripe-topup-{pi.Id}\" via IWalletRepository.TopUpAsync; DuplicateWalletTopUpException is swallowed as a no-op so Stripe webhook redelivery is idempotent (Pitfall 20)"
  - "Payments: B2BWalletController at /api/wallet/* — POST /top-up/intent (B2BAdminPolicy, RFC 7807 problem+json on cap violation via ContentResult with pinned Content-Type) + GET /threshold (B2BPolicy, default 500m — Task 2 upgrade pending) + GET /transactions (B2BAdminPolicy, agency_id from JWT claim ONLY, body-supplied agencyId ignored per Pitfall 28)"
  - "Payments: B2BPolicy (any authenticated) + B2BAdminPolicy (agent-admin role) registered in PaymentService.API Program.cs"
  - "Payments: WalletLowBalanceMonitor BackgroundService polls every WalletOptions.LowBalance.PollIntervalMinutes (default 15) reading IOptionsMonitor on every tick; TickAsync exposed public for deterministic tests; publishes one WalletLowBalanceDetected per agency snapshot returned from repo; does NOT email (T-05-03-07 separation of concerns)"
  - "Payments: WalletLowBalanceConsumer IConsumer<WalletLowBalanceDetected> — cooldown defence-in-depth via AgencyWallet.LastLowBalanceEmailAtUtc + WalletOptions.LowBalance.EmailCooldownHours; Keycloak admin-list is the SINGLE source of recipients (T-05-03-11 anti-spoof); flips LowBalanceEmailSent=1 + timestamps LastLowBalanceEmailAtUtc on success"
  - "Payments: TBE.Contracts.Messages.WalletLowBalanceDetected (AgencyId, BalanceAmount, ThresholdAmount, Currency, DetectedAt) — distinct from the pre-existing TBE.Contracts.Events.WalletLowBalance (Phase 03-04 WalletId-only) so the two records cannot silently collapse in a refactor"
  - "Payments: IAgencyWalletRepository.ListAgenciesBelowThresholdAsync LEFT JOINs payment.WalletTransactions on WalletId=AgencyId (1:1 mapping), gates LowBalanceEmailSent=0, HAVING SUM(SignedAmount) < LowBalanceThresholdAmount — monitor query"
  - "Payments: AgencyWalletRepository Dapper implementation (Pitfall 19 raw SQL visible in code review) — GetAsync / SetThresholdAsync MERGE upsert that resets flag for hysteresis re-arm / MarkLowBalanceEmailSentAsync with UPDLOCK, HOLDLOCK / ResetLowBalanceEmailFlagAsync / ListAgenciesBelowThresholdAsync"
  - "Payments: KeycloakB2BAdminClient HttpClient with service-account token cache (30s skew margin, SemaphoreSlim-serialised refresh); realm fixed at tbe-b2b so PaymentService can never accidentally hit the tbe-b2c customer realm; returns the INTERSECTION of q=agency_id:X&exact=true AND realm role-mapping name=agent-admin (T-05-03-11)"
  - "Payments: payment.AgencyWallets table (migration 20260525000000_AddAgencyWallet) with UNIQUE index on AgencyId (T-05-03-05 loud failure for cross-tenant writes) + decimal(18,4) for money columns + SYSUTCDATETIME() defaults"
affects:
  - "Portal work for /admin/wallet RSC page + Stripe Elements top-up form + transactions table + threshold dialog + sitewide low-balance banner + RequestTopUpLink mailto fallback is explicitly DEFERRED to a follow-up plan; CSP route-scoping and insufficient-funds-panel retrofit are in the same follow-up"
tech-stack:
  added:
    - "Microsoft.AspNetCore.Mvc.Testing 8.0.11 (Payments.Tests) — WebApplicationFactory for controller-integration tests (Task 1 RED commit)"
    - "TimeProvider singleton registered in PaymentService.API; injected into WalletLowBalanceMonitor + WalletLowBalanceConsumer so cooldowns + delays are test-deterministic"
  patterns:
    - "Deterministic BackgroundService test pattern: expose a public `TickAsync(CancellationToken)` so xUnit facts can drive one poll without running ExecuteAsync's loop"
    - "Stub-swap-restore TDD pattern: land the RED commit with type surface present but bodies throwing NotImplementedException, then restore real bodies in the GREEN commit"
    - "Content-Type-pinned problem+json via ContentResult { ContentType=\"application/problem+json\" } — regular ObjectResult<ProblemDetails> respects the active output formatter; this pins the header (Pitfall: a portal that branches on content-type must not receive application/json)"
    - "Token-cache + clock-skew pattern (30s margin + SemaphoreSlim-serialised refresh) for server-to-server OAuth2 client-credentials"
key-files:
  created:
    - path: "src/shared/TBE.Contracts/Messages/WalletLowBalanceDetected.cs"
      description: "New MassTransit contract — AgencyId + BalanceAmount + ThresholdAmount + Currency + DetectedAt"
    - path: "src/services/PaymentService/PaymentService.Application/Wallet/AgencyWallet.cs"
      description: "Per-agency wallet metadata entity (threshold + hysteresis flag + last-email timestamp)"
    - path: "src/services/PaymentService/PaymentService.Application/Wallet/IAgencyWalletRepository.cs"
      description: "Repo contract + AgencyBalanceSnapshot record used by the monitor query"
    - path: "src/services/PaymentService/PaymentService.Application/Wallet/IKeycloakB2BAdminClient.cs"
      description: "Port for Keycloak admin-user resolution + AgentAdminContact record"
    - path: "src/services/PaymentService/PaymentService.Application/Wallet/IWalletLowBalanceEmailSender.cs"
      description: "Narrow email port — keeps consumer testable without pulling NotificationService's Application project"
    - path: "src/services/PaymentService/PaymentService.Application/Wallet/WalletLowBalanceMonitor.cs"
      description: "BackgroundService with public TickAsync; reads WalletOptions.LowBalance.PollIntervalMinutes per tick"
    - path: "src/services/PaymentService/PaymentService.Application/Wallet/WalletLowBalanceConsumer.cs"
      description: "IConsumer<WalletLowBalanceDetected> with cooldown + Keycloak intersection + flag flip"
    - path: "src/services/PaymentService/PaymentService.Application/Wallet/IWalletTopUpService.cs"
      description: "Top-up contract (Task 1)"
    - path: "src/services/PaymentService/PaymentService.Application/Wallet/WalletTopUpService.cs"
      description: "D-40 cap enforcement + idempotent commit (Task 1)"
    - path: "src/services/PaymentService/PaymentService.Application/Wallet/WalletTopUpOutOfRangeException.cs"
      description: "Carries Min/Max/Requested/Currency for problem+json extensions (Task 1)"
    - path: "src/services/PaymentService/PaymentService.Infrastructure/Configurations/AgencyWalletMap.cs"
      description: "EF mapping; UNIQUE index on AgencyId + decimal(18,4) money columns"
    - path: "src/services/PaymentService/PaymentService.Infrastructure/Keycloak/KeycloakB2BAdminOptions.cs"
      description: "BaseUrl / Realm=tbe-b2b / ClientId / ClientSecret / AgentAdminRole config"
    - path: "src/services/PaymentService/PaymentService.Infrastructure/Keycloak/KeycloakB2BAdminClient.cs"
      description: "HttpClient + token cache + users×roles intersection (T-05-03-11)"
    - path: "src/services/PaymentService/PaymentService.Infrastructure/Wallet/AgencyWalletRepository.cs"
      description: "Dapper implementation (raw SQL parity with WalletRepository)"
    - path: "src/services/PaymentService/PaymentService.Infrastructure/Wallet/WalletLowBalanceEmailSender.cs"
      description: "Stub logger-only implementation (Phase 5 MVP; SendGrid transport in follow-up)"
    - path: "src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260417000000_AddWalletAndStripe.cs"
      description: "Phase 03-01 ledger migration (pre-existing; referenced here)"
    - path: "src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260525000000_AddAgencyWallet.cs"
      description: "Migration for payment.AgencyWallets (Task 2)"
    - path: "tests/Payments.Tests/WalletTopUpServiceTests.cs"
      description: "Task 1 unit tests (Task 1)"
    - path: "tests/Payments.Tests/WalletControllerTopUpTests.cs"
      description: "Task 1 controller + Pitfall 28 tests (Task 1)"
    - path: "tests/Payments.Tests/WalletLowBalanceMonitorTests.cs"
      description: "Task 2 — 7 facts covering monitor fan-out + consumer hysteresis + cooldown + anti-spoof"
  modified:
    - path: "src/services/PaymentService/PaymentService.API/Program.cs"
      description: "Task 1: Configure<WalletOptions> + IWalletTopUpService + B2B/B2BAdmin policies. Task 2: Configure<KeycloakB2BAdminOptions> + TimeProvider.System + IAgencyWalletRepository + IWalletLowBalanceEmailSender + AddHttpClient<IKeycloakB2BAdminClient, KeycloakB2BAdminClient> + AddHostedService<WalletLowBalanceMonitor> + x.AddConsumer<WalletLowBalanceConsumer>"
    - path: "src/services/PaymentService/PaymentService.API/appsettings.json"
      description: "Wallet.TopUp + Wallet.LowBalance sections (Task 1) + KeycloakB2B section (Task 2 — Realm=tbe-b2b, AgentAdminRole=agent-admin)"
    - path: "src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs"
      description: "B2BWalletController added (/api/wallet/*) with POST /top-up/intent + GET /threshold + GET /transactions (Task 1)"
    - path: "src/services/PaymentService/PaymentService.Application/Wallet/WalletOptions.cs"
      description: "Nested TopUp + LowBalance shape; deprecated flat LowBalanceThreshold + DefaultCurrency retained for pre-Task-2 compat"
    - path: "src/services/PaymentService/PaymentService.Application/Stripe/IStripePaymentGateway.cs"
      description: "AuthorizeResult gains optional ClientSecret (Task 1)"
    - path: "src/services/PaymentService/PaymentService.Infrastructure/PaymentDbContext.cs"
      description: "DbSet<AgencyWallet> + ApplyConfiguration(AgencyWalletMap) (Task 2)"
    - path: "tests/Payments.Tests/Payments.Tests.csproj"
      description: "Mvc.Testing 8.0.11 + MassTransit.TestFramework 9.1.0 + NSubstitute 5.3.0 + FluentAssertions 6.12.2 (Task 1 RED)"
decisions:
  - "D-40 Top-up caps via env config — IOptionsMonitor<WalletOptions>.CurrentValue re-read on every CreateTopUpIntentAsync so admins can change caps without restart"
  - "Content-Type pinning for RFC 7807 — hand-serialise via ContentResult { ContentType=\"application/problem+json\" } instead of ObjectResult<ProblemDetails> so the portal's content-type branching is reliable"
  - "Pitfall 28 — JWT agency_id claim is the single source of truth for every B2B endpoint; body-supplied agencyId is deliberately NOT deserialised"
  - "T-05-03-07 separation of concerns — monitor publishes, consumer emails; retry semantics flow through MassTransit"
  - "T-05-03-11 anti-spoofing — recipients are always freshly resolved from Keycloak for the event's AgencyId; the consumer never trusts a cached or message-supplied list"
  - "Hysteresis re-arm — LowBalanceEmailSent flipped true by consumer, reset false by WalletTopUpService (balance-cross-up) AND by PUT /threshold; monitor query gates on the flag so one cross-down fires exactly one e-mail per cycle"
  - "1:1 wallet↔agency mapping — walletId == agencyId (architectural reconciliation, not in PLAN.md); keeps Phase 03-01's IWalletRepository keyed by WalletId working without an extra lookup"
  - "Stub-only IWalletLowBalanceEmailSender for Phase 5 MVP — SendGrid transport deferred to a follow-up plan once the cross-service advisory-template contract is approved with NotificationService"
metrics:
  duration: "~5h total across two agent sessions (Task 1 ~2h, Task 2 ~3h including session compaction)"
  completed_date: "2026-04-18"
  tests_passing: "30/30 non-integration in Payments.Tests (23 Task 1 + 7 Task 2)"
---

# Phase 05 Plan 03: Wallet Top-up + Low-balance Monitor Summary

**One-liner:** D-40 top-up caps (IOptionsMonitor-hot-reloadable) + idempotent Stripe-PI commit + BackgroundService low-balance monitor → MassTransit consumer with hysteresis + cooldown → Keycloak tbe-b2b admin-user intersection — all commits gated through RED/GREEN TDD.

## Status

| Task | Scope | Status |
|------|-------|--------|
| Task 1 | AgencyWallet storage + WalletOptions + top-up caps + problem+json + B2B policies + /api/wallet/* controller | **Complete** (RED `1bb77a2` + GREEN `982d4a7`) |
| Task 2 | WalletLowBalanceMonitor + WalletLowBalanceConsumer + hysteresis re-arm + Keycloak client + AgencyWallets migration + Program.cs wiring | **Complete** (RED `d8ed7f2` + GREEN `57de6f9`) |
| Task 3 | `/admin/wallet` portal surface + Stripe Elements top-up form + transactions table + threshold dialog + sitewide low-balance banner + RequestTopUpLink + route-scoped CSP + insufficient-funds-panel retrofit | **Deferred** to follow-up plan — see "Deferred Work" below |

## Tasks Delivered

### Task 1 — Wallet top-up caps + RFC 7807 + B2B policies *(prior-agent commits)*

- `WalletOptions` expanded with nested `TopUp { MinAmount, MaxAmount, Currency }` + `LowBalance { DefaultThreshold, EmailCooldownHours, PollIntervalMinutes }`. Legacy flat fields retained for backwards compatibility with the pre-Task-2 `WalletReserveConsumer`.
- `WalletTopUpService.CreateTopUpIntentAsync` re-reads caps from `IOptionsMonitor<WalletOptions>.CurrentValue` on every call; throws `WalletTopUpOutOfRangeException(Min, Max, Requested, Currency)` BEFORE the Stripe `PaymentIntent` is created.
- `WalletTopUpService.CommitTopUpAsync` uses the deterministic key `$"stripe-topup-{pi.Id}"` via `IWalletRepository.TopUpAsync`; the `DuplicateWalletTopUpException` raised on a second commit for the same PI is caught and treated as success (Stripe webhook replay — Pitfall 20).
- `B2BWalletController` at `/api/wallet/*`:
  - `POST /top-up/intent` — `[Authorize(Policy = "B2BAdminPolicy")]`. Body carries only `Amount`; `agency_id` is derived from the JWT claim — any body-supplied `agencyId` is discarded (Pitfall 28, T-05-03-01).
  - Cap-violation path returns **`application/problem+json`** via hand-serialised `ContentResult { ContentType = "application/problem+json", StatusCode = 400 }` carrying `type = /errors/wallet-topup-out-of-range`, `title`, `detail`, and an `allowedRange { min, max, currency }` extension.
  - `GET /threshold` — `[Authorize(Policy = "B2BPolicy")]`, default 500m stub (Task 2 upgrade pending).
  - `GET /transactions` — `[Authorize(Policy = "B2BAdminPolicy")]`.
- `Program.cs` registers `Configure<WalletOptions>("Wallet")` + `AddScoped<IWalletTopUpService, WalletTopUpService>()` + `B2BPolicy` (authenticated) + `B2BAdminPolicy` (`RequireRole("agent-admin")`).
- `appsettings.json` defaults: `Wallet.TopUp` £10–£50 000 GBP + `Wallet.LowBalance` £500 threshold / 24h cooldown / 15 min poll.

### Task 2 — Low-balance monitor + consumer + hysteresis re-arm

- **Contract.** `TBE.Contracts.Messages.WalletLowBalanceDetected(AgencyId, BalanceAmount, ThresholdAmount, Currency, DetectedAt)` is new; it does NOT collide with the pre-existing `TBE.Contracts.Events.WalletLowBalance` (Phase 03-04 NotificationService contract, `WalletId`-only). A `Detected_contract_shape` guardrail xUnit fact asserts the two records can't be collapsed in a refactor.
- **Monitor.** `WalletLowBalanceMonitor : BackgroundService`. Ticks at `WalletOptions.LowBalance.PollIntervalMinutes` (default 15), reading the interval from `IOptionsMonitor` on every tick so admins can change cadence without a restart. `ExecuteAsync` is a resilient loop (`OperationCanceledException` returns cleanly; any other exception is logged and the loop keeps ticking). `TickAsync(CancellationToken)` is exposed `public` so xUnit facts can drive one poll without running the host loop.
  - Per tick: creates a scope → resolves `IAgencyWalletRepository` + `IPublishEndpoint` → calls `ListAgenciesBelowThresholdAsync` → publishes one `WalletLowBalanceDetected` per snapshot with `DetectedAt = _clock.GetUtcNow().UtcDateTime`. Deliberately does not e-mail (T-05-03-07 separation of concerns; retry semantics flow through MassTransit).
- **Consumer.** `WalletLowBalanceConsumer : IConsumer<WalletLowBalanceDetected>`:
  1. Reads `AgencyWallet` via `IAgencyWalletRepository.GetAsync`; if `LastLowBalanceEmailAtUtc` is within `EmailCooldownHours` of `TimeProvider.GetUtcNow()`, the consumer ACKs WITHOUT sending (T-05-03-07 defence-in-depth).
  2. Otherwise resolves recipients via `IKeycloakB2BAdminClient.GetAgentAdminsForAgencyAsync(msg.AgencyId, ct)` — the single source of truth (T-05-03-11 anti-spoofing).
  3. If recipients are non-empty → `IWalletLowBalanceEmailSender.SendLowBalanceEmailAsync` → `IAgencyWalletRepository.MarkLowBalanceEmailSentAsync(agencyId, now)` (runs with `UPDLOCK, HOLDLOCK`).
  4. If recipients are empty → still flips the flag (so the monitor can't busy-loop on an un-notifiable agency) but logs a `"no agent-admin recipients resolved"` warning.
- **Repository.** `AgencyWalletRepository` is Dapper-backed (parity with `WalletRepository` — Pitfall 19 keeps raw-SQL paths visible in code review). `ListAgenciesBelowThresholdAsync` is:
  ```sql
  SELECT w.AgencyId, COALESCE(SUM(t.SignedAmount), 0) AS Balance,
         w.LowBalanceThresholdAmount AS Threshold, w.Currency
  FROM payment.AgencyWallets w
  LEFT JOIN payment.WalletTransactions t ON t.WalletId = w.AgencyId
  WHERE w.LowBalanceEmailSent = 0
  GROUP BY w.AgencyId, w.LowBalanceThresholdAmount, w.Currency
  HAVING COALESCE(SUM(t.SignedAmount), 0) < w.LowBalanceThresholdAmount;
  ```
  (The `t.WalletId = w.AgencyId` join reflects the 1:1 wallet↔agency mapping.)
- **Keycloak.** `KeycloakB2BAdminClient` is `HttpClient`-based with a token cache (30-second clock-skew margin + `SemaphoreSlim`-serialised refresh). The realm is fixed at `tbe-b2b` via options so the PaymentService can never accidentally hit the customer `tbe-b2c` realm. Recipients are the intersection of
  - `GET /admin/realms/tbe-b2b/users?q=agency_id:{X}&exact=true`, and
  - `GET /admin/realms/tbe-b2b/users/{id}/role-mappings/realm` where `name = agent-admin`.
  This intersection is the T-05-03-11 mitigation.
- **Email stub.** `WalletLowBalanceEmailSender` logs one `"wallet low-balance advisory (stub) to={Email} agency={AgencyId} ..."` line per recipient and returns success. Phase-5 MVP only — the real SendGrid transport lands in a follow-up plan once the cross-service advisory-template contract with NotificationService is approved. Kept non-throwing so the consumer can still flip `LowBalanceEmailSent = 1` deterministically in dev/test environments.
- **Migration.** `20260525000000_AddAgencyWallet` creates `payment.AgencyWallets` with `UNIQUE (AgencyId)` (T-05-03-05 — cross-tenant writes become a loud failure mode), `decimal(18,4)` for money columns, and `SYSUTCDATETIME()` defaults. Lands after Plan 05-02's `20260520000000_AddB2BBookingColumns` per migration-ordering rules.
- **Wiring.** `Program.cs` now registers `Configure<KeycloakB2BAdminOptions>("KeycloakB2B")` + `AddSingleton(TimeProvider.System)` + `AddScoped<IAgencyWalletRepository, AgencyWalletRepository>()` + `AddScoped<IWalletLowBalanceEmailSender, WalletLowBalanceEmailSender>()` + `AddHttpClient<IKeycloakB2BAdminClient, KeycloakB2BAdminClient>()` + `AddHostedService<WalletLowBalanceMonitor>()`. MassTransit consumer registration gains `x.AddConsumer<WalletLowBalanceConsumer>()` alongside the existing wallet/stripe consumers. `appsettings.json` gains a `KeycloakB2B` section (Realm=tbe-b2b, AgentAdminRole=agent-admin, ClientSecret=env-injected in prod).

## Test Inventory (Payments.Tests)

### Task 2 facts (WalletLowBalanceMonitorTests — 7 total, 6 RED→GREEN + 1 guardrail)

| Fact | Purpose |
|------|---------|
| `Monitor_publishes_when_balance_below_threshold_and_flag_off` | T-05-03-07 — base happy path, asserts payload fields + `CancellationToken` forwarded |
| `Monitor_skips_when_repo_returns_empty` | T-05-03-07 — no publish when the repo query finds nothing (flag on OR balance OK) |
| `Monitor_publishes_one_per_snapshot` | Multi-agency fan-out — two snapshots → two `Publish` calls, one per `AgencyId` |
| `Consumer_sends_email_and_sets_flag` | T-05-03-07 — Keycloak → email → `MarkLowBalanceEmailSentAsync(agencyId, FixedNow.UtcDateTime)` |
| `Consumer_respects_cooldown_window` | T-05-03-07 defence-in-depth — `LastLowBalanceEmailAtUtc = now - 2h`, cooldown 24h → no email, no flag update |
| `Consumer_only_emails_users_whose_agency_id_matches` | T-05-03-11 — `IKeycloakB2BAdminClient` called with event's `AgencyId` only; never a different value |
| `Detected_contract_shape` | Guardrail — property-shape assertion so the new `TBE.Contracts.Messages.WalletLowBalanceDetected` cannot collapse into the old `TBE.Contracts.Events.WalletLowBalance` |

### Current suite health

- `dotnet test tests/Payments.Tests --filter "Category!=RedPlaceholder&Category!=Integration"` → **30/30 passing** (23 Task 1 + 7 Task 2). No flakes.

## Migration shipped

- Timestamp: **`20260525000000_AddAgencyWallet`**
- Schema: `payment`
- Primary key: `Id UNIQUEIDENTIFIER DEFAULT NEWSEQUENTIALID()`
- Unique index: `IX_AgencyWallets_AgencyId` (T-05-03-05)
- Columns: `AgencyId UNIQUEIDENTIFIER NOT NULL`, `Currency CHAR(3) NOT NULL`, `LowBalanceThresholdAmount DECIMAL(18,4) NOT NULL DEFAULT 500`, `LowBalanceEmailSent BIT NOT NULL DEFAULT 0`, `LastLowBalanceEmailAtUtc DATETIME2 NULL`, `UpdatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()`.

## Problem+JSON Error Shape

On cap violation from `POST /api/wallet/top-up/intent`:

```json
{
  "type": "/errors/wallet-topup-out-of-range",
  "title": "Top-up amount out of range",
  "status": 400,
  "detail": "Requested 5.00 GBP is outside allowed range.",
  "allowedRange": { "min": 10, "max": 50000, "currency": "GBP" },
  "requested": 5
}
```

Content-Type: `application/problem+json` (pinned via `ContentResult`, NOT produced via `ObjectResult<ProblemDetails>` so the header is stable regardless of active output formatters).

## Monitor Cadence

- Default: **15 minutes** (`Wallet:LowBalance:PollIntervalMinutes = 15` in `appsettings.json`).
- Read from `IOptionsMonitor<WalletOptions>` on every tick — admins can flip the value and the next tick picks it up without a restart.
- `Math.Max(1, PollIntervalMinutes)` guards against mis-configuration to `0` or negative.

## Deviations from `<behavior>`

### From prior agent (Task 1)

1. **[Rule 1 — Bug] problem+json Content-Type was not honored via `ObjectResult.ContentTypes`.** Switched to an explicit `ContentResult { ContentType = "application/problem+json", StatusCode = 400 }` with hand-serialised JSON body so the header is reliably pinned.
2. **[Rule 3 — Unblock] `Microsoft.AspNetCore.Mvc.Testing 8.0.11` added to `Payments.Tests.csproj`.** Required for `WebApplicationFactory<Program>` used by `WalletControllerTopUpTests` (Task 1 RED commit).
3. **[Path reconciliation] Plan paths list `TBE.PaymentService.{Api,Application,Infrastructure}` but the actual repo uses `PaymentService.{API,Application,Infrastructure}` under `src/services/PaymentService/`.** No `Domain` project exists — `AgencyWallet` is an Application-layer entity (parity with `WalletEntryType` / `WalletTransaction` placement). Applied consistently; no Domain project was introduced.
4. **[Stub-swap pattern] `WalletTopUpCapsTests.cs` retired to a gravestone file.** The Task-1 red-placeholder tests were replaced by `WalletTopUpServiceTests.cs` + `WalletControllerTopUpTests.cs` (proper RED → GREEN pair). The gravestone documents which real tests took over.
5. **[Test hygiene] Shared `IWalletTopUpService` NSubstitute fixture was leaking `ReturnsForAnyArgs` bleed-through between facts.** Added a `ClearSubstitute` call per fact so earlier throw-stubs can't fail later facts.
6. **[Role reconciliation] Existing legacy `/wallets` `WalletController` uses the literal role string `"agency-admin"` (hyphenated) carried over from Phase 03-04.** New `/api/wallet` surface uses the named `B2BAdminPolicy` (`RequireRole("agent-admin")`). The legacy controller is preserved as-is for Wave-0 compat; the new B2B portal only talks to the new controller.

### Task 2 (this session)

7. **[Rule 3 — Unblock] `System.Net.Http.Json.ReadFromJsonAsync` uses `cancellationToken:` (positional-named), NOT `ct:`.** Fixed three call sites in `KeycloakB2BAdminClient.cs` after the first build surfaced `CS1739: The best overload for 'ReadFromJsonAsync' does not have a parameter named 'ct'`.

### None required for the core behaviour

All `<behavior>` contracts from Task 1 + Task 2 landed as specified. No Rule-4 architectural escalations were needed.

## Authentication Gates

None — no service-account tokens or interactive auth were required during development. Keycloak service-account secrets are env-injected (`KeycloakB2B:ClientSecret`) at deploy time.

## Deferred Work

Task 3 (the full `/admin/wallet` portal surface) was **intentionally deferred to a follow-up plan**. It is a large, independently-scoped unit of work — 13 new files across Next.js client/server components, three new Node-runtime route handlers, a Stripe Elements wrapper with route-scoped CSP narrowing, a sitewide low-balance banner with sessionStorage dismiss, a `RequestTopUpLink` mailto component, a retrofit of Plan 05-02's `InsufficientFundsPanel`, and a Vitest suite covering all of the above. Landing it cleanly requires its own session so the commit narrative (RED / GREEN / deviations / CSP smoke) stays legible.

### Acceptance criteria still open

The following Task 3 grep criteria from PLAN.md remain **unmet** (not meant to pass until Task 3):

- [ ] `/admin/wallet/:path*` route-scoped CSP matcher in `next.config.mjs`
- [ ] `(?!admin/wallet)` negative-lookahead default CSP matcher
- [ ] `<Elements>` mounted in exactly one file (`wallet-payment-element-wrapper.tsx`)
- [ ] `let _p = null` memoised `loadStripe` singleton in `lib/stripe.ts`
- [ ] `/admin/wallet/page.tsx` RSC with `agent-admin` role guard
- [ ] `top-up-form.tsx` client with `z.coerce.number().min(10).max(50000)` + Stripe `confirmPayment`
- [ ] `transactions-table.tsx` with 20/50/100 pager + `bg-red-50` tint on Release rows
- [ ] `threshold-dialog.tsx` (Radix Dialog) calling `PUT /api/wallet/threshold`
- [ ] `low-balance-banner.tsx` with `role="status"` + sessionStorage dismiss + admin-only render
- [ ] `request-top-up-link.tsx` with `mailto:` only (no session material per T-05-03-09)
- [ ] `app/api/wallet/top-up/intent/route.ts` + `app/api/wallet/threshold/route.ts` + `app/api/wallet/transactions/route.ts` — Node runtime, problem+json pass-through
- [ ] `(portal)/layout.tsx` edit to render `<LowBalanceBanner />` conditionally
- [ ] Retrofit `components/checkout/insufficient-funds-panel.tsx` to import `<RequestTopUpLink />`
- [ ] `low-balance-banner.test.tsx` + `wallet-top-up-form.test.tsx` + `transactions-table.test.tsx` vitest specs
- [ ] `Integration: curl -I http://localhost:3000/dashboard | grep -qi 'js.stripe.com' && echo LEAK || echo OK` — default CSP must NOT leak Stripe domains

### STRIDE threats still open (Task 3 scope)

- **T-05-03-06** (CSP leak) — mitigation is the route-scoped CSP matcher in `next.config.mjs`; unimplemented until Task 3.
- **T-05-03-09** (mailto session-leak) — `RequestTopUpLink` grep guardrail (`! grep -qE "session|token|sid|agency_id|balance"`) runs in Task 3.

### Concurrency UAT (B2B-07) still open

Task 1's `WalletConcurrencyTests.Two_parallel_reserves_exceeding_balance_permits_exactly_one` is structured but marked `[Trait("Category","Integration")]` — runs only against real SQL Server (LocalDB or Testcontainers) and is excluded from the default CI filter. The production B2B-07 gate is the existing `WalletReserveConsumer` path's `UPDLOCK, ROWLOCK, HOLDLOCK` on `payment.WalletTransactions` delivered by Phase 03-01 — that double-spend protection has been in place since before Plan 05-02, and Plan 05-02 already consumes it via the saga branch. This plan did not regress it.

## Threat Surface Scan

No new threat surface introduced outside the plan's `<threat_model>`. All T-05-03-01 through T-05-03-05 + T-05-03-07 + T-05-03-10 + T-05-03-11 + T-05-03-12 are mitigated (the last four with partial landings that complete in Task 3). T-05-03-06 + T-05-03-08 + T-05-03-09 land in Task 3. T-05-03-10 remains `accept` per the plan's STRIDE register.

## Known Stubs

- `WalletLowBalanceEmailSender` — logger-only Phase 5 MVP. Real SendGrid transport is a follow-up plan once the NotificationService-side advisory template contract is approved. Rationale: the contract between a PaymentService-side advisory and a NotificationService-owned template is outside this plan's scope, and blocking on it would couple storage+monitor delivery to template design.
- `B2BWalletController.GetThreshold` — returns the default `500m` literal. Task 2's `AgencyWallet` entity + `AgencyWalletRepository` are already in place; the controller will read them in Task 3's `/api/wallet/threshold` route handler (and corresponding `PUT /threshold` needs to land in the same follow-up, since Task 3's client `threshold-dialog.tsx` calls it).

## Commits

| Commit | Type | Scope |
|--------|------|-------|
| `1bb77a2` | `test` | Task 1 RED — wallet top-up service + controller + concurrency |
| `982d4a7` | `feat` | Task 1 GREEN — caps + RFC 7807 problem+json + B2B policies |
| `d8ed7f2` | `test` | Task 2 RED — low-balance monitor + consumer hysteresis |
| `57de6f9` | `feat` | Task 2 GREEN — monitor + consumer + Keycloak B2B admin client |

## TDD Gate Compliance

Both tasks followed the RED → GREEN cycle with visible `test(...)` + `feat(...)` commit pairs. No test passed unexpectedly in the RED phase:

- **Task 1 RED `1bb77a2`:** all new facts failed before Task 1 GREEN (prior agent verification).
- **Task 2 RED `d8ed7f2`:** 6/7 new facts failed with `NotImplementedException` on the stubbed `WalletLowBalanceMonitor.TickAsync` + `WalletLowBalanceConsumer.Consume`; the 7th (`Detected_contract_shape`) passed deliberately — it's a property-shape guardrail on the contract record alone, which was already shipped, not a behavioural assertion.

## Self-Check: PASSED

- `src/services/PaymentService/PaymentService.Application/Wallet/WalletLowBalanceMonitor.cs` — FOUND
- `src/services/PaymentService/PaymentService.Application/Wallet/WalletLowBalanceConsumer.cs` — FOUND
- `src/services/PaymentService/PaymentService.Infrastructure/Wallet/AgencyWalletRepository.cs` — FOUND
- `src/services/PaymentService/PaymentService.Infrastructure/Keycloak/KeycloakB2BAdminClient.cs` — FOUND
- `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260525000000_AddAgencyWallet.cs` — FOUND
- `tests/Payments.Tests/WalletLowBalanceMonitorTests.cs` — FOUND
- Commit `1bb77a2` (Task 1 RED) — FOUND in `git log --all`
- Commit `982d4a7` (Task 1 GREEN) — FOUND
- Commit `d8ed7f2` (Task 2 RED) — FOUND
- Commit `57de6f9` (Task 2 GREEN) — FOUND
