---
phase: 5
slug: b2b-agent-portal
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-17
---

# Phase 5 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.x (backend) + Vitest + Playwright (portal) |
| **Config file** | `tests/*.Tests/*.csproj`, `src/portals/b2b-web/vitest.config.ts`, `src/portals/b2b-web/playwright.config.ts` (Wave 0 installs) |
| **Quick run command** | `dotnet test tests/BookingService.Tests/BookingService.Tests.csproj --filter "Category!=RedPlaceholder&Category!=Integration"` |
| **Full suite command** | `dotnet test && pnpm --filter b2b-web test && pnpm --filter b2b-web test:e2e` |
| **Estimated runtime** | ~180 seconds (unit + fast integration); ~6 min with e2e |

---

## Sampling Rate

- **After every task commit:** Run quick command (xUnit fast filter)
- **After every plan wave:** Run full suite
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds for quick command

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD after planning | — | — | — | — | — | — | — | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

*Table will be populated during `/gsd-plan-phase 5` planner pass — one row per task with concrete dotnet/vitest/playwright command.*

---

## Wave 0 Requirements

- [ ] `src/portals/b2b-web/` scaffold forked from `ui/starterKit` (mirror Plan 04-00)
- [ ] `tests/BookingService.Tests/Agency*Tests.cs` — red placeholders for B2B-03/04/05/06/07
- [ ] `tests/PaymentService.Tests/WalletTopUp*Tests.cs` — red placeholder for B2B-06 wallet top-up path
- [ ] Portal Vitest + Playwright config files installed
- [ ] `infra/keycloak/realm-tbe-b2b.json` delta staged (realm patch only, not full export)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Concurrent wallet double-spend test | B2B-07 | Requires two simultaneous HTTP requests with assert-once semantics | UAT: run `scripts/wallet-double-spend.sh` (Wave 3) or two `curl` calls in parallel against `POST /api/b2b/bookings` with amounts summing above balance; assert exactly one 200 + one 402 |
| Agency isolation smoke | B2B-01, B2B-05, B2B-08 | Requires two real Keycloak users in different agencies | UAT: login as agency-A-agent, confirm booking X, logout, login as agency-B-agent, navigate to `/agent/bookings/X` → expect 404/403 |
| Ticketing deadline 24h/2h alert firing | B2B-09 | Requires time travel or PNR with real TTL | UAT: seed a PNR with `TicketingTimeLimit = now + 23h`, run `dotnet test --filter TicketingMonitorTests`; assert `Warn24HSent=true`, email artifact present in MailHog |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
