# Phase 4: B2C Portal (Customer-Facing) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-16
**Phase:** 04-b2c-portal-customer-facing
**Areas discussed:** Frontend structure + auth wiring, Trip Builder UX + checkout, Search UX + URL state, PDF receipt + voucher delivery

---

## Frontend structure + auth wiring

### App layout

| Option | Description | Selected |
|--------|-------------|----------|
| Fork starterKit → src/portals/b2c-web | Copy ui/starterKit into src/portals/b2c-web. Each portal is a separate Next.js app. Keep ui/starterKit pristine. | ✓ |
| pnpm workspace monorepo from day 1 | Shared packages/ui and packages/api-client across portals. | |
| Single Next.js app, route-based portals | One app with /b2c/*, /b2b/*, /backoffice/* route groups. | |

### Auth

| Option | Description | Selected |
|--------|-------------|----------|
| Auth.js v5, JWT session, forward access_token to YARP | JWT in httpOnly cookie. Server fetches forward Bearer token. Refresh rotation in callback. | ✓ |
| Auth.js v5, database session | Session backed by MSSQL via Prisma. | |
| BFF pattern: Next.js API routes proxy YARP | All API calls routed through Next.js API routes. | |

### Verify gate

| Option | Description | Selected |
|--------|-------------|----------|
| Use Keycloak built-in verification, gate booking step | Keycloak verify-email flow. Anonymous browse + search. Booking gated on email_verified. Dashboard read-only without verification. | ✓ |
| Custom verification endpoint, gate everything | Custom verify endpoint in CrmService. Block all authenticated routes. | |
| Verify on signup, no gate after | Force verification immediately after signup. | |

---

## Trip Builder UX + checkout

### TB layout

| Option | Description | Selected |
|--------|-------------|----------|
| Side-by-side: flight panel + hotel panel for same dates | Two parallel panels for same destination + dates; one flight + one hotel into single basket. | ✓ |
| Stepwise wizard: flight → hotel → review | Sequential 3-step flow. | |
| Combined results page — packaged cards | Backend pre-bundles flight+hotel combinations. | |

### Pay flow

| Option | Description | Selected |
|--------|-------------|----------|
| Single combined PaymentIntent for total basket | One Stripe PaymentIntent. BookingService creates two bookings linked via BasketId. | ✓ |
| Two PaymentIntents, parallel authorize | Authorize flight and hotel separately in parallel. | |
| Sequential PaymentIntents in same flow | Authorize flight first, then hotel; rollback on failure. | |

### Partial fail

| Option | Description | Selected |
|--------|-------------|----------|
| Confirm the success, refund the failure, email both outcomes | Capture flight portion, void hotel auth, single email covering both. Dashboard shows flight, no orphan hotel. | ✓ |
| All-or-nothing: void flight if hotel fails | Roll back entire basket on either failure. | |
| Hold both until both succeed | Don't capture either until both ticketed. | |

---

## Search UX + URL state

### URL state

| Option | Description | Selected |
|--------|-------------|----------|
| All search + filter state in URL via nuqs | All criteria and active filters in URL search params. Deep-linkable, shareable. | ✓ |
| Form state in URL, filter state in memory | Initial criteria in URL; filter sidebar in component state. | |
| All in memory, no URL state | Client-side state only. | |

### Filter mode

| Option | Description | Selected |
|--------|-------------|----------|
| Initial search server-side, filters/sort client-side | Server returns full set (cap 200). Filters/sort run client-side over cached TanStack data. | ✓ |
| Every filter triggers server fetch | Server applies filters/sort. | |
| Hybrid — client filter, server sort | Client handles filters; sort always re-runs on server. | |

### Layout

| Option | Description | Selected |
|--------|-------------|----------|
| Stacked cards, no paging — cap 200, scroll list | Cards top-to-bottom (segments, times, price, airline). Server caps at 200. Same on mobile. | ✓ |
| Cards + 'Load more' button (chunks of 25) | Render 25 initially; button reveals next 25. | |
| Comparison table — dense rows | Tabular layout (airline / depart / arrive / duration / stops / price). | |

---

## PDF receipt + voucher delivery

### PDF gen

| Option | Description | Selected |
|--------|-------------|----------|
| Server-side QuestPDF in BookingService, served via /api/bookings/{id}/receipt.pdf | Reuse Phase 3 QuestPDF. Same template as email attachment. Frontend renders download link. | ✓ |
| Client-side via @react-pdf/renderer | Generate PDF in browser on click. | |
| Server-side but new microservice (PdfService) | Dedicated PdfService for all PDF rendering. | |

### Voucher

| Option | Description | Selected |
|--------|-------------|----------|
| Reuse RazorLight + QuestPDF in NotificationService | Add HotelVoucher template + QuestPDF doc; consumer subscribes to HotelBookingConfirmed. | ✓ |
| New transactional template per supplier | Per-supplier voucher templates. | |
| Email links to download voucher (no attachment) | Deep link to /api/bookings/{id}/voucher.pdf. | |

### Dashboard

| Option | Description | Selected |
|--------|-------------|----------|
| Single GET /customers/me/bookings from BookingService | Reuse Phase 3 D-21 read endpoint via Next.js RSC + Auth.js session. | ✓ |
| Aggregated read across BookingService + CrmService | Compose data from BookingService and CrmService. | |
| BFF endpoint in Next.js API route assembling both | Next.js /api/dashboard route fans out internally. | |

---

## Claude's Discretion

- Exact card visual design, palette, brand assets
- nuqs vs native useSearchParams + custom serializer
- Skeleton/loading UX patterns
- Server actions vs API routes for booking initiation
- Specific Tailwind/shadcn-style composition decisions inside the forked starterKit
- Concrete URL parameter naming
- i18n scope (English-only at launch)
- Single-currency display until requirements expand
- Toast/error/empty-state copy

## Deferred Ideas

- pnpm workspaces — defer until B2B Phase 6
- Multi-language i18n — defer
- Multi-currency display — defer
- Native mobile apps — out of scope per PROJECT.md
- Loyalty / points UI — out of scope
- True dynamic packages — v2 (PKG2-01)
- Richer cancellation/modification UI beyond FLTB-10
- Per-supplier branded voucher templates
- Dedicated PdfService microservice
- BFF pattern routing all calls through Next.js API routes
