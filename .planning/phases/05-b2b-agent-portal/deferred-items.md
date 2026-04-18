# Phase 05 — deferred items

Running log of items identified by plans in this phase but deferred to a
follow-up plan. Each bullet should be picked up by a fresh `/gsd-plan`
invocation when the phase returns to them.

## From Plan 05-03 Task 3

- [ ] `/admin/wallet` portal surface (13 files — Next.js client/server
      components, Stripe Elements wrapper, route-scoped CSP narrowing,
      sitewide low-balance banner, RequestTopUpLink mailto,
      insufficient-funds-panel retrofit + Vitest suite).

## From Plan 05-04 (Agency invoice + IDOR gates)

### Backend — BookingService

- [ ] `AgentBookingsController.VoidAsync(Guid id, CancellationToken ct)`
      endpoint with:
  - `[Authorize(Policy="B2BAdminPolicy")]`
  - 404 Pitfall 10 on cross-tenant (never 403 — never leak existence)
  - 409 D-39 post-ticket cancel (NotBookable RFC 7807)
  - 202 AcceptedAtAction on pre-ticket with `VoidRequested` publish
- [ ] `AgentBookingsController.ListForAgencyAsync` filter extensions:
  - Client-name contains-filter, PNR equals-filter, status-filter,
    `from` / `to` `DateTime?` bracket, 20/50/100 page size cap
  - Server-side sort
  - nuqs URL-sync pre-work (no server impact)
- [ ] `BookingSaga` `VoidRequested` event + activity handling
  - Pre-ticket: wallet release compensation + GDS void publish
  - Post-ticket: ignore / log (controller already 409s before saga reaches)
- [ ] `B2BAdminPolicy` registration in `BookingService.API/Program.cs`
      (copy-paste from PaymentService Plan 05-03 reg)

### Backend — NotificationService (new surface)

- [ ] `AgencyInvoiceDocument.cs` QuestPDF template — GROSS only (D-43).
      Tests via PdfPig text extraction must assert "NET" / "Markup" /
      "Commission" / negative-sign literals are absent from the rendered
      PDF text stream.
- [ ] `InvoicesController` at route `/api/invoices/{bookingId}.pdf`
  - `[Authorize(Policy="B2BPolicy")]`
  - Reads saga state → 404 if not found OR agency mismatch (Pitfall 10)
  - `application/pdf` + `Content-Disposition: inline; filename="..."`
- [ ] Rewrite the RED placeholders in `tests/Notifications.Tests/`:
  - `AgencyInvoiceControllerTests.cs` must expect `404 NotFoundResult`
    (NOT `403 ForbidResult`) on cross-tenant — plan is authoritative
    over the staged red placeholder.
- [ ] Register `B2BPolicy` in NotificationService (not currently wired
      there; only PaymentService + BookingService have it).

### Backend — Consumers (B2B-09 email fan-out)

- [ ] `TicketingDeadlineConsumer`:
  - `IConsumer<TicketingDeadlineWarning>` — amber "Heads-up" email
  - `IConsumer<TicketingDeadlineUrgent>` — red "URGENT:" email
  - Uses the Plan 05-03 `IKeycloakB2BAdminClient` to resolve recipients
    (intersection of `agency_id` attribute AND `agent-admin` OR `agent`
    role; exclude `agent-readonly`).
  - Stub `ITicketingDeadlineEmailSender` (logger-only Phase-5 MVP,
    mirroring `WalletLowBalanceEmailSender`).

### Portal — b2b-web (Plan 05-04 Task 3, 16 files)

- [ ] `app/(portal)/dashboard/page.tsx` — RSC page composing
      `GET /api/dashboard/summary` + `GET /api/wallet/me` responses
      server-side and rendering a 2-column grid (D-44).
- [ ] `app/(portal)/dashboard/ttl-alerts-card.tsx` — amber < 24h, red < 2h
- [ ] `app/(portal)/dashboard/wallet-summary-card.tsx`
- [ ] `app/(portal)/dashboard/recent-bookings-card.tsx`
- [ ] `app/(portal)/dashboard/quick-links-grid.tsx` — 4 tiles, admin-only
      hidden tile for `/admin/wallet`
- [ ] `app/(portal)/bookings/page.tsx` + filters + table + pager
      (20/50/100; nuqs URL-synced filters)
- [ ] `app/(portal)/bookings/[id]/page.tsx` + status-card + ttl-countdown
      (`aria-live=off` each tick, `aria-live=polite` on threshold cross)
      + void-booking-button (Radix AlertDialog destructive confirm) +
      documents-panel
- [ ] Route handlers: `app/api/bookings/route.ts`,
      `app/api/bookings/[id]/void/route.ts`,
      `app/api/bookings/[id]/invoice.pdf/route.ts`,
      `app/api/bookings/[id]/e-ticket.pdf/route.ts`,
      `app/api/dashboard/summary/route.ts` — all server proxies,
      Pitfall 14 `await params`, Pitfall 11 stream-through
      `new Response(upstream.body, ...)` for PDFs
- [ ] `app/forbidden/page.tsx` — generic copy (never leak booking
      existence; ref'd by Pitfall 10 on any 404 that the handler
      chooses to redirect rather than JSON-respond)

## Audit notes

- Contracts `TicketingDeadlineWarning` / `TicketingDeadlineUrgent` are
  already published by the TTL monitor at the correct horizons — but
  **nothing consumes them yet**. This means a running BookingService
  with no B2B consumer registered will silently accumulate
  `skipped_messages` on the default exchange. Follow-up plan should
  register the consumer in the same commit as the email sender stub.
