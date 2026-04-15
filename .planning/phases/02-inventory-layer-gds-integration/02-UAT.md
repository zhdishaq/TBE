---
status: testing
phase: 02-inventory-layer-gds-integration
source:
  - 02-01-SUMMARY.md
  - 02-02-SUMMARY.md
  - 02-03-SUMMARY.md
  - 02-04-SUMMARY.md
started: 2026-04-15T00:00:00Z
updated: 2026-04-15T00:00:00Z
---

## Current Test

number: 1
name: Cold Start Smoke Test
expected: |
  Kill any running services. Start all services from scratch (docker compose up or dotnet run on each service).
  All services boot without errors. The AddMarkupRules migration runs successfully on PricingService startup.
  A health check or basic request to FlightConnectorService (e.g. GET /health or any endpoint) returns a response (not a crash).
awaiting: user response

## Tests

### 1. Cold Start Smoke Test
expected: Kill any running services. Start all services from scratch (docker compose up or dotnet run on each service). All services boot without errors. The AddMarkupRules migration runs on PricingService startup. A basic request to any connector endpoint returns a response without crashing.
result: [pending]

### 2. Flight Search — Amadeus adapter responds
expected: POST /flights/search on FlightConnectorService with a valid IATA route (e.g. {"origin":"LHR","destination":"JFK","departureDate":"2026-06-01","passengers":1}) returns HTTP 200 with a JSON array of flight offers. Each offer has price.grandTotal, segments, and a sourceRef field.
result: [pending]

### 3. Flight Search — IATA validation rejects bad input
expected: POST /flights/search with a non-IATA origin like {"origin":"London","destination":"JFK","departureDate":"2026-06-01","passengers":1} returns HTTP 400 (bad request), not a 500 or unhandled exception.
result: [pending]

### 4. SearchService fan-out deduplicates results
expected: POST /search/flights (SearchService) with a valid request returns offers from both Amadeus and Sabre combined. If both GDSes return the same flight (same SourceRef), it appears only once in the response. Results are sorted by price ascending.
result: [pending]

### 5. Hotel search — Hotelbeds adapter responds
expected: POST /hotels/search on HotelConnectorService with {"destinationCode":"LON","checkIn":"2026-06-01","checkOut":"2026-06-05","rooms":[{"adults":2}]} returns HTTP 200 with a JSON array of hotel offers. Each offer has a propertyName, roomType, and price.grandTotal.
result: [pending]

### 6. Hotel search — empty Rooms validation
expected: POST /hotels/search with an empty rooms array ({"destinationCode":"LON","checkIn":"2026-06-01","checkOut":"2026-06-05","rooms":[]}) returns HTTP 400, not a 500 or Hotelbeds API error.
result: [pending]

### 7. Car search — Amadeus Transfer adapter responds
expected: POST /cars/search on HotelConnectorService with {"pickupLocationCode":"LHR","dropoffLocationCode":"LGW","pickupDateTime":"2026-06-01T10:00:00","passengers":2} returns HTTP 200 with a JSON array of car/transfer offers. Each offer has vehicleCategory, supplierName, and price.grandTotal.
result: [pending]

### 8. Pricing markup applies correctly
expected: POST /pricing/apply on PricingService with a list of UnifiedFlightOffers returns priced offers where grossSellingPrice >= netFare (markup applied). If no rule matches, grossSellingPrice equals netFare (passthrough). Response includes appliedRuleId or null.
result: [pending]

### 9. Search result caching — second request is served from cache
expected: Make the same POST /search/flights request twice in quick succession. The second response comes back noticeably faster (cache hit). Both responses return the same offer data.
result: [pending]

### 10. GDS rate limiting kicks in on rapid requests
expected: Send more than 8 POST /search/flights requests within 1 second. After the 8th request, subsequent requests within that second receive HTTP 429 (Too Many Requests), not 500 errors.
result: [pending]

## Summary

total: 10
passed: 0
issues: 0
pending: 10
skipped: 0

## Gaps

[none yet]
