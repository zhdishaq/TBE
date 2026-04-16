# Phase 4: B2C Portal (Customer-Facing) - Pattern Map

**Mapped:** 2026-04-16
**Files analyzed:** ~65 (new/modified; grouped where shape is identical)
**Analogs found:** 58 / 65 (6 frontend files have no in-repo analog — Auth.js, Stripe Elements, nuqs, Playwright — use RESEARCH.md excerpts; 1 Keycloak admin client has no analog either)
**Analog search scope:**
- `ui/starterKit/` (Next.js 16 / Metronic 9 baseline — JavaScript `.jsx`)
- `src/services/BookingService/` (controllers, saga state, EF entities, DI wiring)
- `src/services/NotificationService/` (RazorLight + QuestPDF + MassTransit consumer + EmailIdempotencyLog)
- `src/services/PaymentService/` (Stripe.net `PaymentIntent` authorize/capture, webhook)
- `src/shared/TBE.Contracts/Events/` (event record conventions)
- `src/shared/TBE.Common/` (Messaging, Security, Telemetry DI helpers)

---

## File Classification

### Frontend — `src/portals/b2c-web/` (forked from `ui/starterKit`, TS per Pitfall 17 recommendation)

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `package.json` | config | n/a | `ui/starterKit/package.json` | exact (fork + 5 additions) |
| `next.config.mjs` | config | n/a | `ui/starterKit/next.config.mjs` | exact (+ CSP headers for Stripe) |
| `tsconfig.json` | config | n/a | `ui/starterKit/jsconfig.json` | role-match (JS→TS) |
| `eslint.config.mjs` | config | n/a | `ui/starterKit/eslint.config.mjs` | exact |
| `postcss.config.cjs` | config | n/a | `ui/starterKit/postcss.config.cjs` | exact |
| `styles/globals.css` | config | n/a | `ui/starterKit/styles/globals.css` | exact |
| `lib/utils.ts` (cn) | utility | n/a | `ui/starterKit/lib/utils.js` | exact |
| `lib/auth.ts` | config | request-response | **NO ANALOG** (Auth.js v5 new) | use RESEARCH Pattern 1 |
| `auth.config.ts` (edge) | config | request-response | **NO ANALOG** (Auth.js v5 new) | use RESEARCH Pitfall 3 |
| `middleware.ts` | middleware | request-response | **NO ANALOG** | use RESEARCH Code Example |
| `app/api/auth/[...nextauth]/route.ts` | API route | request-response | `ui/starterKit/app/api/health/route.js` | role-match (handler export shape only) |
| `app/api/bookings/[id]/receipt.pdf/route.ts` | API route | streaming | `ui/starterKit/app/api/health/route.js` | role-match (route handler shape) |
| `lib/api-client.ts` (gatewayFetch) | utility | request-response | **NO direct analog** | use RESEARCH Pattern 1 |
| `lib/stripe.ts` (loadStripe) | utility | client-only | **NO ANALOG** | use RESEARCH Code Example |
| `lib/search-params.ts` (nuqs parsers) | utility | client state | **NO ANALOG** | use RESEARCH Pattern 3 |
| `lib/query-client.ts` | utility | client cache | **NO in-repo analog** (starterKit has TanStack Query installed but unused) | use TanStack Query v5 docs |
| `lib/formatters.ts` (money/date) | utility | transform | `ui/starterKit/lib/helpers.js` | role-match |
| `types/auth.d.ts` | model (type aug) | n/a | **NO ANALOG** | use RESEARCH Pattern 1 |
| `types/api.ts` | model (DTO) | n/a | `ui/starterKit/` has no DTOs; mirror backend `BookingDtoPublic` | role-match (just TS shapes) |
| `app/layout.tsx` | component (RSC) | request-response | `ui/starterKit/app/layout.jsx` | exact |
| `app/page.tsx` (landing) | component (RSC) | request-response | `ui/starterKit/app/page.jsx` | role-match (redirect or hero) |
| `app/(public)/login/page.tsx` | component (RSC) | request-response | `ui/starterKit/app/(layouts)/layout-1/page.jsx` | role-match (page shell) |
| `app/(public)/register/page.tsx` | component (RSC) | request-response | same as above | role-match |
| `app/(public)/verify-email/page.tsx` | component (RSC) | request-response | same | role-match |
| `app/flights/page.tsx` (search form) | component (RSC shell) | request-response | `ui/starterKit/app/(layouts)/layout-1/page.jsx` | role-match (RSC + client widget split) |
| `app/flights/results/page.tsx` | component (RSC shell) | request-response → client | same | role-match |
| `app/flights/[offerId]/page.tsx` | component (RSC) | request-response | same | role-match |
| `app/hotels/**` (same three) | component | same | same | role-match |
| `app/cars/**` (same three) | component | same | same | role-match |
| `app/trips/build/page.tsx` | component (client) | client state | **NO ANALOG** | use RESEARCH Pattern 4 |
| `app/checkout/layout.tsx` | component (RSC) | request-response | `ui/starterKit/app/(layouts)/layout-1/layout.jsx` | role-match |
| `app/checkout/details/page.tsx` | component (client) | form | **NO ANALOG** — RHF+zod form | use RESEARCH "Don't Hand-Roll" row |
| `app/checkout/payment/page.tsx` | component (RSC + Elements) | request-response | **NO ANALOG** | use RESEARCH Pattern 2 |
| `app/checkout/processing/page.tsx` | component (client poll) | polling | **NO ANALOG** | use RESEARCH Pitfall 6 |
| `app/bookings/page.tsx` (dashboard) | component (RSC) | request-response | `ui/starterKit/app/(layouts)/layout-1/page.jsx` | role-match |
| `app/bookings/[id]/page.tsx` | component (RSC) | request-response | same | role-match |
| `components/ui/*.tsx` | component library | n/a | `ui/starterKit/components/ui/*.jsx` | **exact — copy wholesale** |
| `components/search/flight-search-form.tsx` | component (client) | form | `ui/starterKit/app/(layouts)/layout-1/page.jsx` (date-range popover) | role-match |
| `components/search/airport-combobox.tsx` | component (client) | typeahead | `ui/starterKit/components/ui/command.jsx` (cmdk) | role-match |
| `components/search/passenger-selector.tsx` | component (client) | form | Radix Popover (`ui/starterKit/components/ui/popover.jsx`) | role-match |
| `components/search/date-range-picker.tsx` | component (client) | form | layout-1 page (react-day-picker) | exact |
| `components/results/flight-card.tsx` | component (client) | render | `ui/starterKit/components/ui/card.jsx` | role-match |
| `components/results/hotel-card.tsx` | component (client) | render | same | role-match |
| `components/results/filter-rail.tsx` | component (client) | client transform | starterKit sidebar components | role-match |
| `components/results/sort-bar.tsx` | component (client) | client transform | `components/ui/select.jsx` | role-match |
| `components/trip-builder/basket-footer.tsx` | component (client) | client state | **NO ANALOG** | Tailwind sticky footer; UI-SPEC |
| `components/trip-builder/partial-failure-banner.tsx` | component (client) | render | `ui/starterKit/components/ui/alert.jsx` | role-match |
| `components/checkout/payment-element-wrapper.tsx` | component (client) | request-response | **NO ANALOG** | use RESEARCH Pattern 2 |
| `components/checkout/stepper.tsx` | component | render | `ui/starterKit/components/ui/stepper.jsx` | exact (already shipped) |
| `components/checkout/email-verify-gate.tsx` | component (client) | modal | `ui/starterKit/components/ui/dialog.jsx` | role-match |
| `hooks/use-flight-search.ts` | utility (hook) | cache | **NO ANALOG** | use RESEARCH Pattern 3 |
| `hooks/use-basket.ts` | utility (hook) | client state | `ui/starterKit/hooks/use-mounted.js` | role-match (hook scaffold) |
| `hooks/use-session.ts` | utility (hook) | client-only | **NO ANALOG** | thin Auth.js v5 `useSession` wrap |
| `vitest.config.ts` / `tests/setup.ts` | test config | n/a | **NO ANALOG** (Wave 0) | RESEARCH Validation Architecture |
| `playwright.config.ts` / `e2e/**` | test | e2e | **NO ANALOG** (Wave 0) | RESEARCH Validation Architecture |
| `e2e/fixtures/auth.ts` | test fixture | request-response | **NO ANALOG** | RESEARCH Validation Architecture |

### Backend — `.NET` additions (reuse Phase 3 patterns heavily)

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `src/services/BookingService/BookingService.API/Controllers/ReceiptsController.cs` | controller | streaming (PDF) | `BookingService.API/Controllers/BookingsController.cs` | **exact** (auth, ownership, DbContext pattern) |
| `src/services/BookingService/BookingService.API/Controllers/BasketsController.cs` | controller | CRUD + event-driven | `BookingService.API/Controllers/BookingsController.cs` | **exact** |
| `src/services/BookingService/BookingService.Application/Pdf/IBookingReceiptPdfGenerator.cs` | service (interface) | transform | `NotificationService.Application/Pdf/IETicketPdfGenerator.cs` | exact |
| `src/services/BookingService/BookingService.Infrastructure/Pdf/QuestPdfBookingReceiptGenerator.cs` | service (impl) | transform | `NotificationService.Infrastructure/Pdf/QuestPdfETicketGenerator.cs` | **exact** |
| `src/services/BookingService/BookingService.Application/Baskets/Basket.cs` (entity) | model | persistence | `BookingService.Application/Saga/BookingSagaState.cs` | role-match |
| `src/services/BookingService/BookingService.Infrastructure/Configurations/BasketMap.cs` | model (EF map) | persistence | `BookingService.Infrastructure/Configurations/BookingSagaStateMap.cs` | **exact** |
| `src/services/BookingService/BookingService.Infrastructure/Migrations/xxxxxxxx_AddBaskets.cs` | migration | n/a | `BookingService.Infrastructure/Migrations/20260416000000_AddBookingSagaState.cs` | exact |
| `src/services/NotificationService/NotificationService.Application/Consumers/HotelBookingConfirmedConsumer.cs` | consumer | event-driven | `NotificationService.Application/Consumers/BookingConfirmedConsumer.cs` | **exact** |
| `src/services/NotificationService/NotificationService.Application/Consumers/BasketConfirmedConsumer.cs` | consumer | event-driven | same | **exact** |
| `src/services/NotificationService/NotificationService.Application/Pdf/IHotelVoucherPdfGenerator.cs` | service (interface) | transform | `IETicketPdfGenerator.cs` | exact |
| `src/services/NotificationService/NotificationService.Infrastructure/Pdf/HotelVoucherDocument.cs` | service (impl) | transform | `QuestPdfETicketGenerator.cs` | exact |
| `src/services/NotificationService/NotificationService.API/Templates/HotelVoucher.cshtml` | template | render | `NotificationService.API/Templates/FlightConfirmation.cshtml` | **exact** |
| `src/services/NotificationService/NotificationService.Application/Templates/Models/HotelVoucherModel.cs` | model (DTO) | n/a | `Templates/Models/FlightConfirmationModel.cs` | **exact** |
| `src/services/NotificationService/NotificationService.Application/Templates/Models/BasketConfirmationModel.cs` | model (DTO) | n/a | same | **exact** |
| `src/shared/TBE.Contracts/Events/HotelEvents.cs` | model (events) | n/a | `TBE.Contracts/Events/SagaEvents.cs` | **exact** |
| `src/shared/TBE.Contracts/Events/BasketEvents.cs` | model (events) | n/a | same | **exact** |
| `src/services/CrmService/**` Keycloak admin client (resend-verify-email) | service | request-response | **NO in-repo analog** | follow `Pitfall 8` in RESEARCH |
| `src/services/InventoryService/**` IATA typeahead endpoint | controller + Redis | request-response | `SearchService.API/Controllers/SearchController.cs` (not read in depth) | role-match |

---

## Pattern Assignments

### `src/services/BookingService/BookingService.API/Controllers/ReceiptsController.cs` (controller, streaming)

**Analog:** `src/services/BookingService/BookingService.API/Controllers/BookingsController.cs`

**Imports pattern** (analog lines 1–9):
```csharp
using System.Security.Claims;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TBE.BookingService.Infrastructure;
using TBE.Contracts.Events;

namespace TBE.BookingService.API.Controllers;
```

**Auth + ownership pattern** (analog lines 18–24 for attributes, 65–89 for per-action ownership check — **copy verbatim**):
```csharp
[ApiController]
[Route("bookings")]
[Authorize]
public class ReceiptsController(
    BookingDbContext db,
    IBookingReceiptPdfGenerator pdfGen,
    ILogger<ReceiptsController> logger) : ControllerBase
{
    private const string BackofficeRole = "backoffice-staff";
    // ...
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? string.Empty;
    // ...
    if (booking.UserId != userId && !User.IsInRole(BackofficeRole))
        return Forbid();
```

**Core pattern for GET (shape to follow, lines 65–89 of analog):**
```csharp
[HttpGet("{id:guid}/receipt.pdf")]
public async Task<IActionResult> GetReceiptAsync(Guid id, CancellationToken ct)
{
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub") ?? string.Empty;

    var booking = await db.BookingSagaStates.AsNoTracking()
        .FirstOrDefaultAsync(s => s.CorrelationId == id, ct);
    if (booking is null) return NotFound();
    if (booking.UserId != userId && !User.IsInRole(BackofficeRole)) return Forbid();

    var bytes = await pdfGen.GenerateAsync(booking, ct);
    return File(bytes, "application/pdf", $"receipt-{booking.BookingReference}.pdf");
}
```

**DTO shape convention** (analog lines 120–151): DTO names `BookingDto` (internal, includes `UserId`) and `BookingDtoPublic` (omits `UserId` + PII). Receipt may not need a DTO (returns `FileContentResult`) but any new public JSON responses MUST follow the `*DtoPublic` pattern.

---

### `src/services/BookingService/BookingService.API/Controllers/BasketsController.cs` (controller, CRUD + event-driven)

**Analog:** `src/services/BookingService/BookingService.API/Controllers/BookingsController.cs`

**POST + outbox publish pattern** (analog lines 28–63 — **copy verbatim** for request validation, userId extraction, `IPublishEndpoint.Publish`, and `AcceptedAtAction` response):
```csharp
[HttpPost]
public async Task<IActionResult> PostAsync([FromBody] CreateBasketRequest req, CancellationToken ct)
{
    var errors = new List<string>();
    if (req.FlightOfferId == Guid.Empty) errors.Add("FlightOfferId required");
    // …
    if (errors.Count > 0) return BadRequest(new { errors });

    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub") ?? string.Empty;
    if (string.IsNullOrEmpty(userId)) return Unauthorized();

    var basketId = Guid.NewGuid();
    await publishEndpoint.Publish(new BasketInitiated(basketId, userId, /*...*/, DateTimeOffset.UtcNow), ct);
    return AcceptedAtAction(nameof(GetStatusAsync), new { id = basketId },
        new { basketId, status = "Initiated" });
}
```

**Idempotency-key naming convention** — follows Phase 3 PaymentService pattern. See `src/services/PaymentService/PaymentService.Application/Stripe/StripePaymentGateway.cs` lines 62–65:
```csharp
var requestOptions = new RequestOptions
{
    IdempotencyKey = $"booking-{bookingId}-authorize"
};
```
For basket PIs: `$"basket-{basketId}-authorize"`, `$"basket-{basketId}-capture-flight"`, `$"basket-{basketId}-capture-hotel"` (RESEARCH Pattern 4, Option A).

---

### `src/services/BookingService/BookingService.Infrastructure/Pdf/QuestPdfBookingReceiptGenerator.cs` (service impl, transform)

**Analog:** `src/services/NotificationService/NotificationService.Infrastructure/Pdf/QuestPdfETicketGenerator.cs` — **copy verbatim**, adapt fields.

**License bootstrap** (analog lines 13–18 — MUST reproduce):
```csharp
static QuestPdfBookingReceiptGenerator()
{
    QuestPDF.Settings.License = LicenseType.Community;
    // TODO(prod): switch to LicenseType.Commercial before production launch.
}
```

**Document shape** (analog lines 20–54): `Document.Create(container => container.Page(page => { page.Margin(30); page.Header()…; page.Content().Column(col => {…}); page.Footer()…; })).GeneratePdf()`.
Receipt content (D-15 + FLTB-03): base fare / YQ-YR / taxes separated; PNR; ticket; all-in total; currency via `Intl`-style formatter (server-side: `decimal.ToString("N2")` + currency code).

---

### `src/services/NotificationService/NotificationService.Application/Consumers/HotelBookingConfirmedConsumer.cs` (consumer, event-driven)

**Analog:** `src/services/NotificationService/NotificationService.Application/Consumers/BookingConfirmedConsumer.cs` — **copy 1:1**, change: `IConsumer<BookingConfirmed>` → `IConsumer<HotelBookingConfirmed>`; `EmailType.FlightConfirmation` → `EmailType.HotelVoucher`; renderer template key and PDF generator.

**Idempotency-first insert pattern** (analog lines 58–78 — **copy verbatim**):
```csharp
var idemp = new EmailIdempotencyLog
{
    EventId = eventId,
    EmailType = EmailType.HotelVoucher,
    BookingId = evt.BookingId,
    Recipient = contact.Email,
    SentAtUtc = DateTime.UtcNow,
};
_db.EmailIdempotencyLogs.Add(idemp);
try { await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false); }
catch (DbUpdateException ex) when (IdempotencyHelpers.IsUniqueViolation(ex))
{
    _log.LogInformation("NOTF-06: duplicate {EmailType} for event {EventId} skipped",
        EmailType.HotelVoucher, eventId);
    return;
}
```

**Render + attach pattern** (analog lines 80–114): call `_renderer.RenderAsync(templateKey, model, ct)` → call `_pdfGen.Generate(docModel)` → assemble `EmailEnvelope` with `EmailAttachment("voucher.pdf", "application/pdf", bytes, "voucher.pdf")` → `_delivery.SendAsync`.

**Failure propagation** (analog lines 115–121): throw on `!result.Success` so MassTransit retry policy applies.

---

### `src/services/NotificationService/NotificationService.API/Templates/HotelVoucher.cshtml` (template)

**Analog:** `src/services/NotificationService/NotificationService.API/Templates/FlightConfirmation.cshtml` — **copy wholesale**, change `@model` and inner `<table>` rows.

**Subject-comment convention** (analog line 2 — **mandatory**, parsed by `RazorLightEmailTemplateRenderer` via regex):
```html
@model TBE.NotificationService.API.Templates.Models.HotelVoucherModel
<!--SUBJECT:Hotel Voucher — @Model.SupplierRef-->
<!DOCTYPE html>
<html>
<body style="font-family:Arial,sans-serif;color:#111827;max-width:640px;margin:0 auto;">
  @{ await IncludeAsync("_Header", Model); }
  …
  @{ await IncludeAsync("_Footer", Model); }
</body>
</html>
```
Include `_Header`/`_Footer` partials (already in `Templates/`).

**Renderer-side contract** — nothing new; `RazorLightEmailTemplateRenderer` already resolves `{templateKey}.cshtml` from `{AppContext.BaseDirectory}/Templates` (see `RazorLightEmailTemplateRenderer.cs` lines 25–29). Ensure the new `HotelVoucher.cshtml` is marked `<Content CopyToOutputDirectory="..."/>` in `NotificationService.API.csproj` same as siblings.

---

### `src/shared/TBE.Contracts/Events/HotelEvents.cs` and `BasketEvents.cs` (event records)

**Analog:** `src/shared/TBE.Contracts/Events/SagaEvents.cs`

**Record convention** (analog lines 48–55 — **follow exactly**):
```csharp
namespace TBE.Contracts.Events;

/// <summary>Terminal success event. EventId is the notification idempotency key per D-19.</summary>
public record HotelBookingConfirmed(
    Guid BookingId,
    Guid EventId,                    // REQUIRED — drives EmailIdempotencyLog insert
    string BookingReference,
    string SupplierRef,
    string PropertyName,
    DateOnly CheckInDate,
    DateOnly CheckOutDate,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset At);
```
All terminal events MUST carry `EventId` (D-19 idempotency). Failure callbacks use `(Guid BookingId, string Cause)` (analog lines 80–100).

---

### `src/services/BookingService/BookingService.Application/Baskets/Basket.cs` + `Infrastructure/Configurations/BasketMap.cs`

**Analog:** `BookingSagaState.cs` + `BookingSagaStateMap.cs`.

**Mapping pattern** (from `BookingSagaStateMap.cs` lines 14–37):
```csharp
b.ToTable("Basket", "Booking");              // dedicated schema
b.HasKey(x => x.BasketId);
b.Property(x => x.Version).IsRowVersion();   // optimistic concurrency
b.Property(x => x.UserId).HasMaxLength(64).IsRequired();
b.Property(x => x.TotalAmount).HasColumnType("decimal(18,4)");
b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
b.Property(x => x.StripePaymentIntentId).HasMaxLength(50);
b.HasIndex(x => x.UserId);
```

Register in `BookingDbContext.OnModelCreating` the same way `BookingSagaStateMap` is registered (`BookingDbContext.cs` line 27): `modelBuilder.ApplyConfiguration(new BasketMap());` and add `public DbSet<Basket> Baskets => Set<Basket>();`.

---

### `src/services/BookingService/BookingService.API/Program.cs` (modified — register receipt generator + baskets controller inheritance)

**Analog:** existing `Program.cs` (entire file).

No new Program.cs — **add registrations** at the DI section (before `builder.Services.AddControllers()`, around line 77):
```csharp
builder.Services.AddScoped<IBookingReceiptPdfGenerator, QuestPdfBookingReceiptGenerator>();
```
JWT bearer block (lines 42–54) already validates against Keycloak with `Audience` from config — **no change** required; just ensure the `tbe-b2c` audience-mapper is added on the Keycloak side (Pitfall 4).

---

### Frontend — `src/portals/b2c-web/components/ui/*` (copy wholesale)

**Analog:** `ui/starterKit/components/ui/*.jsx` — **fork wholesale into `src/portals/b2c-web/components/ui/`**. 77 component files; Phase 4 uses at minimum: `button.jsx`, `card.jsx`, `dialog.jsx`, `alert.jsx`, `alert-dialog.jsx`, `popover.jsx`, `command.jsx`, `calendar.jsx`, `input.jsx`, `label.jsx`, `select.jsx`, `skeleton.jsx`, `sonner.jsx`, `stepper.jsx`, `tabs.jsx`, `sheet.jsx`, `badge.jsx`, `separator.jsx`, `form.jsx` (if present — otherwise build thin RHF wrapper).

**Per Pitfall 17:** ship the forked files as `.jsx` untouched; set `tsconfig.json` `"allowJs": true, "checkJs": false`. New Phase 4 code is `.ts`/`.tsx`. Do NOT rewrite Metronic components to TS in this phase.

---

### Frontend — `src/portals/b2c-web/app/layout.tsx`

**Analog:** `ui/starterKit/app/layout.jsx` — copy, add `SessionProvider`-equivalent only if a client `useSession` hook is required; otherwise Auth.js v5 RSC reads `auth()` directly and no provider is needed.

**Imports + providers** (analog lines 1–9):
```tsx
import { Suspense } from 'react';
import { Inter } from 'next/font/google';
import { ThemeProvider } from 'next-themes';
import { cn } from '@/lib/utils';
import { Toaster } from '@/components/ui/sonner';
import { TooltipProvider } from '@/components/ui/tooltip';
import '@/styles/globals.css';
```

**Shell** (analog lines 18–43): keep the `<ThemeProvider>` + `<TooltipProvider>` + `<Toaster />` tree. Remove the `next/font` `Inter` if a heavier custom font is later required (UI-SPEC keeps system default; leaving Inter is acceptable).

---

### Frontend — `src/portals/b2c-web/app/api/bookings/[id]/receipt.pdf/route.ts` (pass-through, streaming)

**Analog:** `ui/starterKit/app/api/health/route.js` (only for route handler shape).

**Handler export shape** (analog lines 1–13):
```ts
import { NextResponse } from 'next/server';
export async function GET(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;   // Pitfall 11 — params is a Promise in Next.js 16
  const upstream = await gatewayFetch(`/bookings/${id}/receipt.pdf`);
  if (!upstream.ok) return NextResponse.json({ error: 'not found' }, { status: upstream.status });
  // Pitfall 14 — stream through, never buffer
  return new Response(upstream.body, {
    headers: {
      'content-type': 'application/pdf',
      'content-disposition': upstream.headers.get('content-disposition') ?? `attachment; filename="receipt-${id}.pdf"`,
    },
  });
}
```

---

### Frontend — `src/portals/b2c-web/components/search/date-range-picker.tsx`

**Analog:** `ui/starterKit/app/(layouts)/layout-1/page.jsx` lines 41–88 — **copy the Popover+Calendar pattern verbatim**:
```tsx
<Popover open={isOpen} onOpenChange={setIsOpen}>
  <PopoverTrigger asChild>
    <Button variant="outline">
      <CalendarDays size={16} className="me-0.5" />
      {date?.from ? ( /* format */ ) : <span>Pick a date range</span>}
    </Button>
  </PopoverTrigger>
  <PopoverContent className="w-auto p-0" align="end">
    <Calendar mode="range" defaultMonth={tempDateRange?.from} selected={tempDateRange}
              onSelect={setTempDateRange} numberOfMonths={2} />
    <div className="flex items-center justify-end gap-1.5 border-t border-border p-3">
      <Button variant="outline" onClick={handleDateRangeReset}>Reset</Button>
      <Button onClick={handleDateRangeApply}>Apply</Button>
    </div>
  </PopoverContent>
</Popover>
```
Apply UI-SPEC: minimum date = today, maximum = today + 361 days.

---

### Frontend — `src/portals/b2c-web/components/search/airport-combobox.tsx`

**Analog:** `ui/starterKit/components/ui/command.jsx` — cmdk scaffolding already in starterKit.

**Import convention** (analog lines 1–7):
```tsx
'use client';
import React from 'react';
import { Command as CommandPrimitive } from 'cmdk';
import { Check, Search } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Dialog, DialogContent, DialogTitle } from '@/components/ui/dialog';
```
Wrap cmdk `<Command>` / `<CommandInput>` / `<CommandList>` / `<CommandItem>` already exported from `command.jsx`. Debounce 200ms + `AbortController` (Pitfall 10); min 2 chars (UI-SPEC).

---

### Frontend — files with NO in-repo analog

| File | Use Pattern From |
|------|------------------|
| `lib/auth.ts` | RESEARCH §Pattern 1 (verbatim) |
| `auth.config.ts` | RESEARCH §Pitfall 3 — edge-subset split |
| `middleware.ts` | RESEARCH §Code Examples (middleware w/ email_verified) |
| `lib/api-client.ts` | RESEARCH §Pattern 1 `gatewayFetch` block |
| `lib/stripe.ts` | RESEARCH §Code Examples (memoized `loadStripe`) |
| `components/checkout/payment-element-wrapper.tsx` | RESEARCH §Pattern 2 (Elements + PaymentElement) |
| `app/checkout/payment/page.tsx` | RESEARCH §Pattern 2 (server-side PI creation) |
| `app/checkout/processing/page.tsx` | RESEARCH §Pitfall 6 (polling pattern; 90s cap) |
| `lib/search-params.ts` | RESEARCH §Pattern 3 (nuqs parsers) |
| `hooks/use-flight-search.ts` | RESEARCH §Pattern 3 (TanStack Query + nuqs key) |
| `app/trips/build/page.tsx` + basket | RESEARCH §Pattern 4 (Option A: two PaymentIntents) |
| CSP headers in `next.config.mjs` | RESEARCH §Pitfall 16 (Stripe domain whitelist) |
| Keycloak admin client (resend-verify) | RESEARCH §Pitfall 8 (`tbe-b2c-admin` service client) |
| Playwright / Vitest setup | RESEARCH §Validation Architecture (Wave 0) |

---

## Shared Patterns

### Authentication (backend controllers)

**Source:** `src/services/BookingService/BookingService.API/Controllers/BookingsController.cs` lines 18–24, 43–47, 83–84
**Apply to:** `ReceiptsController`, `BasketsController`, any new InventoryService typeahead controller.

```csharp
[ApiController]
[Route("…")]
[Authorize]                              // class-level, enforces COMP-04
public class Xxx(…) : ControllerBase
{
    private const string BackofficeRole = "backoffice-staff";

    public async Task<IActionResult> ActionAsync(…)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub") ?? string.Empty;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        // …
        if (entity.UserId != userId && !User.IsInRole(BackofficeRole)) return Forbid();
    }
}
```

### JWT bearer configuration (backend Program.cs)

**Source:** `src/services/BookingService/BookingService.API/Program.cs` lines 42–54
**Apply to:** no new services in Phase 4 need this — BookingService already registers; any NEW .NET service would copy this block.

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
    {
        o.Authority = builder.Configuration["Keycloak:Authority"];
        o.Audience  = builder.Configuration["Keycloak:Audience"];
        o.RequireHttpsMetadata = builder.Environment.IsProduction();
    });
builder.Services.AddAuthorization(opt =>
{
    opt.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});
```

### MassTransit consumer with EF outbox (new consumers)

**Source:** `src/services/NotificationService/NotificationService.Application/Consumers/BookingConfirmedConsumer.cs` (whole file)
**Apply to:** `HotelBookingConfirmedConsumer`, `BasketConfirmedConsumer`, any partial-failure basket consumer.

Key rules:
1. `EmailIdempotencyLog` insert-before-send (lines 58–78).
2. Try/catch `DbUpdateException` with `IdempotencyHelpers.IsUniqueViolation` to swallow duplicates.
3. Throw on delivery failure so MassTransit retries.
4. `EmailType` enum — add new constants (`EmailType.HotelVoucher`, `EmailType.BasketConfirmation`) in `NotificationService.Application/Email/EmailType.cs`.

### Stripe idempotency key naming

**Source:** `src/services/PaymentService/PaymentService.Application/Stripe/StripePaymentGateway.cs` lines 62–64, 92–94
**Apply to:** every new Stripe call (basket authorize/capture).

Pattern: `$"{aggregate-kind}-{aggregate-id}-{operation}"` — e.g. `basket-{basketId}-authorize`, `basket-{basketId}-capture-flight`, `basket-{basketId}-capture-hotel`, `basket-{basketId}-refund-hotel` (D-09 partial-failure refund).

### EF Core entity mapping

**Source:** `src/services/BookingService/BookingService.Infrastructure/Configurations/BookingSagaStateMap.cs` lines 12–38
**Apply to:** `BasketMap.cs`.

Key rules: dedicated schema (`"Saga"` / `"Booking"`), `IsRowVersion()` on `Version`, explicit `HasMaxLength`, `decimal(18,4)` for money, `HasIndex` on foreign-key / lookup columns.

### Frontend: `cn` utility + Tailwind

**Source:** `ui/starterKit/lib/utils.js` (lines 1–12) — `cn(...inputs)` = `twMerge(clsx(inputs))`.
**Apply to:** every new TS/TSX component. Re-export from `src/portals/b2c-web/lib/utils.ts` after `.js → .ts` rename (the implementation is unchanged; JSDoc → TSDoc).

### Frontend: Radix primitive wrapping

**Source:** any `ui/starterKit/components/ui/*.jsx` (e.g. `button.jsx` lines 1–7 for import convention; `command.jsx` for cmdk; layout-1's `page.jsx` for Popover+Calendar).
**Apply to:** every new component in `components/search/`, `components/results/`, `components/trip-builder/`, `components/checkout/`.

Rule: never import Radix directly in feature components — import from `@/components/ui/*`. Icons: `lucide-react` only (per UI-SPEC).

### Event record naming (TBE.Contracts)

**Source:** `src/shared/TBE.Contracts/Events/SagaEvents.cs` (entire file)
**Apply to:** `HotelEvents.cs`, `BasketEvents.cs`.

Rule: every terminal event carries `Guid EventId` (idempotency key for notification). Failure events have `(Guid BookingId, string Cause)`. Use `DateTimeOffset At`.

### Next.js 16 async `params` / `searchParams` (Pitfall 11)

**Apply to:** every `page.tsx` and `route.ts` in new b2c-web app.

```ts
export default async function Page({ params, searchParams }: {
  params: Promise<{ id: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { id } = await params;
  const q = await searchParams;
  // …
}
```

---

## No Analog Found

Files requiring RESEARCH.md patterns (no existing in-repo reference):

| File | Role | Reason | Source |
|------|------|--------|--------|
| `src/portals/b2c-web/lib/auth.ts` | config | Auth.js v5 is NEW to repo | RESEARCH §Pattern 1 |
| `src/portals/b2c-web/auth.config.ts` | config | Edge-subset split is Auth.js v5 specific | RESEARCH §Pitfall 3 |
| `src/portals/b2c-web/middleware.ts` | middleware | No prior Next.js middleware exists | RESEARCH §Code Examples |
| `src/portals/b2c-web/lib/stripe.ts` | utility | No prior frontend Stripe integration | RESEARCH §Code Examples |
| `src/portals/b2c-web/components/checkout/payment-element-wrapper.tsx` | component | PaymentElement is new | RESEARCH §Pattern 2 |
| `src/portals/b2c-web/app/checkout/processing/page.tsx` | page | Polling saga status is new | RESEARCH §Pitfall 6 |
| `src/portals/b2c-web/lib/search-params.ts` | utility | nuqs new to repo | RESEARCH §Pattern 3 |
| `src/portals/b2c-web/app/trips/build/page.tsx` | page | Trip Builder combined-PI flow is new | RESEARCH §Pattern 4 |
| Keycloak admin service client (resend-verify-email) | service | No Keycloak admin calls exist yet | RESEARCH §Pitfall 8 |
| `src/portals/b2c-web/vitest.config.ts` / `playwright.config.ts` | test config | Frontend test infra not present | RESEARCH §Validation Architecture (Wave 0) |

---

## Metadata

**Analog search scope:** `ui/starterKit/`, `src/services/BookingService/`, `src/services/NotificationService/`, `src/services/PaymentService/`, `src/shared/TBE.Contracts/`, `src/shared/TBE.Common/`.
**Files scanned:** ~30 .cs analogs, ~10 .jsx analogs, 1 .cshtml analog, entire `TBE.Contracts/Events/`.
**Pattern extraction date:** 2026-04-16
