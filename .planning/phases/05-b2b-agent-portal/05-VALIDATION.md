---
phase: 5
slug: b2b-agent-portal
status: approved
nyquist_compliant: true
wave_0_complete: true
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
| T-05-00-01 | 05-00 | 0 | B2B-01 | Pitfall 19 | Portal session cookie scope isolated to b2b host (`__Secure-tbe-b2b.session-token`); no cross-origin leakage | unit | `pnpm --filter tbe-b2b-web test -- --run tests/smoke.test.tsx` | ✅ | ✅ green |
| T-05-00-02 | 05-00 | 0 | B2B-01 | D-32 | Separate Keycloak realm `tbe-b2b` (no OIDC brokering); audience-bound access tokens | manual | `bash infra/keycloak/verify-audience-smoke-b2b.sh` (fails-closed when env unset) | ✅ | ⬜ pending |
| T-05-00-03 | 05-00 | 0 | B2B-01 | Pitfall 17 | CSP omits `js.stripe.com` on non-wallet routes; Stripe only loads under `/admin/wallet/*` | unit | `grep -n "js.stripe.com" src/portals/b2b-web/next.config.mjs` (must appear only in walletCsp) | ✅ | ✅ green |
| T-05-00-04 | 05-00 | 0 | B2B-01 | — | Portal scaffold compiles + smoke test renders `<AgentPortalBadge />` | unit | `pnpm --filter tbe-b2b-web test` | ✅ | ✅ green |
| T-05-00-05 | 05-00 | 0 | B2B-01 | — | Portal dev server binds :3001 + /dashboard redirects unauthenticated to /api/auth/signin | e2e | `pnpm --filter tbe-b2b-web exec playwright test e2e/smoke.spec.ts` | ✅ | ⬜ pending |
| T-05-00-06 | 05-00 | 0 | B2B-01 | Pitfall 17 | UI components byte-for-byte identical to b2c-web (77 files verified via `diff -r` exit 0) | unit | `diff -r src/portals/b2c-web/components/ui src/portals/b2b-web/components/ui` | ✅ | ✅ green |
| T-05-00-07 | 05-00 | 0 | B2B-01 | D-33 | Audience smoke script exit codes documented (0=valid, 1=missing aud, 2=env unset) | manual | `bash infra/keycloak/verify-audience-smoke-b2b.sh` | ✅ | ⬜ pending |
| T-05-01-01 | 05-01 | 1 | B2B-05 | Pitfall 26 | `GET /api/b2b/agent/bookings` filters by server-side `agency_id` claim only, never request body | unit | `dotnet test tests/BookingService.Tests --filter "FullyQualifiedName~AgentBookingsControllerTests.ListForMe_returns_bookings_filtered_by_agency_id_claim_only_not_sub"` | ✅ | ❌ red |
| T-05-01-02 | 05-01 | 1 | B2B-05 | Pitfall 28 | Missing `agency_id` claim → 401 fail-closed | unit | `dotnet test tests/BookingService.Tests --filter "FullyQualifiedName~AgentBookingsControllerTests.ListForMe_returns_401_when_agency_id_claim_missing"` | ✅ | ❌ red |
| T-05-01-03 | 05-01 | 1 | B2B-05 | Pitfall 26 | Cross-agency booking GET → 403 (IDOR prevention) | unit | `dotnet test tests/BookingService.Tests --filter "FullyQualifiedName~AgentBookingsControllerTests.GetById_returns_403_when_booking_belongs_to_different_agency"` | ✅ | ❌ red |
| T-05-01-04 | 05-01 | 1 | B2B-07 | D-37 | Per-booking markup override = agent-admin role only (403 for agent/agent-readonly) | unit | `dotnet test tests/BookingService.Tests --filter "FullyQualifiedName~AgentBookingsControllerTests.OverrideMarkup_returns_403_when_role_is_not_agent_admin"` | ✅ | ❌ red |
| T-05-02-01 | 05-02 | 2 | B2B-04 | D-24 | `BookingSagaState.Channel` defaults to `Channel.B2C` (0) for backward compatibility | unit | `dotnet test tests/BookingService.Tests --filter "FullyQualifiedName~BookingSagaB2BChannelTests.BookingSagaState_Channel_defaults_to_B2C"` | ✅ | ❌ red |
| T-05-02-02 | 05-02 | 2 | B2B-04 | D-24 | `BookingInitiated` contract carries `Channel` + `AgencyId` + `WalletId` | unit | `dotnet test tests/BookingService.Tests --filter "FullyQualifiedName~BookingSagaB2BChannelTests.BookingInitiated_carries_Channel_and_AgencyId_and_WalletId"` | ✅ | ❌ red |
| T-05-02-03 | 05-02 | 2 | B2B-04 | D-24 | B2B branch at `PnrCreated` publishes `WalletReserveCommand` (not `AuthorizePaymentCommand`) | unit | `dotnet test tests/BookingService.Tests --filter "FullyQualifiedName~BookingSagaB2BBranchTests.PnrCreated_publishes_WalletReserveCommand_when_Channel_is_B2B"` | ✅ | ❌ red |
| T-05-02-04 | 05-02 | 2 | B2B-04 | — | B2C branch unchanged — still publishes `AuthorizePaymentCommand` | unit | `dotnet test tests/BookingService.Tests --filter "FullyQualifiedName~BookingSagaB2BBranchTests.PnrCreated_publishes_AuthorizePaymentCommand_when_Channel_is_B2C"` | ✅ | ❌ red |
| T-05-02-05 | 05-02 | 2 | B2B-04 | D-39 | Pre-ticket compensation publishes `WalletReleaseCommand`; post-`TicketIssued` does NOT (manual Phase 6) | unit | `dotnet test tests/BookingService.Tests --filter "FullyQualifiedName~BookingSagaB2BBranchTests.Compensation_publishes_WalletReleaseCommand_on_pre_ticket_failure_and_not_after_TicketIssued"` | ✅ | ❌ red |
| T-05-02-06 | 05-02 | 2 | B2B-08 | D-36 | `ApplyMarkup` resolver picks RouteClass-specific row when present | unit | `dotnet test tests/Pricing.Tests --filter "FullyQualifiedName~ApplyMarkupTests.ApplyMarkup_uses_RouteClass_specific_row_when_present"` | ✅ | ❌ red |
| T-05-02-07 | 05-02 | 2 | B2B-08 | D-36 | `ApplyMarkup` falls back to agency base row when no RouteClass override matches | unit | `dotnet test tests/Pricing.Tests --filter "FullyQualifiedName~ApplyMarkupTests.ApplyMarkup_falls_back_to_base_row_when_RouteClass_not_matched"` | ✅ | ❌ red |
| T-05-02-08 | 05-02 | 2 | B2B-08 | D-41 | PricingService returns net/markup/gross/commission with commission==markup (v1) | unit | `dotnet test tests/Pricing.Tests --filter "FullyQualifiedName~ApplyMarkupTests.ApplyMarkup_returns_net_markup_gross_commission_and_commission_equals_markup_v1"` | ✅ | ❌ red |
| T-05-02-09 | 05-02 | 2 | B2B-08 | Pitfall 23 | `AgencyPriceRequested` consumer publishes `AgencyPriceResponse` with server-side markup (never echoed from client) | unit | `dotnet test tests/Pricing.Tests --filter "FullyQualifiedName~AgencyPriceRequestedConsumerTests.Consumer_publishes_AgencyPriceResponse_with_computed_markup_for_agency"` | ✅ | ❌ red |
| T-05-03-01 | 05-03 | 3 | B2B-06 | D-40 | Wallet top-up below `MinTopUpAmountCents` returns 400 problem+json BEFORE Stripe PaymentIntent | unit | `dotnet test tests/Payments.Tests --filter "FullyQualifiedName~WalletTopUpCapsTests.TopUp_returns_400_problem_json_when_amount_below_MinTopUpAmountCents"` | ✅ | ❌ red |
| T-05-03-02 | 05-03 | 3 | B2B-06 | D-40 | Wallet top-up above `MaxTopUpAmountCents` returns 400 problem+json BEFORE Stripe PaymentIntent | unit | `dotnet test tests/Payments.Tests --filter "FullyQualifiedName~WalletTopUpCapsTests.TopUp_returns_400_problem_json_when_amount_above_MaxTopUpAmountCents"` | ✅ | ❌ red |
| T-05-03-03 | 05-03 | 3 | B2B-06 | — | problem+json response body contains `min`, `max`, `attempted` fields for portal UX | unit | `dotnet test tests/Payments.Tests --filter "FullyQualifiedName~WalletTopUpCapsTests.TopUp_problem_json_contains_min_max_attempted_fields"` | ✅ | ❌ red |
| T-05-04-01 | 05-04 | 4 | B2B-10 | D-43 | Agency invoice PDF renders gross (base + surcharges + taxes) and total only | integration | `dotnet test tests/Notifications.Tests --filter "FullyQualifiedName~AgencyInvoiceDocumentTests.Invoice_renders_gross_base_surcharges_taxes_and_total_only"` | ✅ | ❌ red |
| T-05-04-02 | 05-04 | 4 | B2B-10 | D-43 | PDF text never contains NET/markup/commission strings (PdfPig substring negative assertion) | integration | `dotnet test tests/Notifications.Tests --filter "FullyQualifiedName~AgencyInvoiceDocumentTests.Invoice_never_renders_NET_markup_or_commission_strings"` | ✅ | ❌ red |
| T-05-04-03 | 05-04 | 4 | B2B-10 | D-43 | VAT line renders only when `InvoiceModel.Vat` is not null | unit | `dotnet test tests/Notifications.Tests --filter "FullyQualifiedName~AgencyInvoiceDocumentTests.Invoice_renders_VAT_line_when_model_Vat_is_not_null"` | ✅ | ❌ red |
| T-05-04-04 | 05-04 | 4 | B2B-10 | Pitfall 26 | `GET /api/b2b/invoice/{id}` returns 403 when booking `AgencyId` differs from caller claim | unit | `dotnet test tests/Notifications.Tests --filter "FullyQualifiedName~AgencyInvoiceControllerTests.GetInvoice_returns_403_when_booking_AgencyId_differs_from_claim"` | ✅ | ❌ red |
| T-05-04-05 | 05-04 | 4 | B2B-10 | Pitfall 28 | `GET /api/b2b/invoice/{id}` returns 401 when `agency_id` claim is missing (fail-closed) | unit | `dotnet test tests/Notifications.Tests --filter "FullyQualifiedName~AgencyInvoiceControllerTests.GetInvoice_returns_401_when_agency_id_claim_missing"` | ✅ | ❌ red |
| T-05-04-06 | 05-04 | 4 | B2B-10 | — | Invoice response content-type = `application/pdf` (inline browser render) | unit | `dotnet test tests/Notifications.Tests --filter "FullyQualifiedName~AgencyInvoiceControllerTests.GetInvoice_returns_stream_content_type_application_pdf"` | ✅ | ❌ red |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

*Red-placeholder count (22): populated by Plan 05-00 Task 2 (Wave 0) — each ❌ red row becomes ✅ green when its owning plan (01–04) implements the behaviour.*

---

## Wave 0 Requirements

- [x] `src/portals/b2b-web/` scaffold forked from `src/portals/b2c-web/` (commit 8b8a376; 112 files, 77 UI components byte-for-byte via `diff -r` exit 0)
- [x] `tests/BookingService.Tests/Agency*Tests.cs` — 9 red placeholders for B2B-05 (Plan 05-01) and B2B-04 (Plan 05-02)
- [x] `tests/Payments.Tests/WalletTopUpCapsTests.cs` — 3 red placeholders for B2B-06 wallet top-up path (D-40)
- [x] Portal Vitest + Playwright config files installed (`vitest.config.ts`, `playwright.config.ts`, smoke tests green)
- [x] `infra/keycloak/realm-tbe-b2b.json` delta staged (realm patch with audience mapper + agency-id-attribute mapper; commit d5d1f35)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Concurrent wallet double-spend test | B2B-07 | Requires two simultaneous HTTP requests with assert-once semantics | UAT: run `scripts/wallet-double-spend.sh` (Wave 3) or two `curl` calls in parallel against `POST /api/b2b/bookings` with amounts summing above balance; assert exactly one 200 + one 402 |
| Agency isolation smoke | B2B-01, B2B-05, B2B-08 | Requires two real Keycloak users in different agencies | UAT: login as agency-A-agent, confirm booking X, logout, login as agency-B-agent, navigate to `/agent/bookings/X` → expect 404/403 |
| Ticketing deadline 24h/2h alert firing | B2B-09 | Requires time travel or PNR with real TTL | UAT: seed a PNR with `TicketingTimeLimit = now + 23h`, run `dotnet test --filter TicketingMonitorTests`; assert `Warn24HSent=true`, email artifact present in MailHog |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies (28 rows)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (22 red placeholders, all traced to plan+requirement)
- [x] No watch-mode flags
- [x] Feedback latency < 30s (`dotnet test tests/BookingService.Tests ...` 6s; vitest smoke 5.13s)
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** Wave 0 complete (2026-04-17, Plan 05-00)
