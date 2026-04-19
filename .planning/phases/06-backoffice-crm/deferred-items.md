# Phase 06 Backoffice/CRM — Deferred Items

Items discovered during plan execution that are **out of scope** for the
current plan (per the GSD executor SCOPE BOUNDARY rule) but should be
tracked for a future dedicated clean-up plan.

---

## Discovered during 06-04 Task 2 (credit-limit enforcement)

### Pre-existing: `WalletControllerTopUpTests` WebApplicationFactory host resolution

**Symptom:**
`dotnet test tests/Payments.Tests/ --filter "Category!=RedPlaceholder"` reports
7 failures with:
```
System.InvalidOperationException : The entry point exited without ever building an IHost.
  at Microsoft.Extensions.Hosting.HostFactoryResolver.HostingListener.CreateHost()
  at Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory`1.CreateHost(IHostBuilder builder)
  at Payments.Tests.WalletControllerTestFactory.CreateHost(IHostBuilder builder)
    in tests/Payments.Tests/WalletControllerTopUpTests.cs:line 175
```

**Failing tests (all in `WalletControllerTopUpTests`):**
- `T-05-03-03: above-max returns 400 problem+json`
- …and 6 siblings that hit `ClientFor(...)` at line 43.

**Verification that this is pre-existing (not caused by 06-04 Task 2):**
Stashed the entire Task 2 diff (`git stash push -u -m task2-inflight …`)
and re-ran the same filtered test command against commit `afbdf06` (the
worktree base). Same 7 failures, same stack trace. Restored the stash
afterwards.

**Why deferred:**
- Root cause is a `WebApplicationFactory` host-factory / entry-point
  resolution issue in `WalletControllerTestFactory` — unrelated to
  CreditLimit / D-61.
- SCOPE BOUNDARY forbids auto-fixing pre-existing infrastructure issues
  that would touch files outside the current plan's blast radius.
- Fixing it properly needs an isolated investigation into the
  `Program.cs` top-level-statements + `Main` convention collision that
  `HostFactoryResolver` flags — plausibly a future plan under the
  "payments test stabilisation" bucket.

**Impact on 06-04 Task 2:**
- Zero: the Task 2 baseline filter (`Category!=RedPlaceholder`) returns
  the same 46 passed / 7 failed result both with AND without Task 2
  applied, so the task introduced no regressions.
- The new `CreditLimitEnforcementTests` are all RedPlaceholder-tagged
  (MSSQL Testcontainer required) so they do not run under the baseline
  filter and do not add to the failure count.

**Recommended follow-up:**
Schedule a small "Phase 06 test infra stabilisation" plan that:
1. Pins `WalletControllerTestFactory` to an explicit `IHost` builder
   (`Host.CreateDefaultBuilder(...).ConfigureWebHostDefaults(...)`)
   rather than relying on `HostFactoryResolver` scraping `Program.cs`.
2. Asserts the factory is repeatably constructible in a standalone
   xUnit collection fixture.
3. Re-runs the 7 affected tests and confirms the baseline is fully
   green before any further payments plans merge.
