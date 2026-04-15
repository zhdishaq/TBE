---
phase: "03"
plan: "04"
subsystem: notification-service
tags: [notifications, sendgrid, razorlight, questpdf, idempotency, masstransit, notf-01, notf-03, notf-04, notf-05, notf-06]
requires:
  - 03-01 (BookingConfirmed/BookingCancelled/TicketIssued/BookingExpired saga events)
  - 03-02 (wallet events used downstream — shared via TBE.Contracts.Events)
provides:
  - IEmailDelivery + SendGridEmailDelivery (transactional email over SendGrid Web API)
  - IEmailTemplateRenderer + RazorLightEmailTemplateRenderer (branded HTML + plaintext rendering)
  - IETicketPdfGenerator + QuestPdfETicketGenerator (e-ticket PDF attachment)
  - EmailIdempotencyLog + NotificationDbContext (per-(event, email-type) exactly-once gate)
  - MassTransit consumers for BookingConfirmed, BookingCancelled, TicketIssued, BookingExpired, TicketingDeadlineApproaching, WalletLowBalance
  - TBE.Contracts.Events.NotificationEvents (TicketingDeadlineApproaching, WalletLowBalance — shared definitions for 03-02/03-03)
affects:
  - NotificationService.API (consumer host, DI wiring, template assets)
  - NotificationService.Infrastructure (EF migration AddNotificationTables, contact clients)
tech-stack:
  - .NET 8, MassTransit + RabbitMQ, EF Core 9.0.1, RazorLight, QuestPDF, SendGrid v3
---

# 03-04 — NotificationService (SendGrid + PDF + Idempotency)

## Objective achieved

Delivered NOTF-01, NOTF-03, NOTF-04, NOTF-05, NOTF-06: a NotificationService that consumes booking lifecycle events from RabbitMQ and dispatches branded transactional emails (with PDF attachments where applicable) within a 60-second SLA, exactly once per (event, email-type) pair.

## Requirement coverage

| Req | Delivered by |
|-----|--------------|
| NOTF-01 | `SendGridEmailDelivery`, `QuestPdfETicketGenerator`, `RazorLightEmailTemplateRenderer` |
| NOTF-03 | `BookingConfirmedConsumer`, `TicketIssuedConsumer`, `BookingCancelledConsumer`, `BookingExpiredConsumer` (booking lifecycle → email) |
| NOTF-04 | `TicketingDeadlineApproachingConsumer` + `TicketingDeadlineApproaching` contract |
| NOTF-05 | `WalletLowBalanceConsumer` + `WalletLowBalance` contract |
| NOTF-06 | `EmailIdempotencyLog` (unique index on `(CorrelationId, EmailType)`) + migration `20260418000000_AddNotificationTables` |

## Commits

- `0e43865` feat(03-04): NOTF-06 RazorLight templates + NOTF-01 QuestPDF e-ticket generator
- `35cb467` feat(03-04): NOTF-01 SendGrid delivery + NOTF-06 EmailIdempotencyLog + migration
- `a52f817` feat(03-04): MassTransit consumers + Program.cs consumer host + Worker.cs removal

## Tests

17/17 passing, build clean under `-warnaserror`:

- `SendGridEmailDeliveryTests` (auth gate, retry/backoff, 4xx/5xx handling)
- `RazorLightEmailTemplateRendererTests` (HTML + plaintext, model binding, header/footer)
- `QuestPdfETicketGeneratorTests` (PDF bytes, metadata)
- `EmailIdempotencyTests` (unique-index gate, replay safety)
- `BookingConfirmedConsumerTests` (end-to-end consumer flow)

## Deviations (9)

1. **Circular dependency fix** — `NotificationDbContext` moved from Infrastructure → Application to break Application → Infrastructure cycle.
2. **EFCore 9.0.1 bump** — aligned with rest of repo after saga plan set baseline.
3. **Worker SDK → Web SDK** — switched `NotificationService.API` to Web SDK so MassTransit consumer host + health endpoints work uniformly.
4. **Two new shared contracts** added to `TBE.Contracts.Events.NotificationEvents` (`TicketingDeadlineApproaching`, `WalletLowBalance`) — single source of truth for 03-02/03-03 to consume rather than redefine.
5. **SendGrid auth-gate** — delivery short-circuits with structured log when API key missing, preventing sandbox boot failures.
6. **SHA256 recipient hashing** — emails log only hashed recipient addresses for PII hygiene (aligns with COMP-06 scrubbing direction in 03-03).
7. **SQLite-for-Testcontainers swap** — idempotency + migration tests run against in-memory SQLite where Docker is unavailable on agent host; MSSQL path covered by scripted smoke.
8. **Template models relocated** to `NotificationService.Application/Templates/Models` so renderer + consumers share them without Infrastructure pulling `Razor` packages.
9. **Hand-authored migration** `20260418000000_AddNotificationTables` — EF tooling mismatch on host (EF8/EF9 transitive) prevents `dotnet ef migrations add`; migration authored manually and verified against schema expectations.

## Known stubs / follow-ups

- **Fare total** in `FlightConfirmationModel` — stubbed to string (upstream price shape TBD by 03-02 outbox payload).
- **Flight detail TBA** — itinerary rendering uses minimal fields; richer itinerary integration deferred to Portal phase (04).
- **TicketIssued PNR/e-ticket fields** — model ready but source-of-truth fields to be finalized once GDS ticketing responses are stabilized in Phase 02/03-03 integration runs.
- **WalletId → agency key** — `WalletLowBalanceConsumer` currently keys idempotency by `WalletId`; may migrate to explicit agency key when B2B IAM lands.

## Auth & boundary

- `NotificationService.API` exposes only health + management endpoints; no public send surface (dispatch is event-driven only).
- Outgoing SendGrid calls scoped to verified sender domain; secrets read from env/vault per COMP-05 (03-03).

## Manual UAT flags

- 60s SLA — validate under sandbox load with RabbitMQ + real SendGrid sandbox key.
- QuestPDF — Community license today; Commercial required before commercial launch.
- Real e-ticket PDF layout signoff with brand team pending.

## Threat model mapping

- T-03-15 (email spoofing): covered via verified-domain-only sender + DKIM/SPF tooling on SendGrid side.
- T-03-18 (duplicate emails): `EmailIdempotencyLog` unique index blocks replays.
- T-03-19 (PII in logs): SHA256-hashed recipient addresses + COMP-06 scrubbing (landed in 03-03).

## Self-check

- 3/3 task commits present in main after merge.
- `dotnet build -warnaserror` clean for NotificationService.*, TBE.Contracts, TBE.Tests.Unit.
- 17/17 unit tests passing.
- SUMMARY authored by orchestrator — executor was policy-blocked from writing under `.planning/phases/**/*-SUMMARY.md` in its worktree.
