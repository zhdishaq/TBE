# Phase 4: B2C Portal (Customer-Facing) - Context

**Gathered:** 2026-04-16
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver a publicly launchable B2C portal: customers can register, verify email, log in, search flights/hotels/cars, build trips combining flight + hotel, complete payment via Stripe Elements, receive confirmation emails with e-tickets/vouchers within 60 seconds, and download a PDF receipt — all on mobile or desktop.

Out of scope for Phase 4 (already deferred to other phases): B2B agent portal (Phase 6), backoffice/CRM (Phase 7), cancellation/modification UX beyond what FLTB-10 already covers, true dynamic package fares (PKG-04 keeps independent refs), loyalty.

</domain>

<decisions>
## Implementation Decisions

### Frontend Project Structure
- **D-01:** Fork `ui/starterKit` (Metronic 9 — Next.js 16, React 19, Tailwind v4, ReUI/Radix, TanStack Query, react-hook-form, zod, sonner) into `src/portals/b2c-web/` as the B2C app. Keep `ui/starterKit` as the pristine reference for future portals (B2B Phase 6, backoffice Phase 7).
- **D-02:** Each portal will be a separate Next.js app under `src/portals/` (no monorepo tooling for Phase 4). Add pnpm workspaces later if and when shared component packages emerge — premature for v1.
- **D-03:** Frontend stack pinned to what `ui/starterKit/package.json` ships: Next.js 16 App Router, React 19, Tailwind v4, ReUI / Radix UI primitives, TanStack Query v5, react-hook-form + zod, sonner for toasts, lucide-react icons. No additional UI libraries unless strictly necessary.

### Authentication
- **D-04:** Auth.js v5 with Keycloak OIDC provider against the `tbe-b2c` realm. Session strategy: **JWT in httpOnly cookie**. No database session.
- **D-05:** Server-side data fetching uses `auth()` (Auth.js v5 server helper) to read the session and forward `access_token` as `Authorization: Bearer …` to YARP gateway. No tokens exposed to client JavaScript. Refresh token rotation handled in the Auth.js callback chain.
- **D-06:** Email verification uses **Keycloak's built-in verify-email flow** (no custom verifier in CrmService). Anonymous users can browse and search. Authenticated-but-unverified users can view their dashboard read-only. **The booking checkout step is gated on `email_verified=true` in the JWT** — attempts to proceed open a "verify your email" modal with a resend link.

### Trip Builder UX & Checkout
- **D-07:** Trip Builder layout: **side-by-side flight + hotel panels** for the same destination/dates. Customer picks one flight and one hotel into a single basket. Matches the ROADMAP wording exactly.
- **D-08:** Single combined Stripe **PaymentIntent** for the basket total (flight subtotal + hotel subtotal + taxes/surcharges shown line-itemized). One charge on the customer's statement. BookingService still creates two independent bookings with their own references and cancellation policies (PKG-04), linked via a parent `BasketId`.
- **D-09:** Partial-failure UX: **confirm the success, refund/release the failure, send a single email covering both outcomes.** Example — flight ticketed, hotel inventory gone: capture the flight portion, void the hotel-portion authorization, email "Flight booked: ABC123. Hotel unavailable — your card was only charged for the flight. Try another hotel?". Dashboard shows the flight; no orphan hotel record.
- **D-10:** Trip Builder still relies on the same Phase 3 saga primitives: each component goes through PNR/hold → authorize → ticket/confirm → capture. The combined PaymentIntent is captured in two stages mapped to flight-ticketed and hotel-confirmed events; Stripe `partial capture` API is used for the partial-success path.

### Search UX & URL State
- **D-11:** All search and filter state is encoded in URL search params via **`nuqs`** (or Next.js native `useSearchParams` if nuqs is not desired) — origin, destination, dates, pax breakdown, cabin class, sort key, all active filters. Result pages are deep-linkable, shareable, and browser-back works correctly. TanStack Query keys are derived from URL state.
- **D-12:** **Initial search executes server-side via the gateway → Pricing/Inventory services**, returning up to 200 results. **Filters (stops, airline, departure window, price range) and sort run client-side** over the cached TanStack Query data. Re-fetch only when search criteria change (origin/destination/dates/pax). No GDS call per filter click.
- **D-13:** Flight result layout: **stacked cards, no pagination** (server cap 200, mobile-friendly vertical scroll). Each card renders airline, segments, times, duration, stops, all-in price (FLTB-03 split between fare / YQ-YR / taxes). Hotel results follow the same card pattern with photos and key amenities.
- **D-14:** Mobile completion target ≤5 steps (B2C-05): `search → results+filter → select+review → passenger/guest details → payment`. Confirmation screen is post-completion (does not count toward the 5).

### PDF Receipt & Voucher Delivery
- **D-15:** PDF receipt generation: **server-side QuestPDF in BookingService**, exposed via `[Authorize] GET /api/bookings/{id}/receipt.pdf`. Reuses Phase 3 QuestPDF setup (already used for e-ticket attachments). Same template as the email attachment — single source of truth. The dashboard renders a download link to this endpoint.
- **D-16:** Hotel voucher email (NOTF-02): **reuse RazorLight + QuestPDF in NotificationService**. Add a `HotelVoucher.cshtml` Razor template + a `HotelVoucherDocument` QuestPDF document. New consumer subscribes to `HotelBookingConfirmed`. Same SendGrid path and `EmailIdempotencyLog` table as Phase 3 (D-19). Zero new infrastructure.
- **D-17:** Customer dashboard data: **single `GET /customers/me/bookings` call to BookingService** (reuses Phase 3 D-21 read endpoint). Dashboard route is a Next.js RSC that reads the Auth.js session, calls the gateway server-side, and renders. Client refresh uses TanStack Query. Filter upcoming-vs-past in the projection by departure date.

### Search Form Specifics
- **D-18:** IATA airport autocomplete (B2C-03): driven by a typeahead endpoint backed by Redis. Initial seed list is the IATA airports CSV loaded into Redis on InventoryService startup. No external API at typeahead time.
- **D-19:** Date pickers use `react-day-picker` (already in starterKit). Passenger selector supports adult/child/infant-on-lap/infant-in-seat (FLTB-01) — separate counters with airline-rule validation (max 9 pax, infants ≤ adults, etc.).

### Claude's Discretion
- Exact card visual design, palette, brand assets — Metronic defaults stand until brand assets exist.
- Whether to use `nuqs` specifically vs native `useSearchParams` + custom serializer — pick whichever has cleaner ergonomics during planning.
- Skeleton/loading UX patterns — Metronic skeletons, suspense boundaries.
- Server actions vs API routes for booking initiation — planner picks; default to API routes calling YARP for consistency with non-Next clients (future B2B).
- Specific Tailwind utility patterns / shadcn-style component composition decisions inside the forked starterKit.
- Concrete query parameter names in URL state (e.g., `from=` vs `o=`, `dep=` vs `d=`).
- Internationalization scope — i18next ships in starterKit; English-only at launch unless requirements expand. Multi-currency display deferred (single GBP display assumed unless clarified).
- Choice of toast wording, error copy, empty-state copy.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & roadmap
- `.planning/REQUIREMENTS.md` — B2C-01..08, HOTB-01..05, CARB-01..03, PKG-01..04, NOTF-02, FLTB-01..03 acceptance criteria
- `.planning/ROADMAP.md` §Phase 4 — Locked plan scope (4 plans, UAT criteria, depends on Phase 3)

### Architecture & stack
- `.planning/research/ARCHITECTURE.md` — Service topology and gateway routing
- `.planning/research/STACK.md` — Confirmed Next.js, MSSQL, Stripe, Keycloak, MassTransit, RabbitMQ
- `.planning/research/PITFALLS.md` — Known traps (esp. Stripe authorize-vs-capture, GDS pricing TTL)
- `.planning/research/SUMMARY.md` — Synthesized critical rules

### Prior phase decisions (locked)
- `.planning/phases/01-infrastructure-foundation/01-CONTEXT.md` — Service layout, shared projects, RabbitMQ topology, Keycloak realms (`tbe-b2c` realm is the auth target for this phase)
- `.planning/phases/02-inventory-layer-gds-integration/02-RESEARCH.md` — Search adapter contracts; this phase consumes them via the gateway for flight/hotel/car search
- `.planning/phases/03-core-flight-booking-saga-b2c/03-CONTEXT.md` — Booking saga, Stripe authorize-before-capture (D-11), webhook-only confirmation (D-12), idempotency keys (D-13), QuestPDF (D-18), RazorLight templates (D-17), customer read endpoints (D-21), email idempotency (D-19)

### UI baseline
- `ui/starterKit/package.json` — Pinned frontend stack (Next.js 16, React 19, Tailwind v4, ReUI/Radix, TanStack Query, react-hook-form, zod)
- `ui/starterKit/` (full tree) — Component library, layouts, conventions to fork into `src/portals/b2c-web/`
- `ui/demos/` — Reference layouts and patterns

### Compliance
- `.planning/PROJECT.md` §Constraints — PCI-DSS scope (SAQ-A via Stripe Elements only), GDPR
- `.planning/REQUIREMENTS.md` §COMP — COMP-01 (no card data server-side), COMP-04 (JWT on all endpoints)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ui/starterKit/` — Metronic 9 Next.js 16 starter with Tailwind v4, ReUI/Radix primitives, TanStack Query, react-hook-form + zod, sonner toasts, react-day-picker. Will be forked into `src/portals/b2c-web/`.
- `ui/demos/` — Reference layouts, page patterns, and component compositions to mine for B2C UX.
- Phase 3 `BookingService` read endpoints (`GET /bookings/{id}`, `GET /customers/{id}/bookings` per D-21) — dashboard consumes these directly.
- Phase 3 `NotificationService` RazorLight + QuestPDF + SendGrid pipeline — extend for hotel voucher (NOTF-02).
- Phase 3 `BookingService` QuestPDF e-ticket renderer — extend / share template for receipt PDF endpoint.
- Phase 2 GDS connector services (`FlightConnectorService`) — search results flow through the gateway from these.
- Phase 1 YARP gateway — all B2C → backend traffic routes here with JWT validation.
- `tbe-b2c` Keycloak realm — already provisioned in Phase 1.

### Established Patterns
- Three-project .NET service layout (API/Application/Infrastructure) — backend additions follow this.
- MassTransit + EFCore outbox for async messaging — any new event consumers (hotel-confirmed, basket-paid) follow this.
- Cross-service comms only via `TBE.Contracts` messages — no direct project references.
- Stripe authorize-then-capture, webhook-only confirmation, deterministic idempotency keys — locked from Phase 3, applies to combined-basket PaymentIntent too.

### Integration Points
- Frontend (`src/portals/b2c-web/`) → YARP gateway → BookingService / PricingService / InventoryService / CrmService.
- Auth.js v5 callback → Keycloak `tbe-b2c` realm OIDC discovery doc.
- Server-side fetch in Next.js RSC → Auth.js `auth()` → Authorization: Bearer JWT → YARP.
- Stripe Elements (frontend) → Stripe API → Stripe webhook → BookingService (Phase 3 PAY-02).
- New `HotelBookingConfirmed` event → NotificationService voucher consumer (NOTF-02).

</code_context>

<specifics>
## Specific Ideas

- "Mobile-responsive booking flow completable in under 5 steps" (B2C-05) is **measured as: search → results → select → details → payment**. Confirmation screen does not count.
- Trip Builder's basket UI must clearly show the two independent components (flight ref + hotel ref) per PKG-04 — never imply a single combined cancellation policy.
- Search results page uses URL state so customers can share "https://…/flights?from=LHR&to=JFK&dep=2026-08-01..." links with friends. This is a marketing/sharing feature, not just engineering convenience.
- Receipt PDF shown in dashboard must include all-in pricing breakdown with YQ/YR separated from government taxes (matches FLTB-03 and the locked decision in `.planning/research/PITFALLS.md` re EU/UK regulation).
- Email verification gate fires at the checkout step, not at signup or login — customers can window-shop without committing email verification.
- All API calls from the browser go through YARP via `Authorization: Bearer …` injected server-side. The browser never sees the access_token directly.

</specifics>

<deferred>
## Deferred Ideas

- pnpm workspaces / shared packages across portals — defer until B2B Phase 6 makes the duplication painful.
- Multi-language i18n — i18next ships in starterKit but English-only at launch.
- Multi-currency display — single base currency at launch; revisit when destinations/markets expand.
- Native mobile apps — web-responsive only per PROJECT.md out-of-scope.
- Loyalty / points UI — out of scope per PROJECT.md.
- True dynamic packages (PKG2-01) — v2.
- Cancellation/modification self-service UI beyond FLTB-10's basic cancellation request — richer flows can land in a follow-up phase.
- Per-supplier branded voucher templates — defer; reuse single template until brand requirements emerge.
- Dedicated PdfService microservice — premature; reuse Phase 3 setup.
- BFF pattern routing all calls through Next.js API routes — not chosen; server-side fetch with token forwarding is sufficient.

</deferred>

---

*Phase: 04-b2c-portal-customer-facing*
*Context gathered: 2026-04-16*
