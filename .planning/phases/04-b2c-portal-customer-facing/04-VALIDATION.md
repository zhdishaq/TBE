---
phase: 4
slug: b2c-portal-customer-facing
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-16
---

# Phase 4 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | vitest 2.x (frontend), Playwright 1.x (E2E), xUnit (.NET backend additions) |
| **Config file** | `ui/vitest.config.ts` (Wave 0), `ui/playwright.config.ts` (Wave 0), existing `tests/*.csproj` |
| **Quick run command** | `pnpm --filter ui test:unit --run` |
| **Full suite command** | `pnpm --filter ui test:unit --run && pnpm --filter ui test:e2e && dotnet test` |
| **Estimated runtime** | ~120 seconds (unit ~20s, E2E ~80s, .NET ~20s) |

---

## Sampling Rate

- **After every task commit:** Run `pnpm --filter ui test:unit --run` (changed-file mode where possible)
- **After every plan wave:** Run full suite
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

> Populated by gsd-planner / gsd-nyquist-auditor. Each task in PLAN.md must map to a row here with an `<automated>` verification command or be flagged as a Wave 0 dependency or manual-only.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 4-XX-XX | TBD | TBD | TBD | TBD | TBD | TBD | TBD | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `ui/vitest.config.ts` + `ui/package.json` test scripts — vitest harness for B2C portal
- [ ] `ui/playwright.config.ts` + `ui/e2e/` scaffolding — E2E harness with Stripe test keys
- [ ] `ui/test/setup.ts` + MSW handlers — mock backend during unit tests
- [ ] Confirm Auth.js v5 beta + Next 16 + nuqs 2.8 mutual compatibility (smoke test) per RESEARCH.md Open Question 7
- [ ] Stripe test-mode keys + webhook signing secret available in `.env.test`

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Confirmation email arrives within 60s | NOTF-02 | SendGrid delivery latency requires real inbox | UAT script: book in test mode, time email arrival in dedicated test mailbox |
| Mobile responsive flow ≤5 steps | B2C-04 | Step-count + viewport judgment | UAT: complete booking on iPhone 12 viewport, count screens |
| PDF receipt rendering quality | B2C-08 | Visual review | UAT: download PDF, verify itinerary + payment summary correct |
| Hotel voucher PDF correctness | HOTB-04 | Visual review of supplier-format voucher | UAT: book hotel, open voucher PDF, confirm fields |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
