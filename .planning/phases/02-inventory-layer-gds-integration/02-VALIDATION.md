---
phase: 2
slug: inventory-layer-gds-integration
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-15
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit / NUnit (.NET) |
| **Config file** | `*.Tests/*.csproj` |
| **Quick run command** | `dotnet test --filter "Category=Unit" --no-build` |
| **Full suite command** | `dotnet test --no-build` |
| **Estimated runtime** | ~60 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "Category=Unit" --no-build`
- **After every plan wave:** Run `dotnet test --no-build`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 02-01-01 | 01 | 1 | INV-01 | — | OAuth2 token not logged | unit | `dotnet test --filter "IInventoryConnector"` | ❌ W0 | ⬜ pending |
| 02-01-02 | 01 | 1 | INV-02 | — | Raw GDS response mapped to canonical model | unit | `dotnet test --filter "UnifiedFlightOffer"` | ❌ W0 | ⬜ pending |
| 02-01-03 | 01 | 2 | INV-04 | — | Fan-out Task.WhenAll returns aggregated results | integration | `dotnet test --filter "FanOut"` | ❌ W0 | ⬜ pending |
| 02-02-01 | 02 | 2 | INV-03 | — | Both adapters return same canonical model | unit | `dotnet test --filter "SecondGDS"` | ❌ W0 | ⬜ pending |
| 02-03-01 | 03 | 2 | INV-05 | — | HMAC-SHA256 signing correct for Hotelbeds | unit | `dotnet test --filter "Hotelbeds"` | ❌ W0 | ⬜ pending |
| 02-03-02 | 03 | 2 | INV-06 | — | Car hire adapter returns canonical model | unit | `dotnet test --filter "CarHire"` | ❌ W0 | ⬜ pending |
| 02-04-01 | 04 | 3 | INV-07 | — | Markup applied before cache write | unit | `dotnet test --filter "PricingService"` | ❌ W0 | ⬜ pending |
| 02-04-02 | 04 | 3 | INV-08 | — | Redis cache hit skips GDS call | integration | `dotnet test --filter "RedisCache"` | ❌ W0 | ⬜ pending |
| 02-04-03 | 04 | 3 | INV-09 | — | Rate-limit guard blocks excess GDS calls | unit | `dotnet test --filter "RateLimit"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `FlightService.Tests/InventoryConnectorTests.cs` — stubs for INV-01, INV-02
- [ ] `FlightService.Tests/FanOutTests.cs` — stubs for INV-04
- [ ] `FlightService.Tests/AmadeusAdapterTests.cs` — stubs for Amadeus OAuth2 + mapping
- [ ] `FlightService.Tests/SecondGDSAdapterTests.cs` — stubs for INV-03
- [ ] `HotelService.Tests/HotelbedsAdapterTests.cs` — stubs for INV-05
- [ ] `HotelService.Tests/CarHireAdapterTests.cs` — stubs for INV-06
- [ ] `PricingService.Tests/PricingEngineTests.cs` — stubs for INV-07
- [ ] `FlightService.Tests/RedisCacheTests.cs` — stubs for INV-08, INV-09

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live Amadeus API returns real flight results | INV-01 | Requires live sandbox credentials | Run search request against Amadeus test environment; verify non-empty results with valid offer IDs |
| GDS rate limits not breached under load | INV-09 | Requires live GDS connection | Run 50 concurrent searches; confirm no 429 responses from GDS |
| Booking token survives Redis TTL window | INV-08 | Requires real-time wait | Store token, wait 89 sec, retrieve — must succeed; wait 91 sec, retrieve — must miss |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
