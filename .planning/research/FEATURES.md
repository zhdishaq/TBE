# Features Research

**Domain:** Full travel booking engine (B2C + B2B + Backoffice + CRM)
**Researched:** 2026-04-12
**Confidence:** HIGH (training data through Aug 2025; travel industry feature expectations are stable and well-documented)
**Note:** WebSearch and WebFetch were unavailable. Findings draw on training knowledge of GDS platforms (Amadeus, Sabre, Galileo), major OTA patterns (Booking.com, Expedia, Kayak), B2B travel systems (Travelport, Tourplan, Tramada), and industry standards. Flag for live verification if any specific claim drives a major architectural decision.

---

## B2C Portal — Table Stakes

These are features users expect to find before they even search. Missing any of these causes immediate abandonment.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Flight search: one-way, return, multi-city | Baseline since 2005. Any site without it is not taken seriously. | Medium | Must support flexible date grids too |
| Airport/city autocomplete (IATA) | Users cannot type IATA codes. Lookup must feel instant. | Low | Needs a maintained IATA airport dataset |
| Cabin class filter (Economy/Business/First) | Users self-segment by budget — essential for premium travellers | Low | Filter at search, not just result |
| Passenger count selector (ADT/CHD/INF) | Families book together; per-passenger pricing must display correctly | Medium | Infant-on-lap vs seat distinction matters for GDS pricing |
| Live availability and real-time pricing | Cached stale fares cause booking failures and trust loss | High | GDS search is expensive; cache results with short TTL (5–15 min) |
| Price-inclusive display (taxes shown) | Regulatory expectation in most markets; users distrust "from" prices | Medium | All-in price must be shown before payment step |
| Sort and filter on results | Sort by price, duration, stops. Filter by airline, stops, departure window | Low | Without this users feel overwhelmed on busy routes |
| Flight detail expansion (layovers, equipment, baggage) | Users need to know what they are buying before proceeding | Low | Layover duration, terminal, baggage allowance per segment |
| Seat selection (at least basic map) | Airlines publish seat maps via NDC/GDS; users abandon without this | High | Can start with "no preference" option, add interactive map in v2 |
| Ancillary/add-ons at booking (bags, meals) | Standard post-NDC expectation. Skipping it loses ancillary revenue. | High | Only feasible via NDC-capable GDS connection or direct airline API |
| Hotel search (location, dates, occupancy) | Basic search with map view is expected | Medium | Powered by Hotelbeds/similar aggregator |
| Hotel photo gallery and room descriptions | Users book on visual trust — text-only listings convert poorly | Low | Served from aggregator content API |
| Hotel map view | Users evaluate location before price | Medium | Google Maps embed or Mapbox; must show property pins |
| Room rate breakdown with cancellation policy | Refundable vs non-refundable is a decision driver | Low | Aggregators provide this; must be displayed clearly |
| Car hire search (pickup/dropoff location, dates, vehicle category) | Expected if advertised as a product | Medium | Supplied by RentalCars/Cartrawler or similar |
| Guest checkout or simple registration | Forced account creation is a top abandonment cause | Low | Allow booking with email only, offer account post-booking |
| Booking confirmation page and email | Users need immediate proof. Email must arrive within 60 seconds. | Medium | Email via async worker; include PNR, e-ticket or voucher |
| Online payment via card (3DS compliant) | Non-negotiable for B2C. Stripe or similar handles 3DS2. | High | PCI-DSS scope must be understood before implementation |
| Booking management (view, cancel) for logged-in users | Users expect to retrieve bookings without calling | Medium | My Bookings page: status, documents, cancel option |
| Mobile-responsive design | 60–70% of travel searches are mobile; non-responsive sites lose majority of traffic | Medium | Next.js with responsive Tailwind — not a separate mobile site |
| SSL and trust signals (lock icon, payment logos) | Users will not enter card details on an untrusted-looking site | Low | HTTPS everywhere; Stripe trust badge at checkout |
| Privacy policy and cookie consent (GDPR) | Legal requirement in EU/UK markets | Low | Required before launch in EU-adjacent markets |

---

## B2C Portal — Differentiators

Features that are not expected but create meaningful conversion lift or loyalty when present.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Flexible date calendar / "cheapest day" grid | Shows price by day — reduces search iterations, increases conversion | High | Requires fare-calendar API calls (Amadeus has this); expensive to implement correctly |
| Price alert subscriptions | Retains users who are not ready to book yet; drives return traffic | Medium | Background job polling fares; email/push notification |
| AI-powered destination recommendations | "Where should I go?" based on budget, interests, season | High | GPT/LLM-powered; requires content data layer for destinations |
| Package bundles (flight + hotel, presented as unit) | Saves user time and can be priced with commission bundled | High | See Dynamic Packaging section — this is a significant separate workstream |
| Loyalty point display and earn estimates | If business has a loyalty scheme, showing "you'll earn X points" increases booking | Medium | Requires loyalty service integration; do NOT build loyalty in v1 |
| Visa and entry requirement check | Users want to know if they need a visa — reduces post-booking cancellations | Low–Medium | Integrate IATA Timatic API or Travel Audience |
| Live chat / AI chatbot for support | Reduces phone support load; resolves common questions instantly | High | Use third-party (Intercom, Drift) rather than building |
| Multi-currency display | Essential if serving international audiences | Medium | Currency conversion at display level; charging currency may differ |
| Saved searches and wishlists | Encourages account creation and return visits | Low | Simple DB feature once user auth exists |
| Trip sharing (share itinerary link) | Social utility; useful for group bookings | Low | Generate a shareable read-only booking view |
| CO2 emissions per flight | Growing expectation from environmentally-aware travellers | Low | Per-segment kg CO2 data from providers like Atmosfair or Skyscanner API |

---

## B2B Agent Portal — Table Stakes

Travel agents using a booking system have professional expectations. They are faster and more demanding than consumers. Missing these causes them to bypass the portal entirely and use the GDS terminal directly.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| All B2C search capabilities plus agent pricing | Agents need to see NET prices and GROSS prices simultaneously | Medium | Price display must be toggleable or dual-column |
| Commission and markup visibility per booking | Agents cannot use a system that hides their earnings | Medium | Commission = (selling price - net cost). Must be calculated and shown live |
| Agent-specific markup tool | Agent should be able to add a markup % or fixed amount before presenting to client | Medium | Final price = net + markup; system recalculates on the fly |
| Client profile management (book on behalf of) | Agents book for their clients, not themselves — passenger details pre-fill | Medium | Pax profile store: name, DOB, passport, FF numbers |
| Multi-passenger booking in one session | Group and family bookings are bread-and-butter for agents | Medium | Supports adding multiple different passengers |
| Queue management (PNR queue) | GDS queues are how agents organise work — ticketing queue, cancellation queue, etc. | High | Core GDS concept; must integrate with Amadeus/Sabre queue system |
| Booking list with status and filters | Agents manage dozens of bookings daily — needs search by client name, PNR, status, date | Medium | Server-side paginated list with filters |
| Credit wallet balance display and deduction | Agent must see remaining credit before booking; deduction must be atomic | High | Wallet service with reservation + deduction at confirmation; see pitfalls |
| Receipt and invoice generation per booking | Agents issue invoices to their clients — this is an operational necessity | Medium | PDF generation: agent invoice and client receipt as separate templates |
| Ticketing (issue e-ticket against PNR) | After booking, flights need to be ticketed within time limit (ticketing deadline) | High | Amadeus/Sabre ticketing command via API; failure to ticket causes auto-cancellation |
| Void and refund initiation | Agents cancel bookings — system must support same-day void and post-departure refund request | High | GDS void vs airline refund — two separate flows |
| Reissue / date change | Business travel especially requires date changes | High | One of the hardest GDS operations; Fare difference calculation required |
| Booking remarks and internal notes | Agents attach OSI/SSR remarks to PNRs (wheelchair, meal, special handling) | Medium | GDS SSR/OSI codes; must be sent correctly to airline |
| Agency admin: sub-agent management | Agency owner manages their own team — create/deactivate sub-agents | Low | Role hierarchy: agency admin > sub-agent |
| Agent reporting (bookings by period, revenue, commission) | Agents need to reconcile with their accounts | Medium | Date-range reports downloadable as CSV/Excel |
| Role-based access control | Admin sees all bookings; sub-agent sees only their own | Low | Standard RBAC; not complex in single-tenant system |
| Deadline alerts (ticketing deadline, option deadline) | GDS bookings have time limits — if missed, booking is auto-cancelled | Medium | Background job checking deadlines; in-portal alert + email |

---

## B2B Agent Portal — Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Split payment (partial deposit) | Allows agent to collect deposit now, balance later | Medium | Requires payment schedule logic tied to booking |
| Commission advance / hold report | Shows expected future commission income | Low | Simple query on confirmed bookings not yet travelled |
| Bulk booking import (group travel) | For agents handling corporate or group travel | High | CSV/Excel import of passenger lists; complex validation |
| Client communication templates | Pre-built email templates for agents to send to their clients from within the system | Medium | Templating engine + SMTP send; useful but not critical |
| Automated ticketing at deadline | System auto-tickets PNR at N hours before deadline if not yet ticketed | High | Risk-heavy feature; requires fallback handling for failures |
| Agent performance dashboard | Show conversion rate, top destinations, revenue trends per agent | Low | Nice reporting layer; not blocking operations |
| Offline PNR import | Agent creates PNR in GDS terminal; imports it into the system by PNR reference | Medium | Retrieve PNR from GDS and record it in local DB |

---

## Backoffice / Midoffice — Table Stakes

Backoffice is the operations control layer. If this is weak, the business runs on spreadsheets alongside the system — a known failure mode.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Unified booking list (all channels) | B2C and B2B bookings in one view — operations team does not work with two lists | Low | Polymorphic booking type: channel tag on each booking |
| Booking detail view (full PNR, payments, status history) | Ops team needs full audit trail per booking | Medium | Status history: created → confirmed → ticketed → travelled / cancelled |
| Manual booking entry (offline sales) | Phone/walk-in bookings must be entered manually — this is unavoidable in travel | Medium | Form to create a booking without going through search flow |
| Modify / cancel / reissue actions | Operations acts on bookings raised by agents or customers | High | All GDS write operations: cancel segment, reissue ticket, void |
| Refund management | Track refund requests, process status, expected return date | Medium | Refunds from airline can take weeks — must track state |
| Supplier contract management | Negotiated hotel/car rates entered as contracts with validity dates and commission % | Medium | Used to override or supplement aggregator pricing |
| Payment reconciliation | Match booking payments to bank statements; identify discrepancies | High | Critical for financial integrity; often underestimated |
| BSP/ARC report generation | IATA BSP (or ARC for USA) weekly sales report — regulatory filing requirement for IATA-accredited agents | High | Automated BSP file generation if IATA-accredited; else manual |
| Agent wallet top-up management | Operations approves wallet recharge requests from agents | Low | Simple approval workflow: request → review → credit |
| User management (backoffice staff, roles) | Different staff have different access: finance, operations, management | Low | Standard RBAC for backoffice roles |
| MIS reports: sales, revenue, bookings by product/period | Management needs business performance visibility | Medium | Pre-built report templates; export to Excel |
| Supplier invoice matching | Match supplier invoices against booked costs | Medium | Important for cost control but often built in v2 |
| Markup and commission rules engine | Define commission % by route, supplier, product, agent tier | High | Rules evaluated at pricing time; complex but essential for margin control |
| Audit log | Every action on a booking logged with user, timestamp, before/after | Low | Required for dispute resolution and compliance |
| Email template management | Ops team can edit confirmation email content without developer | Low | Template editor (or at least variable-driven templates in config) |

---

## CRM for Travel — Table Stakes

Generic CRMs (Salesforce, HubSpot) miss critical travel-specific data relationships. A travel CRM must understand bookings, not just contacts.

| Feature | Why Generic CRM Fails | Travel-Specific Requirement |
|---------|----------------------|----------------------------|
| Customer 360 profile | Generic CRM has contacts with deals. Travel needs contacts with bookings, segments, destinations, loyalty numbers, passport details, dietary/assistance preferences. | Customer profile must link to booking history natively — not via a custom integration bolt-on |
| Booking history on the contact | A CRM without booking history is useless for upsell | Every booking (past + upcoming) visible on contact; filter by product type |
| Travel preferences store | Seat preference, meal preference, cabin preference, preferred airlines, hotel chain loyalty numbers | Stored at contact level; pre-fills booking forms and agent tools |
| Passport and document management | Agents frequently need passport details at booking | Secure storage of passport number, expiry, nationality; accessible to booking flow |
| Upcoming trip view | "Who is travelling in the next 30 days?" is a standard ops query | Segment on future booking dates; used for pre-travel upsell and support prep |
| Agency/agent CRM (B2B side) | Generic CRM is contact-centric. Travel needs agency-as-account with sub-agents as contacts and a wallet as a financial relationship. | Agency entity: wallet balance, credit limit, commission tier, assigned account manager |
| Follow-up and task management | Agents need reminders for ticketing deadlines, option deadlines, outstanding quotations | Task tied to booking + deadline; triggers from booking status changes |
| Communication log | Emails/calls related to a booking attached to that booking, not just to the contact | Booking-level communication thread, not just contact-level |
| Quotation / enquiry management | Travel enquiries often start as quotes before becoming bookings | Quote object: product, dates, price, expiry, status (sent/accepted/expired/lost) |
| Post-travel feedback collection | Trigger feedback request N days after travel date | Automated post-travel email; feedback stored against booking |

**Key architectural implication:** The CRM in a travel system is not a standalone product — it is a read/write view on the booking database. The customer record is the booking history. This means the CRM must be built as part of the same data model, not as a separate system integrated via API. A bolted-on generic CRM will always feel disconnected because it does not have native booking objects.

---

## Dynamic Packaging — How It Works

Dynamic packaging (DP) is the assembly of flight + hotel (± car) into a bundle priced and sold as one product. It is technically and commercially distinct from selling each product separately.

### Commercial Model

**How pricing works:**
- The system queries flight inventory (GDS) and hotel inventory (aggregator) independently.
- A "package price" is computed as: `net_flight + net_hotel + markup_package`. The markup on a package is typically higher than on individual components (because the customer sees one price and cannot price-compare easily).
- Commission is earned on both components. Some aggregators have specific "package rates" (lower net, must be sold as bundle) — these are separate rate codes from standalone hotel rates.
- The bundle is sold at a single "from" price per person. Tax breakdown must be available for regulatory compliance.

**Cancellation complexity:**
- If a customer cancels a package, both the flight and hotel cancellation policies apply separately. The flight may be non-refundable while the hotel is refundable. The system must calculate the net refund correctly and present it pre-cancellation.
- Partial cancellation (e.g., cancel hotel, keep flight) must be explicitly handled or explicitly blocked with a clear message.

### Technical Model

**Search flow:**
1. User submits package search: origin, destination, dates, occupancy.
2. System triggers parallel async calls: flight search (GDS) and hotel search (aggregator) for the same destination/dates.
3. Results are correlated by destination and dates. A flight result is paired with each compatible hotel result.
4. The pairing creates a "package" record: `{ flight_option, hotel_option, package_price, saving_vs_separate }`.
5. Results are ranked (by price, or by package savings).
6. User selects a package. System holds both the flight PNR (GDS option/hold) and hotel availability.

**Booking flow:**
1. Flight is priced and held (GDS PNR with option expiry, typically 24–72 hours).
2. Hotel is confirmed against aggregator (or held, depending on aggregator's hold capability).
3. Payment is taken for the full package amount in one transaction.
4. Flight is ticketed (or queued for ticketing). Hotel voucher is issued.
5. Confirmation email contains both components as one itinerary.

**Critical coupling problem:** GDS holds expire independently of hotel availability. If the flight hold expires before the customer pays (session timeout, payment failure), the package breaks. The system must:
- Show a hold expiry timer on the booking page.
- Handle hold expiry gracefully (re-price, warn user, retry hold).
- Avoid confirming the hotel before the flight is secured.

**Package rate vs standalone rate:**
- Some aggregators (Hotelbeds, Juniper) have a specific "PACKAGE" rate plan distinct from the retail rate. These rates are contractually required to be sold only as part of a flight+hotel bundle. Using them standalone violates the contract. The system must enforce this at the product layer.

**Saving calculation:**
- `saving = (flight_standalone_price + hotel_standalone_price) - package_price`
- This "saving" is a marketing figure. It requires knowing the standalone price of each component. For flights, the standalone price is real (GDS fare). For hotels, the standalone price is the retail rack rate (not the net price). The system must use the correct reference price for savings display, or the claim is misleading.

### Recommended Approach for v1

Do not build true dynamic packaging in v1. The search correlation, hold management, cancellation splitting, and rate plan enforcement add 2–3 months of complexity. Instead:

**v1 approach:** Sell flights and hotels as separate products side-by-side, with a "build your trip" UX that encourages users to book both. This is sometimes called "semi-dynamic packaging" or "trip builder." The user gets two separate bookings, two confirmations. No package pricing. This delivers 80% of the user experience at 20% of the complexity.

**v2 approach:** True DP with package pricing, single booking reference, package cancellation policy, and package rate plans.

---

## Anti-Features (Defer from v1)

These are features that look valuable but introduce disproportionate complexity, rare edge cases, or deep integration requirements that block core feature delivery. Defer all of these from v1.

| Anti-Feature | Why to Avoid in v1 | What to Do Instead |
|--------------|-------------------|-------------------|
| True dynamic packaging | Correlated search, hold management, package cancellation splitting, package rate enforcement — each is a project. Combined they can delay v1 by months. | Trip builder UX: sell flight + hotel separately, visually grouped |
| Loyalty / points programme | Points accrual, redemption, tier management, points expiry, partner earn — a separate product. Building this before core booking is validated wastes months. | Capture "loyalty number" in passenger profile only; full program is v3+ |
| NDC ancillaries (full) | NDC ancillary catalogues (seat maps, bags, meals via NDC) require airline-by-airline certification. Each airline's NDC implementation differs. | Offer "contact us to add bags" as fallback; build NDC ancillaries airline-by-airline in v2 |
| Group booking (10+ pax) | Group bookings require group desks, group fares, name-later options, group deposit schedules. Entirely different workflow from individual PNR. | Handle as manual offline booking via backoffice |
| Automated BSP filing | BSP file format (HOT file) generation is complex and error-prone. Incorrect submissions have financial consequences. | Generate sales data report; submit BSP manually or via accountant in v1 |
| Interline and codeshare complexity | Complex fare construction rules for multi-carrier itineraries. The GDS handles this, but displaying it correctly and handling changes requires deep fare expertise. | Rely on GDS to return valid itineraries; do not build custom interline logic |
| Real-time seat maps (NDC) | Airline-specific NDC certification required per carrier. Significant per-airline effort. | Offer "no preference / window / aisle" at booking; seat map in v2 |
| Multi-currency settlement | Charging customers in local currency while settling with suppliers in another introduces FX risk, reporting complexity, and potential regulatory issues. | Single settlement currency for v1; display-only multi-currency if needed |
| Affiliate / white-label distribution | Letting third-party websites embed your search adds OAuth, revenue sharing, separate reporting, and margin complexity | Not applicable (out of scope by PROJECT.md) |
| Live chat build (custom) | Chat widgets are commoditised. Building custom adds nothing. | Use Intercom, Crisp, or Chatwoot (self-hosted) — integrate in a day |
| Native mobile app | Responsive web covers 80% of mobile use cases. A native app requires a separate codebase, release cycle, and store review. | Responsive Next.js in v1; native app post-validation |
| Insurance sales | Adding travel insurance as a product requires insurer partnership agreements, regulatory licensing in each market (often needs specific FCA/broker authorisation in UK), and claims handling integration. | Display third-party links to insurer; do not process insurance in-platform |
| Visa processing | Visa applications handled in-platform requires partnerships with visa agencies, complex country-specific rules, and liability. | Link to IATA Timatic or a visa information provider for information only |
| Flexible / "mix and match" fare families | Showing branded fare families (Basic/Standard/Flex) from every airline consistently requires per-airline mapping. Inconsistency in display erodes trust. | Show cheapest available; let agents know higher fares exist via GDS terminal |
| Hotel rate parity monitoring | Checking if your hotel rates are cheaper than OTA rates requires a competitive intelligence feed — a separate product. | Not needed in v1 |
| Automated ticket reissue on schedule change | When airline changes a schedule, the PNR must be reissued. Automating this correctly is very hard — incorrect reissue can generate new ticketing fees. | Detect schedule changes via GDS notification queue; require manual backoffice action |

---

## Feature Dependencies (What Must Be Built Before What)

Some features have hard upstream dependencies. Building out of order creates rework.

```
GDS connectivity (Amadeus/Sabre API client)
  └─ Flight search results
       └─ Flight availability + pricing
            └─ PNR creation (booking)
                 ├─ Ticketing (requires confirmed PNR)
                 ├─ Void (requires un-ticketed PNR)
                 ├─ Reissue (requires ticketed PNR)
                 └─ Agent portal queue management

Hotel aggregator API (Hotelbeds/Duffel)
  └─ Hotel search results
       └─ Hotel availability + rates
            └─ Hotel booking (voucher)
                 └─ Hotel cancellation

User authentication (B2C + B2B separation)
  ├─ B2C booking management (My Bookings)
  ├─ B2B agent login
  │    ├─ Agent credit wallet (requires agent identity)
  │    └─ Commission display (requires agent pricing rules)
  └─ Backoffice login (separate role from B2C/B2B)

Payment gateway (Stripe)
  └─ B2C checkout
       └─ Refund initiation (requires original charge ID)

Booking database (unified booking model)
  ├─ Backoffice booking list
  ├─ CRM customer 360
  ├─ Agent portal booking list
  └─ MIS reporting

CRM customer profile
  └─ Passenger pre-fill in booking forms
       └─ Passport management (stored on profile)

Agent wallet service
  └─ B2B booking completion (wallet deduction is part of booking commit)
       └─ Agent reporting (wallet transaction history)

Email notification service (async)
  ├─ Booking confirmation (B2C and B2B)
  ├─ Ticketing deadline alerts
  └─ Post-booking documents (e-ticket PDF)

Markup / pricing rules engine
  ├─ Agent-specific pricing (applied at search time)
  └─ Package price calculation (applied at correlation time)
```

**Critical path for v1 MVP:**
1. GDS client → flight search → PNR create → payment → ticket → confirmation email
2. Hotel client → hotel search → hotel book → voucher email
3. Auth → B2C account → My Bookings
4. Auth → B2B agent → wallet → agent booking list

Everything else (reissue, void, CRM, backoffice reports, car hire) is built on top of this foundation.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| B2C table stakes | HIGH | Validated by decade of OTA research; Expedia/Booking.com set these standards |
| B2B agent portal | HIGH | GDS terminal workflows are well-documented; Amadeus/Sabre agent tooling sets expectations |
| Backoffice features | HIGH | Standard travel mid-office requirements; consistent across Tourplan, Tramada, and IATA documentation |
| CRM travel-specific needs | HIGH | The gap between generic CRM and travel-specific CRM is well-understood in the industry |
| Dynamic packaging mechanics | HIGH | Package pricing rules, hold mechanics, and cancellation splits are GDS-standard documented behaviour |
| Anti-features rationale | MEDIUM | Deferral rationale is based on industry patterns; specific complexity estimates should be validated with GDS docs |
| Feature dependencies | HIGH | Hard technical dependencies (PNR must exist before ticketing, etc.) are facts of GDS protocol |

## Sources

Note: WebSearch and WebFetch were unavailable during this research session. Findings are based on training data through August 2025, covering:
- IATA standards documentation (NDC, BSP, EDIFACT)
- Amadeus and Sabre developer documentation (public APIs)
- OTA patterns from Booking.com, Expedia, Kayak, Skyscanner public behaviour
- B2B travel system patterns from Tourplan, Tramada, TravelPort
- Travel industry analyst reports (Phocuswright, Skift) available in training data

For production-critical decisions, verify against:
- https://developers.amadeus.com (current API capabilities)
- https://www.iata.org/en/programs/ops-infra/bsp/ (BSP requirements)
- https://hotelbeds.com/developer-documentation (hotel aggregator rate types)
