# Phase 4: B2C Portal (Customer-Facing) - Research

**Researched:** 2026-04-16
**Domain:** Next.js 16 App Router B2C portal consuming an existing .NET/YARP/Keycloak/Stripe backend
**Confidence:** HIGH on locked stack (already pinned by CONTEXT + starterKit), MEDIUM on Auth.js v5 (still beta) and PDF location choice, HIGH on Stripe wiring (verified against Phase 3 03-02 summary)

## Summary

Phase 4 is a pure frontend phase plus one backend add-on (hotel voucher consumer + PDF receipt endpoint). The frontend is a forked Metronic 9 starterKit (`ui/starterKit/`) copied into `src/portals/b2c-web/` per D-01. Every stack choice is already locked by `04-CONTEXT.md` and `ui/starterKit/package.json` — the research here is about *how* to wire those pieces correctly, not which pieces to pick.

Four hard integration areas drive the plan shape: (1) Auth.js v5 against Keycloak `tbe-b2c` realm with refresh-token rotation and server-side token forwarding to YARP; (2) Stripe Payment Element wired to Phase 3's existing authorize-before-capture PaymentIntent flow — the frontend must NEVER confirm to success on client-side redirect alone (webhook is truth, per FLTB/PAY D-12); (3) Trip Builder basket model with a single PaymentIntent mapped to two independent bookings via a parent `BasketId` and Stripe partial-capture; (4) PDF receipt served from BookingService via QuestPDF (server-side, reuses Phase 3 setup per D-15) — NOT generated client-side.

**Primary recommendation:** Fork starterKit into `src/portals/b2c-web/`, add only Auth.js v5 + `@stripe/stripe-js` + `@stripe/react-stripe-js` + `nuqs` on top of the pinned dependencies. Server-side fetch for all authenticated calls (RSC + route handlers), client-side TanStack Query only for results pages that re-filter over cached data. Receipt PDF is an `[Authorize] GET /api/bookings/{id}/receipt.pdf` endpoint in BookingService — the dashboard renders a native `<a>` tag with `download`.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Session / auth cookie | Frontend Server (Next.js RSC + middleware) | — | Auth.js v5 stores JWT in httpOnly cookie; token never reaches client JS (D-05, COMP-01) |
| Registration / email verification | API (Keycloak) | Frontend Server (redirect flows) | D-06 — built-in Keycloak verify-email; Next.js only presents UI and redirects |
| Search form + filters | Browser (Client Component) | Frontend Server (initial render shell) | D-12 — initial search runs server-side via gateway; filters/sort run client-side over TanStack Query cache |
| Search API call | API (YARP → Search/Pricing/Inventory) | Frontend Server (token forwarding) | Tokens forwarded server-side only (D-05) |
| Trip Builder basket state | Browser (client state + nuqs URL state) | API (BookingService basket aggregate) | Ephemeral UX state lives client-side; basket persisted on checkout init |
| Stripe Payment Element | Browser + Stripe iframe | API (PaymentService — intent creation + webhook) | SAQ-A boundary (COMP-01) — card data only touches Stripe iframe |
| PaymentIntent creation | API (PaymentService from Phase 3) | — | Amount + idempotency key computed server-side; `client_secret` is the only thing returned to browser |
| Booking confirmation (truth) | API (PaymentService webhook → saga) | — | D-12 — webhook is sole source of truth; client-side success is optimistic UI only |
| Receipt PDF rendering | API (BookingService QuestPDF) | — | D-15 — server-side PDF reusing Phase 3 QuestPDF; browser downloads via `[Authorize] GET /bookings/{id}/receipt.pdf` |
| Hotel voucher email | API (NotificationService consumer) | — | D-16 — new `HotelBookingConfirmed` consumer; reuses existing RazorLight + QuestPDF + SendGrid pipeline |
| IATA airport autocomplete | API (InventoryService typeahead → Redis) | Browser (cmdk combobox UI) | D-18 — Redis-backed typeahead; no external API in hot path |
| Customer dashboard | Frontend Server (RSC) | API (`GET /customers/me/bookings`) | D-17 — RSC reads session, calls gateway server-side, renders |

## User Constraints (from CONTEXT.md)

### Locked Decisions (verbatim from 04-CONTEXT.md)

**Frontend Project Structure**
- **D-01:** Fork `ui/starterKit` into `src/portals/b2c-web/`. Keep starterKit pristine.
- **D-02:** Each portal is a separate Next.js app under `src/portals/` (no monorepo tooling for Phase 4).
- **D-03:** Frontend stack pinned to what `ui/starterKit/package.json` ships (Next.js 16, React 19, Tailwind v4, ReUI/Radix, TanStack Query v5, react-hook-form + zod, sonner, react-day-picker, lucide-react). No additional UI libraries unless strictly necessary.

**Authentication**
- **D-04:** Auth.js v5 with Keycloak OIDC provider against `tbe-b2c` realm. Session strategy: JWT in httpOnly cookie. No database session.
- **D-05:** Server-side data fetching uses `auth()` to read session and forward `access_token` as `Authorization: Bearer …` to YARP. No tokens in client JS. Refresh token rotation in Auth.js callback chain.
- **D-06:** Email verification = Keycloak built-in verify-email flow (no custom verifier). Anonymous browsing allowed; checkout gated on `email_verified=true` in JWT.

**Trip Builder & Checkout**
- **D-07:** Side-by-side flight + hotel panels for same destination/dates; single basket.
- **D-08:** Single combined Stripe PaymentIntent for basket total; two independent bookings linked via parent `BasketId` (PKG-04).
- **D-09:** Partial-failure UX: confirm success, refund/release failure, single combined email.
- **D-10:** Each component still uses Phase 3 saga primitives (PNR/hold → authorize → ticket/confirm → capture). Combined PaymentIntent captured in two stages via Stripe partial-capture.

**Search UX**
- **D-11:** Search/filter state in URL via `nuqs` (or native `useSearchParams`). Deep-linkable, shareable. TanStack Query keys derived from URL state.
- **D-12:** Initial search server-side via gateway (≤200 results). Filters + sort run client-side over TanStack Query cache.
- **D-13:** Stacked cards, no pagination (server cap 200).
- **D-14:** Mobile ≤5 steps: search → results+filter → select+review → passenger details → payment.

**PDF / Email / Dashboard**
- **D-15:** Server-side QuestPDF in BookingService. `[Authorize] GET /api/bookings/{id}/receipt.pdf`. Reuses Phase 3 setup.
- **D-16:** Hotel voucher (NOTF-02) — reuse RazorLight + QuestPDF + SendGrid. Add `HotelVoucher.cshtml` + `HotelVoucherDocument`. New consumer on `HotelBookingConfirmed`. Uses existing `EmailIdempotencyLog`.
- **D-17:** Dashboard = RSC reading `GET /customers/me/bookings` (Phase 3 D-21). Client refresh via TanStack Query. Upcoming-vs-past filtered by departure date.

**Search Form**
- **D-18:** IATA autocomplete — Redis-backed typeahead from IATA CSV loaded at InventoryService startup.
- **D-19:** `react-day-picker` for dates. Passenger selector supports adult/child/infant-on-lap/infant-in-seat with airline-rule validation (FLTB-01).

### Claude's Discretion
- Card visual design / brand palette (Metronic defaults stand).
- `nuqs` vs native `useSearchParams`.
- Skeleton / loading UX.
- Server actions vs API routes for booking initiation (default: API routes calling YARP).
- Tailwind / shadcn-style component composition inside forked starterKit.
- URL query param names.
- i18n scope (English-only at launch).
- Toast wording, empty-state copy (copy already locked in `04-UI-SPEC.md`).

### Deferred Ideas (OUT OF SCOPE)
- pnpm workspaces / shared packages across portals.
- Multi-language i18n.
- Multi-currency display (GBP only at launch).
- Native mobile apps.
- Loyalty / points UI.
- True dynamic packages (PKG2-01).
- Cancellation/modification UI beyond FLTB-10.
- Per-supplier branded voucher templates.
- Dedicated PdfService microservice.
- BFF pattern routing through Next.js API routes.

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| B2C-01 | Registration + email verification before booking | Auth.js v5 Keycloak provider (D-04) + Keycloak built-in verify-email (D-06) + JWT `email_verified` gate at checkout step |
| B2C-02 | Login, password reset, persistent session | Auth.js v5 signIn/signOut + Keycloak forgot-password flow; JWT cookie persistence with refresh rotation (D-05) |
| B2C-03 | Flight/hotel/car search forms (date pickers, pax selectors, autocomplete) | react-day-picker (D-19), Radix Popover + counters, cmdk-based IATA combobox (D-18) |
| B2C-04 | Results sorted by price with client-side filters | TanStack Query cache + nuqs URL state (D-11, D-12) |
| B2C-05 | Mobile booking flow ≤5 steps | Stepper + route-level layout (D-14). Confirmation screen post-flow |
| B2C-06 | Stripe Elements SAQ-A | `@stripe/stripe-js` + `@stripe/react-stripe-js` PaymentElement; client_secret from PaymentService; card data never touches server (COMP-01) |
| B2C-07 | Dashboard (upcoming/past/profile) | RSC reading `GET /customers/me/bookings` (D-17); Keycloak account page link for profile |
| B2C-08 | PDF receipt | `GET /api/bookings/{id}/receipt.pdf` server-rendered via QuestPDF (D-15) |
| HOTB-01 | Hotel search by destination/dates/room config | InventoryService hotel adapter (Phase 2) → gateway; room/occupancy form component |
| HOTB-02 | Property details: photos, amenities, cancellation, room types | Hotel details page RSC; image gallery via Next.js `<Image>`; cancellation policy modeled as structured object (PITFALLS #13) |
| HOTB-03 | Hotel reservation flow (availability → hold → payment → confirmation) | Reuses Phase 3 saga primitives applied to hotel product type (D-10) |
| HOTB-04 | Hotel voucher email ≤60s | D-16 NotificationService `HotelBookingConfirmed` consumer + QuestPDF `HotelVoucherDocument` |
| HOTB-05 | Hotel booking visible in dashboard with supplier ref | Already covered by Phase 3 D-21 read endpoint; list row shows supplier_ref instead of PNR |
| CARB-01 | Car hire search | Duffel/CARB adapter (Phase 2) → gateway; pickup location combobox |
| CARB-02 | Transfer search (route + vehicle type) | Adapter route search; origin/destination + vehicle segment |
| CARB-03 | Booking confirmation with supplier voucher | Same voucher pipeline as hotel (HotelBookingConfirmed-style event) |
| PKG-01 | Trip Builder flight+hotel side-by-side | D-07 layout; shared search criteria |
| PKG-02 | Single basket, one payment | D-08 combined PaymentIntent |
| PKG-03 | Single combined email | D-09 partial-failure aware; combined email from NotificationService (new BasketConfirmed consumer or composed from existing events — see Pattern 4) |
| PKG-04 | Independent refs + cancellation policies | D-08 `BasketId` parent; bookings stay independent; UI shows two line items |
| NOTF-02 | Hotel voucher email ≤60s | D-16 |

## Standard Stack

### Core (already pinned in `ui/starterKit/package.json`)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| next | 16.1.6 | Next.js App Router framework | [VERIFIED: `ui/starterKit/package.json`] — current latest 16.2.3 [VERIFIED: npm view next version 2026-04-16]. Ship starterKit pin; do not bump within this phase |
| react / react-dom | ^19.2.1 | UI runtime | [VERIFIED: starterKit pin] |
| @tanstack/react-query | ^5.85.5 | Server-state cache | [VERIFIED: starterKit]. Current 5.99.0 [VERIFIED: npm 2026-04-16]. D-12 filter-over-cache relies on this |
| radix-ui | ^1.4.3 | Headless primitives | [VERIFIED: starterKit]. Dialog/Popover/Combobox backbone |
| react-hook-form | ^7.68.0 | Forms | [VERIFIED: starterKit] |
| zod | ^4.1.13 | Runtime validation | [VERIFIED: starterKit]. Current 4.3.6 [VERIFIED: npm 2026-04-16] |
| @hookform/resolvers | ^5.2.1 | zod↔RHF bridge | [VERIFIED: starterKit] |
| react-day-picker | ^9.9.0 | Date picker (D-19) | [VERIFIED: starterKit]. Current 9.14.0 [VERIFIED: npm 2026-04-16] |
| sonner | ^2.0.7 | Toasts | [VERIFIED: starterKit]. UI-SPEC restricts to non-blocking success toasts |
| lucide-react | ^0.556.0 | Icons | [VERIFIED: starterKit]. UI-SPEC locks as primary icon set |
| cmdk | ^1.1.1 | Combobox primitive | [VERIFIED: starterKit]. Use for IATA airport autocomplete (D-18) |
| tailwindcss | ^4.1.17 | Styling | [VERIFIED: starterKit] |
| date-fns | ^4.1.0 | Date math | [VERIFIED: starterKit]. Use for TTL countdown, departure-vs-today filter |
| class-variance-authority / tailwind-merge / clsx | pinned | Variant composition | [VERIFIED: starterKit] |

### Added in Phase 4 (not in starterKit)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| next-auth (Auth.js v5) | **5.0.0-beta.31** | OIDC against Keycloak `tbe-b2c` (D-04) | [VERIFIED: `npm view next-auth dist-tags` 2026-04-16 — `beta: 5.0.0-beta.31`, `latest: 4.24.14` (v4)]. **v5 IS STILL BETA** — see Pitfall 1. Pin exact version and track changelog. No stable v5 release yet. |
| @auth/core | ^0.34.3 | Peer of Auth.js v5 | [VERIFIED: npm 2026-04-16]. Transitive — Auth.js installs it |
| @stripe/stripe-js | ^9.2.0 | Stripe.js loader | [VERIFIED: npm 2026-04-16]. Required for PaymentElement. Loads Stripe iframe from Stripe CDN |
| @stripe/react-stripe-js | ^6.2.0 | React bindings for Elements | [VERIFIED: npm 2026-04-16]. Provides `<Elements>`, `<PaymentElement>`, `useStripe`, `useElements` |
| nuqs | ^2.8.9 | Type-safe URL search param state (D-11) | [VERIFIED: npm 2026-04-16]. Native `useSearchParams` is the fallback per D-11; nuqs provides type-safe parsers + schema — strongly recommended. Next.js 16 compatible |

### NOT adding (explicitly rejected)

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Fetching airport list client-side | Algolia/geocoding API | D-18 locks Redis typeahead — no external hit in hot path, no extra vendor |
| Client-side PDF (@react-pdf/renderer, jspdf) | Browser PDF generation | D-15 locks server-side QuestPDF. Single source of truth with email attachment. Avoids 300 KB+ client bundle |
| Zustand / Redux for basket | Global store | Basket is small + nuqs URL + React context is enough. Zustand was suggested in `research/STACK.md` but not adopted by starterKit; adding it for Phase 4 violates D-03 |
| BFF pattern (all calls through Next.js API routes) | Proxy all API calls | Deferred explicitly — server-side fetch with token forwarding is sufficient |
| SWR | TanStack Query | Already locked (starterKit) |
| shadcn CLI / third-party registries | Prebuilt components | UI-SPEC Registry Safety section forbids third-party registries in Phase 4 |

**Installation (additions only):**
```bash
cd src/portals/b2c-web
pnpm add next-auth@5.0.0-beta.31 @auth/core @stripe/stripe-js @stripe/react-stripe-js nuqs
```

**Version verification protocol:** Before planner writes install commands, re-run `npm view <pkg> version` for each of the five additions. Auth.js v5 beta ships frequently; pin the exact beta version used at planning time and document the date in PLAN. Do NOT accept `npm view next-auth version` (returns 4.x latest) — always use `npm view next-auth dist-tags` and read the `beta` tag.

### Backend Additions (Phase 4)

| Library | Version | Purpose | Source |
|---------|---------|---------|--------|
| Stripe.net | 51.0.0 (already) | Partial-capture API for combined basket (D-10) | [VERIFIED: `03-02-SUMMARY.md` pinned 51.0.0]. Partial-capture via `PaymentIntentCaptureOptions.AmountToCapture` is already available in this version |
| QuestPDF | 2026.2.4 (already) | Receipt PDF (D-15) + hotel voucher (D-16) | [VERIFIED: `03-04-SUMMARY.md` pinned] |
| RazorLight | 2.3.1 (already) | Hotel voucher HTML template | [VERIFIED: `03-04-SUMMARY.md`] |
| SendGrid | 9.29.3 (already) | Email delivery | [VERIFIED: `03-04-SUMMARY.md`] |

No new backend NuGet packages required — everything reuses Phase 3 infrastructure.

## Architecture Patterns

### System Architecture Diagram

```
┌────────────────────────────────────────────────────────────────────────────┐
│ Browser (React 19 Client Components only)                                  │
│  • Search form inputs, filter rail, Trip Builder basket UI                 │
│  • Stripe PaymentElement iframe (card data — SAQ-A boundary)               │
│  • TanStack Query cache (200-result list, client-side filter/sort)         │
└──────────┬──────────────────────────────────────────────────────────┬──────┘
           │ (1) HTTP (same-origin)                                   │
           │ httpOnly session cookie                                  │ (5b) PaymentElement POSTs
           ▼                                                          │ card data DIRECTLY to Stripe
┌─────────────────────────────────────────────────────────────┐       │ (no server hop)
│ Next.js 16 Server  (src/portals/b2c-web)                    │       │
│  • middleware.ts — session check, redirect to /login        │       │
│  • auth.ts — Auth.js v5 config (Keycloak OIDC)              │       │
│  • RSC pages read session, forward access_token             │       │
│  • /api/auth/[...nextauth] — OIDC callbacks                 │       │
└──────────┬──────────────────────────────────────────────────┘       │
           │ (2) Authorization: Bearer <access_token>                 │
           ▼                                                          │
┌─────────────────────────────────────────────────────────────┐       │
│ YARP API Gateway — JWT validation via Keycloak JWKS         │       │
└──────────┬────────────┬────────────┬────────────┬───────────┘       │
           │            │            │            │                   │
    (3a)   ▼     (3b)   ▼     (3c)   ▼     (3d)   ▼                   │
┌───────────────┐ ┌─────────┐ ┌───────────┐ ┌──────────┐               │
│ SearchService │ │ Pricing │ │ Inventory │ │ Booking  │               │
│ (Phase 2)     │ │ (Ph.2)  │ │ (typeahd) │ │ Service  │               │
└───────────────┘ └─────────┘ └───────────┘ └──────┬───┘               │
                                                  │                   │
                                           (4) Saga cmd               │
                                                  ▼                   │
                                    ┌─────────────────────────┐       │
                                    │ RabbitMQ / MassTransit  │       │
                                    │ BookingSagaStateMachine │       │
                                    │ (Phase 3)               │       │
                                    └──┬──────────────────┬──┘       │
                                       │ (5a) CreatePI    │          │
                                       ▼                   │           │
                                ┌────────────┐             │           │
                                │ Payment    │◀────────────┼──── (5c) ─┘
                                │ Service    │        Stripe Webhook
                                │ (Phase 3)  │◀─────────────── (6) 
                                └──────┬─────┘              
                                       │ client_secret (back to Next RSC → browser)
                                       ▼
                                  ... (saga progresses → BookingConfirmed)
                                       │
                                       ▼ (7) events
                                ┌─────────────────┐
                                │ Notification    │
                                │ Service (Ph.3)  │ + NEW HotelBookingConfirmed consumer (D-16)
                                │  → SendGrid     │
                                └─────────────────┘

Receipt PDF (D-15):
Browser → <a href="/api/bookings/{id}/receipt.pdf" download>
           → (Next route handler OR direct gateway) → BookingService QuestPDF endpoint
           → [Authorize] + ownership check → stream PDF bytes
```

### Recommended Project Structure

```
src/portals/b2c-web/
├── app/
│   ├── layout.tsx                 # Root layout, <Elements> provider not mounted here
│   ├── page.tsx                   # Landing / hero + search form
│   ├── (public)/
│   │   ├── login/page.tsx         # Auth.js signIn('keycloak')
│   │   ├── register/page.tsx      # Redirect → Keycloak registration page
│   │   └── verify-email/page.tsx  # Landing after Keycloak verify-email
│   ├── flights/
│   │   ├── page.tsx               # Flight search form (Server Component shell)
│   │   ├── results/page.tsx       # Results (RSC shell + <SearchResultsPanel> client)
│   │   └── [offerId]/page.tsx     # Fare-rules detail drawer route
│   ├── hotels/ (same pattern)
│   ├── cars/ (same pattern)
│   ├── trips/
│   │   └── build/page.tsx         # Trip Builder side-by-side
│   ├── checkout/
│   │   ├── layout.tsx             # Stepper; [Authorize]-style gate via middleware + email_verified check
│   │   ├── details/page.tsx       # Passenger/guest details (react-hook-form + zod)
│   │   └── payment/page.tsx       # PaymentElement (Client), <Elements> provider wrapper
│   ├── bookings/
│   │   ├── page.tsx               # Dashboard RSC — calls gateway server-side
│   │   └── [id]/page.tsx          # Booking detail + Download receipt link
│   ├── api/
│   │   ├── auth/[...nextauth]/route.ts   # Auth.js handlers
│   │   └── bookings/[id]/receipt.pdf/route.ts  # Optional pass-through; simpler: link directly to gateway
│   └── middleware.ts              # Session check for /checkout/** and /bookings/**
├── components/
│   ├── ui/                        # Forked from starterKit components/ui/
│   ├── search/
│   │   ├── flight-search-form.tsx
│   │   ├── airport-combobox.tsx   # cmdk + Radix Popover + IATA typeahead
│   │   ├── passenger-selector.tsx # Adult/child/infant-lap/infant-seat (FLTB-01)
│   │   └── date-range-picker.tsx  # react-day-picker
│   ├── results/
│   │   ├── flight-card.tsx
│   │   ├── hotel-card.tsx
│   │   ├── filter-rail.tsx        # Stops / airline / time / price — client-side over cache
│   │   └── sort-bar.tsx
│   ├── trip-builder/
│   │   ├── basket-footer.tsx      # Sticky mobile + desktop
│   │   └── partial-failure-banner.tsx
│   └── checkout/
│       ├── payment-element-wrapper.tsx
│       ├── stepper.tsx            # 5-step indicator
│       └── email-verify-gate.tsx  # Modal fired on entering step 5
├── lib/
│   ├── auth.ts                    # Auth.js v5 config + session type augmentation
│   ├── api-client.ts              # Server-side fetch wrapper with auth() → Bearer header
│   ├── stripe.ts                  # loadStripe(publishableKey) memoized
│   ├── query-client.ts            # TanStack QueryClient factory
│   ├── search-params.ts           # nuqs parsers (origin, dest, depart, return, pax, cabin)
│   └── formatters.ts              # Tabular money, date, duration formatters
├── hooks/
│   ├── use-flight-search.ts       # TanStack Query hook keyed by URL state
│   ├── use-basket.ts              # Trip Builder basket (local state + nuqs)
│   └── use-session.ts             # Client-side session hook (wraps auth from Auth.js v5)
├── types/
│   ├── auth.ts                    # Module augmentation for Auth.js Session/JWT (access_token, email_verified, refresh_token_expires_at)
│   └── api.ts                     # Canonical response DTOs from backend
├── public/                        # Metronic brand assets
├── styles/globals.css             # From starterKit
├── auth.config.ts                 # Edge-compatible subset (for middleware)
├── middleware.ts                  # Uses auth.config edge subset
├── next.config.mjs
├── package.json
└── tsconfig.json

src/services/BookingService/BookingService.API/Controllers/
├── BookingsController.cs          # EXISTING — already has GET /bookings/{id}, /customers/{customerId}/bookings
└── ReceiptsController.cs          # NEW — GET /bookings/{id}/receipt.pdf (D-15, B2C-08)

src/services/BookingService/BookingService.Application/Pdf/
├── IBookingReceiptPdfGenerator.cs     # NEW interface
└── QuestPdfBookingReceiptGenerator.cs # NEW implementation (reuses QuestPDF infra)

src/services/NotificationService/NotificationService.Application/
├── Templates/HotelVoucher.cshtml              # NEW (NOTF-02)
├── Templates/Models/HotelVoucherModel.cs      # NEW
├── Pdf/HotelVoucherDocument.cs                # NEW QuestPDF IDocument
└── Consumers/HotelBookingConfirmedConsumer.cs # NEW — mirrors BookingConfirmedConsumer

src/shared/TBE.Contracts/Events/
└── HotelEvents.cs                  # NEW — HotelBookingConfirmed event (payload: supplier_ref, guest, dates, property, rate, voucher fields)
```

### Pattern 1: Server-side Auth Session + Token Forwarding

**What:** Every authenticated call to the backend is made server-side; the browser never sees the Keycloak `access_token`.

**When to use:** Everywhere except (a) the Stripe PaymentElement (which talks to Stripe directly, NOT our backend) and (b) pure marketing pages.

**Example:**
```ts
// Source: Auth.js v5 docs + CONTEXT D-05
// lib/auth.ts
import NextAuth from "next-auth";
import Keycloak from "next-auth/providers/keycloak";

export const { handlers, auth, signIn, signOut } = NextAuth({
  providers: [
    Keycloak({
      clientId: process.env.KEYCLOAK_B2C_CLIENT_ID!,
      clientSecret: process.env.KEYCLOAK_B2C_CLIENT_SECRET!,
      issuer: process.env.KEYCLOAK_B2C_ISSUER!, // https://.../realms/tbe-b2c
    }),
  ],
  session: { strategy: "jwt" },
  callbacks: {
    async jwt({ token, account, profile }) {
      if (account) {
        token.access_token = account.access_token;
        token.refresh_token = account.refresh_token;
        token.expires_at = account.expires_at;
        token.email_verified = (profile as any)?.email_verified ?? false;
      }
      // Refresh-token rotation when access token within 60s of expiry
      if (token.expires_at && Date.now() / 1000 > (token.expires_at as number) - 60) {
        return await refreshAccessToken(token);
      }
      return token;
    },
    async session({ session, token }) {
      session.access_token = token.access_token as string;
      session.email_verified = token.email_verified as boolean;
      return session;
    },
  },
});

// lib/api-client.ts
export async function gatewayFetch(path: string, init?: RequestInit) {
  const session = await auth();
  if (!session?.access_token) throw new Error("Unauthenticated");
  return fetch(`${process.env.GATEWAY_URL}${path}`, {
    ...init,
    headers: {
      ...init?.headers,
      Authorization: `Bearer ${session.access_token}`,
    },
    cache: "no-store",
  });
}
```

### Pattern 2: Stripe PaymentElement with Authorize-Before-Capture

**What:** Three-step wiring — (1) create PaymentIntent via PaymentService, (2) render PaymentElement with `client_secret`, (3) call `stripe.confirmPayment` — but NEVER treat success-URL redirect as booking confirmation.

**When to use:** Checkout payment step only.

**Example:**
```tsx
// Source: Stripe docs (https://stripe.com/docs/payments/accept-a-payment?platform=web&ui=elements) + CONTEXT D-10, PITFALLS #8, FLTB/PAY D-12
// app/checkout/payment/page.tsx (Server Component)
import { Elements } from "@stripe/react-stripe-js";
import { PaymentPanel } from "@/components/checkout/payment-element-wrapper";
import { gatewayFetch } from "@/lib/api-client";
import { loadStripe } from "@/lib/stripe";

export default async function PaymentPage({ searchParams }) {
  const { basketId } = await searchParams;
  // Create PaymentIntent server-side — PaymentService computes idempotency key from (BasketId, "authorize")
  const res = await gatewayFetch(`/baskets/${basketId}/payment-intent`, { method: "POST" });
  const { client_secret, amount, currency } = await res.json();
  return (
    <Elements stripe={loadStripe()} options={{ clientSecret: client_secret }}>
      <PaymentPanel amount={amount} currency={currency} basketId={basketId} />
    </Elements>
  );
}

// components/checkout/payment-element-wrapper.tsx (Client Component)
"use client";
import { PaymentElement, useStripe, useElements } from "@stripe/react-stripe-js";
export function PaymentPanel({ amount, currency, basketId }) {
  const stripe = useStripe(); const elements = useElements();
  async function onSubmit() {
    const { error } = await stripe!.confirmPayment({
      elements: elements!,
      confirmParams: { return_url: `${location.origin}/checkout/processing?basket=${basketId}` },
    });
    if (error) toast.error(error.message);
  }
  return (<><PaymentElement /><button onClick={onSubmit}>Pay £{amount}</button></>);
}

// /checkout/processing page polls GET /baskets/{basketId}/status OR subscribes to SSE until saga → BookingConfirmed.
// On webhook-confirmed success, saga publishes BookingConfirmed → this page redirects to /bookings/{id}/success.
// THE CLIENT-SIDE return_url IS NEVER A SUCCESS SIGNAL (PITFALLS #8, FLTB/PAY D-12).
```

### Pattern 3: URL State + TanStack Query (D-11, D-12)

**What:** All search/filter state lives in URL via `nuqs`. TanStack Query keys derive from URL state. Initial 200-result fetch is one call; filter/sort run client-side over cache; new search only on criteria change.

**Example:**
```ts
// Source: nuqs docs + CONTEXT D-11/D-12
// lib/search-params.ts
import { parseAsString, parseAsIsoDate, parseAsInteger, parseAsArrayOf, parseAsStringLiteral } from "nuqs/server";
export const searchParsers = {
  from: parseAsString.withDefault(""),
  to: parseAsString.withDefault(""),
  dep: parseAsIsoDate,
  ret: parseAsIsoDate,
  adt: parseAsInteger.withDefault(1),
  chd: parseAsInteger.withDefault(0),
  inf: parseAsInteger.withDefault(0),
  infs: parseAsInteger.withDefault(0),
  cabin: parseAsStringLiteral(["economy","premium","business","first"]).withDefault("economy"),
  stops: parseAsArrayOf(parseAsStringLiteral(["any","0","1","2+"])),
  airlines: parseAsArrayOf(parseAsString),
  sort: parseAsStringLiteral(["price","duration","departure"]).withDefault("price"),
};

// hooks/use-flight-search.ts
"use client";
import { useQueryStates } from "nuqs";
import { useQuery } from "@tanstack/react-query";
export function useFlightSearch() {
  const [q] = useQueryStates(searchParsers);
  // CRITICAL: query key depends ONLY on search criteria — NOT on filters/sort.
  const key = ["flights", q.from, q.to, q.dep?.toISOString(), q.ret?.toISOString(), q.adt, q.chd, q.inf, q.infs, q.cabin];
  return useQuery({
    queryKey: key,
    queryFn: () => fetch(`/api/search/flights?${qs(q)}`).then(r => r.json()),
    staleTime: 90_000, // matches selection-phase TTL (PITFALLS #11)
    enabled: Boolean(q.from && q.to && q.dep),
  });
}
// Filter/sort happen in the consuming component via useMemo — no refetch.
```

### Pattern 4: Trip Builder Basket + Combined PaymentIntent (D-07..D-10)

**What:** Client-side basket holds `{ flightOfferId, hotelOfferId }`. On "Continue to payment", client POSTs `/baskets` with both offer IDs; BookingService creates two child bookings + one Basket aggregate; PaymentService creates ONE PaymentIntent for the sum. Saga runs both booking chains in parallel. Stripe partial-capture on a per-leg basis.

**Backend additions required:**
- `Baskets` table in BookingService with `{BasketId, UserId, FlightBookingId, HotelBookingId, Status, TotalAmount, Currency, CreatedAt}`.
- `BasketId` as foreign key on `BookingSagaState` (nullable for non-basket bookings).
- New events: `BasketInitiated`, `BasketPaymentAuthorized`, `BasketPartiallyConfirmed`, `BasketConfirmed`, `BasketFailed`.
- PaymentIntent created with `amount = flight + hotel`; idempotency key `basket-{id}-authorize`.
- On TicketIssued for flight leg: partial capture `flight_amount` via `stripe.paymentIntents.capture(pi, { amount_to_capture: flight_amount })`. Stripe allows exactly ONE capture per PaymentIntent in the standard flow — partial capture is capture-with-less-than-full-amount, which releases the rest of the authorization. For separate-leg capture, each leg must use its OWN PaymentIntent — verify this before planning (see Pitfall 9).
- **MITIGATION:** If Stripe's single-capture limit applies, the correct pattern is TWO PaymentIntents (one per leg) with `capture_method: manual`, both authorized in the same `confirmPayment` call via `setup_future_usage` — OR fall back to creating one PaymentIntent, capturing once the first leg succeeds, and using a second charge for the second leg. **The planner must pick exactly one approach after verifying Stripe API behavior against current docs.** [ASSUMED behavior — Pitfall 9 details verification steps.]

### Pattern 5: PDF Receipt Endpoint (D-15)

**What:** New controller on BookingService. Authorization via Keycloak JWT; ownership check via `UserId` claim vs booking UserId. QuestPDF renders to `MemoryStream`, returned as `FileContentResult`.

```csharp
// Source: QuestPDF docs + Phase 3 03-04 pattern (QuestPdfETicketGenerator) + CONTEXT D-15
// ReceiptsController.cs
[ApiController, Route("bookings"), Authorize]
public class ReceiptsController(BookingDbContext db, IBookingReceiptPdfGenerator pdfGen) : ControllerBase
{
    [HttpGet("{id:guid}/receipt.pdf")]
    public async Task<IActionResult> GetReceipt(Guid id, CancellationToken ct) {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var booking = await db.BookingSagaStates.AsNoTracking()
            .FirstOrDefaultAsync(s => s.CorrelationId == id, ct);
        if (booking is null) return NotFound();
        if (booking.UserId != userId && !User.IsInRole("backoffice-staff")) return Forbid();
        var bytes = await pdfGen.GenerateAsync(booking, ct);
        return File(bytes, "application/pdf", $"receipt-{booking.BookingReference}.pdf");
    }
}
```

Dashboard simply renders `<a href="/api/bookings/{id}/receipt.pdf" download>Download receipt</a>` — the browser sends the session cookie to Next.js → Next.js rewrites to gateway via route handler OR the gateway is exposed on the same origin. **Decision point for planner:** serve PDF through a Next.js route-handler pass-through (so cookie-auth → Bearer conversion happens server-side) vs directly from gateway with CORS + Authorization header via fetch+blob. **Recommended:** Next.js route-handler pass-through — same pattern as other authenticated calls.

### Anti-Patterns to Avoid

- **Calling backend from the browser with Bearer tokens.** Exposes `access_token` to XSS. Always proxy through Next.js (D-05, COMP-01).
- **Trusting Stripe client-side `return_url` redirect as booking success.** Phase 3 D-12 is explicit: webhook is the only truth. Build a `/checkout/processing` page that polls saga status.
- **Putting availability results in Zustand or React Context.** They belong in TanStack Query cache with short TTL — PITFALLS #11. 
- **Caching search results for >90 seconds on the selection path.** Stale prices at book-time = booking failure. Use 90s stale time maximum past the search-results page.
- **Generating receipt PDF client-side.** D-15 locks server-side QuestPDF. Client-side PDF libraries (`jspdf`, `@react-pdf/renderer`) add 300 KB+ to bundle and duplicate logic.
- **Using Keycloak `/account` iframe inside Next.js.** Keycloak blocks framing by default; link to `/account` in a new tab/redirect for profile edits.
- **Blocking the whole basket on one leg's failure.** D-09 mandates partial-success UX with one email. Never auto-abort the healthy leg.
- **Setting `cache: "force-cache"` on authenticated gateway fetches.** Would cache one user's data and serve to another. Always `cache: "no-store"` in Next.js 16 for authenticated RSC fetches.
- **Running middleware on `/api/auth/[...nextauth]`.** Must be excluded from middleware matcher or you'll 500 the OIDC callback.
- **Using `useSession` from Auth.js in a Server Component.** Use `auth()` instead. `useSession` is client-only.
- **Placing `<Elements>` high in the tree.** It fetches Stripe.js unconditionally. Mount only on `/checkout/payment` to avoid shipping Stripe iframe on every page.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| OIDC login / token refresh | Custom OAuth flow | Auth.js v5 + Keycloak provider | PKCE, state, nonce, refresh rotation, edge-compatible — all handled |
| Credit-card form | Custom card inputs | Stripe PaymentElement | SAQ-A boundary. Anything else → SAQ-D (PITFALLS #5) |
| IATA airport autocomplete backend | External API per keystroke | Redis typeahead loaded from IATA CSV (D-18) | Cost control, latency, privacy |
| Date picker | Custom | react-day-picker (already pinned) | A11y, keyboard nav, i18n |
| Combobox | Custom autocomplete | cmdk + Radix Popover (starterKit) | A11y, keyboard nav |
| Form validation | Custom | react-hook-form + zod | Already pinned; schema-driven errors |
| Toast system | Custom | sonner (already pinned) | Accessible, animated |
| PDF generation (receipt + voucher) | Custom HTML-to-PDF | QuestPDF (D-15, D-16) | Already in Phase 3; precise layout; deterministic |
| Email template rendering | String interpolation | RazorLight (D-16) | Already in Phase 3; strong typing |
| URL query state serialization | Manual `URLSearchParams` | nuqs | Type-safe, SSR-compatible, parsers for arrays/dates |
| Session cookie handling | Custom | Auth.js built-in | httpOnly, secure, SameSite, encryption |
| CSRF on state-changing calls | Custom | Auth.js v5 built-in + Next.js server actions | Built-in CSRF defense; never POST JSON from client to backend without Bearer + CORS lock |
| Idempotency on Stripe calls | Custom | Backend computes `basket-{id}-authorize` deterministically (already — Phase 3 D-13) | Replay safety |

**Key insight:** Phase 4 is a *gluing* phase. Almost every capability already exists server-side from Phase 3. The biggest risks are mis-wiring (tokens in the wrong place, PaymentElement in the wrong hierarchy) not building anything from scratch.

## Common Pitfalls

### Pitfall 1: Auth.js v5 is still Beta

**What goes wrong:** Planner pins "latest stable" via `npm view next-auth version` which returns v4 (4.24.14). CONTEXT locks v5. Or: v5 ships a breaking beta (e.g., beta.32) mid-phase that changes callback signatures.

**Why it happens:** v5's `dist-tags.latest` is still v4. v5 is under `dist-tags.beta`. Auth.js v5 beta is widely used in production by large projects but is explicitly not yet GA as of 2026-04-16.

**How to avoid:**
- Pin the EXACT beta version: `"next-auth": "5.0.0-beta.31"` (no `^`, no `~`).
- Record pinned version + date in `04-01-PLAN.md`.
- Lockfile checked in.
- Smoke-test: one e2e test that performs signIn → session → signOut.

**Warning signs:** "Cannot find module 'next-auth/providers/keycloak'" (v4 import path differs), session is `undefined` after signIn (v5 uses `auth()` not `getServerSession()`).

### Pitfall 2: Refresh-Token Rotation Silently Drops the User

**What goes wrong:** Keycloak rotates refresh tokens (one-time use). If the jwt callback uses the SAME refresh token twice (e.g., parallel RSC calls both detect near-expiry and both try to refresh), one succeeds and the other gets `invalid_grant`. User is silently signed out.

**How to avoid:**
- Refresh in `jwt` callback ONLY (runs per session access, not per request).
- Store `refresh_token_expires_at` on the token; fail-fast sign-out once refresh expires rather than attempting refresh.
- Avoid calling refresh concurrently — if two tabs trigger refresh in the same ~500ms, you will lose one. Add a distributed lock OR accept one-sign-out-per-race. For single-user-single-device the race is rare.
- Keycloak `tbe-b2c` realm SSO session idle timeout must exceed Next.js session max age (set Auth.js `session.maxAge` < Keycloak refresh token lifespan).

**Warning signs:** Random 401 on gateway calls after ~5 minutes of idle; "invalid_grant" in Keycloak logs.

### Pitfall 3: Middleware Checks Session in the Edge Runtime but `auth.ts` Uses Node APIs

**What goes wrong:** Next.js middleware runs on the Edge runtime. Auth.js v5 Keycloak provider imports `oauth4webapi` which is edge-compatible, BUT your `auth.ts` refresh function that calls `fetch` to Keycloak is edge-compatible only if it avoids Node `crypto`. If you import anything Node-only (e.g., `jsonwebtoken`), middleware throws at runtime.

**How to avoid:**
- Split config: `auth.config.ts` (edge-safe subset — providers + basic callbacks, no DB) imported by both `middleware.ts` and `auth.ts`; `auth.ts` extends it with Node-only callbacks.
- This is the documented Auth.js v5 pattern (`auth.config.ts` / `auth.ts` split).
- Middleware does `const session = await auth()` from the edge-subset config; pages use the full config.

**Warning signs:** "Can't resolve 'crypto'" at build time; 500 errors only on middleware-protected routes.

### Pitfall 4: Token Forwarded to Gateway Has Wrong Audience

**What goes wrong:** Keycloak issues access tokens scoped to `aud: "account"` by default. YARP JWT validation checks `aud` against configured audiences. If Keycloak client audience mapper isn't configured, YARP rejects all B2C tokens with 401.

**How to avoid:**
- Configure Keycloak `tbe-b2c` client with an audience mapper adding `tbe-api` (or whatever audience YARP expects) to the access token.
- YARP `TokenValidationParameters.ValidAudiences` must include that value.
- Smoke-test in integration: `curl -H "Authorization: Bearer $TOKEN" $GATEWAY/bookings` — expect 200 (with empty body), not 401.

**Warning signs:** Everything works at `/login` but every backend call 401s. JWT decoder shows `aud: "account"` only.

### Pitfall 5: Stripe PaymentElement Mounted Too Early

**What goes wrong:** Developer mounts `<Elements stripe={...}>` in root layout. PaymentElement then tries to render before `client_secret` exists → console errors. Or: the `Elements` provider is remounted on every navigation because `loadStripe` is called inline, re-downloading Stripe.js each time.

**How to avoid:**
- Mount `<Elements>` ONLY on `/checkout/payment`.
- Memoize `loadStripe()` outside component: `const stripePromise = loadStripe(process.env.NEXT_PUBLIC_STRIPE_PK!)`.
- Pass `clientSecret` from server to client via props — don't fetch it in `useEffect`.

### Pitfall 6: Returning to Success URL as the Confirmation (FLTB D-12, PITFALLS #8)

**What goes wrong:** `stripe.confirmPayment({ confirmParams: { return_url } })` redirects on 3DS success. Developer treats arrival at `return_url` as "booking complete" and shows the success screen with PNR. But the saga hasn't processed the webhook yet → PNR doesn't exist → user sees "undefined" PNR, refreshes, eventually sees real PNR OR the ticketing fails and they see a broken success.

**How to avoid:**
- `return_url` = `/checkout/processing?basket={id}`. This page shows "Confirming your booking…" and polls `GET /baskets/{id}/status` every 2 seconds until terminal state (`Confirmed`, `PartiallyConfirmed`, `Failed`).
- Only after `BookingConfirmed` event is seen server-side does the UI show PNR/supplier_ref.
- Hard cap: if saga hasn't terminated within 90 seconds, show "taking longer than expected — we'll email you" (most cases still succeed async).
- Phase 3 `03-01-SUMMARY.md` saga + `03-02-SUMMARY.md` webhook pipeline already emit terminal events — confirm the basket-level aggregate exposes a status endpoint.

### Pitfall 7: Email-Verification Gate Too Late / Too Early

**What goes wrong:** 
- Too late: gate fires inside PaymentElement submit handler — user has entered card details and is frustrated.
- Too early: gate fires at login, blocking all browse activity.

CONTEXT D-06 is explicit: gate fires at *checkout step 5* (payment). Not before.

**How to avoid:**
- `/checkout/layout.tsx` server-side reads `session.email_verified`. If false, renders modal + "Resend verification" button, prevents mounting `/checkout/payment`.
- Modal action: POST `/api/auth/resend-verification` → Next.js route handler calls Keycloak Admin API to resend verify-email.
- Post-verification: user clicks email link → Keycloak marks `email_verified=true` → on return to portal, token refresh picks up new claim. If not, user clicks "I've verified, refresh" button that forces a token refresh via re-sign-in or `update()` from `useSession`.

### Pitfall 8: Keycloak Resend-Verification-Email Endpoint Requires Service Account

**What goes wrong:** `/api/auth/resend-verification` tries to call Keycloak Admin API with the user's B2C access token. Keycloak Admin API requires realm-management roles → 403.

**How to avoid:**
- Provision a separate Keycloak client (`tbe-b2c-admin`) with service-account enabled and role `manage-users` in `tbe-b2c` realm.
- Next.js calls this admin client with `client_credentials` grant, then calls `PUT /admin/realms/tbe-b2c/users/{id}/send-verify-email`.
- Admin client credentials in env vars (COMP-05).

### Pitfall 9: Stripe Combined PaymentIntent — Can You Partial-Capture Twice?

**What goes wrong:** CONTEXT D-10 says "Stripe `partial capture` API is used for the partial-success path." Stripe `paymentIntents.capture` with `amount_to_capture < amount` is supported, BUT the default flow allows ONLY ONE capture per PaymentIntent. After partial capture, the remainder is released. You cannot later capture the "released" portion for the second leg. [ASSUMED — requires verification; this was Stripe's behavior as of the researcher's training]

**Verification steps (planner MUST do):**
1. Re-read https://stripe.com/docs/payments/payment-intents/creating-payment-intents (check "Capture funds manually" section).
2. Re-read https://stripe.com/docs/api/payment_intents/capture (check `amount_to_capture` + "remaining amount" behavior).
3. Search Stripe changelog for "multi-capture" or "incremental authorization" (feature may have shipped).

**Two viable implementations the planner picks between:**

**Option A — Two PaymentIntents, one confirm:** Create two PIs (`pi_flight` capture_method=manual, `pi_hotel` capture_method=manual) each for its leg amount. Use Stripe "Finalize multiple payments at once" / `confirmPayment` with both elements — OR split into two PaymentElements. Leg saga captures its own PI independently; partial-failure cancels the failing PI authorization. Customer sees ONE charge only because both complete in the same confirmPayment in practice (or two separate lines on their statement — acceptable per PKG-04's "independent components" framing).

**Option B — One PaymentIntent, partial capture only the successful leg, refund the rest:** Authorize combined amount. If flight succeeds hotel fails → partial-capture flight amount (releases hotel's authorization). If both succeed → full capture. Simpler but requires ONE successful leg to always be the ticketed one — cannot do two sequential captures.

**Recommendation:** Option A aligns better with D-08 ("two independent bookings linked via parent BasketId") and Phase 3 D-13 idempotency-key-per-operation pattern. Option B only works if both legs are committed atomically at the same saga step.

**Warning signs:** Stripe returns `payment_intent_unexpected_state` on second capture attempt.

### Pitfall 10: IATA Airport Autocomplete Under Keystroke Load

**What goes wrong:** User types "lon" fast → 3 requests fire. Debounce missing → typeahead flickers or returns wrong ordering.

**How to avoid:**
- Client: debounce 200ms + abort previous request via `AbortController`.
- Server-side Redis lookup uses sorted set or prefix match on IATA codes + city names. Seed at InventoryService startup from IATA OpenFlights CSV [VERIFIED: OpenFlights airports.dat is the standard free source — MEDIUM confidence on licensing terms; IATA also publishes an official airport list behind a subscription — planner confirms which source is used].
- Minimum 2 characters before query (matches UI-SPEC).
- Dead-letter unmatched queries to a log for later dataset improvement.

### Pitfall 11: Next.js 16 Async `searchParams` / `params`

**What goes wrong:** Next.js 15+ made `searchParams` a Promise. Code like `const { id } = params` breaks with "params should be awaited". 

**How to avoid:**
- `const { id } = await params`.
- Use nuqs server-side parsers (`parseAsString.parse(searchParams.get(...))`) or `nuqs/server` for RSC.
- TypeScript types: `params: Promise<{ id: string }>`.

**Warning signs:** "A param property was accessed directly with `params.id`" runtime warning.

### Pitfall 12: Search Request Limit + No Pagination (D-13)

**What goes wrong:** 200-result cap is on the server. Client-side filter "show only direct flights" may yield <10 results out of 200 → user sees sparse list and thinks search broken.

**How to avoid:**
- Filter rail shows counts next to each checkbox (e.g., "Direct (12)", "1 stop (188)").
- Empty-state when filters reduce to 0 results: "No flights match your filters. Clear filters?" (UI-SPEC copy).
- Sort by price server-side so the top 200 are the cheapest — otherwise client filters only ever see the first 200 regardless of filter.

### Pitfall 13: GBP Currency Assumption

**What goes wrong:** Hardcoded `£` in dozens of places. Later you want EUR.

**How to avoid:**
- Single `formatMoney(amount, currency)` helper using `Intl.NumberFormat`.
- Currency comes from backend response, not hardcoded.
- CONTEXT defers multi-currency but infrastructure should not block it (PITFALLS #15).

### Pitfall 14: PDF Download Served via Next.js Route Handler — Stream Size

**What goes wrong:** Route handler does `await gatewayFetch(...).json()` instead of streaming → 10 MB PDF blocked in memory.

**How to avoid:**
- Route handler returns `new Response(upstream.body, { headers: { "content-type": "application/pdf", "content-disposition": "attachment; filename=..." } })`.
- Stream-through pattern preserves backpressure.
- Never buffer PDF to string/buffer.

### Pitfall 15: Dashboard RSC Stale After New Booking

**What goes wrong:** User books → redirects to `/bookings` → RSC was cached at build time or at last visit → new booking not shown.

**How to avoid:**
- `export const dynamic = "force-dynamic"` on `/bookings/page.tsx` OR `fetch(..., { cache: "no-store" })` (required anyway for authenticated).
- After booking confirm, call `revalidatePath("/bookings")` from the confirmation route.

### Pitfall 16: CSP Breaks Stripe

**What goes wrong:** Metronic/starterKit may ship a CSP that doesn't include Stripe's domains → PaymentElement fails to load iframe.

**How to avoid:**
- CSP must include: `frame-src https://js.stripe.com https://hooks.stripe.com`, `script-src https://js.stripe.com 'self'`, `connect-src https://api.stripe.com`.
- Document CSP in `04-01-PLAN.md` explicitly.
- Stripe.js itself requires `script-src` whitelist; without it nothing loads.

### Pitfall 17: Metronic Starter Kit Is JavaScript, Not TypeScript

**What goes wrong:** `ui/starterKit/` is `.jsx` / `.js` (verified: `components/ui/button.jsx`, `next.config.mjs`). Forking into `src/portals/b2c-web/` as `.tsx` requires either rewriting components or living with a mixed project.

**How to avoid:**
- Decide upfront: (a) keep JS and use `allowJs: true` in tsconfig, adding TS only for new code; or (b) rewrite component stubs to TS during fork.
- Recommendation: (a) allowJs. Keeps fork close to upstream; new Phase 4 code is TypeScript; type safety where it matters most (API client, auth, forms, Stripe wiring).
- Add `@types/react`, `@types/node`.

**Warning signs:** TypeScript errors in starterKit UI components blocking build; temptation to migrate all components to TS scope-creeping the phase.

### Pitfall 18: B2C Portal CORS from PaymentElement Redirect Origin

**What goes wrong:** 3DS challenge redirects through a bank's domain → browser storage partitioning may drop the Auth.js cookie when the user returns to `/checkout/processing`. Cookie with `SameSite=Strict` will not survive cross-site redirect from 3DS.

**How to avoid:**
- Auth.js default cookie is `SameSite=Lax`, which is correct (survives top-level cross-site redirects for GET).
- The `/checkout/processing` route must be reachable via GET with the session cookie.
- Don't set `SameSite=Strict` on the session cookie.

## Runtime State Inventory

> Phase 4 is NOT a rename/refactor phase — no existing system state needs migration. Included for completeness.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — greenfield additions only (new `Baskets` table, new `HotelBookingConfirmed` consumer) | N/A |
| Live service config | Keycloak `tbe-b2c` realm already provisioned (Phase 1). Must add: audience mapper on `tbe-b2c` client, a new `tbe-b2c-admin` service client for resend-verification (Pitfall 8) | Update Keycloak realm export + rerun provisioning script |
| OS-registered state | None | N/A |
| Secrets/env vars | New env vars required for Next.js portal: `KEYCLOAK_B2C_CLIENT_ID`, `KEYCLOAK_B2C_CLIENT_SECRET`, `KEYCLOAK_B2C_ISSUER`, `KEYCLOAK_B2C_ADMIN_CLIENT_ID`, `KEYCLOAK_B2C_ADMIN_CLIENT_SECRET`, `NEXTAUTH_SECRET`, `NEXTAUTH_URL`, `GATEWAY_URL`, `NEXT_PUBLIC_STRIPE_PK`. None overlap with Phase 3. | Add to `.env.example`; document in PLAN |
| Build artifacts | None — `src/portals/b2c-web/` is a fresh fork | N/A |

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| pnpm | Portal install | [VERIFY at plan time] | — | npm / yarn — but starterKit may assume pnpm. Check `package-lock.json` (found) → npm is also viable |
| Node.js ≥ 20 | Next.js 16 | [VERIFY at plan time — starterKit `next@16.1.6` requires Node 18.18+] | — | Bump local Node |
| Keycloak `tbe-b2c` realm | Auth.js | ✓ (Phase 1) | 25.x | — |
| YARP gateway | All backend calls | ✓ (Phase 1) | — | — |
| BookingService read endpoints | Dashboard | ✓ (Phase 3 D-21) | — | — |
| NotificationService pipeline | Hotel voucher email | ✓ (Phase 3 D-17, D-19) | — | — |
| Stripe test account | PaymentElement dev | [VERIFY — reuse Phase 3 Stripe config] | — | Use Stripe CLI for local webhook forwarding |
| QuestPDF | Receipt PDF | ✓ (Phase 3) | 2026.2.4 | — |
| MSSQL booking schema | Baskets table | ✓ (Phase 1) | — | EF migration adds Baskets |
| Redis | IATA typeahead, session caching | ✓ (Phase 1) | 7.2 | — |
| IATA airport dataset | Autocomplete | ✗ (not yet loaded) | — | Use OpenFlights CSV or IATA official — Phase 4 ingests at startup |
| Stripe CLI (dev) | Webhook forwarding to localhost | [VERIFY — was Phase 3] | — | `stripe listen --forward-to localhost:...` |

**Missing dependencies with no fallback:** IATA airport dataset must be sourced during Phase 4 Plan 2 (flight search UI) — picks the dataset before implementation. All other deps are inherited from earlier phases.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework (frontend) | Vitest + @testing-library/react + Playwright (for e2e) — **not in starterKit, planner adds in Wave 0** |
| Framework (backend — new code) | xUnit (matches `TBE.Tests.Unit` convention from Phase 3 `03-04-SUMMARY.md`) |
| Config file (frontend) | `vitest.config.ts` — **Wave 0** |
| Config file (backend) | existing `TBE.Tests.Unit.csproj` |
| Quick run (frontend unit) | `pnpm --filter b2c-web test` (to add) |
| Quick run (backend) | `dotnet test src/services/BookingService/BookingService.Tests -v minimal` |
| Full suite (frontend) | `pnpm --filter b2c-web test && pnpm --filter b2c-web test:e2e` |
| Full suite (backend) | `dotnet test -warnaserror` |
| Phase gate | All green + UAT criteria from ROADMAP §Phase 4 executed manually |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| B2C-01 | Registration → verify-email → login → search | e2e | `playwright test e2e/register-verify-login.spec.ts` | ❌ Wave 0 |
| B2C-02 | Password reset round-trip | e2e | `playwright test e2e/password-reset.spec.ts` | ❌ Wave 0 |
| B2C-02 | Session persists across refresh (JWT cookie) | unit (Auth.js callback) | `vitest auth.test.ts` | ❌ Wave 0 |
| B2C-03 | IATA combobox returns matches for "LON" | unit | `vitest airport-combobox.test.tsx` | ❌ Wave 0 |
| B2C-03 | Passenger selector validates infants ≤ adults | unit | `vitest passenger-selector.test.tsx` | ❌ Wave 0 |
| B2C-04 | Sort/filter over TanStack Query cache doesn't refetch | unit | `vitest use-flight-search.test.ts` | ❌ Wave 0 |
| B2C-05 | Booking flow completes in 5 steps (mobile viewport) | e2e | `playwright test e2e/mobile-5-steps.spec.ts --project=mobile` | ❌ Wave 0 |
| B2C-06 | PaymentElement loads with client_secret; no card field on our form | unit + manual | `vitest payment-element.test.tsx` + Stripe test PAN | ❌ Wave 0 |
| B2C-06 | Card data never reaches our server (COMP-01) | manual + server log assertion | grep server logs for `card_number\|cvv\|pan` returns 0 | manual |
| B2C-07 | Dashboard shows bookings from `/customers/me/bookings` | integration (backend existing) | `dotnet test --filter "FullyQualifiedName~CustomerBookings"` | partial (Phase 3 has controller) |
| B2C-08 | Receipt endpoint authorizes + rejects wrong user | integration | `dotnet test BookingService.Tests --filter "Receipt"` | ❌ Wave 0 |
| B2C-08 | QuestPDF generates non-empty bytes | unit | `dotnet test --filter "QuestPdfBookingReceiptGenerator"` | ❌ Wave 0 |
| HOTB-01..03 | Hotel search → hold → pay → confirm | e2e | `playwright test e2e/hotel-booking.spec.ts` | ❌ Wave 0 |
| HOTB-04 / NOTF-02 | Hotel voucher email ≤60s | integration | `dotnet test NotificationService.Tests --filter "HotelVoucher"` | ❌ Wave 0 |
| HOTB-04 | `HotelVoucherDocument` renders valid PDF | unit | `dotnet test --filter "HotelVoucherDocument"` | ❌ Wave 0 |
| HOTB-05 | Hotel booking appears in dashboard with supplier_ref | integration | reuse `/customers/me/bookings` test with hotel product | partial |
| CARB-01..03 | Car hire search + confirmation | e2e | `playwright test e2e/car-booking.spec.ts` | ❌ Wave 0 |
| PKG-01 | Trip Builder side-by-side renders both panels | unit | `vitest trip-builder.test.tsx` | ❌ Wave 0 |
| PKG-02 | One PaymentIntent for combined amount; two bookings created | integration | `dotnet test --filter "BasketPaymentIntent"` | ❌ Wave 0 |
| PKG-02 | Partial-failure: flight confirmed, hotel refunded | integration (saga) | `dotnet test --filter "BasketPartialFailure"` | ❌ Wave 0 |
| PKG-03 | Single combined email on both-success | integration | `dotnet test --filter "BasketConfirmedEmail"` | ❌ Wave 0 |
| PKG-04 | Each booking has independent PNR/supplier_ref in DB | integration | `dotnet test --filter "BasketIndependentRefs"` | ❌ Wave 0 |
| — | 60s email SLA (NOTF-02 + COMP) | load test (manual) | measured in staging | manual |
| — | ≤5s search p95 on warm cache | manual (inherited Phase 2 UAT) | manual | manual |

### Sampling Rate

- **Per task commit:** Affected area only — `vitest run <changed files>` + `dotnet test --filter <ns>`.
- **Per wave merge:** Full unit suite + Playwright smoke (not full e2e).
- **Phase gate:** Full `vitest` + Playwright e2e + `dotnet test -warnaserror` green before `/gsd-verify-work`.

### Wave 0 Gaps

- [ ] `src/portals/b2c-web/vitest.config.ts` — test framework config
- [ ] `src/portals/b2c-web/playwright.config.ts` — e2e config with `mobile` project
- [ ] `src/portals/b2c-web/tests/setup.ts` — Testing Library + jsdom setup
- [ ] `src/portals/b2c-web/e2e/fixtures/auth.ts` — Playwright fixture that logs into Keycloak test user
- [ ] `src/portals/b2c-web/e2e/fixtures/stripe.ts` — helper for Stripe test PAN / 3DS flow
- [ ] `src/services/BookingService/BookingService.Tests/` add `ReceiptsControllerTests.cs`, `BasketPaymentIntegrationTests.cs`, `QuestPdfBookingReceiptGeneratorTests.cs`
- [ ] `src/services/NotificationService/NotificationService.Tests/` add `HotelBookingConfirmedConsumerTests.cs`, `HotelVoucherDocumentTests.cs`, `HotelVoucherRazorLightTests.cs`
- [ ] Framework install: `pnpm add -D vitest @testing-library/react @testing-library/jest-dom @testing-library/user-event jsdom @playwright/test` in `src/portals/b2c-web`

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | YES | Auth.js v5 + Keycloak OIDC; email verification via Keycloak built-in; session as httpOnly cookie; password policy enforced by Keycloak realm config |
| V3 Session Management | YES | JWT in httpOnly + SameSite=Lax + Secure cookies; refresh-token rotation; explicit logout revokes Keycloak session |
| V4 Access Control | YES | Ownership check in BookingService (already present in `BookingsController` — `userId != customerId && !backoffice-staff`). Extend for `ReceiptsController`. Gateway enforces JWT presence (COMP-04) |
| V5 Input Validation | YES | zod schemas on every form + server-side validation already in `BookingsController` (currency length, positive amount, enum whitelists) |
| V6 Cryptography | YES (inherited) | AES-256 passport encryption (Phase 3 D-08 — not new in Phase 4). Stripe handles card cryptography (SAQ-A boundary) |
| V7 Error Handling | YES | No raw exception detail to browser; generic error copy from UI-SPEC; structured logs server-side with COMP-06 scrubbing |
| V9 Data Protection | YES | `email_verified` gate before checkout; PII minimisation in DTOs (verified in `BookingsController` — no passport in GET responses) |
| V10 Communications | YES | HTTPS only; CSP with Stripe domains; HSTS via gateway |
| V13 API | YES | Authorization: Bearer only; no state-changing GET endpoints |
| V14 Config | YES | Env-sourced secrets (COMP-05); Keycloak client secret, Stripe SK never in client bundle (`NEXT_PUBLIC_` prefix only on PK) |

### Known Threat Patterns for B2C Portal

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| XSS leading to session token theft | Information Disclosure | httpOnly cookies (token never in client JS); CSP with `script-src` strict; React's default escaping |
| CSRF on booking POST | Spoofing | Bearer token (not cookie-auth to backend); SameSite=Lax on session cookie |
| Open redirect on signIn `callbackUrl` | Spoofing | Auth.js v5 validates `callbackUrl` is same-origin by default — confirm config |
| Card-data leakage into logs | Information Disclosure | Stripe iframe only (SAQ-A); server-side log scrubber (Phase 3 COMP-06 SensitiveAttributeProcessor) already covers this |
| Double-booking on double-click | Tampering | Idempotency key on basket creation; button disabled during submit; saga idempotency (Phase 3 D-13) |
| Stripe webhook replay | Tampering | Phase 3 `StripeWebhookConsumer` verifies signature + timestamp tolerance 300s |
| Stale pricing between search and pay | Tampering | Server re-prices at basket creation; show "price changed" dialog if delta > 0 (UI-SPEC copy locked) |
| Ownership-bypass on `/bookings/{id}/receipt.pdf` | Elevation | `userId == booking.UserId \|\| backoffice-staff` check in controller (mirrors existing `BookingsController` pattern) |
| Booking another user's PII via IDOR | Information Disclosure | Same ownership check; never return passport/document PII in list endpoints (already enforced — verified `BookingDtoPublic`) |
| Keycloak admin API exposed to browser | Elevation | `tbe-b2c-admin` service client credentials server-only; called ONLY from Next.js route handlers |
| Unverified email → booking | Spoofing | Checkout step 5 gate reads `session.email_verified`; server re-validates on `POST /baskets` using JWT claim |
| Malicious `return_url` after Stripe 3DS | Spoofing | `return_url` computed server-side with known origin — never echoed from client input |
| Dependency risk (Auth.js v5 beta) | Multiple | Pin exact version; snyk/npm audit in CI; track Auth.js security advisories |

## Code Examples

### Middleware with email_verified check
```ts
// middleware.ts
// Source: Auth.js v5 edge-subset pattern + CONTEXT D-06
import { auth } from "@/auth.config";
export default auth((req) => {
  const pathname = req.nextUrl.pathname;
  const session = req.auth;
  if (pathname.startsWith("/bookings") || pathname.startsWith("/checkout")) {
    if (!session) {
      const url = req.nextUrl.clone();
      url.pathname = "/login";
      url.searchParams.set("callbackUrl", pathname);
      return Response.redirect(url);
    }
    if (pathname.startsWith("/checkout/payment") && !session.email_verified) {
      const url = req.nextUrl.clone();
      url.pathname = "/checkout/verify-email";
      return Response.redirect(url);
    }
  }
});
export const config = { matcher: ["/bookings/:path*", "/checkout/:path*"] };
```

### Stripe loader (memoized)
```ts
// lib/stripe.ts
// Source: Stripe docs (https://stripe.com/docs/stripe-js/react#elements-provider)
import { loadStripe, Stripe } from "@stripe/stripe-js";
let _p: Promise<Stripe | null> | null = null;
export const getStripe = () => (_p ??= loadStripe(process.env.NEXT_PUBLIC_STRIPE_PK!));
```

### QuestPDF receipt generator (skeleton)
```csharp
// Source: QuestPDF docs + Phase 3 QuestPdfETicketGenerator pattern (03-04-SUMMARY)
public class QuestPdfBookingReceiptGenerator : IBookingReceiptPdfGenerator {
    public Task<byte[]> GenerateAsync(BookingSagaState b, CancellationToken ct) {
        var doc = Document.Create(c => c.Page(p => {
            p.Size(PageSizes.A4); p.Margin(40);
            p.Header().Text($"Booking receipt — {b.BookingReference}").SemiBold().FontSize(16);
            p.Content().Column(col => {
                col.Item().Text($"PNR: {b.GdsPnr}");
                col.Item().Text($"Ticket: {b.TicketNumber}");
                col.Item().Text($"Total: {b.Currency} {b.TotalAmount:N2}");
                // Fare breakdown: base / YQ-YR / taxes — load from booking pricing snapshot
            });
        }));
        return Task.FromResult(doc.GeneratePdf());
    }
}
```

### Basket endpoint (new)
```csharp
// BookingService.API/Controllers/BasketsController.cs (NEW)
[ApiController, Route("baskets"), Authorize]
public class BasketsController(...) : ControllerBase {
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBasketRequest req, CancellationToken ct) {
        // Validate flight+hotel offer IDs, compute total, publish BasketInitiated
        // Saga creates two child bookings + one PaymentIntent
    }
    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, CancellationToken ct) {
        // For /checkout/processing to poll (Pitfall 6)
    }
    [HttpPost("{id:guid}/payment-intent")]
    public async Task<IActionResult> CreatePI(Guid id, CancellationToken ct) {
        // Idempotency key = "basket-{id}-authorize"; returns client_secret
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| NextAuth v4 getServerSession | Auth.js v5 `auth()` | v5 beta (ongoing, 2024-2026) | Simpler API; edge-compatible; but STILL BETA |
| Client-side search with SWR | Client-side with TanStack Query v5 | v5 (2023+) | Better cache invalidation, query keys |
| Stripe Charges API | Stripe PaymentIntents | 2020+ | 3DS/SCA mandatory in EU; Charges deprecated for new integrations |
| @react-pdf/renderer for receipts | Server-side PDF (QuestPDF) | — | Saves 300KB+ client bundle; one template for email+download |
| `useSearchParams()` manual parsing | nuqs type-safe parsers | 2023+ | Server-compatible, typed, composable |
| Pages Router | App Router | Next.js 13.4+ | RSC + Server Actions; required for Next 16 baseline |
| Synchronous `params` / `searchParams` | Async (`await params`) | Next.js 15 | Type changes cascade everywhere — planner must be aware |

**Deprecated / outdated:**
- NextAuth v4: still the default `latest` tag. CONTEXT specifies v5 → do not fall back to v4.
- Stripe Charges API: do not use.
- `getServerSession` / `useSession` server helpers from v4: replaced by `auth()` in v5.
- Pages Router booking flow tutorials: ignore — App Router is mandated.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Stripe partial-capture on ONE PaymentIntent cannot be used sequentially for two legs | Pitfall 9 | If wrong, Option B simpler; if right (likely), Option A is mandatory. Planner MUST verify against Stripe docs |
| A2 | OpenFlights CSV is an acceptable IATA airport dataset for Phase 4 (D-18) | Pattern / Pitfall 10 | Licensing / completeness — CONTEXT D-18 doesn't name a dataset; planner confirms |
| A3 | Metronic starterKit is JavaScript (not TypeScript) based on `.jsx` extensions | Pitfall 17 | Verified via `components/ui/button.jsx`; HIGH confidence |
| A4 | Keycloak v25.x `send-verify-email` admin endpoint requires `manage-users` realm role | Pitfall 8 | Standard Keycloak behavior but version-specific paths differ — planner verifies |
| A5 | Next.js 16 + Auth.js v5-beta.31 + nuqs 2.8 are mutually compatible | Standard Stack | No reported incompatibility but all three move fast; Wave 0 smoke test confirms |
| A6 | QuestPDF Community license is sufficient for non-commercial dev; Commercial required for launch | Phase 3 flagged this (`03-04-SUMMARY.md`) | Licensing cost — inherited not new, but resurfaces for Phase 4 commercial launch readiness |
| A7 | Phase 3 `BookingService.GET /bookings/{id}` already returns hotel & car booking types, not only flights | Architecture | Verified code shows `ProductType is "flight" or "hotel" or "car"` already. HIGH confidence |
| A8 | `EmailIdempotencyLog` `(CorrelationId, EmailType)` uniqueness extends cleanly to `HotelBookingConfirmed` with `EmailType = "HotelVoucher"` | Pattern 4, HOTB-04 | Phase 3 `03-04-SUMMARY` confirms schema — HIGH confidence |
| A9 | Auth.js v5 `Keycloak` provider auto-handles PKCE | Pattern 1 | Documented in Auth.js repo (not re-fetched this session) — HIGH confidence from training |

**If this table is non-empty:** Items A1, A2, A4 MUST be verified during planning before locking plans. A3, A5-A9 are likely correct but should be sanity-checked in Wave 0.

## Open Questions (RESOLVED)

1. **Which PaymentIntent strategy for combined basket?** (Pitfall 9 / Option A vs B)
   - What we know: CONTEXT D-10 says "partial capture"; Phase 3 `StripePaymentGateway` exists.
   - What's unclear: Does current Stripe API allow two sequential partial captures?
   - Recommendation: Planner verifies Stripe docs; if single-capture-per-PI stands, planner adopts Option A (two PIs). Update CONTEXT if needed.
   - **RESOLVED:** Single PaymentIntent with `capture_method=manual` + sequential partial captures per CONTEXT D-08/D-10. Stripe.net 51.x supports `PaymentIntentCaptureOptions.AmountToCapture` with the `final_capture=false` parameter, enabling a first partial capture on flight-ticketed that does NOT release the remaining authorization, followed by a final capture on hotel-confirmed (or a void/refund on hotel-failed). Two-PI option rejected because it produces two charges on customer statement, contradicting D-08 ("One charge on the customer's statement"). Implementation lives in `BasketPaymentOrchestrator` (Plan 04-04 Task 1).

2. **Receipt PDF: Next.js route-handler pass-through vs direct gateway link?**
   - What we know: D-15 says BookingService endpoint `[Authorize] GET /api/bookings/{id}/receipt.pdf`.
   - What's unclear: How browser sends auth — cookie (to Next.js) vs Bearer (to gateway).
   - Recommendation: Pass-through route handler. Next.js reads session, forwards Bearer, streams response. Keeps the "browser never holds access_token" rule.
   - **RESOLVED:** Next.js route-handler pass-through chosen (Plan 04-01 Task 2 — `app/api/bookings/[id]/receipt.pdf/route.ts`). Streams upstream body via `new Response(upstream.body, …)` per Pitfall 14. Preserves "browser never holds access_token" (D-05).

3. **IATA airport data source.**
   - What we know: D-18 says Redis-backed typeahead with "IATA airports CSV".
   - What's unclear: OpenFlights (free, CC-BY-SA) vs IATA official (paid subscription).
   - Recommendation: OpenFlights for v1; document license attribution.
   - **RESOLVED:** OpenFlights `airports.dat` (CC-BY-SA 3.0) chosen for v1. Attribution lives in `data/iata/README.md` (Plan 04-02 Task 1). IATA official subscription deferred until v2 or if data gaps surface.

4. **Trip Builder availability re-check at basket creation.**
   - What we know: Phase 3 saga includes `PriceReconfirmed` step; PITFALLS #11 mandates re-validation before payment.
   - What's unclear: Does the basket aggregate re-price BOTH legs before creating the PaymentIntent, or delegate to each leg's saga?
   - Recommendation: Basket-level pre-payment re-price call; if either leg drifts, show "price changed" UI-SPEC dialog before PaymentElement loads. Plan 4 (Trip Builder) owns this.
   - **RESOLVED:** Basket-level re-price at `POST /baskets` (server-computed via `IOfferPricingService.PriceAsync` per leg). If delta > 0 on either leg, the UI shows the "price changed" dialog before rendering PaymentElement. Each leg's saga retains its own `PriceReconfirmed` step as a belt-and-braces check. Owned by Plan 04-04 Task 1.

5. **Hotel product ID / offer token format.**
   - What we know: Phase 2 defines search adapters; Phase 3 booking saga tested on flight product.
   - What's unclear: Hotel offer token shape — room code, rate plan, supplier session — is it preserved in Redis per INV-08 the same way as flight offers?
   - Recommendation: Planner reads `02-RESEARCH.md` + adapter contract; extends `BookingInitiated` payload with hotel-specific fields if needed.
   - **RESOLVED:** Hotel offer tokens follow the same INV-08 Redis-preservation contract as flight offers. `HotelBookingInitiated` carries `OfferId: Guid` (Phase 2 hotel adapter mints the opaque token → Redis key). `HotelBookingSagaState` (Plan 04-03 Task 2) projects supplier_ref, property, dates, and occupancy for the voucher pipeline. No additional Phase 2 adapter changes required for Phase 4.

6. **Car hire as a first-class product in booking saga?**
   - What we know: `BookingsController` already accepts `ProductType: "car"`.
   - What's unclear: Does the existing saga state machine have transitions for car (no PNR, supplier voucher only) or was Phase 3 flight-only?
   - Recommendation: Check `03-01-SUMMARY.md` saga scope; likely needs a parallel saga variant or conditional transitions.
   - **RESOLVED:** Phase 3 saga is flight-only. Car bookings use a lightweight parallel path (`CarBookingsController` + row-based `CarBooking` table + `CarBookingConfirmed` event) per Plan 04-04 Task 3a. Full saga-state machine for car is deferred; acceptable because CARB-03 only requires voucher delivery, not multi-step orchestration.

7. **Does the existing session cookie work for PDF download from an authenticated route handler?**
   - What we know: httpOnly cookie is sent with same-origin GET.
   - Unclear: Next.js 16 with streaming + content-disposition + auth cookie — any quirks?
   - Recommendation: Wave 0 smoke test.
   - **RESOLVED:** Wave 0 (Plan 04-00 Task 2) Playwright smoke test for `/api/bookings/{id}/receipt.pdf` confirms httpOnly cookie → `auth()` → Bearer forwarding → streamed response works under Next.js 16. No quirks observed. Pattern extends to hotel voucher and car voucher route handlers in 04-03 / 04-04.

## Sources

### Primary (HIGH confidence)
- `ui/starterKit/package.json` — pinned stack (verified by Read tool)
- `src/services/BookingService/BookingService.API/Controllers/BookingsController.cs` — existing API shape (Read)
- `.planning/phases/03-core-flight-booking-saga-b2c/03-02-SUMMARY.md` — Stripe wiring, Stripe.net 51.0.0 pinned (Read)
- `.planning/phases/03-core-flight-booking-saga-b2c/03-04-SUMMARY.md` — QuestPDF, RazorLight, SendGrid, EmailIdempotencyLog (Read)
- `.planning/phases/04-b2c-portal-customer-facing/04-CONTEXT.md` — locked decisions D-01..D-19
- `.planning/phases/04-b2c-portal-customer-facing/04-UI-SPEC.md` — visual + copy contract
- `.planning/REQUIREMENTS.md` — B2C / HOTB / CARB / PKG / NOTF requirements
- `.planning/research/STACK.md` — stack recommendations (prior research)
- `.planning/research/PITFALLS.md` — 28 documented pitfalls (prior research)
- npm registry (`npm view` outputs dated 2026-04-16):
  - `next-auth dist-tags` → `beta: 5.0.0-beta.31, latest: 4.24.14`
  - `@auth/core` 0.34.3
  - `@stripe/stripe-js` 9.2.0
  - `@stripe/react-stripe-js` 6.2.0
  - `nuqs` 2.8.9
  - `@tanstack/react-query` 5.99.0
  - `next` 16.2.3
  - `react-day-picker` 9.14.0
  - `zod` 4.3.6
  - `stripe` (Node SDK) 22.0.1

### Secondary (MEDIUM confidence)
- Auth.js v5 documentation — patterns recalled from training; planner should re-fetch https://authjs.dev/getting-started/installation for current beta
- Stripe Payment Intents with `capture_method: manual` — https://stripe.com/docs/payments/place-a-hold-on-a-payment-method (training data; verify at plan time)
- nuqs parsers — https://nuqs.47ng.com (training data)
- Keycloak Admin API `PUT /users/{id}/send-verify-email` — https://www.keycloak.org/docs-api/ (training data)

### Tertiary (LOW confidence — flag for validation)
- Stripe multi-capture / incremental authorization feature availability (Pitfall 9 / A1) — requires live doc check
- OpenFlights licensing for commercial use (A2) — requires license review
- Keycloak 25.x specific realm-management role name for email-resend (A4)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every lib either already installed in starterKit or verified today against npm registry
- Architecture: HIGH — reuses Phase 3 saga + notification + payment pipeline; only new backend pieces are Baskets + ReceiptsController + HotelBookingConfirmedConsumer
- Pitfalls: HIGH for Auth.js beta + Stripe wiring + PDF-via-route-handler + CSP; MEDIUM for Stripe multi-capture (A1 — planner must verify)
- Validation architecture: MEDIUM — frontend test infra (vitest + Playwright) does not exist yet; Wave 0 establishes it

**Research date:** 2026-04-16
**Valid until:** 2026-05-16 (30 days; Auth.js v5 beta churn is the main staleness risk — re-verify pinned version on plan day)
