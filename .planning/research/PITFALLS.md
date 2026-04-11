# Pitfalls Research

**Domain:** Full-stack travel booking engine (B2C + B2B + backoffice)
**Stack:** .NET/C# microservices, Next.js, MSSQL, RabbitMQ, Redis, Docker
**Researched:** 2026-04-12
**Confidence note:** External search tools unavailable. Findings drawn from training knowledge (cutoff August 2025) on GDS integrations, PCI-DSS standards, distributed systems, and travel platform architecture. HIGH confidence on structural/standard items; MEDIUM confidence on version-specific API details that change frequently. Flag items marked [VERIFY] before implementation.

---

## GDS Integration Gotchas

### Pitfall 1: Stateful Session Architecture (SOAP-era GDS APIs)

**What goes wrong:** Amadeus, Sabre, and Galileo legacy SOAP APIs are fundamentally stateful. A search (availability) creates a session; the subsequent sell/book call must reuse the same session token on the same back-end node. If you load-balance across GDS nodes without session affinity, the second call hits a node with no knowledge of the first, and you get cryptic errors like "transaction already active" or "segment mismatch" rather than a clean booking failure.

**Why it's worse than it looks:** Most developers assume REST semantics — stateless calls where any replica can handle any request. The GDS world predates REST. Amadeus Web Services (AWS SOAP) and Sabre Web Services both issue a session token on SignIn that must be passed back verbatim for every subsequent call in the same logical transaction. Token lifetime is typically 15-30 minutes idle. If you pool connections naively (e.g., share one session across concurrent requests), you will corrupt mid-session state silently — booking calls will succeed ACK at the transport layer but produce wrong PNRs or ghost bookings.

**Consequences:** Double bookings, ghost PNRs that hold inventory but have no confirmed ticket, sessions exhausted under load (GDS providers cap concurrent sessions per office ID/PCC), inexplicable booking failures only visible in GDS back-end logs.

**Prevention:**
- Implement a dedicated GDS session pool per office/PCC with strict request-to-session affinity. Each in-flight booking transaction locks one session exclusively.
- Use Amadeus REST APIs (Self-Service or Enterprise REST) where available — they are stateless and far easier to work with. [VERIFY: Enterprise REST API coverage for your markets]
- For SOAP: implement a session manager service that tracks session state (available / in-use / expired) and enforces exclusive checkout/checkin semantics with timeout-based forced release.
- Cap pool size to your contracted session limit. Expose a metric so you can alert before exhaustion.

**Detection:** Monitor for sessions checked out but never returned (stuck sessions). GDS providers will shut your office ID down if you exhaust sessions and leave them open.

---

### Pitfall 2: GDS Rate Limits and Segment-Sell Penalties

**What goes wrong:** Every GDS search that hits live availability is billable. Each "availability request" (AVS) and "fare quote" (FQ) carries a cost — typically fractions of a cent, but they compound rapidly. Search-heavy UX patterns (live-as-you-type date pickers, multi-city explorers, speculative searches that are never booked) can generate thousands of AVS calls per hour.

Beyond cost, Amadeus and Sabre both enforce rate limits per PCC (pseudo city code / office ID). Hitting those limits results in throttling or temporary suspension of the office ID — taking down all bookings, not just search.

**Why it's worse than it looks:** Segment sell/cancel churning attracts penalties. If your booking flow sells a segment (creates a PNR) and then abandons it without ticketing (common in failed payment flows), that "ghost segment" stays on airline inventory. Airlines track the ratio of booked-to-ticketed segments per agency PCC. A high churn ratio (many books, few tickets) triggers warnings and eventually suspension of booking rights on specific airlines.

**Consequences:** Unexpected GDS transaction bills 10-50x higher than projected, office ID suspension, airline blacklisting of your PCC.

**Prevention:**
- Cache search results aggressively in Redis (see caching pitfalls below).
- Never hit live GDS availability for exploratory/inspirational search — use cached or NDC aggregator data for the browse phase; hit GDS only when the user signals booking intent.
- In the booking flow, do NOT sell segments until payment is confirmed or at minimum authorized. Use "passive segments" or fare-lock hold operations if the GDS supports it.
- Monitor segment sell/ticket ratio as a first-class metric. Target >80% ticket rate on sold segments.
- Implement circuit breakers per GDS endpoint so a provider outage doesn't cascade into an avalanche of retry calls.

---

### Pitfall 3: PNR Lifecycle and Ticketing Deadline Management

**What goes wrong:** A PNR (Passenger Name Record) is not a ticket. A PNR that is never ticketed within the ticketing time limit (TTL) will auto-cancel. TTLs vary from 0 minutes (immediate ticketing required, common for low-cost carriers via GDS) to 24-72 hours (international fares, IATA settlement rules). Your system must track TTLs per PNR and either ticket before the deadline or proactively cancel and refund.

**Why it's worse than it looks:** TTLs are not always returned in the booking response. They can be embedded in fare rules as free-text strings like "TICKET MUST BE ISSUED NO LATER THAN 16JAN OR BY 24 HOURS OF BOOKING WHICHEVER IS EARLIER." Parsing this reliably requires a custom fare-rule parser. Failing to parse it means you miss the deadline, the PNR silently drops, and the customer's card was charged but they have no ticket. You then face manual intervention, potential rebooking at higher fares, and a furious customer.

**Consequences:** Customers charged without valid booking, chargebacks, manual recovery costs, potential ATOL/IATA regulatory exposure.

**Prevention:**
- Store the TTL on every PNR record. Default to a conservative 2-hour TTL if none is returned, then refine.
- Build a TTL monitor background service (hosted service in .NET) that queries open PNRs every 5 minutes. Flag any PNR within 30 minutes of TTL as critical.
- Implement a "dead letter" flow: if ticketing fails, the TTL monitor triggers automatic cancellation + refund initiation before the deadline passes.
- Parse fare rule free text for ticketing time limits using regex patterns covering IATA standard formats. Log unparsed rules for manual review.
- The ticketing deadline is often earlier than the TTL: queuing for ticketing and then having a queue processor fail means you need buffering time.

---

### Pitfall 4: Price Validation at Booking Time (Fare Guarantee Window)

**What goes wrong:** Fares returned in a search response are not guaranteed. Between the search and the booking sell, prices change. Most GDS integrations require a "price verification" call (Amadeus: FlightOffersPricing; Sabre: BargainFinderMax revalidation) immediately before the sell command. Skipping this step results in either booking at a stale price (losing money on the delta if the fare went up) or a booking rejection from the GDS.

**Why it's worse than it looks:** Even with a pricing call, the window between pricing and sell is typically only seconds. Under load, if the pricing and sell are not handled atomically (as a single locked sequence), race conditions between concurrent users on the same flight/class can result in the sell failing after the customer has been informed "booking confirmed." The GDS sell is not transactional in the ACID sense.

**Consequences:** Price discrepancies billed to the agency, sell failures presented to customers as confirmations, revenue leakage.

**Prevention:**
- Always call FlightOffersPricing (or equivalent) as the penultimate step before sell, with a maximum staleness of 60 seconds.
- Present the re-validated price to the customer before charging — if it changed, show the new price and require re-confirmation.
- The sequence must be: price → authorize payment → sell → capture payment → ticket. Never capture before sell confirmation.

---

## PCI-DSS Compliance Requirements

### Pitfall 5: Treating Stripe as Full PCI Scope Elimination

**What goes wrong:** Teams assume that because they use Stripe (or another PCI-compliant gateway), they have zero PCI obligations. This is false. PCI-DSS scope depends on how card data flows through your systems, not just who processes it.

**Why it's worse than it looks:** If you use Stripe.js / Stripe Elements (card data entered directly into Stripe-hosted fields, never touching your servers), you qualify for SAQ-A (Self-Assessment Questionnaire type A) — the lightest PCI tier, roughly 22 controls. But if you:
- Build a custom card form and POST the number to your own server (even briefly to forward to Stripe)
- Log request bodies that might contain card data
- Store any card field values in browser local storage, Redux state, or server-side session
- Pass card data through your API gateway or load balancer

...you immediately escalate to SAQ-D or full QSA audit, which is 300+ controls including physical datacenter requirements, quarterly penetration tests, annual on-site audits, and network segmentation requirements.

**Consequences:** Non-compliance fines (card brands can levy $5,000–$100,000/month on your acquiring bank, who passes it to you), breach liability, loss of card processing rights, reputational damage.

**Prevention:**
- Use Stripe Elements or Stripe's Payment Element exclusively. Card numbers must never touch your servers.
- Implement Content Security Policy (CSP) headers that explicitly whitelist only Stripe's domains for script execution. An XSS attack that exfiltrates card data from a Stripe Element is still your liability under PCI if CSP was absent.
- Disable all request/response body logging at the API gateway layer for any endpoint that could receive card data.
- Complete your SAQ annually and document it. Even SAQ-A requires a quarterly vulnerability scan by an Approved Scanning Vendor (ASV).
- Never store CVV/CVC — this is prohibited under PCI regardless of tier. Even tokenized storage of CVV is non-compliant.
- If you add Apple Pay, Google Pay, or 3DS2 flows, re-verify SAQ applicability — these can change your scope.

---

### Pitfall 6: B2B Wallet Flow and PCI Scope Creep

**What goes wrong:** B2B agents using credit wallets don't pay by card at checkout — the agency pre-loads the wallet. Teams assume this removes PCI scope for the B2B portal entirely. It doesn't: the wallet top-up flow uses a card payment, and if agents can also optionally pay by card at booking time (common for overflow bookings beyond their credit limit), the B2B portal is in scope too.

**Prevention:**
- Apply the same Stripe Elements approach to the wallet top-up UI as to B2C checkout.
- Implement clear architectural separation: the wallet deduction at booking time involves no card data; document this explicitly in your scope boundary.
- If you ever allow agents to bypass the wallet and pay by card per-booking, treat that entire agent portal flow as PCI-in-scope.

---

### Pitfall 7: PCI Scope Creep via Logging and Distributed Tracing

**What goes wrong:** Modern microservices use distributed tracing (OpenTelemetry, Application Insights) and structured logging. Booking service request/response payloads are routinely logged for debugging. It only takes one developer adding `log.Information("Booking request: {request}", JsonSerializer.Serialize(bookingRequest))` where `bookingRequest` includes a card token or (worse) a card number passed through from the frontend to drop your entire logging infrastructure into PCI scope.

**Prevention:**
- Implement a log scrubbing middleware on all services that redacts any field matching patterns like `cardNumber`, `cvv`, `pan`, `card_number`.
- Enforce this via a custom ILogger wrapper or middleware that runs before logs are emitted to any sink (Application Insights, Seq, ELK, etc.).
- Review distributed trace attributes — OpenTelemetry spans should never carry card field values as attributes.
- Include a logging audit in your pre-launch security review.

---

## Distributed Transaction Failures

### Pitfall 8: Payment Succeeds, GDS Ticketing Fails (The Phantom Booking)

**What goes wrong:** The canonical failure scenario in travel booking:
1. Customer submits booking
2. Stripe payment authorized and captured — customer's card is charged
3. GDS ticketing call fails (timeout, GDS outage, fare no longer available, PNR TTL expired mid-flow)
4. Customer receives no ticket but money is gone
5. Your system has no automated recovery path

**Why it's worse than it looks:** This is not rare. GDS ticketing endpoints have higher latency (2-10 seconds) and lower availability (99.5% is typical) than payment gateways. Under load, partial failures are expected. Without a saga pattern with explicit compensation logic, these orphaned charges accumulate silently. Customers get charged, notice they have no e-ticket email, and raise chargebacks — which cost $15-100 each on top of the refund and trigger card-brand monitoring programs if your chargeback ratio exceeds 1%.

**Consequences:** Mass chargebacks, card brand monitoring, Stripe account termination, regulatory exposure (selling travel without delivering it).

**Prevention — Implement the Booking Saga correctly:**

The saga must be choreography-based (via RabbitMQ events) or orchestration-based (a booking orchestrator service). For a travel booking the recommended sequence is:

```
1. BookingRequested event published
2. Inventory Lock step: GDS PNR created (soft hold) → success or rollback
3. Payment step: Stripe authorize (NOT capture) → success or cancel PNR + fail
4. Ticketing step: GDS issue ticket → success or compensate
5. On ticketing success: Stripe capture payment, send confirmation
6. On ticketing failure: Stripe cancel authorization, cancel PNR, notify customer, trigger retry queue
```

Key rules:
- **Authorize before capture.** Stripe authorization holds the funds without charging. Capture only after you have a ticket number. Reversal of an authorization is free and instant; refunding a capture costs a card scheme fee and takes 5-10 days.
- **Every saga step must have a named compensation transaction** and that compensation must be idempotent (safe to run twice).
- **Publish compensating events to a dead-letter queue** when compensation itself fails. A human must handle these — build a backoffice screen to view and act on failed compensation events.
- **The ticketing step is not retriable indefinitely.** Set a maximum retry count (e.g., 3 attempts over 10 minutes). After max retries, force-compensate: cancel PNR, void authorization, and queue a customer notification.
- **Timeout the entire saga.** If the saga has not reached a terminal state (confirmed or failed) within 15 minutes, force it to the failed state via a saga timeout monitor.

---

### Pitfall 9: RabbitMQ Message Loss During Broker Restart

**What goes wrong:** RabbitMQ queues are in-memory by default. A broker restart (Docker container restart, server patch) drops all unacknowledged messages. If a booking saga event is in-flight during a restart, the saga stalls at the current step with no retry — funds held, PNR open, customer waiting.

**Prevention:**
- Declare all booking-related queues and exchanges as `durable: true` with messages published as `persistent: true` (delivery mode 2).
- Use publisher confirms. Do not consider a message "sent" until the broker ACKs it.
- Use consumer manual acknowledgment. Only ACK a message after the processing step completes and any resulting state change is committed.
- Run RabbitMQ with a quorum queue configuration (3-node cluster) for the booking queues in production. Classic mirrored queues are deprecated in RabbitMQ 3.13+. [VERIFY current RabbitMQ version]
- Implement outbox pattern: before publishing a saga event, write it to an `OutboxMessages` table in the same MSSQL transaction as your state change. A separate outbox processor publishes to RabbitMQ and marks messages sent. This eliminates the dual-write problem entirely.

---

### Pitfall 10: Idempotency Gaps Under Retry

**What goes wrong:** A saga step times out at the infrastructure layer (HTTP 504 from the GDS adapter) but the GDS actually processed it. The orchestrator retries, creating a second PNR for the same booking. Now you have two open PNRs. If both proceed to ticketing, you've double-ticketed — two charges, two tickets issued, only one customer.

**Prevention:**
- Every saga step must accept and store an idempotency key (booking reference + step name). Before executing a step, check if it was already completed for this key.
- GDS adapters must query "does a PNR exist for booking X?" before creating a new one.
- Store the GDS PNR locator in the booking record as soon as it is returned. On retry, skip creation and use the existing locator.
- Stripe's API is idempotency-key native — always pass an idempotency key to every Stripe call.

---

## Search Caching Problems

### Pitfall 11: Caching Availability as if It Were Catalog Data

**What goes wrong:** Teams cache GDS search results in Redis with long TTLs (30 minutes, 1 hour) to save on GDS transaction costs. A user searches, gets cached results, clicks to book a specific itinerary, and the fare or availability is gone. The booking fails at the GDS sell step with "no seats available at requested fare class." The customer sees a confusing error after being shown a price.

**Why it's worse than it looks:** Airline inventory is highly dynamic — a seat at a specific fare class can be sold by another agent in another country in milliseconds. A 30-minute cache is fine for inspirational/browse UX but completely wrong for the booking intent flow. The failure compounds: if the cheapest cache hit is unavailable, the next cheapest might also be unavailable. The customer experiences multiple "try again" failures before a booking succeeds, destroying trust.

**Consequences:** Booking abandonment, customer service contacts, negative reviews ("prices were wrong"), lost revenue.

**Prevention:**
- Tiered caching strategy:
  - **Browse phase** (search results page): cache aggressively, TTL 5-15 minutes. Label results "prices from" to set expectations.
  - **Selection phase** (fare detail / passenger details page): re-validate availability and price, TTL 60-90 seconds.
  - **Pre-payment phase** (payment page): call FlightOffersPricing live, no cache. This is the canonical re-validation step.
- Cache the search response structure (airline options, route combinations) separately from prices. Route combinations are stable; prices are not.
- Display a "prices may have changed, click to refresh" affordance if the user's search result is more than 5 minutes old.
- Use Redis cache key design that encodes the validity window: `search:{hash}:{ttl_bucket}` where the TTL bucket is a Unix epoch rounded to the nearest 5 minutes. This ensures stale cache misses are explicit rather than returning data beyond the intended window.

---

### Pitfall 12: Redis Cache Stampede on Cold Start or Cache Expiry

**What goes wrong:** A popular route's cache entry expires at 09:00. At 09:00:00.001, 50 concurrent users trigger 50 simultaneous GDS availability calls for the same route. GDS sees 50 requests where it expected 1, hits the rate limit, and returns errors for all 50 users. This is the "thundering herd" / cache stampede problem.

**Prevention:**
- Implement probabilistic early expiration (jitter): randomize TTLs within a ±20% window so not all entries expire simultaneously.
- Use a distributed lock (Redis SETNX or Redlock) on cache population: only one process fetches from GDS for a given cache key; all others wait and read the result.
- Implement stale-while-revalidate: serve the stale cache entry immediately, trigger a background refresh. Accept that some users get a 2-minute-old price rather than a GDS error.
- For the most popular routes (Top 20 by search volume), implement pre-warming: a scheduled job refreshes these cache entries before expiry.

---

### Pitfall 13: Hotel and Car Rate Validity Windows

**What goes wrong:** Hotel rates returned by aggregators (Hotelbeds, etc.) have explicit `ratePlanCode` validity windows and are not guaranteed beyond the session. Re-checking availability before booking is a separate API call. Unlike flights where availability is seat-class based, hotel rate codes can expire, be restricted to specific booking dates, or require minimum-stay conditions that vary by rate type.

**Prevention:**
- Store the full rate details (ratePlanCode, validity window, cancellation policy hash) with every search result.
- At booking time, always re-fetch the rate via the hotel aggregator's "pre-book" or "check rate" endpoint — never trust the original search response for final price.
- Cache hotel search results with a shorter TTL (3-5 minutes) than flights (5-15 minutes). Hotel availability can be restricted by channel at any moment.
- Model cancellation policy as a structured object, not a free-text string. You will need to display it, enforce it, and calculate refund amounts from it.

---

## Pricing and Tax Complexity

### Pitfall 14: YQ/YR Fuel Surcharges — Not a Tax, Not a Fare

**What goes wrong:** The total amount a customer pays for a flight includes: base fare + airport/government taxes (designated by IATA tax codes like GB, US, etc.) + carrier-imposed surcharges (YQ = fuel surcharge, YR = carrier surcharge). YQ/YR are NOT taxes — they are airline fees — but airlines embed them in the "tax" breakdown returned by GDS. If you display them as "taxes and fees" without separating them, you may violate consumer protection regulations in certain jurisdictions (EU, UK, Australia) that require base fare to be quoted separately from taxes and mandatory charges.

**Why it's worse than it looks:** YQ surcharges are often larger than the base fare on long-haul routes. A $50 base fare with $300 YQ displayed as "fare + taxes" misleads customers about the nature of the charge. The advertising standards issue aside, YQ is relevant to refund calculations: some fares refund YQ on cancellation even when the base fare is non-refundable. Getting this wrong means either over-refunding or under-refunding.

**Consequences:** Regulatory fines (EU Regulation 1008/2008, UK CAA guidelines), customer disputes, incorrect refund calculations.

**Prevention:**
- Parse the tax breakdown from GDS fare quotes and categorize each tax code. Maintain a tax code reference table (IATA publishes this) mapping codes to: government tax vs. carrier surcharge vs. airport fee.
- Expose three separate amounts in your data model: `baseFare`, `governmentTaxes`, `carrierSurcharges`. Display rules can merge these for UX but the underlying data must be separate.
- Store the full itemized tax breakdown on every booking record — you will need it for refund calculations, accounting, and BSP/ARC settlement.

---

### Pitfall 15: Multi-Currency and FX Rate Management

**What goes wrong:** GDS fares are quoted in the "settlement currency" of the origin country (USD, EUR, GBP, etc.). Your customers may pay in a different currency. If you convert at the time of search and display a price in GBP, then convert again at booking time using a different FX rate, the price changes between search and booking — customers call this a "bait and switch" even if it's just FX movement.

**Why it's worse than it looks:** FX rates can move 1-3% in a 15-minute search session during volatile market periods. On a $2,000 booking, that's $20-60 of unexpected discrepancy. Multiply across thousands of bookings and you have uncontrolled FX exposure on your margin.

**Consequences:** Customer complaints, revenue leakage from FX margin mismatch, accounting complexity (which rate do you record for revenue recognition?).

**Prevention:**
- Lock the FX rate at search time and store it with the search result cache key. All subsequent displays of that result use the locked rate.
- Set a maximum price validity window (e.g., 10 minutes) after which FX is re-fetched and the price is updated with a UI notification.
- Source FX rates from a reliable provider (ECB rates, Open Exchange Rates API, or your bank's API) on a scheduled basis (every 15 minutes). Never compute FX on-demand per request from a live API — too much latency and failure surface.
- In your booking record, store: GDS fare currency, GDS fare amount, display currency, display amount, FX rate used, FX rate timestamp. This is required for accounting reconciliation.
- Add a markup factor to FX conversion (e.g., 2-3%) to buffer against rate fluctuation between sale and settlement. This is industry standard and must be disclosed in your pricing terms.

---

### Pitfall 16: Markup Rules Engine Complexity

**What goes wrong:** Travel agencies apply markups to supplier fares: flat fee per booking, percentage of base fare, percentage of total fare, minimum/maximum markup caps, different markup rules per supplier/route/product type/agent tier/booking date. Teams start with hardcoded markup logic and discover within weeks that the business needs 15 different markup rules applied in a specific precedence order.

**Prevention:**
- Build a rules engine from phase 1. At minimum: a `MarkupRule` table with columns for scope (product type, supplier, route pattern, agent tier), rule type (flat / percentage-of-base / percentage-of-total), amount, min/max cap, priority order.
- Evaluate rules in priority order; first matching rule wins (or sum all matching rules, depending on business requirement — clarify this with the business before building).
- Log which markup rule was applied to every booking. This is essential for finance reconciliation.
- The markup engine must also handle commission (agent earns X% of margin) — design the model to support both markup-in and commission-out in the same calculation chain.

---

### Pitfall 17: BSP/ARC Settlement and ADM Risk

**What goes wrong:** IATA Billing and Settlement Plan (BSP) requires that tickets issued through GDS are settled weekly via BSP. If your GDS ticketing produces fares that don't match the settled fare (e.g., due to a pricing bug, markup applied incorrectly, or ticket reissued at wrong price), IATA or the airline issues an Agency Debit Memo (ADM) — an invoice for the difference, plus penalty. ADMs arrive weeks or months after the booking and are often disputed, consuming significant backoffice time.

**Prevention:**
- Implement a BSP reconciliation report that matches your internal booking records against the BSP statement line by line.
- Flag any discrepancy immediately for investigation.
- Never manipulate fare amounts in the PNR fare calculation field — markups must be applied as agency service fees (ASF) on top of the filed fare, not by modifying the filed fare. Modifying filed fares is the primary cause of ADMs.
- Consult your GDS account manager about how to correctly structure service fees in PNRs for your market — the mechanism differs between EMEA, APAC, and Americas.

---

## B2B Wallet / Credit System Risks

### Pitfall 18: Concurrent Balance Deductions (Race Condition)

**What goes wrong:** An agency has a £500 credit balance. Two agents from the same agency submit two bookings simultaneously — £300 and £400. Each agent's booking service reads the balance (£500), checks it's sufficient, proceeds to book. Both checks pass. Both bookings complete. Balance is decremented twice: £500 - £300 = £200, then £200 - £400 = -£200. The agency is now £200 in debt and two bookings are confirmed.

**Why it's worse than it looks:** This is not a theoretical edge case — concurrent booking submissions by agents in the same agency are common, especially during peak periods or when agents race to secure inventory. Simple database reads before decrement are insufficient without database-level locking.

**Consequences:** Overdraft exposure, disputed bookings, financial loss, and if the agency refuses to pay the overdraft, you absorb it.

**Prevention:**
- Use optimistic concurrency with a `RowVersion` / `ETag` on the wallet balance record. The deduction update includes a `WHERE RowVersion = @expectedVersion` clause. If another transaction committed first, the update affects 0 rows, signaling a conflict — retry or fail.
- Alternatively, use a pessimistic lock: `SELECT balance FROM AgencyWallet WITH (UPDLOCK, ROWLOCK) WHERE agencyId = @id` before decrement. This serializes deductions per agency. Acceptable given that concurrent bookings from the same agency are rare enough that lock contention is minimal.
- Consider an event-sourced wallet: append-only `WalletTransaction` entries rather than updating a balance field. Balance is computed as the sum of all transactions. Idempotency is enforced via unique transaction IDs. This eliminates the race condition entirely but adds read complexity.
- Implement an overdraft limit field (default 0) — some B2B contracts allow a small overdraft buffer. This makes the business rule explicit rather than assuming zero tolerance.

---

### Pitfall 19: Wallet Deduction After Booking Failure

**What goes wrong:** The saga deducts the wallet balance, then the GDS ticketing step fails. Compensation must restore the wallet balance. If the compensation event is lost (RabbitMQ restart, service crash), the wallet balance is permanently lower than it should be. The agent calls to complain their balance is wrong; you have no audit trail to reconstruct what happened.

**Prevention:**
- Every wallet deduction must be recorded as an immutable transaction entry (not just a balance update). Transaction entry has: bookingReference, amount, type (DEBIT/CREDIT/REFUND), status (PENDING/CONFIRMED/REVERSED), timestamp.
- The saga compensation step creates a REVERSED entry, not an update to the original entry.
- Implement a wallet reconciliation job that runs nightly: for every booking in a terminal failed state, verify a matching wallet reversal transaction exists. Alert on discrepancies.
- Expose an admin screen showing all wallet transactions per agency with their associated booking status. This is the audit trail for dispute resolution.

---

### Pitfall 20: Credit Limit Enforcement Across Distributed Services

**What goes wrong:** The wallet service is one microservice. The booking service is another. If the booking service caches the credit balance locally (e.g., fetched at session start) to avoid inter-service calls, the cached balance can become stale. An agent can exceed their limit using cached stale reads.

**Prevention:**
- Credit limit checks must always query the wallet service in real time at the point of booking commitment. No caching of wallet balance for authorization decisions.
- The wallet deduction and the booking confirmation must be in the same saga step with compensation — see Pitfall 18/19.
- Consider an "account on hold" mechanism: the wallet service can be told to place a hold of £X against a pending booking, reducing available balance before the booking completes. On success, the hold converts to a confirmed deduction; on failure, the hold is released. This prevents double-spend without the race condition of reading-then-writing.

---

## GDS Certification Process

### Pitfall 21: Underestimating Certification Lead Time

**What goes wrong:** Teams plan to "sort out GDS credentials" in the final sprint before launch. GDS certification is a formal process with its own timeline, requirements, and dependencies entirely outside your control. It takes longer than expected and can block your entire go-live.

**Typical process for Amadeus:**
1. Apply for an Amadeus developer account (self-service REST APIs: days; enterprise/production: weeks)
2. Submit agency application with IATA/ARC number, business registration documents
3. Receive test credentials for the GDS certification environment
4. Build against the test environment, which has limited inventory (specific test PNRs only — not real availability)
5. Submit for certification review: Amadeus QA team validates that your integration follows their technical guidelines (session management, error handling, segment sell rules)
6. Receive production credentials — this step alone can take 4-8 weeks after certification submission

**For Sabre:** Similar process; requires a Sabre Red App or Sabre Web Services agreement. Certification is managed via your Sabre account manager. Timeline similar to Amadeus.

**For Galileo (Travelport):** Travelport Universal API certification requires a commercial agreement with Travelport and a similar review process. [VERIFY: Travelport's current certification pathway, as they have been restructuring their API products]

**Why it's worse than it looks:** You may not be able to use real inventory in the test environment, meaning your integration testing against realistic search results is limited until production credentials are issued. You cannot go live with B2C customers on test credentials.

**Consequences:** Launch blocked for weeks or months waiting for GDS credentials, development work piling up that cannot be validated end-to-end.

**Prevention:**
- Initiate GDS certification applications at project kickoff, not at the end.
- Start with Amadeus Self-Service REST APIs (amadeus.com/developer) — these have same-day test credentials and a straightforward promotion path to production. Use these for initial development.
- For full GDS access (Amadeus Enterprise, Sabre, Galileo), begin the commercial and certification process in parallel with early development phases — target having test credentials within 6 weeks of project start.
- Design your GDS adapter layer to be provider-agnostic from day 1. Build Amadeus first (best REST API, most accessible), then add Sabre/Galileo behind the same adapter interface. This means a Sabre certification delay doesn't block your Amadeus go-live.

---

### Pitfall 22: Test Environment Limitations Masking Production Bugs

**What goes wrong:** GDS test environments are not representative of production. Test environments typically have a small set of curated test flight segments, simplified fare structures, and no real-time airline schedule data. Integration bugs in edge cases (e.g., multi-segment itineraries, codeshares, married segment restrictions, split ticketing) that exist in production inventory simply do not surface in test.

**Prevention:**
- After receiving production credentials, run an extended shadow-mode period: execute searches and price verifications against production GDS but do NOT issue any real tickets. Log all responses and look for parsing errors, unexpected response structures, and edge cases.
- Build a test harness that replays captured production responses (sanitized) against your integration layer. This allows regression testing against real-world data without live GDS costs.
- Plan for a soft launch with a small internal group making real bookings before opening to B2C customers.

---

## First-Time Builder Traps

### Pitfall 23: Building the Full Stack Before Proving the Booking Loop

**What goes wrong:** First-time travel platform teams design and build CRM, backoffice, B2B portal, marketing pages, loyalty system, and advanced search filters — all before verifying that the core booking loop (search → select → pay → GDS ticket → confirm) works end-to-end in production.

**Why it's worse than it looks:** The booking loop is the hardest part. It involves GDS integration, payment orchestration, distributed transactions, and real-money flows with real regulatory implications. Everything else is standard CRUD software. Teams spend months on the easy stuff, then discover the hard stuff has fundamental architectural problems that require rewrites of the booking, payment, and GDS adapter services — work that was assumed to be "solved."

**Prevention:**
- Phase 1 deliverable is ONLY the booking loop: one product type (flights), one GDS (Amadeus), one payment method (Stripe B2C). Everything else is deferred.
- The booking loop is not done until real money moves through it in production with a real ticket issued.
- Do not build backoffice features that depend on booking data until booking data is trustworthy.

---

### Pitfall 24: Unified Search Layer Underestimation

**What goes wrong:** "We'll just aggregate results from all our sources and merge them." The reality: Amadeus returns flights in the NDC or X+ format, Sabre returns in its own format, hotel aggregators return in yet another format, each with different field names, different currency representations, different availability semantics, different error codes. Building a truly unified search layer that normalizes all of these into a consistent internal canonical model — and handles partial source failures gracefully (one GDS down, others still return results) — is a multi-month engineering effort in itself.

**Consequences:** Teams either build a brittle mapper that breaks on edge cases, or they leak provider-specific data structures into the frontend, creating tight coupling that makes future provider changes expensive.

**Prevention:**
- Define your canonical search result model first, before touching any API. Every provider adapter transforms into this model. The frontend only ever sees the canonical model.
- Use the Adapter pattern rigorously: one adapter class per provider, one canonical model, all business logic operates on canonical model only.
- Handle partial source failures: if Amadeus is down, return results from other sources with a banner "some results may be unavailable." Do not fail the entire search on one provider's outage.
- Budget 6-8 weeks of engineering time specifically for the search normalization layer. It is not a simple task.

---

### Pitfall 25: Ignoring Fare Rules and Ancillaries

**What goes wrong:** Teams build booking to sell the base itinerary and ignore: fare rules (change fees, cancellation penalties, minimum stay requirements, advance purchase requirements), ancillaries (baggage, seat selection, meals), and the interplay between them. This creates:
- Customer selects a non-refundable fare, expects a refund, and you have no system to enforce/display the restriction
- Customer books and later asks to add baggage, but your system has no ancillary flow
- Agent tries to rebook a changed flight but your system has no ticket modification (reissue) flow

**Consequences:** Customer service team handling every change/cancel manually, reissue errors causing ADMs, customers disputing "non-refundable" charges because they weren't shown the restriction clearly.

**Prevention:**
- Display fare rules at point of sale — this is a legal requirement in most jurisdictions (EU Package Travel Directive, US DoT rules).
- Model three fare flexibility tiers in your UI: non-refundable, refundable with fee, fully flexible. Map fare rule codes to these tiers in your adapter.
- Ancillaries are a Phase 2 feature but the data model must support them from Phase 1. Reserve space in the booking model for ancillary line items; don't bolt them on later.
- Build a "manage booking" flow that at minimum supports: view e-ticket, request cancellation (with rules-based refund calculation), and name correction. Everything else can be handled offline initially.

---

### Pitfall 26: Treating Email Delivery as Fire-and-Forget

**What goes wrong:** Booking confirmation emails are sent synchronously in the booking path (or via RabbitMQ without retry). If the email provider is down or the template rendering fails, either: (a) the booking call hangs waiting for email, delaying confirmation for the customer, or (b) the email is silently dropped with no retry.

A customer who books a flight and receives no confirmation email assumes the booking failed. They call customer support, or worse, they book again — creating a double booking.

**Prevention:**
- Email delivery must be fully asynchronous via RabbitMQ, completely decoupled from the booking confirmation response.
- The customer must receive a booking confirmation screen/page immediately, showing the booking reference, before any email is sent.
- Implement email delivery with retry logic: if the provider fails, requeue the email event with exponential backoff. Keep a `NotificationLog` table tracking sent/pending/failed status per booking.
- Use a transactional email provider (SendGrid, Postmark, Resend) with delivery webhooks. Record bounce and delivery events back into the notification log.
- An operations screen should show "booking confirmed / email not sent" cases so support staff can manually resend.

---

### Pitfall 27: GDPR / Data Residency Ignored Until Launch

**What goes wrong:** Travel platforms collect: passport numbers, dates of birth, nationality, payment card tokens, travel history, contact details. This is highly sensitive personal data under GDPR (EU), UK GDPR, and similar regimes. Teams build the data model without considering: data retention policies, right to erasure (customer wants account deleted), data portability, lawful basis for processing, and data residency (if your servers are in the US, can you store EU customer passport numbers there?).

**Consequences:** ICO/DPA enforcement action (fines up to €20M or 4% of global turnover), customer access requests overwhelming support teams, architectural refactor needed to implement erasure after the fact.

**Prevention:**
- Assign a data classification to every field in your data model at design time: PII / sensitive PII (passport, DOB, nationality) / financial / non-personal. This informs retention and erasure rules.
- Passport data must be encrypted at rest (AES-256 at minimum) in the database. MSSQL Always Encrypted is appropriate for the most sensitive fields.
- Implement soft-delete with a retention scheduler: when a customer requests erasure, pseudonymize their PII but retain the booking records (financial retention requirement is typically 7 years). Do not literally delete booking rows — you need them for accounting.
- Confirm data residency with your infrastructure provider before storing passport data. Azure UK South or EU West regions are appropriate for UK/EU customers. [VERIFY with your legal counsel]

---

### Pitfall 28: No Operational Runbook for GDS Outages

**What goes wrong:** GDS systems have planned maintenance windows (typically weekend early-morning local time in the GDS data center's time zone) and unplanned outages. First-time teams have no runbook for what happens when Amadeus is down: Do you show an error? Show cached results? Redirect to a competitor link? Notify agents? Can bookings in-flight be recovered?

**Prevention:**
- Define and document degraded-mode behavior for each GDS before launch: GDS A down → serve results from cache + GDS B only; all GDS down → show "flight search temporarily unavailable" with ETA.
- Subscribe to GDS status pages and integrate alerts into your Slack/Teams channel.
- Build a feature flag (a simple config entry in Redis or a feature flag service) that allows disabling a specific GDS provider without a deployment.
- Practice the runbook in a staging drill before launch. Confirm that disabling Amadeus via feature flag actually routes traffic correctly and doesn't produce uncaught null reference exceptions.

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|---|---|---|
| GDS Adapter (Phase 1) | Stateful session handling breaks under concurrent bookings | Session pool with exclusive checkout; prefer REST APIs |
| Booking Saga (Phase 1) | Payment captured before ticket confirmed | Authorize-then-capture only; saga with compensation |
| Search & Caching (Phase 1) | Long Redis TTLs cause stale price failures at booking | Tiered TTL strategy; mandatory re-validation before payment |
| PCI Compliance (Phase 1) | Card data touches server logs | Stripe Elements only; log scrubbing middleware from day 1 |
| Pricing Engine (Phase 2) | YQ/YR misclassified as taxes | Tax code reference table; separate data model fields |
| B2B Wallet (Phase 2) | Concurrent deductions exceed credit limit | UPDLOCK or optimistic concurrency; account holds |
| Multi-GDS (Phase 2) | Different response formats leak into frontend | Canonical model first; strict adapter pattern |
| BSP Settlement (Phase 3) | Markup applied to filed fare causes ADMs | Service fee mechanism; BSP reconciliation job |
| GDPR / Data (Phase 1+) | Passport data unencrypted at rest | Always Encrypted for sensitive PII from schema design |
| GDS Certification (Ongoing) | Certification delay blocks launch | Apply at project start; build Amadeus REST first |

---

## Sources

**Confidence:** HIGH for structural patterns (PCI-DSS scope rules, saga pattern, GDS session architecture, BSP mechanics, GDPR requirements). MEDIUM for version-specific API details (Amadeus/Sabre specific endpoint names, RabbitMQ version-specific features, current certification timelines). External search tools were unavailable; verify MEDIUM items against current official documentation before implementation.

- PCI-DSS v4.0: https://www.pcisecuritystandards.org/document_library/ [verify SAQ-A eligibility with Stripe Elements]
- Amadeus for Developers: https://developers.amadeus.com/self-service
- Stripe Docs (PCI compliance): https://stripe.com/docs/security/guide
- IATA BSP Manual: https://www.iata.org/en/services/finance/bsp/
- Saga Pattern: https://microservices.io/patterns/data/saga.html
- RabbitMQ Quorum Queues: https://www.rabbitmq.com/quorum-queues.html
- EU Regulation 1008/2008 (price transparency): https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=celex:32008R1008
