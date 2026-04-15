# Roadmap: TBE — Travel Booking Engine

**Milestone:** v1.0 — Full Platform
**Core Value:** A unified booking platform where both direct customers and travel agents can search real inventory, complete bookings end-to-end, and have those bookings managed through a single backoffice — without switching systems.

---

## Phase 1: Infrastructure Foundation

**Goal:** Every infrastructure service runs locally with a single command, JWT-authenticated requests reach the correct microservice, and all database schemas exist — so development on every subsequent phase can start without environmental blockers.

**Requirements:** INFRA-01, INFRA-02, INFRA-03, INFRA-04, INFRA-05, INFRA-06, INFRA-07

**Depends on:** —

### Plans

1. **Docker Compose stack** — define all service containers (API Gateway, FlightService, HotelService, BookingService, PaymentService, PricingService, NotificationService, CrmService, BackofficeService), with MSSQL, RabbitMQ, Redis, and Keycloak; single `docker-compose up` brings everything up with correct dependency ordering and persisted volumes
2. **YARP gateway + Keycloak auth** — configure YARP routes for B2C, B2B, and backoffice traffic; provision three Keycloak realms (`tbe-b2c`, `tbe-b2b`, `tbe-backoffice`) with correct client configs and JWKS-backed JWT validation at the gateway
3. **RabbitMQ / MassTransit wiring** — configure MassTransit 8.x over RabbitMQ with outbox pattern; define core exchange topology (`booking.commands`, `booking.events`, `inventory.search.requests`); verify message delivery end-to-end with a test consumer
4. **MSSQL migrations + shared services** — run EF Core migrations for all service schemas at startup; wire Redis distributed cache and session; configure Serilog structured logging to a centralized sink; implement health-check endpoints on every service

### UAT Criteria

- [ ] Running `docker-compose up` from a clean checkout starts all services with no manual steps; all containers reach healthy state within 3 minutes
- [ ] A request to the API gateway with a valid Keycloak JWT for the `tbe-b2c` realm is routed to the correct downstream service and returns 200; a request without a token returns 401
- [ ] Keycloak admin console shows three realms (`tbe-b2c`, `tbe-b2b`, `tbe-backoffice`) each with a configured client; a user can obtain a token from each realm
- [ ] A test message published to RabbitMQ is consumed and acknowledged by the target service; the outbox table shows the message was recorded before dispatch
- [ ] MSSQL contains all expected schemas and tables after startup; `SELECT` queries against booking, payment, and CRM schemas return empty tables with correct column definitions
- [ ] The `/health` endpoint on each service returns `{"status":"Healthy"}` and Serilog emits a structured JSON log entry per request

---

## Phase 2: Inventory Layer & GDS Integration

**Goal:** A real Amadeus flight search returns normalized, priced results through the unified inventory abstraction — with search results cached in Redis — so the booking saga in Phase 3 has a proven inventory pipeline to build on.

**Requirements:** INV-01, INV-02, INV-03, INV-04, INV-05, INV-06, INV-07, INV-08, INV-09

**Depends on:** Phase 1

### Plans

1. **IInventoryConnector abstraction + Amadeus adapter** — define `IFlightAvailabilityProvider`, `IHotelAvailabilityProvider`, and `ICarAvailabilityProvider` interfaces in `TBE.Domain.Contracts`; implement Amadeus REST adapter using Refit with OAuth2 client-credentials flow; fan-out search via `Task.WhenAll`; map raw GDS response to the canonical `UnifiedFlightOffer` model
2. **Sabre/Galileo second GDS adapter** — implement a Sabre REST adapter (or Galileo via Travelport uAPI SOAP) behind the same `IFlightAvailabilityProvider` interface; register both adapters as keyed DI services; verify both return results in the same canonical model
3. **Hotel (Hotelbeds) + car hire adapters** — implement Hotelbeds REST adapter with HMAC-SHA256 `DelegatingHandler`; implement car hire adapter (Duffel or equivalent); wire both into the fan-out search alongside flight connectors
4. **Pricing Service + Redis search caching** — implement markup/commission rules engine in the Pricing Service; apply rules to raw inventory prices before returning to the Search Service; cache search results in Redis with tiered TTL (browse 10 min, selection 90 sec); store booking tokens in Redis through to booking saga; implement Redis sliding-window GDS rate-limit guard

### UAT Criteria

- [ ] A flight search request to the Search Service returns results from Amadeus in the canonical format with all-in pricing (base + surcharges + taxes separated) within 5 seconds on a warm cache
- [ ] The same search request returns results from the second GDS (Sabre or Galileo) in the same canonical format; results from both sources appear in a single deduplicated response
- [ ] A hotel search by destination and dates returns Hotelbeds results with property name, room type, cancellation policy, and priced rate in the canonical model
- [ ] A repeated search within the TTL window is served from Redis (no GDS call made); a search after TTL expiry triggers a live GDS call
- [ ] A booking token (fare snapshot) stored in Redis at selection time is retrievable by booking session ID at the point the saga starts — without a re-search
- [ ] The Pricing Service applies a configured markup rule and returns a gross selling price that differs correctly from the raw net fare

---

## Phase 3: Core Flight Booking Saga (B2C)

**Goal:** A real end-to-end B2C flight booking completes in production — PNR created, payment authorized via Stripe, ticket issued by the GDS, payment captured, and a confirmation email with e-ticket delivered within 60 seconds — with full compensation logic if any step fails.

**Requirements:** FLTB-01, FLTB-02, FLTB-03, FLTB-04, FLTB-05, FLTB-06, FLTB-07, FLTB-08, FLTB-09, FLTB-10, PAY-01, PAY-02, PAY-03, PAY-04, PAY-05, PAY-06, PAY-07, PAY-08, NOTF-01, NOTF-03, NOTF-04, NOTF-05, NOTF-06, COMP-01, COMP-02, COMP-04, COMP-05, COMP-06

**Depends on:** Phase 2

> NOTF-02 (hotel voucher email) relocated to Phase 4 — Phase 3 is flight-only per CONTEXT.
> COMP-03 (GDPR erasure + PII tombstone) relocated to Phase 6 — erasure belongs with the backoffice/CRM surfaces that own customer records.

### Plans

**Plans:** 4 plans

- [ ] 03-01-PLAN.md - MassTransit booking saga state machine: BookingInitiated -> PriceReconfirmed -> PNRCreated -> PaymentAuthorized -> TicketIssued -> PaymentCaptured -> BookingConfirmed; persist saga state via MassTransit.EntityFrameworkCore (Optimistic concurrency per D-01); compensation chain for every step (PNR void, Stripe refund, payment release); SagaDeadLetter table for compensation failures
- [ ] 03-02-PLAN.md - Stripe Payment Service + B2B credit wallet: Payment Intents authorize-before-capture, deterministic idempotency keys, webhook signature verification (tolerance 300s), append-only WalletTransactions ledger with Dapper UPDLOCK/ROWLOCK/HOLDLOCK reads (no mutable balance), wallet top-up via Stripe
- [ ] 03-03-PLAN.md - TTL monitor + compliance hardening: AES-256-GCM field encryption primitive in TBE.Common, OpenTelemetry SensitiveAttributeProcessor scrubbing PCI/PII, secrets migration from appsettings to .env, fare-rule parser (Amadeus/Sabre/Galileo) with 2h fallback per D-07, BackgroundService TTL monitor (5-min poll) emitting 24h/2h advisory events
- [ ] 03-04-PLAN.md - Notification Service + email templates: SendGrid 9.29.3 + RazorLight 2.3.1 + QuestPDF 2026.2.4; consumers for BookingConfirmed/Cancelled/TicketIssued/Expired/TicketingDeadlineApproaching/WalletLowBalance; EmailIdempotencyLog with unique (EventId, EmailType) index for NOTF-05; remove Worker.cs placeholder

### UAT Criteria

- [ ] A B2C user completes a flight booking end-to-end: selects a flight, enters passenger details and card via Stripe Elements, submits payment — within 60 seconds they receive a confirmation email containing the airline e-ticket number and itinerary
- [ ] If the GDS ticketing call fails after payment is authorized, the saga automatically voids the PNR and refunds the Stripe Payment Intent; the customer receives a cancellation email, not a charge with no ticket
- [ ] A booking with a 2-hour TTL triggers the TTL monitor alert within 5 minutes of the deadline; the system voids the PNR and initiates refund before the deadline expires if the booking is unpaid
- [ ] Card data never appears in any server-side log, database column, or OpenTelemetry span; Stripe Elements is the only form that handles card input (SAQ-A boundary confirmed)
- [ ] A customer can view their booking status and itinerary in their account; a cancellation request generates the correct refund based on fare rules
- [ ] The B2B credit wallet deduction is atomic: concurrent booking attempts by the same agency cannot both succeed if combined they exceed the available balance; the transaction log shows a reservation entry followed by a commit or release entry — never a mutable balance update

---

## Phase 4: B2C Portal (Customer-Facing)

**Goal:** The B2C portal is publicly launchable — customers can search flights, hotels, and car hire; build a trip with both flight and hotel; complete bookings end-to-end on mobile or desktop; and download their booking receipt as a PDF.

**Requirements:** B2C-01, B2C-02, B2C-03, B2C-04, B2C-05, B2C-06, B2C-07, B2C-08, HOTB-01, HOTB-02, HOTB-03, HOTB-04, HOTB-05, CARB-01, CARB-02, CARB-03, PKG-01, PKG-02, PKG-03, PKG-04, NOTF-02

**Depends on:** Phase 3

**UI hint**: yes

**Plans:** 5 plans

- [ ] 04-00-PLAN.md — Wave 0 scaffold: fork `ui/starterKit` → `src/portals/b2c-web/`, wire Auth.js v5 edge-split + CSP, configure Vitest + Playwright + live Keycloak auth smoke, red-placeholder .NET test scaffolds, Keycloak `tbe-b2c` audience mapper + `tbe-b2c-admin` service client (Pitfalls 1/3/4/8/16/17)
- [ ] 04-01-PLAN.md — Auth + account portal: Keycloak-backed login/register/verify, resend-verification via admin client, RSC dashboard from `GET /customers/me/bookings`, receipt PDF via QuestPdfBookingReceiptGenerator (B2C-01, B2C-02, B2C-07, B2C-08, NOTF-02)
- [ ] 04-02-PLAN.md — Flight search + booking UI: IATA typeahead (OpenFlights + Redis), search form/results/detail with nuqs URL state + TanStack Query (D-11/12), checkout details → Stripe PaymentElement → /checkout/processing polling → success, email-verify gate (B2C-03, B2C-04, B2C-05, B2C-06, NOTF-02)
- [ ] 04-03-PLAN.md — Hotel search + booking UI: hotel search/results/detail, HotelBookingsController + HotelBookingSagaState, HotelBookingConfirmed event + NotificationService consumer + HotelVoucher.cshtml + QuestPDF HotelVoucherDocument (HOTB-01..05, NOTF-02 primary)
- [ ] 04-04-PLAN.md — Car hire + Trip Builder: car + transfer search + car voucher, Baskets table + BasketsController + BasketPaymentOrchestrator using Option A two-PaymentIntents with deterministic idempotency keys, combined email (full & partial-failure per D-09) via BasketConfirmedConsumer (CARB-01..03, PKG-01..04)

### UAT Criteria

- [ ] A new customer registers with email and password, verifies their email, logs in, and searches for a flight — all without leaving the browser or calling support
- [ ] A flight search returns results within 5 seconds; results are filterable by stops, airline, and price range; selecting a result shows fare rules and all-in pricing before the payment step
- [ ] A customer completes a flight booking on a mobile device in 5 steps or fewer, enters card details via Stripe Elements, and receives a confirmation email with e-ticket within 60 seconds
- [ ] A customer searches for a hotel, views photos and cancellation policy, completes payment, and receives a confirmation email with hotel voucher within 60 seconds
- [ ] A customer uses Trip Builder to add both a flight and a hotel to a basket and completes checkout in a single payment; both booking confirmations appear in one email and separately in "My Bookings"
- [ ] The booking receipt PDF is downloadable from the customer dashboard and contains the correct booking reference, itinerary, and payment summary

---

## Phase 5: B2B Agent Portal

**Goal:** Travel agents can log in, search inventory with dual NET/GROSS pricing, complete bookings on behalf of customers using an atomic credit wallet, receive ticketing deadline alerts, and download booking documents — without any dependency on the B2C portal.

**Requirements:** B2B-01, B2B-02, B2B-03, B2B-04, B2B-05, B2B-06, B2B-07, B2B-08, B2B-09, B2B-10

**Depends on:** Phase 4

**UI hint**: yes

### Plans

1. **Keycloak B2B realm + agent RBAC** — configure `tbe-b2b` realm with roles (`agent-admin`, `agent`, `agent-readonly`); agency admin creates and deactivates sub-agent accounts; agents share SSO with the backoffice realm; YARP routes B2B portal traffic through B2B-authenticated gateway policies
2. **Agent pricing + booking flow** — display dual pricing (net fare + agency markup + gross selling price) simultaneously on search results; show commission amount per booking before confirmation; agent can set a per-booking markup; booking-on-behalf flow where agent enters passenger details; credit wallet balance shown in agent header; booking blocked with clear message when wallet balance is insufficient
3. **Credit wallet service** — implement wallet reservation at booking initiation, commitment on confirmation, and release on failure using serializable MSSQL transactions (no EF Core for deduction — raw SQL with `UPDLOCK`); append-only `WalletTransactions` log with no mutable balance field; wallet top-up via Stripe for agency admin; low-balance alert email when balance drops below configurable threshold
4. **Agent dashboard + documents** — agent dashboard showing bookings made, current wallet balance, and ticketing deadline alerts (24-hour warning and 2-hour urgent warning for pending PNRs); agent can view and download booking documents (e-tickets, hotel vouchers); PDF invoice generation per booking for agent-to-client billing; server-side paginated booking list filterable by client name, PNR, status, and date

### UAT Criteria

- [ ] An agency admin logs in, creates a sub-agent account, and the sub-agent can log in and search inventory without admin privileges; the sub-agent sees only their own bookings in the list
- [ ] An agent's search results show three price columns simultaneously: net fare, agency markup, and gross selling price; the commission amount is displayed before the agent confirms the booking
- [ ] An agent attempts two simultaneous bookings that together exceed the wallet balance; exactly one succeeds and one is rejected with "insufficient funds" — the wallet balance is never negative and the transaction log shows a reservation for the successful booking only
- [ ] A PNR with a ticketing deadline within 24 hours appears with a warning indicator on the agent dashboard; one within 2 hours appears with an urgent indicator; the agent receives an email alert for both thresholds
- [ ] An agent completes a booking on behalf of a customer (entering passenger details themselves) and can download the e-ticket PDF from the booking detail page
- [ ] Wallet top-up by agency admin via Stripe adds the correct amount to the wallet; the transaction log shows a top-up entry with balance before and after

---

## Phase 6: Backoffice & CRM

**Goal:** The operations team can manage all bookings, agents, and finances from a single backoffice — and customer and agency relationships are maintained natively through a CRM that is driven by booking events, not manual data entry.

**Requirements:** BO-01, BO-02, BO-03, BO-04, BO-05, BO-06, BO-07, BO-08, BO-09, BO-10, CRM-01, CRM-02, CRM-03, CRM-04, CRM-05, COMP-03

**Depends on:** Phase 5

**UI hint**: yes

### Plans

1. **Unified booking management** — backoffice booking list showing all channels (B2C + B2B) with full details, audit log, and status history; staff can cancel or modify a booking with reason logging; `BookingEvents` table is append-only and `DENY UPDATE/DELETE` enforced at DB level; each event stores a complete pricing snapshot and supplier response; failed saga compensations appear in a dead-letter queue view with human-actionable status; staff can requeue or manually resolve failed compensations
2. **Manual booking entry + supplier contracts** — form to create an offline booking (phone/walk-in sale) without going through the search flow, with supplier reference; supplier contract management for staff to enter negotiated net rates with validity dates and commission percentages; payment reconciliation view matching bookings to payments received and flagging discrepancies
3. **MIS reporting + financial views** — MIS reports for booking volumes by product, revenue, top agents, and top routes; exportable as CSV/Excel; payment reconciliation view flagging discrepancies between bookings and payments received
4. **CRM service (event-sourced projections)** — CRM service subscribes to `BookingConfirmed`, `BookingCancelled`, `UserRegistered` events and builds denormalized read models; Customer 360 view (profile, all bookings, contact history); agency management (create agencies, assign agents, set credit limits); booking search by PNR, customer name, email, or booking reference; communication log for staff notes on customer and agency records; upcoming trips view (future bookings filterable by status)

### UAT Criteria

- [ ] A backoffice staff member searches for a booking by PNR and views the full audit log showing every state transition with user, timestamp, and pricing snapshot at each event; the `BookingEvents` table rejects any `UPDATE` or `DELETE` statement at the database level
- [ ] Staff creates a manual booking (offline sale) with a supplier reference; it appears in the unified booking list alongside online bookings with a "manual" channel tag
- [ ] A failed saga compensation (e.g., GDS ticketing failure that could not auto-recover) appears in the dead-letter queue view; staff can requeue it or mark it manually resolved with a reason
- [ ] The MIS report for a selected date range shows correct booking counts by product type and total revenue; the report is exportable as Excel
- [ ] The Customer 360 view for a customer shows their profile, all past and upcoming bookings, and any staff notes — populated automatically from booking events without manual data entry
- [ ] A staff member searches for an agency, views its current wallet balance, credit limit, assigned agents, and complete transaction history; they can add a communication log note to the agency record

---

## Phase 7: Hardening & Go-Live

**Goal:** The platform is production-ready — load tested, observable with distributed tracing and alerting, a second GDS provider integrated and verified, GDS production credentials active, and a deployment runbook completed.

**Requirements:** INV-03 (second GDS in production), INFRA-06 (extended: production observability)

**Depends on:** Phase 6

### Plans

1. **Second GDS production integration + credential cutover** — verify Sabre or Galileo adapter against production GDS credentials (not sandbox); confirm bookable fares, PNR creation, and ticketing work on both GDS providers end-to-end; switch Amadeus credentials from test to production environment; confirm segment sell/ticket ratio monitoring is active per GDS PCC
2. **Distributed tracing + alerting** — deploy OpenTelemetry collector; instrument all services with traces, metrics, and structured logs; scrub PCI-sensitive fields from all span attributes before export; configure alerts for: saga compensation failures, GDS error rate spikes, TTL monitor backlog, Stripe webhook failures, wallet balance anomalies
3. **Load testing + performance validation** — run load tests against flight search (target: 50 concurrent searches, p95 < 3s on warm cache), booking saga (target: 10 concurrent bookings without saga conflicts), and wallet deduction (target: concurrent deductions on same wallet produce zero double-spends); identify and resolve bottlenecks before go-live
4. **Security audit + deployment runbook** — conduct security review: PCI-DSS SAQ-A controls verified, JWT validation on all endpoints confirmed, passport data encryption verified, GDS credentials confirmed out of source code; produce deployment runbook covering environment promotion, secrets rotation, database backup/restore, and rollback procedure

### UAT Criteria

- [ ] A real flight booking completes end-to-end using production Amadeus credentials — PNR created in live GDS, payment via Stripe live mode, e-ticket issued, confirmation email received within 60 seconds
- [ ] A real flight booking completes end-to-end using the second GDS (Sabre or Galileo) production credentials in the same way
- [ ] A load test of 50 concurrent flight searches completes with p95 response time under 3 seconds when Redis cache is warm; GDS rate limits are not breached during the test
- [ ] 10 concurrent booking attempts on the same wallet where 5 can succeed result in exactly 5 successes and 5 rejections — no negative balance, no double-spend, no saga corruption
- [ ] OpenTelemetry traces are visible in the collector for a complete booking flow from search to confirmation; no span attribute contains a card number, CVV, or raw passport number
- [ ] The deployment runbook is executed in a staging environment by following written steps only — environment comes up correctly, a booking completes, and rollback procedure is verified

---

## Progress

| Phase | Goal | Requirements | Status |
|-------|------|--------------|--------|
| 1 — Infrastructure Foundation | All services running, authenticated, and wired | INFRA-01 to INFRA-07 | Not started |
| 2 — Inventory Layer & GDS Integration | Amadeus search works end-to-end via unified abstraction | INV-01 to INV-09 | Not started |
| 3 — Core Flight Booking Saga (B2C) | Real booking completes: PNR → authorize → ticket → capture → email | FLTB-01–10, PAY-01–08, NOTF-01–06, COMP-01–06 | Not started |
| 4 — B2C Portal (Customer-Facing) | B2C portal launchable; customers search, book, and manage flights + hotels + car | B2C-01–08, HOTB-01–05, CARB-01–03, PKG-01–04 | Not started |
| 5 — B2B Agent Portal | Agents log in, book with credit wallets, receive TTL alerts | B2B-01 to B2B-10 | Not started |
| 6 — Backoffice & CRM | Operations manages all bookings and finances; CRM driven by booking events | BO-01–10, CRM-01–05 | Not started |
| 7 — Hardening & Go-Live | Production-ready: load tested, second GDS live, monitoring active | INV-03 (prod), INFRA-06 (prod observability) | Not started |

---

## Coverage

**v1 requirements total:** 76
**Mapped to phases:** 76
**Unmapped:** 0

| Requirement group | Phase |
|-------------------|-------|
| INFRA-01 to INFRA-07 | Phase 1 |
| INV-01, INV-02, INV-04, INV-05, INV-06, INV-07, INV-08, INV-09 | Phase 2 |
| INV-03 | Phase 7 (second GDS production verification) |
| FLTB-01 to FLTB-10 | Phase 3 |
| PAY-01 to PAY-08 | Phase 3 |
| NOTF-01 to NOTF-06 | Phase 3 |
| COMP-01 to COMP-06 | Phase 3 |
| B2C-01 to B2C-08 | Phase 4 |
| HOTB-01 to HOTB-05 | Phase 4 |
| CARB-01 to CARB-03 | Phase 4 |
| PKG-01 to PKG-04 | Phase 4 |
| B2B-01 to B2B-10 | Phase 5 |
| BO-01 to BO-10 | Phase 6 |
| CRM-01 to CRM-05 | Phase 6 |

---

*Roadmap created: 2026-04-12*
*Last updated: 2026-04-12 after initial creation*
