---
status: testing
phase: 04-b2c-portal-customer-facing
source:
  - 04-00-SUMMARY.md
  - 04-01-SUMMARY.md
  - 04-02-SUMMARY.md
  - 04-03-SUMMARY.md
  - 04-04-SUMMARY.md
started: 2026-04-17T00:00:00Z
updated: 2026-04-17T00:30:00Z
---

## Current Test

number: 4
name: Register Redirects to Keycloak
expected: |
  Browse to http://localhost:3000/register. Immediately redirects to Keycloak's hosted registration page
  at `{issuer}/protocol/openid-connect/registrations` — the portal does NOT reimplement the registration form.
awaiting: user response

## Tests

### 1. Cold Start Smoke Test
expected: Kill any running b2c-web dev server and backend services. Restart infra (Redis/SQL/RabbitMQ), then each service (BookingService, InventoryService, NotificationService, PaymentService), then `pnpm dev` in src/portals/b2c-web. All services boot without errors. EF migrations (AddReceiptFareBreakdown, AddHotelBookingSagaState, AddBaskets, AddCarBooking) run. InventoryService seeds OpenFlights airports into Redis. GET http://localhost:3000/ returns 200 and renders the TBE hero.
result: pass

### 2. B2C Portal Landing Page Loads
expected: Browse to http://localhost:3000/. Page renders with TBE hero/heading. No console errors. CSP header is set (check DevTools → Network → page request → Response Headers → content-security-policy contains `https://js.stripe.com`).
result: pass

### 3. Login via Keycloak
expected: Browse to http://localhost:3000/login. Click Sign in — browser redirects to Keycloak hosted login (tbe-b2c realm). Enter test user credentials. After submit, browser lands back on the portal (not on Keycloak). Auth.js session cookie exists (DevTools → Application → Cookies → `authjs.session-token` or similar).
result: pass

### 4. Register Redirects to Keycloak
expected: Browse to http://localhost:3000/register. Immediately redirects to Keycloak's hosted registration page at `{issuer}/protocol/openid-connect/registrations` — the portal does NOT reimplement the registration form itself.
result: [pending]

### 5. Password Reset Redirects to Keycloak
expected: Browse to http://localhost:3000/password-reset (or click "Forgot password?" on /login). Redirects to `{issuer}/login-actions/reset-credentials` — the portal does NOT reimplement password reset.
result: [pending]

### 6. Customer Dashboard — Upcoming/Past Tabs + Empty States
expected: Signed in, browse to http://localhost:3000/bookings. See two tabs: Upcoming (default) and Past. With no bookings, Upcoming shows empty state copy "No upcoming trips" and a CTA linking to `/`. Past shows "Your booking history will show here once you have completed a trip". Clicking the Past tab switches view without a page reload.
result: [pending]

### 7. Download Receipt PDF
expected: With at least one confirmed flight booking in the dashboard, click a booking row → booking detail page renders (reference, PNR, ticket, total). Click "Download receipt". Browser downloads a PDF file with `application/pdf` content type; opening the PDF shows PNR, ticket number, and a three-line fare breakdown (Base fare / Surcharges (YQ/YR) / Taxes).
result: [pending]

### 8. IATA Airport Typeahead
expected: On the flight search form (http://localhost:3000/flights or the landing page's search widget), click Origin and type "lon". Within ~200ms, a dropdown shows LHR (Heathrow), LGW (Gatwick), LCY (London City). Typing only "l" (one char) shows NOTHING — min 2 chars enforced. Rapid typing doesn't spam the server (DevTools → Network shows one /api/airports request per pause, old requests aborted).
result: [pending]

### 9. Flight Search Returns Results
expected: Fill flight search form with LHR → JFK, 2026-06-01, 1 adult, Economy. Submit. URL becomes something like `/flights/results?from=LHR&to=JFK&dep=2026-06-01&...` (nuqs-serialized). Results panel shows a list of FlightCard components, each with airline, route, duration, price. Page doesn't spinner-forever.
result: [pending]

### 10. Flight Filters & Sort Work Without Refetch
expected: On the flight results page, open DevTools → Network → filter to XHR. Click a filter chip (e.g. Stops: Direct) — the list reduces but NO new /api/search/flights request fires. Change sort from "Cheapest" to "Fastest" — list reorders but NO new request. Click the page's Reload button — same results come back (TanStack cache, staleTime 90s).
result: [pending]

### 11. Flight Fare Breakdown on Card
expected: Each FlightCard shows price with three explicit lines: Base fare, Surcharges (YQ/YR), Taxes. Total price carries the label "incl. taxes" (verbatim). Numbers sum to the total (base + surcharges + taxes == total).
result: [pending]

### 12. Deep-Linkable Search URL
expected: Run a flight search. Copy the URL from the address bar. Open an incognito/private window. Paste the URL. The same search runs automatically and the same results appear (URL state via nuqs — search is reproducible by link alone).
result: [pending]

### 13. Email-Verify Gate Blocks Payment
expected: Signed in as a user whose Keycloak `email_verified=false`, try to reach http://localhost:3000/checkout/payment directly. Middleware redirects to /checkout/verify-email (or a gate dialog appears on /checkout/payment that is non-dismissable — no X, no Esc, no backdrop click). Clicking Resend verification triggers POST /api/auth/resend-verification (check Network tab → 202 Accepted). Stripe.js is NOT loaded (DevTools → Network → filter `stripe.js` → 0 requests).
result: [pending]

### 14. Flight Checkout End-to-End
expected: From flight results, pick an offer → /checkout/details. Fill passenger form (name/email/dob). Continue → /checkout/payment. Stripe PaymentElement iframe mounts, accepts Stripe test card 4242 4242 4242 4242 (any future exp, any CVC). Submit → /checkout/processing (not /checkout/success yet). Processing page shows status text (e.g. "Authorizing"). Within ~30 seconds, page navigates to /checkout/success?booking=... (URL has `booking=`, NOT `payment_intent=`). Success page shows "Flight booked. Booking reference: {PNR}" and a Download receipt CTA.
result: [pending]

### 15. Hotel Search + Destination Typeahead
expected: Browse to http://localhost:3000/hotels. Type "lon" into Destination. Dropdown shows London + other matching cities (City, Country format). Fill checkin/checkout dates (future), 1 room / 2 adults / 0 children. Submit → /hotels/results with nuqs-encoded URL. Hotel cards appear.
result: [pending]

### 16. Hotel Card Cancellation Badge + /night Suffix
expected: Each hotel card shows: photo, star rating, amenities chips, a cancellation badge matching one of the three verbatim strings ("Free cancellation" / "Non-refundable" / "Flexible"), a per-night price with the literal "/night" suffix, and a total line like "£456.00 total".
result: [pending]

### 17. Hotel Book Room → Checkout
expected: Click a hotel card → detail page. Click "Book room" on any room. Page navigates to /checkout/details?ref=hotel-{id} (NOT `?hotelBookingId=` — unified B5 ref contract). Continues through /checkout/payment → /checkout/processing → /checkout/success exactly like the flight flow. Success page Download receipt → downloads a PDF voucher (application/pdf) with property name, dates, supplier ref prominent.
result: [pending]

### 18. Car Search + Booking
expected: Browse to http://localhost:3000/cars. Fill pickup/dropoff location codes (e.g. LHR → LGW), dates, 2 passengers. Submit → /cars/results shows car offers (vehicle category, supplier, price). Click an offer → detail page. Click Book → /checkout/details?ref=car-{id}. Proceeds through checkout like flight/hotel. Success page shows Download voucher CTA.
result: [pending]

### 19. Trip Builder Two-Panel Layout + Basket Footer
expected: Browse to http://localhost:3000/trips/build. Page shows TWO side-by-side panels: FlightPanel (left) and HotelPanel (right) on desktop. Sticky BasketFooter at the bottom. Add a flight offer + a hotel offer to the basket. BasketFooter shows both items, a combined total, and TWO separate cancellation policy nodes (check DOM → `[data-testid="flight-cancellation"]` and `[data-testid="hotel-cancellation"]` are separate elements, not merged into one string).
result: [pending]

### 20. Combined Basket Checkout — Single PaymentIntent
expected: From Trip Builder with flight+hotel in basket, click Checkout. Land on /checkout/details?ref=basket-{id}. Proceed to /checkout/payment — ONE Stripe PaymentElement iframe appears (not two), copy discloses "ONE charge on your statement". Enter test card 4242..., submit. /checkout/processing polls saga status. Success page lists BOTH booking refs separately (flight ref + hotel ref), with Download receipt / Download voucher CTAs for each.
result: [pending]

## Summary

total: 20
passed: 3
issues: 0
pending: 17
skipped: 0

## Gaps

[none yet]
