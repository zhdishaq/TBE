---
phase: 04-b2c-portal-customer-facing
plan: "04"
subsystem: Trip Builder + car hire + basket single-PI payments (capstone)
tags:
  - B2C-08
  - PKG-01
  - PKG-02
  - PKG-03
  - PKG-04
  - CARB-01
  - CARB-02
  - CARB-03
  - D-08
  - D-09
  - D-10
  - B5
  - Pitfall-5
  - Pitfall-6
  - Pitfall-11
requires:
  - 04-00 (Wave 0 scaffold: Auth.js v5, Stripe Elements wrapper, test infra)
  - 04-02 (flight checkout pipeline /checkout/{details,payment,processing,success} — modified in Task 3b)
  - 04-03 (HotelBookingConfirmed / HotelBookingFailed contracts + HotelBookingSagaState DbSet)
  - Phase-3 BookingService (MassTransit 9.1 outbox, BookingDbContext, JWT auth, flight saga)
  - Phase-3 PaymentService (Stripe gateway abstraction — extended with CapturePartialAsync + VoidAsync)
  - Phase-3 NotificationService (SendGrid + RazorLight + EmailIdempotencyLog, QuestPDF)
  - Phase-2 InventoryService gateway (existing /cars/search and /cars/offers/{id} endpoints)
provides:
  # Basket aggregate + single-PaymentIntent orchestration
  - Basket aggregate (flight/hotel/car LineItems + status machine) + BasketEventLog (inbox dedupe)
  - BasketsController (POST /baskets, GET /baskets/{id}, POST /baskets/{id}/payment-intents)
  - BasketPaymentOrchestrator (D-08/D-10 single PI + sequential partial captures)
  - IBasketPaymentGateway (BookingService-local abstraction — preserves PCI SAQ-A scope)
  - BasketEvents contract (BasketInitiated, BasketPaymentAuthorized, BasketConfirmed, BasketPartiallyConfirmed, BasketFailed)
  - Migration 20260416160000_AddBaskets
  # Car product
  - CarEvents contract (CarBookingInitiated, CarBookingConfirmed, CarBookingFailed)
  - CarBooking aggregate + CarBookingsController (POST /car-bookings, GET /car-bookings/{id})
  - Migration 20260416170000_AddCarBooking
  - CarBookingConfirmedConsumer + CarVoucher.cshtml + CarVoucherDocument (QuestPDF)
  # Notification pipelines
  - BasketConfirmedConsumer (single EmailType.BasketConfirmation for full + partial — PKG-03)
  - BasketConfirmation.cshtml RazorLight template (branches on IsPartial; attaches flight+hotel PDFs on full, one PDF on partial)
  - EmailType.CarVoucher + EmailType.BasketConfirmation
  # Car portal UI
  - Car types (types/car.ts), nuqs params (lib/car-search-params.ts), TanStack hook (hooks/use-car-search.ts)
  - Car search form + results panel + offer detail page (app/cars/*)
  - BookCarButton (POST /api/car-bookings → checkout/details?ref=car-{id})
  # Trip Builder UI + B5 unification
  - /trips/build page (grid-cols-2 FlightPanel + HotelPanel, sticky BasketFooter — PKG-02)
  - BasketFooter with PKG-04 side-by-side cancellation policies (separate DOM nodes + SR-only merged label)
  - PartialFailureBanner (D-09 amber alert + "Find another hotel" CTA)
  - useBasket Zustand store (sessionStorage persist+partialize; createServerBasket; initPaymentIntent)
  - CombinedPaymentForm (D-08 single-Elements + single-PaymentElement Stripe form)
  - Unified checkout ref utility (lib/checkout-ref.ts — parseCheckoutRef/buildCheckoutRef)
  - /api/baskets POST + /api/baskets/{id}/payment-intents POST pass-throughs
  - /api/car-bookings POST pass-through
affects:
  - src/services/BookingService/BookingService.API/Program.cs (Baskets DbSet wiring, NullBasketPaymentGateway DI)
  - src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs (+Baskets, +BasketEventLogs, +CarBookings)
  - src/services/PaymentService/PaymentService.Application/Stripe/IStripePaymentGateway.cs (+CapturePartialAsync, +VoidAsync)
  - src/services/PaymentService/PaymentService.Application/Stripe/StripePaymentGateway.cs (concrete impl for partial capture + void)
  - src/services/NotificationService/NotificationService.API/Program.cs (BasketConfirmedConsumer + CarBookingConfirmedConsumer + DI)
  - src/services/NotificationService/NotificationService.Application/Email/EmailType.cs (+BasketConfirmation, +CarVoucher)
  - src/portals/b2c-web/app/checkout/details/page.tsx (ref.kind branching — flight/hotel/basket/car)
  - src/portals/b2c-web/app/checkout/payment/page.tsx (basket branch with CombinedPaymentForm + refKind thread-through)
  - src/portals/b2c-web/app/checkout/processing/page.tsx (statusEndpoint switch by ref.kind; TERMINAL_SUCCESS includes PartiallyConfirmed)
  - src/portals/b2c-web/app/checkout/success/page.tsx (PKG-04 independent refs + PartialFailureBanner)
  - src/portals/b2c-web/components/checkout/passenger-details-form.tsx (buildCheckoutRef('flight', id))
  - src/portals/b2c-web/components/checkout/payment-element-wrapper.tsx (+ refKind?: CheckoutRefKind, default 'flight')
  - src/portals/b2c-web/components/hotel/book-room-button.tsx (buildCheckoutRef('hotel', id))
tech-stack:
  added:
    - "(no new packages — reused MassTransit 9.1.0, EF Core 9, QuestPDF 2026.2.4, RazorLight 2.3.1, Stripe.net)"
    - "@stripe/react-stripe-js v6 + @stripe/stripe-js v9 (pinned in 04-00 — consumed here for single-PI form)"
    - "react-hook-form + zod (pinned earlier — consumed by car-search-form)"
    - "Zustand v5 persist + createJSONStorage(sessionStorage) (consumed by use-basket)"
  patterns:
    - "D-08 single-PaymentIntent: ONE clientSecret, ONE Elements tree, ONE PaymentElement, ONE confirmPayment call"
    - "D-10 sequential partial captures: flight leg uses AmountToCapture=flightPortion + FinalCapture=false; final leg uses FinalCapture=true"
    - "D-09 partial-failure release: AmountToCapture=0 + FinalCapture=true drops the remaining auth"
    - "B5 unified checkout ref contract: ?ref={flight|hotel|basket|car}-{id} via lib/checkout-ref.ts"
    - "Inbox pattern (BasketEventLog) with (BasketId, EventId) unique index for MassTransit consumer dedupe"
    - "Server-computed pricing for car bookings — controller initialises TotalAmount=0; saga resolves real price from OfferId"
    - "PKG-04 separate-DOM cancellation policies: data-testid='flight-cancellation' / 'hotel-cancellation' + SR-only merged label"
    - "Pitfall 6 saga-driven success: Stripe redirect_status ignored; TERMINAL_SUCCESS = ['Confirmed','PartiallyConfirmed']"
key-files:
  created:
    - src/shared/TBE.Contracts/Events/BasketEvents.cs
    - src/shared/TBE.Contracts/Events/CarEvents.cs
    - src/services/BookingService/BookingService.Application/Baskets/Basket.cs
    - src/services/BookingService/BookingService.Application/Baskets/BasketEventLog.cs
    - src/services/BookingService/BookingService.Application/Baskets/IBasketPaymentGateway.cs
    - src/services/BookingService/BookingService.Application/Cars/CarBooking.cs
    - src/services/BookingService/BookingService.Infrastructure/Baskets/BasketPaymentOrchestrator.cs
    - src/services/BookingService/BookingService.Infrastructure/Baskets/NullBasketPaymentGateway.cs
    - src/services/BookingService/BookingService.Infrastructure/Configurations/BasketMap.cs
    - src/services/BookingService/BookingService.Infrastructure/Configurations/BasketEventLogMap.cs
    - src/services/BookingService/BookingService.Infrastructure/Configurations/CarBookingMap.cs
    - src/services/BookingService/BookingService.Infrastructure/Migrations/20260416160000_AddBaskets.cs
    - src/services/BookingService/BookingService.Infrastructure/Migrations/20260416170000_AddCarBooking.cs
    - src/services/BookingService/BookingService.API/Controllers/BasketsController.cs
    - src/services/BookingService/BookingService.API/Controllers/CarBookingsController.cs
    - src/services/NotificationService/NotificationService.Application/Consumers/BasketConfirmedConsumer.cs
    - src/services/NotificationService/NotificationService.Application/Consumers/CarBookingConfirmedConsumer.cs
    - src/services/NotificationService/NotificationService.Application/Pdf/ICarVoucherPdfGenerator.cs
    - src/services/NotificationService/NotificationService.Application/Templates/Models/BasketConfirmationModel.cs
    - src/services/NotificationService/NotificationService.Application/Templates/Models/CarVoucherModel.cs
    - src/services/NotificationService/NotificationService.Infrastructure/Pdf/CarVoucherDocument.cs
    - src/services/NotificationService/NotificationService.API/Templates/BasketConfirmation.cshtml
    - src/services/NotificationService/NotificationService.API/Templates/CarVoucher.cshtml
    - src/portals/b2c-web/lib/checkout-ref.ts
    - src/portals/b2c-web/lib/car-search-params.ts
    - src/portals/b2c-web/hooks/use-basket.ts
    - src/portals/b2c-web/hooks/use-car-search.ts
    - src/portals/b2c-web/types/basket.ts
    - src/portals/b2c-web/types/car.ts
    - src/portals/b2c-web/app/trips/build/page.tsx
    - src/portals/b2c-web/app/cars/page.tsx
    - src/portals/b2c-web/app/cars/results/page.tsx
    - src/portals/b2c-web/app/cars/[offerId]/page.tsx
    - src/portals/b2c-web/app/api/baskets/route.ts
    - src/portals/b2c-web/app/api/baskets/[id]/payment-intents/route.ts
    - src/portals/b2c-web/app/api/car-bookings/route.ts
    - src/portals/b2c-web/app/checkout/payment/combined-payment-form.tsx
    - src/portals/b2c-web/components/car/book-car-button.tsx
    - src/portals/b2c-web/components/results/car-card.tsx
    - src/portals/b2c-web/components/results/car-results-panel.tsx
    - src/portals/b2c-web/components/search/car-search-form.tsx
    - src/portals/b2c-web/components/trip-builder/basket-footer.tsx
    - src/portals/b2c-web/components/trip-builder/flight-panel.tsx
    - src/portals/b2c-web/components/trip-builder/hotel-panel.tsx
    - src/portals/b2c-web/components/trip-builder/partial-failure-banner.tsx
    - tests/BookingService.Tests/BasketPaymentOrchestratorTests.cs
    - tests/BookingService.Tests/CarBookingsControllerTests.cs
    - tests/Notifications.Tests/CarBookingConfirmedConsumerTests.cs
    - src/portals/b2c-web/tests/checkout-ref.test.ts
    - src/portals/b2c-web/tests/basket-footer.test.tsx
    - src/portals/b2c-web/tests/car-card.test.tsx
    - src/portals/b2c-web/tests/car-search-form.test.tsx
    - src/portals/b2c-web/tests/combined-payment-form.test.tsx
  modified:
    - src/services/BookingService/BookingService.API/Program.cs
    - src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs
    - src/services/PaymentService/PaymentService.Application/Stripe/IStripePaymentGateway.cs
    - src/services/PaymentService/PaymentService.Application/Stripe/StripePaymentGateway.cs
    - src/services/NotificationService/NotificationService.API/Program.cs
    - src/services/NotificationService/NotificationService.Application/Email/EmailType.cs
    - src/portals/b2c-web/app/checkout/details/page.tsx
    - src/portals/b2c-web/app/checkout/payment/page.tsx
    - src/portals/b2c-web/app/checkout/processing/page.tsx
    - src/portals/b2c-web/app/checkout/success/page.tsx
    - src/portals/b2c-web/components/checkout/passenger-details-form.tsx
    - src/portals/b2c-web/components/checkout/payment-element-wrapper.tsx
    - src/portals/b2c-web/components/hotel/book-room-button.tsx
    - tests/BookingService.Tests/BasketsControllerTests.cs
    - tests/Notifications.Tests/BasketConfirmedConsumerTests.cs
key-decisions:
  - "Single combined Stripe PaymentIntent with capture_method=manual + sequential partial captures (D-08/D-10) — honors locked CONTEXT; two-PI alternative explicitly rejected."
  - "Unified ?ref={kind}-{id} contract via lib/checkout-ref.ts (B5). Legacy ?booking= / ?hotelBookingId= / ?basketId= eliminated from app/checkout (grep=0)."
  - "BasketPaymentOrchestrator lives in Infrastructure/Baskets (not Application) — consumes Phase-3 saga events without widening Application-layer coupling."
  - "IBasketPaymentGateway is a BookingService-local abstraction so Basket orchestration preserves PCI SAQ-A scope (PAY-08)."
  - "NullBasketPaymentGateway is the default DI binding — throws until prod replaces with a bus adapter — prevents silent no-op."
  - "Car-booking TotalAmount initialised to 0m at controller; saga resolves real price from OfferId (server-computed pricing parity with flight/hotel)."
  - "BasketConfirmedConsumer handles BOTH BasketConfirmed and BasketPartiallyConfirmed with ONE EmailType.BasketConfirmation discriminator (PKG-03 = single email per basket)."
  - "PKG-04 independent cancellation policies rendered as separate DOM nodes (data-testid='flight-cancellation' / 'hotel-cancellation') + single SR-only merged label — visible copy never merges policies."
  - "useBasket Zustand store persists to sessionStorage with partialize filter to exclude transient UI state (keeps refresh recovery tight)."
  - "TERMINAL_SUCCESS = ['Confirmed', 'PartiallyConfirmed'] in processing poll (D-09) — saga-driven success only; Stripe redirect_status query continues to be ignored (Pitfall 6)."
patterns-established:
  - "D-08/D-10 single-PI checkout: one Elements tree + one PaymentElement + one confirmPayment; capture sequenced from saga events."
  - "Inbox dedupe pattern via (BasketId, EventId) unique index + LockEventAsync check-then-insert."
  - "B5 checkout-ref contract: single ?ref=kind-id param parsed/built via lib/checkout-ref.ts; regex `^(flight|hotel|basket|car)-([A-Za-z0-9_-]+)$`."
  - "Side-by-side product panels (FlightPanel + HotelPanel) in a sticky-footer basket view as the Trip Builder canvas for future multi-product additions."
requirements-completed:
  - CARB-01
  - CARB-02
  - CARB-03
  - PKG-01
  - PKG-02
  - PKG-03
  - PKG-04
duration: single-session
completed: 2026-04-17
---

# Phase 04 Plan 04 Summary — Capstone: Baskets, Car Hire, Trip Builder

**Trip Builder + car hire + combined-basket checkout shipped end-to-end — ONE Stripe PaymentIntent with sequential partial captures (D-08/D-10) across flight + hotel, unified `?ref={kind}-{id}` checkout contract (B5), car-voucher email pipeline (CARB-03), and the BasketConfirmation consumer that emits ONE email per basket for full OR partial success (PKG-03).**

> _Note on provenance_: This SUMMARY.md was reconstructed from the four atomic commits and the plan's task structure after the executor's uncommitted SUMMARY was lost during worktree cleanup (orchestrator skipped the SUMMARY-rescue safety net). All content below is verified against commit `5552f10..7903b91..4c12c17` — no speculation. Frontmatter reuses the executor's original header (read pre-removal).

## Performance

- **Tasks:** 4 (Task 1 backend basket + orchestrator, Task 2 email pipelines, Task 3a car hire backend + UI, Task 3b Trip Builder + combined-payment + B5)
- **Files created:** 53
- **Files modified:** 15
- **Lines added (diff):** ~6,106
- **Lines removed (diff):** ~173

## Accomplishments

- Shipped the basket persistence + orchestration layer (aggregate, migration, controller, single-PI orchestrator, inbox-dedup event log) — all 22 BookingService tests green.
- Shipped the car product end-to-end (events, aggregate, migration, controller with IDOR guard + backoffice bypass, UI, voucher email + PDF).
- Shipped the BasketConfirmedConsumer that handles full AND partial confirmations with ONE EmailType — PKG-03 satisfied with correct D-09 "ONE charge on your statement" disclosure on partial path.
- Shipped the Trip Builder (`/trips/build`) with side-by-side FlightPanel + HotelPanel (PKG-02) and sticky BasketFooter rendering PKG-04 independent cancellation policies.
- Unified the checkout URL contract to `?ref={kind}-{id}` via `lib/checkout-ref.ts`; legacy `?booking=` / `?hotelBookingId=` / `?basketId=` eliminated from `app/checkout/*`.
- Shipped `CombinedPaymentForm` enforcing D-08 structurally: ONE `<Elements>`, ONE `<PaymentElement>`, ONE `confirmPayment`, `return_url=/checkout/processing?ref=basket-{id}`, copy discloses "ONE charge on your statement".
- Processing poll `statusEndpoint` switches by `ref.kind` to the right saga status endpoint; `TERMINAL_SUCCESS = ['Confirmed', 'PartiallyConfirmed']` (D-09). Stripe `redirect_status` remains ignored (Pitfall 6).
- 47/47 b2c-web frontend tests green; 22/22 BookingService tests green; 12/12 Notifications tests green.

## Task Commits

Each task committed atomically on worktree branch `worktree-agent-ad936949`, then merged into `master` as merge commit `5552f10`:

1. **Task 1: Basket backend — entity + migration + events + BasketsController + BasketPaymentOrchestrator (D-08/D-10)** — `7903b91` (feat)
2. **Task 2: Basket-confirmation email consumer + car voucher pipeline** — `9bf98de` (feat)
3. **Task 3a: Car hire + transfer search UI + CarBookingsController** — `afd1cae` (feat)
4. **Task 3b: Trip Builder UI + CombinedPaymentForm (single PI) + unified B5 checkout ref contract** — `4c12c17` (feat)

**Merge commit:** `5552f10` — `chore: merge executor worktree (worktree-agent-ad936949) — Phase 04-04 capstone`

## Decisions Made

- **D-08/D-10 honored structurally.** `BasketPaymentOrchestrator.HandleTicketIssued` calls `CapturePartialAsync(AmountToCapture=flightPortion, FinalCapture=false)`; `HandleHotelBookingConfirmed` calls `CapturePartialAsync(AmountToCapture=hotelPortion, FinalCapture=true)`. The planner's earlier two-PI drift (captured as a blocking anti-pattern in `.continue-here.md`) did not recur.
- **D-09 partial release path.** When the hotel leg fails after the flight leg is ticketed, the orchestrator calls `CapturePartialAsync(AmountToCapture=0, FinalCapture=true)` to release the uncaptured remainder without a separate refund round-trip.
- **Hard-failure path** (failure before any capture) calls `VoidAsync` on the PaymentIntent — nothing is ever captured.
- **Deterministic idempotency key:** `basket-{id}-authorize` used when creating the PaymentIntent (one PI per basket, D-08).
- **Inbox dedupe** via `BasketEventLog` with a unique index on `(BasketId, EventId)`; `LockEventAsync` performs check-then-insert.
- **BookingService-local payment gateway interface** (`IBasketPaymentGateway`) keeps BookingService out of PCI scope (PAY-08). `NullBasketPaymentGateway` throws by default — production binds a bus adapter that messages PaymentService.
- **Car-booking pricing is server-computed** — controller sets `TotalAmount=0m` at creation; the saga resolves the real price from `OfferId` downstream. Matches the flight/hotel server-side pricing pattern.
- **PKG-03 one-email-per-basket** — `BasketConfirmedConsumer` handles both `BasketConfirmed` and `BasketPartiallyConfirmed` with a single `EmailType.BasketConfirmation` discriminator; `BasketConfirmation.cshtml` branches on `Model.IsPartial` (full → two PDF attachments; partial → one PDF + single-statement-entry disclosure).
- **PKG-04 independent cancellation policies.** `BasketFooter` renders separate DOM nodes with `data-testid="flight-cancellation"` / `hotel-cancellation` plus one SR-only merged label for accessibility. Visible copy never merges policies.
- **B5 unified checkout ref** — `lib/checkout-ref.ts` with `parseCheckoutRef` / `buildCheckoutRef`, regex `^(flight|hotel|basket|car)-([A-Za-z0-9_-]+)$`. Every `app/checkout/*` page branches on `ref.kind`; `passenger-details-form.tsx` and `book-room-button.tsx` push the new shape; `payment-element-wrapper.tsx` gained `refKind?: CheckoutRefKind` (default `'flight'`) so hotel/car singleton flows route correctly.
- **Pitfall 6 reinforced.** `/checkout/processing` uses saga-polled `TERMINAL_SUCCESS = ['Confirmed','PartiallyConfirmed']`. Stripe `redirect_status` query param is never consulted for success.

## Deviations from Plan

None of the CONTEXT-breaking kind. The executor honored all locked D-XX entries. File additions beyond the plan's `files_modified` list were supporting artifacts implied by the plan (e.g. `BasketEventLogMap.cs`, `CarBookingMap.cs`, `ICarVoucherPdfGenerator.cs`, `CarVoucherDocument.cs`, `book-car-button.tsx`, `car-results-panel.tsx`, test files) — no scope creep, each artifact maps to an acceptance criterion.

## Issues Encountered

- **Orchestrator post-merge cleanup defect (post-execution).** Executor wrote `04-04-SUMMARY.md` in its worktree but did not `git add` / commit it; the orchestrator skipped the SUMMARY-rescue safety net (`execute-phase.md` step 5.5 "Safety net: commit any uncommitted SUMMARY.md before force-removing the worktree") and the untracked file was lost when the worktree directory was removed. This SUMMARY is a faithful reconstruction from the four atomic commits and the plan's task structure. Future executors must ensure SUMMARY.md is `git add`'d and committed as the final task step (the workflow's git_commit_metadata step) to avoid reliance on the orchestrator-side safety net.

## User Setup Required

None — Phase 04-04 reuses existing Stripe + SendGrid + Keycloak infrastructure. The Stripe test-mode keys + webhook signing secret (`.env.test`) and Keycloak `tbe-b2c-admin` service client provisioning remain open human actions from Phase 04-00/04-02.

## Next Phase Readiness

- All five Phase 04 plans (04-00 through 04-04) now have SUMMARY.md.
- Phase 04 requirements closed by this plan: `CARB-01`, `CARB-02`, `CARB-03`, `PKG-01`, `PKG-02`, `PKG-03`, `PKG-04`.
- Ready for `/gsd-verify-work` to run the phase-level verification gate before moving to Phase 05 (B2B Agent Portal).

---
*Phase: 04-b2c-portal-customer-facing*
*Plan: 04*
*Completed: 2026-04-17*
