# Phase 03 Deferred Items

## Tests requiring Docker/Testcontainers (out of scope for 03-03)

- `Payments.Tests`: 4 failures (PAY05 reserve/commit, PAY05 reserve/release, PAY06 concurrency, PAY06 idempotent retry) — all fail at Testcontainers startup: `Docker is either not running or misconfigured`. These are pre-existing Phase 3 integration tests that need a running Docker Desktop; not related to plan 03-03 changes.

## Tests with no test runner configured

- `Notifications.Tests`: no test adapter registered — `dotnet test` finds the assembly but no xunit runner. Should be converted in a later notifications plan.

Recorded: 2026-04-15 during plan 03-03 execution.
