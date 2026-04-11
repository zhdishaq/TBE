# TBE — Travel Booking Engine

## What This Is

A full-stack travel booking engine for an own travel business, supporting B2C end-customer bookings, a B2B agent portal, backoffice/midoffice management, and CRM — all in one platform. The system aggregates inventory across GDS providers (Amadeus, Sabre, Galileo) and third-party APIs, enabling search and booking of flights, hotels, packages, and car hire/transfers.

## Core Value

A unified booking platform where both direct customers and travel agents can search real inventory, complete bookings end-to-end, and have those bookings managed through a single backoffice — without switching systems.

## Requirements

### Validated

(None yet — ship to validate)

### Active

**B2C Portal**
- [ ] Customer-facing search and booking for flights, hotels, packages, and car hire/transfers
- [ ] Online payment via payment gateway (Stripe or equivalent) at checkout
- [ ] Booking confirmation emails and itinerary management
- [ ] Customer account: booking history, cancellation, modifications

**B2B Agent Portal**
- [ ] Agent login with role-based access (agency admin, sub-agents)
- [ ] Same inventory search with agent-specific pricing and commission visibility
- [ ] Pre-loaded credit wallet — deducted per booking, rechargeable
- [ ] Agent booking management and reporting

**Inventory & Supplier Connectivity**
- [ ] GDS integration: Amadeus, Sabre, and/or Galileo for flights
- [ ] Third-party API integration: hotel and car aggregators (e.g. Hotelbeds, Duffel)
- [ ] Unified search layer normalizing results from all sources
- [ ] Real-time availability and pricing

**Backoffice / Midoffice**
- [ ] Booking management (view, modify, cancel, reissue)
- [ ] Manual booking entry for offline sales
- [ ] Supplier contract management for negotiated rates
- [ ] MIS reporting and analytics dashboard

**CRM**
- [ ] Customer profile and booking history
- [ ] Agent/agency management
- [ ] Communication logs and follow-up tracking

### Out of Scope

- Multi-tenant SaaS — system is built for one travel business, not resold to multiple agencies
- White-label distribution — no sub-branding or custom-domain tenancy per agency
- Mobile native app — web-responsive only for v1 (mobile app is a future phase)
- Loyalty/points program — deferred until core booking loop is validated

## Context

- **Business type**: Own travel business (not a platform sold to others)
- **Inventory sources**: GDS (Amadeus / Sabre / Galileo) + third-party aggregator APIs — requires a unified search normalization layer
- **Payment split**: Stripe/gateway for B2C; credit wallet system for B2B agents
- **Architecture**: Microservices — each domain (search, booking, payment, CRM, backoffice) is a separate service communicating via RabbitMQ
- **Greenfield**: Starting from zero; no existing codebase

## Constraints

- **Tech Stack — Backend**: .NET / C# microservices
- **Tech Stack — Frontend**: Next.js (React)
- **Tech Stack — Database**: Microsoft SQL Server
- **Tech Stack — Infrastructure**: Docker (containerized deployment)
- **Tech Stack — Messaging**: RabbitMQ for async inter-service communication
- **Tech Stack — Caching**: Redis for session, search results, and rate caching
- **GDS Connectivity**: Requires certified GDS API credentials (Amadeus/Sabre/Galileo) — these must be obtained separately
- **Compliance**: PCI-DSS requirements apply due to payment card processing

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Microservices architecture | Allows independent scaling of search, booking, and payment; matches industry pattern for travel platforms | — Pending |
| Multi-source inventory (GDS + APIs) | GDS for flights (widest coverage), aggregators for hotels/cars (simpler APIs, competitive rates) | — Pending |
| RabbitMQ for async messaging | Decouples booking confirmations, email notifications, wallet debits from the critical booking path | — Pending |
| Redis for search caching | GDS search is expensive and rate-limited; caching results reduces costs and improves search UX | — Pending |
| B2C gateway + B2B wallet split | B2C customers expect card checkout; B2B agents need pre-approved credit to book fast without per-transaction friction | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-04-12 after initialization*
