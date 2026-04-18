# Requirements: TBE — Travel Booking Engine

**Defined:** 2026-04-12
**Core Value:** A unified booking platform where both direct customers and travel agents can search real inventory, complete bookings end-to-end, and have those bookings managed through a single backoffice — without switching systems.

## v1 Requirements

### Infrastructure & Platform (INFRA)

- [ ] **INFRA-01**: Docker Compose environment runs all services locally with a single command
- [ ] **INFRA-02**: YARP API gateway routes requests to correct microservices with JWT validation
- [ ] **INFRA-03**: Keycloak handles authentication for all three portals (B2C, B2B, backoffice) under one SSO session
- [ ] **INFRA-04**: RabbitMQ with MassTransit wires all inter-service async messaging with outbox pattern
- [ ] **INFRA-05**: Redis provides search result caching, session management, and GDS rate-limit buffering
- [ ] **INFRA-06**: Service health checks and basic structured logging (Serilog → centralized sink) are in place
- [ ] **INFRA-07**: MSSQL schemas initialized with migrations for all services at startup

### Inventory & Search (INV)

- [ ] **INV-01**: Unified `IInventoryConnector` abstraction normalizes results from all GDS and API sources into a canonical search model
- [ ] **INV-02**: Amadeus REST API adapter searches flights with real-time availability and pricing
- [ ] **INV-03**: Sabre or Galileo adapter integrated as a second GDS source for flight search
- [ ] **INV-04**: Hotel aggregator adapter (Hotelbeds or equivalent) searches hotel inventory
- [ ] **INV-05**: Car hire aggregator adapter (Duffel or equivalent) searches ground transport
- [ ] **INV-06**: Parallel fan-out search queries all connected sources simultaneously via `Task.WhenAll`
- [ ] **INV-07**: Search results cached in Redis with tiered TTL: browse (10 min), selection (90 sec), payment (no cache)
- [ ] **INV-08**: Source booking token preserved in Redis through the booking saga — no re-search at booking time
- [ ] **INV-09**: Pricing Service applies markup/commission rules to raw inventory prices before returning to frontend

### Flight Booking (FLTB)

- [ ] **FLTB-01**: One-way, return, and multi-city flight search with passenger type support (adult, child, infant-on-lap, infant-in-seat)
- [ ] **FLTB-02**: Fare rules displayed to customer before booking (cancellation policy, baggage, changes)
- [ ] **FLTB-03**: All-in pricing shown at search (base fare + YQ/YR surcharges + taxes, clearly separated — not merged)
- [ ] **FLTB-04**: MassTransit saga orchestrates the full booking flow: PNR create → Stripe authorize → GDS ticket → Stripe capture → confirm
- [ ] **FLTB-05**: GDS PNR created and held before payment is captured (never capture before confirmed ticket number)
- [ ] **FLTB-06**: Ticketing deadline (TTL) extracted from fare rules and stored; saga hard-timeout fires at TTL - 2 minutes
- [ ] **FLTB-07**: Compensation transactions for every saga step: PNR void on payment failure, refund on ticketing failure
- [ ] **FLTB-08**: Booking confirmation email sent with e-ticket / itinerary within 60 seconds of confirmation
- [ ] **FLTB-09**: Customer can view booking status and itinerary in their account
- [ ] **FLTB-10**: Customer can request cancellation (refund eligibility based on fare rules)

### Hotel Booking (HOTB)

- [ ] **HOTB-01**: Hotel search by destination, dates, and room configuration (adults/children per room)
- [ ] **HOTB-02**: Hotel property details displayed: photos, amenities, cancellation policy, room types
- [ ] **HOTB-03**: Hotel reservation flow: availability check → hold → payment → confirmation
- [ ] **HOTB-04**: Booking confirmation email sent with hotel voucher within 60 seconds
- [ ] **HOTB-05**: Customer can view hotel booking in their account with supplier reference

### Car Hire & Transfers (CARB)

- [ ] **CARB-01**: Car hire search by pickup location, dates, and vehicle category
- [ ] **CARB-02**: Transfer search by route (airport to hotel) with vehicle type options
- [ ] **CARB-03**: Booking confirmation with supplier voucher reference

### Packages (PKG)

- [ ] **PKG-01**: "Trip Builder" presents flight and hotel search results side-by-side for the same destination/dates
- [ ] **PKG-02**: Customer can add both a flight and hotel to a single basket and check out in one payment
- [ ] **PKG-03**: Both booking confirmations (flight + hotel) sent in a single confirmation email
- [ ] **PKG-04**: Each component maintains its own booking reference and cancellation policy (not combined into a true package fare)

### B2C Portal (B2C)

- [x] **B2C-01**: Customer can register with email/password; email verification required before booking
- [x] **B2C-02**: Customer can log in, reset password, and maintain a persistent session
- [x] **B2C-03**: Flight/hotel/car search forms with date pickers, passenger selectors, and destination autocomplete
- [x] **B2C-04**: Search results display sorted by price with filters (stops, airline, departure time, price range)
- [x] **B2C-05**: Booking flow is mobile-responsive and completable in under 5 steps
- [x] **B2C-06**: Credit card payment via Stripe Elements (SAQ-A compliant — card data never touches the server)
- [x] **B2C-07**: Customer dashboard: upcoming trips, past bookings, profile management
- [x] **B2C-08**: Booking receipt downloadable as PDF

### B2B Agent Portal (B2B)

- [x] **B2B-01**: Agency admin can create sub-agent accounts with role-based access (admin, agent, read-only)
- [x] **B2B-02**: Agent can log in with SSO shared with backoffice (same Keycloak realm)
- [x] **B2B-03**: Agent sees dual pricing: net fare + agency markup + gross selling price simultaneously
- [x] **B2B-04**: Agent sees commission amount per booking before confirming
- [x] **B2B-05**: Agent can book on behalf of a customer (passenger details entered by agent, not customer)
- [x] **B2B-06**: Credit wallet balance displayed in agent header; booking is blocked if wallet balance insufficient
- [x] **B2B-07**: Wallet deduction is atomic: funds reserved at booking initiation, committed on confirmation, released on failure (no double-spend)
- [x] **B2B-08**: Agent dashboard: bookings made, wallet balance, ticketing deadline alerts for pending PNRs
- [x] **B2B-09**: Ticketing deadline alerts shown for all PNRs approaching TTL (>24h warning, >2h urgent warning)
- [x] **B2B-10**: Agent can view and download booking documents (e-tickets, hotel vouchers)

### Payments & Wallet (PAY)

- [ ] **PAY-01**: Stripe Payment Element handles B2C credit/debit card payments with 3DS support
- [ ] **PAY-02**: Stripe webhook processes payment events; booking saga progresses only on confirmed webhook — not on client-side success
- [ ] **PAY-03**: B2B credit wallet: agency admin can view current balance and transaction history
- [ ] **PAY-04**: Wallet top-up flow: agency admin pays to recharge wallet via Stripe (also SAQ-A compliant)
- [ ] **PAY-05**: Every wallet movement (reservation, deduction, release, top-up) is an immutable transaction record — no mutable balance field
- [ ] **PAY-06**: Wallet balance read uses `UPDLOCK`/`ROWLOCK` or optimistic concurrency to prevent race-condition double-spend on concurrent bookings
- [ ] **PAY-07**: Refunds processed back to original payment method; wallet credits returned to wallet
- [ ] **PAY-08**: Payment Service is isolated — no other service accesses payment internals directly (PCI scope control)

### Backoffice / Midoffice (BO)

- [ ] **BO-01**: Staff can search and view all bookings (flight, hotel, car) with full details and audit log
- [ ] **BO-02**: Staff can manually create a booking (offline sale entry with supplier reference)
- [ ] **BO-03**: Staff can cancel or modify a booking with reason logging
- [ ] **BO-04**: Booking events log is immutable and append-only (DENY UPDATE/DELETE at DB level)
- [ ] **BO-05**: Each booking event stores a complete pricing snapshot and supplier response at the time of the event
- [ ] **BO-06**: Payment reconciliation view: bookings vs. payments received, flagging discrepancies
- [ ] **BO-07**: Supplier contract management: staff can enter negotiated net rates for hotels/packages
- [ ] **BO-08**: MIS reporting: booking volumes by product, revenue, top agents, top routes
- [ ] **BO-09**: Saga compensation failures appear in a backoffice dead-letter queue with human-actionable status
- [ ] **BO-10**: Staff can requeue or manually resolve failed saga compensations

### CRM (CRM)

- [ ] **CRM-01**: Customer 360 view: profile, all bookings, wallet (if agent), contact history
- [ ] **CRM-02**: Agent/agency management: create agencies, assign agents, set credit limits
- [ ] **CRM-03**: Booking search by PNR, customer name, email, or booking reference
- [ ] **CRM-04**: Communication log: staff can add notes to customer or agency records
- [ ] **CRM-05**: Upcoming trips view: all bookings with future travel dates, filterable by status

### Notifications (NOTF)

- [ ] **NOTF-01**: Booking confirmation email (flight) with e-ticket attachment sent within 60 seconds
- [x] **NOTF-02**: Booking confirmation email (hotel) with voucher PDF sent within 60 seconds
- [ ] **NOTF-03**: Booking cancellation email with refund details sent within 60 seconds
- [ ] **NOTF-04**: B2B agent ticketing deadline alert emails: 24-hour warning and 2-hour warning before TTL
- [ ] **NOTF-05**: Wallet low-balance alert email to agency admin when balance drops below configurable threshold
- [ ] **NOTF-06**: Email templates are professional and branded; not plain-text transactional emails

### Compliance & Security (COMP)

- [ ] **COMP-01**: No card data ever stored or logged server-side; Stripe Elements enforces SAQ-A boundary
- [ ] **COMP-02**: Passport/document data encrypted at rest (AES-256) in the booking database
- [ ] **COMP-03**: GDPR: customer can request data erasure; erasure flow removes PII without destroying booking audit records
- [ ] **COMP-04**: All API endpoints require valid JWT; no anonymous access to booking or account data
- [ ] **COMP-05**: GDS credentials stored in environment secrets/vault — never in source code or config files
- [ ] **COMP-06**: OpenTelemetry span attributes scrubbed of PCI-sensitive fields before export

## v2 Requirements

### Advanced Packaging
- **PKG2-01**: True dynamic package pricing (single fare for flight+hotel bundle with unified cancellation policy)
- **PKG2-02**: Package rate contracts with hotel suppliers at negotiated package-only rates

### Distribution
- **DIST-01**: White-label B2C portal (custom domain, branding) for partner agencies
- **DIST-02**: XML/JSON API for third-party channel connectivity

### Mobile
- **MOB-01**: Native iOS app (Swift/React Native) for B2C customers
- **MOB-02**: Native Android app for B2C customers

### Advanced B2B
- **B2B2-01**: Sub-agency hierarchy (agency > sub-agency > agent) with separate wallets
- **B2B2-02**: Agent performance analytics and commission reporting

### Loyalty
- **LOYALT-01**: Customer points/rewards programme
- **LOYALT-02**: Points redemption at checkout

### Advanced Ancillaries
- **ANC-01**: Seat selection with seat map visualization per airline
- **ANC-02**: Baggage add-on per segment per passenger
- **ANC-03**: Travel insurance (requires regulatory licensing)

### BSP Filing
- **BSP-01**: Automated IATA BSP sales report generation and submission

## Out of Scope

| Feature | Reason |
|---------|--------|
| Multi-tenant SaaS | System is for one travel business; multi-tenancy multiplies complexity without current business need |
| True dynamic package fares | 2-3 months additional complexity; trip builder covers 80% of user value at 20% the effort |
| Group bookings (10+ pax) | Separate product with different GDS workflows, pricing, and operations |
| Travel insurance sales | Requires financial regulatory licensing beyond technology scope |
| NDC per-airline ancillaries | Per-airline certification, highly variable — a separate integration programme |
| Loyalty / points programme | Requires validated customer base first; v2 workstream |
| Mobile native apps | Web-responsive first; native apps are v2 |
| Automated BSP filing | High-stakes regulatory submission — manual process until booking volume justifies automation |
| GDS queue management UI | Advanced GDS terminal feature; agents can use GDS directly for queue management in v1 |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| INFRA-01 to INFRA-07 | Phase 1 | Pending |
| INV-01 to INV-09 | Phase 2 | Pending |
| FLTB-01 to FLTB-10 | Phase 3 | Pending |
| PAY-01 to PAY-08 | Phase 3 | Pending |
| NOTF-01 to NOTF-06 | Phase 3 | Pending |
| COMP-01 to COMP-06 | Phase 3 | Pending |
| B2C-01 to B2C-08 | Phase 4 | Pending |
| HOTB-01 to HOTB-05 | Phase 5 | Pending |
| CARB-01 to CARB-03 | Phase 5 | Pending |
| PKG-01 to PKG-04 | Phase 5 | Pending |
| B2B-01, B2B-02 | Phase 5 | Complete (Plan 05-01) |
| B2B-03 to B2B-06 | Phase 5 | Complete (Plan 05-02) |
| B2B-07 | Phase 5 | Complete (Plans 03-01 + 05-02 + 05-03) |
| B2B-08 to B2B-10 | Phase 5 | Pending |
| BO-01 to BO-10 | Phase 7 | Pending |
| CRM-01 to CRM-05 | Phase 7 | Pending |

**Coverage:**
- v1 requirements: 76 total
- Mapped to phases: 76
- Unmapped: 0 ✓

---
*Requirements defined: 2026-04-12*
*Last updated: 2026-04-12 after initial definition*
