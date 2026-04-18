# Phase 5: B2B Agent Portal — Pattern Map

**Mapped:** 2026-04-17
**Files analyzed:** 27 new files + 6 modified files = 33 total
**Analogs found:** 32 / 33 (1 no-analog — `AgencyPriceRequestedConsumer` is role-match only)

Pattern extraction uses read-only Read/Grep tooling. All analog paths are absolute under `C:\Users\zhdishaq\source\repos\TBE`.

---

## File Classification

### Modified files (existing code edited, not copied)

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `src/services/BookingService/BookingService.Application/Saga/BookingSagaState.cs` | saga-state (model) | event-driven | self (extend in place) — prior-column pattern in `BookingSagaState.BaseFareAmount` block | exact |
| `src/services/BookingService/BookingService.Application/Saga/BookingSaga.cs` | saga (state-machine) | event-driven / pub-sub | self — prior `During(PnrCreating, When(PnrCreated)…)` block at lines 119-139 | exact |
| `src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs` | controller (edit) | request-response | self — existing `TopUp` handler lines 47-65 | exact (D-40 cap check inserted in-place) |
| `src/gateway/TBE.Gateway/Program.cs` | config (edit) | request-response | self — existing B2C JWT scheme block | exact (flip `ValidateAudience`) |
| `src/shared/TBE.Contracts/Commands/SagaCommands.cs` | contract (edit) | pub-sub | self — already defines `WalletReserveCommand/Commit/Release` lines 52-62 | **no edit needed** — commands exist |
| `src/shared/TBE.Contracts/Events/BookingEvents.cs` | contract (edit) | pub-sub | self — `BookingInitiated` record | exact (add `Channel` / `AgencyId` / `WalletId` fields to `BookingInitiated`) |

### New backend files

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `src/shared/TBE.Contracts/Enums/Channel.cs` | enum (contract) | — | — (trivial enum) | n/a |
| `src/services/BookingService/BookingService.Infrastructure/Migrations/20260600000000_AddAgencyPricingAndChannel.cs` | migration | batch/DDL | `src/services/BookingService/BookingService.Infrastructure/Migrations/20260500000000_AddReceiptFareBreakdown.cs` | exact |
| `src/services/BookingService/BookingService.API/Controllers/AgentBookingsController.cs` | controller | CRUD / request-response | `src/services/BookingService/BookingService.API/Controllers/BookingsController.cs` (method `ListForCustomerAsync` lines 109-135 + `GetByIdAsync` lines 65-89) | exact |
| `src/services/BookingService/BookingService.API/Controllers/AgentsController.cs` (for POST `/api/b2b/agents` proxy — optional if not handled in Next.js route) | controller | request-response | `src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs` (policy-scoped handler lines 47-65) | role-match |
| `src/services/PricingService/PricingService.Application/AgencyMarkupRules/IAgencyMarkupRepository.cs` | service interface | CRUD | `src/services/PricingService/PricingService.Application/Rules/IPricingRulesEngine.cs` | exact |
| `src/services/PricingService/PricingService.Application/AgencyMarkupRules/ApplyMarkup.cs` | utility (pure func) | transform | `src/services/PricingService/PricingService.Infrastructure/Rules/MarkupRulesEngine.cs` lines 45-67 | exact |
| `src/services/PricingService/PricingService.Application/AgencyMarkupRules/Models/AgencyMarkupRule.cs` | model (EF entity) | CRUD | inline `MarkupRule` model in `MarkupRulesEngine.cs` + `AddMarkupRules` migration column set | role-match |
| `src/services/PricingService/PricingService.Application/Consumers/AgencyPriceRequestedConsumer.cs` | consumer | event-driven | `src/services/PaymentService/PaymentService.Application/Consumers/WalletReserveConsumer.cs` | role-match (only pricing consumer in codebase) |
| `src/services/PricingService/PricingService.Infrastructure/Migrations/20260600000100_AddAgencyMarkupRules.cs` | migration | batch/DDL | `src/services/PricingService/PricingService.Infrastructure/Migrations/20260415000000_AddMarkupRules.cs` | exact |
| `src/services/PricingService/PricingService.Infrastructure/PricingDbContext.cs` (edit) | DbContext (edit) | persistence | self — existing `MarkupRule` configuration lines 24-34 | exact |
| `src/services/NotificationService/NotificationService.Application/Pdf/IAgencyInvoicePdfGenerator.cs` | service interface | transform | `src/services/NotificationService/NotificationService.Application/Pdf/IETicketPdfGenerator.cs` | exact |
| `src/services/NotificationService/NotificationService.Infrastructure/Pdf/AgencyInvoiceDocument.cs` | service (PDF doc) | transform | `src/services/BookingService/BookingService.Infrastructure/Pdf/QuestPdfBookingReceiptGenerator.cs` (primary) + `src/services/NotificationService/NotificationService.Infrastructure/Pdf/CarVoucherDocument.cs` (secondary for row layout) | exact |
| `src/services/NotificationService/NotificationService.API/Templates/AgencyInvoice.cshtml` | template | transform | `src/services/NotificationService/NotificationService.API/Templates/FlightConfirmation.cshtml` | exact |
| `src/services/NotificationService/NotificationService.API/Templates/Models/AgencyInvoiceModel.cs` | DTO | transform | other `*Model.cs` records under `Templates/Models/` | role-match |
| `src/services/NotificationService/NotificationService.API/Controllers/InvoicesController.cs` | controller | streaming | `src/services/BookingService/BookingService.API/Controllers/ReceiptsController.cs` | exact |

### New `infra/`

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `infra/keycloak/realm-tbe-b2b.json` | config (realm delta patch) | — | `infra/keycloak/realm-tbe-b2c.json` | exact |
| `infra/keycloak/verify-audience-smoke-b2b.sh` | script (smoke) | request-response | `infra/keycloak/verify-audience-smoke.sh` | exact |

### New portal files (`src/portals/b2b-web/**`)

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `src/portals/b2b-web/auth.config.ts` | config (edge auth) | request-response | `src/portals/b2c-web/auth.config.ts` | exact (realm+client-id delta only) |
| `src/portals/b2b-web/lib/auth.ts` | service (Node auth) | request-response | `src/portals/b2c-web/lib/auth.ts` | exact (env-var rename only) |
| `src/portals/b2b-web/lib/api-client.ts` | utility | request-response | `src/portals/b2c-web/lib/api-client.ts` | exact (verbatim) |
| `src/portals/b2b-web/lib/checkout-ref.ts` | utility | transform | `src/portals/b2c-web/lib/checkout-ref.ts` | exact (verbatim) |
| `src/portals/b2b-web/lib/stripe.ts` | utility (memo loadStripe) | request-response | `src/portals/b2c-web/lib/stripe.ts` | exact (verbatim) |
| `src/portals/b2b-web/lib/keycloak-b2b-admin.ts` | service (Node-only admin SDK) | request-response | `src/portals/b2c-web/lib/keycloak-admin.ts` | exact (realm + env + `createSubAgent` addition) |
| `src/portals/b2b-web/middleware.ts` | middleware (edge) | request-response | `src/portals/b2c-web/middleware.ts` | exact (role-claim delta + `/admin/*` gate) |
| `src/portals/b2b-web/next.config.mjs` | config (CSP) | — | `src/portals/b2c-web/next.config.mjs` | exact (per-route CSP delta) |
| `src/portals/b2b-web/app/layout.tsx` | RSC layout | request-response | `src/portals/b2c-web/app/layout.tsx` | exact (+ `AgentPortalBadge` / wallet chip mount) |
| `src/portals/b2b-web/app/api/auth/[...nextauth]/route.ts` | route handler | request-response | `src/portals/b2c-web/app/api/auth/[...nextauth]/route.ts` | exact (verbatim) |
| `src/portals/b2b-web/app/api/agents/route.ts` (+ `[id]/deactivate/route.ts`) | route handler (Node) | request-response | `src/portals/b2c-web/app/api/auth/resend-verification/route.ts` | exact |
| `src/portals/b2b-web/app/api/bookings/[id]/invoice.pdf/route.ts` | route handler (stream) | streaming | `src/portals/b2c-web/app/api/bookings/[id]/receipt.pdf/route.ts` | exact |
| `src/portals/b2b-web/app/api/bookings/[id]/e-ticket.pdf/route.ts` | route handler (stream) | streaming | `src/portals/b2c-web/app/api/hotels/[offerId]/voucher.pdf/route.ts` | exact |
| `src/portals/b2b-web/app/(portal)/dashboard/page.tsx` | RSC page | request-response | `src/portals/b2c-web/app/bookings/page.tsx` | role-match (dashboard shape is new — 2-col grid) |
| `src/portals/b2b-web/app/(portal)/bookings/page.tsx` | RSC page | request-response | `src/portals/b2c-web/app/bookings/page.tsx` | exact (agency-wide list replaces per-user partition) |
| `src/portals/b2b-web/app/(portal)/bookings/[id]/page.tsx` | RSC page | request-response | `src/portals/b2c-web/app/bookings/[id]/page.tsx` | exact |
| `src/portals/b2b-web/app/(portal)/checkout/details/page.tsx` | RSC page | request-response | `src/portals/b2c-web/app/checkout/details/page.tsx` | exact (+ customer contact fields; override input gated by role) |
| `src/portals/b2b-web/app/(portal)/checkout/confirm/page.tsx` | RSC page | request-response | `src/portals/b2c-web/app/checkout/payment/page.tsx` (shape only — **no `<Elements>` mount**) | role-match (wallet debit is new) |
| `src/portals/b2b-web/app/(portal)/admin/wallet/page.tsx` | RSC page | request-response | `src/portals/b2c-web/app/checkout/payment/page.tsx` (PaymentElement mount pattern) | role-match |
| `src/portals/b2b-web/app/(portal)/admin/agents/page.tsx` | RSC page | request-response | `src/portals/b2c-web/app/bookings/page.tsx` | role-match |
| `src/portals/b2b-web/components/layout/agent-portal-badge.tsx` | component (UI) | — | none (new — 1px indigo outline pill spec in UI-SPEC §Color) | no-analog |
| `src/portals/b2b-web/components/layout/wallet-chip.tsx` | component (client) | polling | `src/portals/b2c-web/components/account/dashboard-tabs.tsx` (TanStack Query pattern referenced in RESEARCH.md Example 3) | role-match |
| `src/portals/b2b-web/components/results/dual-pricing-row.tsx` | component | transform | `src/portals/b2c-web/components/results/flight-card.tsx` | exact (extend for 4-column NET/markup/GROSS/commission) |
| `src/portals/b2b-web/components/checkout/debit-summary.tsx` | component | request-response | `src/portals/b2c-web/components/checkout/payment-element-wrapper.tsx` (shape: submit button + amount label) | role-match |
| `src/portals/b2b-web/components/checkout/insufficient-funds-panel.tsx` | component | — | no-analog (new; UI-SPEC §7 specifies the replacement-in-place shape) | no-analog |
| `src/portals/b2b-web/components/admin/create-sub-agent-dialog.tsx` | component | request-response | `src/portals/b2c-web/components/checkout/email-verify-gate.tsx` (dialog + form shape) | role-match |

---

## Pattern Assignments

### 1. `BookingSagaState.cs` — add `Channel` + agency pricing fields

**Analog:** `C:\Users\zhdishaq\source\repos\TBE\src\services\BookingService\BookingService.Application\Saga\BookingSagaState.cs` (self, extend in place)

**Column-family pattern to mirror** (lines 33-45 — `BaseFareAmount / SurchargeAmount / TaxAmount` block shows the established "add domain-specific fare columns with XML summaries that cite the governing plan"):

```csharp
/// <summary>
/// Fare breakdown persisted for receipt regeneration (04-01 / FLTB-03 / D-15).
/// Sourced from the GDS offer at PNR time and frozen onto the saga so the
/// PDF receipt remains auditable after ticketing. All three are in the
/// booking's <see cref="Currency"/>; their sum should equal <see cref="TotalAmount"/>.
/// </summary>
public decimal BaseFareAmount { get; set; }

/// <summary>YQ/YR carrier-imposed surcharges (kept separate from taxes per FLTB-03 / EU-UK regs).</summary>
public decimal SurchargeAmount { get; set; }

/// <summary>Government taxes and airport fees.</summary>
public decimal TaxAmount { get; set; }
```

**New columns to add** (follow same XML-summary convention, cite `05-CONTEXT D-24 / D-36 / D-37`):

- `public Channel Channel { get; set; } = Channel.B2C;` (default 0 per migration)
- `public Guid? AgencyId { get; set; }`
- `public decimal AgencyNetFare { get; set; }` / `AgencyMarkup` / `AgencyGrossFare` / `AgencyCommission` — all `decimal(18,4)`, summaries cite D-36
- `public decimal? AgencyMarkupOverride { get; set; }` — summary cites D-37 (agent-admin per-booking override)
- `public string? CustomerName { get; set; }` / `CustomerEmail` / `CustomerPhone` — summaries cite D-34 / Pattern 5

**Existing `WalletReservationTxId` (line 27)** — already present; **no schema edit needed.**

---

### 2. `BookingSaga.cs` — B2B branch on `PnrCreated`, `TicketIssued`, compensation

**Analog:** `C:\Users\zhdishaq\source\repos\TBE\src\services\BookingService\BookingService.Application\Saga\BookingSaga.cs`

**Existing B2C `PnrCreated → AuthorizePaymentCommand` block** (lines 119-139 — this is the exact junction where the B2B branch inserts):

```csharp
During(PnrCreating,
    When(PnrCreated)
        .Then(ctx =>
        {
            ctx.Saga.GdsPnr = ctx.Message.Pnr;
            ctx.Saga.TicketingDeadlineUtc = ctx.Message.TicketingDeadlineUtc;
            ctx.Saga.LastSuccessfulStep = "PnrCreated";
        })
        .Publish(ctx => new AuthorizePaymentCommand(
            ctx.Saga.CorrelationId,
            ctx.Saga.TotalAmount * 100m,
            ctx.Saga.Currency,
            ctx.Saga.UserId,
            string.Empty /* PaymentMethodId — provided by checkout flow in 03-02 */))
        .TransitionTo(Authorizing),
    When(PnrCreationFailed)
        .Unschedule(HardTimeout)
        …
```

**Replace with `IfElse` branch** (RESEARCH.md Example 1, lines 572-591 — the literal code to drop in):

```csharp
.IfElse(ctx => ctx.Saga.Channel == Channel.B2B,
    b2b => b2b
        .PublishAsync(ctx => ctx.Init<WalletReserveCommand>(new
        {
            WalletId  = ctx.Saga.WalletId!.Value,
            BookingId = ctx.Saga.CorrelationId,
            Amount    = ctx.Saga.AgencyNetFare,
            Currency  = ctx.Saga.Currency
        }))
        .TransitionTo(WalletReserving),
    b2c => b2c
        .PublishAsync(ctx => ctx.Init<AuthorizePaymentCommand>(new { … }))
        .TransitionTo(Authorizing));
```

**`TicketIssued` B2C→B2B branch** — analog pattern lines 159-170 of current saga. Add new states `WalletReserving` / `WalletCommitting`, and mirror the `CancelAuthorizationCommand` compensation (lines 151-157 and 171-179) with `WalletReleaseCommand`.

**New states to declare** (mirror line 32-34 pattern):
```csharp
public State WalletReserving { get; private set; } = null!;
public State WalletCommitting { get; private set; } = null!;
```

**New events to bind** (mirror line 41-47 + line 62-67 pattern):
```csharp
public Event<WalletReserved> WalletReserved { get; private set; } = null!;
public Event<WalletReservationFailed> WalletReservationFailed { get; private set; } = null!;
public Event<WalletCommitted> WalletCommitted { get; private set; } = null!;
// Event(() => WalletReserved, x => x.CorrelateById(m => m.Message.BookingId));
```

**Commands already shipped** — `WalletReserveCommand/Commit/Release` exist in `SagaCommands.cs` lines 52-62 (verified). No contract edit needed.

---

### 3. Migration `20260600000000_AddAgencyPricingAndChannel.cs`

**Analog:** `C:\Users\zhdishaq\source\repos\TBE\src\services\BookingService\BookingService.Infrastructure\Migrations\20260500000000_AddReceiptFareBreakdown.cs`

**Full excerpt (all 63 lines — follow this shape byte-for-byte, adding the 8 new columns):**

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.BookingService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 05-?? Task ? — persist Channel + agency pricing columns on
    /// Saga.BookingSagaState so B2B bookings carry NET/markup/GROSS/commission
    /// through the saga lifecycle. Hand-authored per 03-01 Deviation #2
    /// (ModelSnapshot not usable — no DbContext design-time factory wired).
    /// </summary>
    public partial class AddAgencyPricingAndChannel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BaseFareAmount",
                schema: "Saga",
                table: "BookingSagaState",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);
            // … (mirror AddColumn blocks for SurchargeAmount, TaxAmount)
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseFareAmount", schema: "Saga", table: "BookingSagaState");
            // …
        }
    }
}
```

**New columns required** (from RESEARCH.md Example 2 + CONTEXT D-24 / D-36 / D-37):

- `Channel int NOT NULL DEFAULT 0`
- `AgencyId uniqueidentifier NULL`
- `AgencyNetFare / AgencyMarkup / AgencyGrossFare / AgencyCommission` — all `decimal(18,4) NOT NULL DEFAULT 0m`
- `AgencyMarkupOverride decimal(18,4) NULL` (D-37)
- `CustomerName nvarchar(200) NULL` / `CustomerEmail nvarchar(256) NULL` / `CustomerPhone nvarchar(32) NULL` (Pattern 5)
- `CreateIndex("IX_BookingSagaState_AgencyId_StartedAtUtc", new[] { "AgencyId", "StartedAtUtc" })` — for agency-wide list query

**Schema name convention** — CONFIRMED `schema: "Saga"` from analog line 22 — do NOT use default `dbo`.

---

### 4. `AgentBookingsController.cs`

**Analog:** `C:\Users\zhdishaq\source\repos\TBE\src\services\BookingService\BookingService.API\Controllers\BookingsController.cs`

**Imports pattern** (lines 1-8 — mirror exactly):

```csharp
using System.Security.Claims;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TBE.BookingService.Infrastructure;
using TBE.Contracts.Events;
```

**Class declaration + primary constructor** (lines 18-26):

```csharp
[ApiController]
[Route("api/b2b/bookings")]   // DIFFERENT from B2C: explicit /api/b2b/ prefix
[Authorize(Policy = "B2BPolicy")]  // per RESEARCH Pattern 2 — not just [Authorize]
public class AgentBookingsController(
    BookingDbContext db,
    IPublishEndpoint publishEndpoint,
    ILogger<AgentBookingsController> logger) : ControllerBase
```

**Agency-wide list query pattern** (mirror `ListForCustomerAsync` at lines 109-135, but **replace** the `WHERE s.UserId == customerId` line with `WHERE s.AgencyId == agencyId` — D-34):

```csharp
[HttpGet("me")]
public async Task<IActionResult> ListForMeAsync(
    int page = 1, int size = 20, string? status = null, bool urgent = false,
    CancellationToken ct = default)
{
    // D-34 — agency-wide visibility for all three roles. NEVER add a
    // "&& s.UserId == requester" clause. The ROADMAP Phase 5 UAT line
    // about "sub-agent sees only their own" is deliberately overridden
    // per 05-CONTEXT.md D-34. Pitfall 26 still requires the agency_id
    // claim filter (below) — that's the only tenancy guard.
    var agencyClaim = User.FindFirst("agency_id")?.Value;
    if (!Guid.TryParse(agencyClaim, out var agencyId))
        return Unauthorized();

    if (page < 1) page = 1;
    if (size is < 1 or > 100) size = 20;

    var query = db.BookingSagaStates.AsNoTracking()
        .Where(s => s.AgencyId == agencyId);  // Pitfall 26 guard

    if (urgent)
        query = query.Where(s =>
            s.TicketingDeadlineUtc < DateTime.UtcNow.AddHours(24));

    var items = await query
        .OrderByDescending(s => s.InitiatedAtUtc)
        .Skip((page - 1) * size).Take(size)
        .Select(s => new AgentBookingDto(/* agency-scoped projection */))
        .ToListAsync(ct);

    return Ok(new { page, size, items });
}
```

**Booking detail (agency-scoped IDOR guard)** — mirror `GetByIdAsync` lines 65-89 but swap the `booking.UserId != userId` check for `booking.AgencyId != agencyId`:

```csharp
if (dto.AgencyId != agencyId)
    return Forbid();   // UI-SPEC §Copywriting: "You cannot view this booking."
```

**POST /api/b2b/bookings (booking-on-behalf)** — mirror `PostAsync` lines 28-63 but stamp `Channel = Channel.B2B`, `AgencyId = agencyClaim`, `CustomerName/Email/Phone = req.Customer.*` onto `BookingInitiated`. The `[FromBody]` request record adds a nested `CustomerContactDto` (see Pattern 5 in RESEARCH).

**Override enforcement (D-37)** — when `req.Override.HasValue`, require `User.IsInRole("agent-admin")` before accepting; else 403.

---

### 5. `WalletController.cs` — D-40 top-up cap check (2-line edit)

**Analog:** `C:\Users\zhdishaq\source\repos\TBE\src\services\PaymentService\PaymentService.API\Controllers\WalletController.cs`

**Existing `TopUp` handler** (lines 47-65):

```csharp
[HttpPost("{walletId:guid}/top-ups")]
[Authorize(Roles = "agency-admin")]
public async Task<IActionResult> TopUp(Guid walletId, [FromBody] TopUpRequest req, CancellationToken ct)
{
    var agencyClaim = User.FindFirst("agency_id")?.Value;
    if (!Guid.TryParse(agencyClaim, out var agencyId))
        return BadRequest(new { error = "agency_id claim missing or invalid" });

    var r = await _stripe.CreateWalletTopUpAsync(
        walletId, agencyId, req.AmountCents, req.Currency, …);
    …
}
```

**Insert** (after the `agency_id` parse, before `CreateWalletTopUpAsync`) — D-40 caps via `IOptionsMonitor<WalletOptions>` (the options class already exists in `PaymentService.Application/Wallet/WalletOptions.cs`; add `MinTopUpAmount` / `MaxTopUpAmount` properties bound to `Wallet:TopUp:MinAmount` / `Wallet:TopUp:MaxAmount` env config per Specifics line in CONTEXT):

```csharp
var caps = _walletOpts.CurrentValue;
if (req.AmountCents < caps.MinTopUpAmountCents || req.AmountCents > caps.MaxTopUpAmountCents)
{
    return BadRequest(new
    {
        type  = "https://tbe.local/problems/wallet-topup-out-of-range",
        title = "Top-up amount outside permitted range",
        status = 400,
        min = caps.MinTopUpAmountCents,
        max = caps.MaxTopUpAmountCents,
        attempted = req.AmountCents
    });
}
```

---

### 6. `AgencyMarkupRule` (entity + repo + `ApplyMarkup`)

**Analog (core calc):** `C:\Users\zhdishaq\source\repos\TBE\src\services\PricingService\PricingService.Infrastructure\Rules\MarkupRulesEngine.cs`

**Rule-resolution pattern** (lines 15-33 — but simpler for D-36's 2-row schema; no priority scoring):

```csharp
// Existing (complex) MarkupRulesEngine priority scoring — lines 26-32
.OrderByDescending(r => (r.AirlineCode != null ? 2 : 0) + (r.RouteOrigin != null ? 1 : 0))
```

**Simplified D-36 resolver** (replace scoring with `ORDER BY RouteClass DESC` so RouteClass-specific row wins, NULL falls back):

```csharp
var rule = await db.AgencyMarkupRules
    .Where(r => r.AgencyId == agencyId && r.IsActive
             && (r.RouteClass == routeClass || r.RouteClass == null))
    .OrderByDescending(r => r.RouteClass)   // non-NULL first
    .FirstOrDefaultAsync(ct);
```

**`ApplyMarkup` calc** — mirror lines 45-67 of `MarkupRulesEngine.cs` exactly, but flatten to `public static` pure function per CONTEXT Pattern 3:

```csharp
public static AgencyQuote ApplyMarkup(decimal netFare, AgencyMarkupRule rule)
{
    var markup = Math.Round(netFare * rule.PercentOfNet / 100m, 2) + rule.FlatAmount;
    var gross  = Math.Round(netFare + markup, 2);
    var commission = Math.Round(markup, 2);  // commission == markup at v1
    return new AgencyQuote(netFare, markup, gross, commission);
}
```

---

### 7. `20260600000100_AddAgencyMarkupRules.cs`

**Analog:** `C:\Users\zhdishaq\source\repos\TBE\src\services\PricingService\PricingService.Infrastructure\Migrations\20260415000000_AddMarkupRules.cs`

**Full shape** (mirror lines 10-45 exactly):

```csharp
migrationBuilder.CreateTable(
    name: "AgencyMarkupRules",
    columns: table => new
    {
        Id           = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        AgencyId     = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        FlatAmount   = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
        PercentOfNet = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
        RouteClass   = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
        IsActive     = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
    },
    constraints: table => table.PrimaryKey("PK_AgencyMarkupRules", x => x.Id));

migrationBuilder.CreateIndex(
    name: "IX_AgencyMarkupRules_AgencyId_IsActive",
    table: "AgencyMarkupRules",
    columns: new[] { "AgencyId", "IsActive" });
```

**PricingDbContext edit** — mirror existing `MarkupRule` config block (lines 24-34 of `PricingDbContext.cs`):

```csharp
modelBuilder.Entity<AgencyMarkupRule>(b =>
{
    b.HasKey(r => r.Id);
    b.Property(r => r.RouteClass).HasMaxLength(16);
    b.Property(r => r.FlatAmount).HasColumnType("decimal(18,4)");
    b.Property(r => r.PercentOfNet).HasColumnType("decimal(9,6)");
    b.HasIndex(r => new { r.AgencyId, r.IsActive });
});
```

---

### 8. `AgencyInvoiceDocument.cs` (QuestPDF generator — GROSS-only, D-43)

**Primary analog:** `C:\Users\zhdishaq\source\repos\TBE\src\services\BookingService\BookingService.Infrastructure\Pdf\QuestPdfBookingReceiptGenerator.cs`

**Full excerpt (76 lines — follow line-for-line):**

```csharp
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
// … (usings mirror; swap BookingService namespaces for NotificationService)

public sealed class AgencyInvoiceDocument : IAgencyInvoicePdfGenerator
{
    static AgencyInvoiceDocument()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        // TODO(prod): switch to LicenseType.Commercial before production launch.
    }

    public Task<byte[]> GenerateAsync(AgencyInvoiceModel m, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(m);
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(t => t.FontSize(11));

                page.Header().Text($"Invoice — {m.BookingReference}").FontSize(18).Bold();

                page.Content().Column(col =>
                {
                    col.Spacing(5);
                    col.Item().Text($"Customer: {m.CustomerName}");
                    col.Item().Text($"Issued:   {m.IssuedAtUtc:u}");
                    col.Item().PaddingVertical(6).LineHorizontal(1);

                    // D-43: GROSS-only. Base fare / YQ-YR / taxes shown per FLTB-03
                    // (Phase 3 precedent) but NET / markup / commission NEVER render.
                    col.Item().Text($"Base fare:        {m.Currency} {m.GrossBaseFare:N2}");
                    col.Item().Text($"YQ/YR surcharges: {m.Currency} {m.GrossSurcharges:N2}");
                    col.Item().Text($"Taxes:            {m.Currency} {m.GrossTaxes:N2}");
                    if (m.Vat.HasValue)
                        col.Item().Text($"VAT:              {m.Currency} {m.Vat:N2}");
                    col.Item().PaddingVertical(6).LineHorizontal(1);
                    col.Item().Text($"Total:            {m.Currency} {m.GrossTotal:N2}").SemiBold().FontSize(13);
                });

                page.Footer().AlignCenter()
                    .Text("Thank you for booking. Keep this invoice for your records.")
                    .FontSize(9);
            });
        }).GeneratePdf();
        return Task.FromResult(bytes);
    }
}
```

**Key delta from receipt generator:** `AgencyInvoiceModel` view model must NOT expose `NetFare`, `AgencyMarkup`, or `AgencyCommission` properties at all. Even if accidentally populated by the caller, the template has no binding for them. Enforce at the DTO level (CONTEXT line 152 — Specifics block).

**Secondary analog for row-block layout:** `CarVoucherDocument.cs` lines 38-65 (PaddingTop blocks for grouped sections).

---

### 9. `AgencyInvoice.cshtml` RazorLight template

**Analog:** `C:\Users\zhdishaq\source\repos\TBE\src\services\NotificationService\NotificationService.API\Templates\FlightConfirmation.cshtml`

**Full excerpt (29 lines — mirror header comment, `<!--SUBJECT:…-->`, `_Header`/`_Footer` includes, table shape):**

```cshtml
@model TBE.NotificationService.API.Templates.Models.AgencyInvoiceModel
<!--SUBJECT:Invoice — @Model.BookingReference-->
<!DOCTYPE html>
<html>
<body style="font-family:Arial,sans-serif;color:#111827;max-width:640px;margin:0 auto;">
  @{ await IncludeAsync("_Header", Model); }
  <div style="padding:24px;">
    <h1 style="color:#0a3d62;">Invoice for @Model.CustomerName</h1>
    <table style="width:100%;border-collapse:collapse;margin-top:12px;">
      <!-- GROSS-only rows — never NET, markup, or commission (D-43) -->
      <tr><td>Base fare:</td><td>@Model.GrossBaseFare.ToString("0.00") @Model.Currency</td></tr>
      <tr><td>YQ/YR surcharges:</td><td>@Model.GrossSurcharges.ToString("0.00") @Model.Currency</td></tr>
      <tr><td>Taxes:</td><td>@Model.GrossTaxes.ToString("0.00") @Model.Currency</td></tr>
      <tr><td><strong>Total:</strong></td><td><strong>@Model.GrossTotal.ToString("0.00") @Model.Currency</strong></td></tr>
    </table>
  </div>
  @{ await IncludeAsync("_Footer", Model); }
</body>
</html>
```

---

### 10. `InvoicesController.cs` (NotificationService)

**Analog:** `C:\Users\zhdishaq\source\repos\TBE\src\services\BookingService\BookingService.API\Controllers\ReceiptsController.cs`

**Full shape** (mirror lines 1-56 — swap `IBookingReceiptPdfGenerator` for `IAgencyInvoicePdfGenerator`, swap `UserId` ownership check for `AgencyId` per Pitfall 26):

```csharp
[ApiController]
[Route("api/bookings")]
[Authorize(Policy = "B2BPolicy")]
public class InvoicesController(
    IAgencyInvoicePdfGenerator pdfGen,
    IAgencyInvoiceViewModelBuilder vmBuilder,   // calls BookingService projection
    ILogger<InvoicesController> logger) : ControllerBase
{
    [HttpGet("{id:guid}/invoice.pdf")]
    public async Task<IActionResult> GetInvoiceAsync(Guid id, CancellationToken ct)
    {
        var agencyClaim = User.FindFirst("agency_id")?.Value;
        if (!Guid.TryParse(agencyClaim, out var agencyId))
            return Unauthorized();

        var vm = await vmBuilder.BuildAsync(id, ct);
        if (vm is null) return NotFound();

        if (vm.AgencyId != agencyId)
        {
            logger.LogWarning("Invoice IDOR guard booking={BookingId} requester_agency={Req} owner={Owner}",
                id, agencyId, vm.AgencyId);
            return Forbid();
        }

        var bytes = await pdfGen.GenerateAsync(vm, ct);
        return File(bytes, "application/pdf", $"invoice-{vm.BookingReference}.pdf");
    }
}
```

---

### 11. `infra/keycloak/realm-tbe-b2b.json` delta patch

**Analog:** `C:\Users\zhdishaq\source\repos\TBE\infra\keycloak\realm-tbe-b2c.json`

**Full 70-line shape — mirror byte-for-byte with these deltas:**

- `"realm": "tbe-b2b"` (was `tbe-b2c`)
- `clientId`: `tbe-agent-portal` (was `tbe-b2c`) — matches existing base realm
- `redirectUris`: `http://localhost:3001/*` (port 3001 per CONTEXT D-22 / Pattern 1)
- **Add** `agency-id-attribute` protocolMapper (not in b2c):

```jsonc
{
  "_comment": "D-23 — emit agency_id user-attribute as JWT claim.",
  "name": "agency-id-attribute",
  "protocolMapper": "oidc-usermodel-attribute-mapper",
  "config": {
    "user.attribute": "agency_id",
    "claim.name": "agency_id",
    "access.token.claim": "true",
    "id.token.claim": "true"
  }
}
```

- Roles section (line 56-59 of b2c analog): `agent`, `agent-admin`, `agent-readonly` (D-32 / D-33)
- Service-account client: `tbe-b2b-admin` with `realm-management: [manage-users, view-users]` (mirror `tbe-b2c-admin` block lines 42-54)
- `secret`: `${KEYCLOAK_B2B_ADMIN_CLIENT_SECRET}`

**`_meta` block** — update `"phase": "05-b2b-agent-portal"`, `"plan": "05-??"`.

---

### 12. `src/portals/b2b-web/lib/keycloak-b2b-admin.ts`

**Analog:** `C:\Users\zhdishaq\source\repos\TBE\src\portals\b2c-web\lib\keycloak-admin.ts`

**Runtime guard + token cache (lines 21-85 — mirror verbatim, swap env var names):**

```ts
if (typeof window !== 'undefined') {
  throw new Error(
    'lib/keycloak-b2b-admin.ts imported into a Client Component — never do that',
  );
}

interface CachedToken {
  accessToken: string;
  expiresAtMs: number;
}
let cached: CachedToken | null = null;

export async function getServiceAccountToken(): Promise<string> {
  const now = Date.now();
  if (cached && cached.expiresAtMs > now + 30_000) return cached.accessToken;

  const issuer       = requireEnv('KEYCLOAK_B2B_ISSUER');
  const clientId     = requireEnv('KEYCLOAK_B2B_ADMIN_CLIENT_ID');
  const clientSecret = requireEnv('KEYCLOAK_B2B_ADMIN_CLIENT_SECRET');

  const response = await fetch(`${issuer}/protocol/openid-connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'client_credentials',
      client_id: clientId,
      client_secret: clientSecret,
    }),
    cache: 'no-store',
  });
  // … (mirror error handling from lines 63-85 exactly — NEVER log body)
}
```

**`adminApiBase()` helper (lines 125-137)** — verbatim, swap `KEYCLOAK_B2C_ISSUER` → `KEYCLOAK_B2B_ISSUER`.

**New: `createSubAgent()` function** (no direct analog — `sendVerifyEmail` lines 92-113 is the closest call-shape, but the method is POST + JSON body). Pitfall 28 is the hard rule — `agencyId` arg always comes from the **route handler's** `session.user.agency_id`, never the request body:

```ts
/**
 * Create a sub-agent under the calling admin's agency.
 * Pitfall 28: `agencyId` is INJECTED by the caller from the admin's
 * session. The HTTP route handler that invokes this function MUST read
 * `agency_id` from `auth()` session — never from the request body.
 */
export async function createSubAgent(params: {
  agencyId: string;
  email: string;
  firstName: string;
  lastName: string;
  role: 'agent' | 'agent-readonly';
}): Promise<void> {
  const token = await getServiceAccountToken();
  const response = await fetch(
    `${adminApiBase()}/users`,
    {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        username: params.email,
        email: params.email,
        firstName: params.firstName,
        lastName: params.lastName,
        enabled: true,
        emailVerified: false,
        attributes: { agency_id: [params.agencyId] },  // Pitfall 28
        realmRoles: [params.role],
      }),
      cache: 'no-store',
    },
  );
  if (!response.ok) {
    // NEVER echo body — Keycloak sometimes reflects the request, which
    // contains the email address (PII-lite).
    throw new Error(`createSubAgent returned ${response.status}`);
  }
}
```

---

### 13. Route handler `app/api/agents/route.ts`

**Analog:** `C:\Users\zhdishaq\source\repos\TBE\src\portals\b2c-web\app\api\auth\resend-verification\route.ts`

**Full 33-line shape — mirror header comment + `runtime = 'nodejs'` + session gate + typed error handling:**

```ts
// Sub-agent create — B2B-10.
//
// Pitfall 28: server-side `agency_id` injection. The caller never sees
// the claim value; we read it from the admin's session and pass it to
// `createSubAgent`. Request body is { email, firstName, lastName, role } —
// agency_id is NEVER accepted from the body.
//
// Runtime: Node only. Never Edge — keycloak-b2b-admin.ts uses Node-only
// in-process caching.

import { auth } from '@/lib/auth';
import { createSubAgent } from '@/lib/keycloak-b2b-admin';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function POST(req: Request) {
  const session = await auth();
  // D-33 / D-34 — role gate; only agent-admin may create sub-agents.
  if (!session?.user?.agency_id || !session.roles?.includes('agent-admin')) {
    return new Response(null, { status: 403 });
  }

  const body = (await req.json()) as {
    email: string;
    firstName: string;
    lastName: string;
    role: 'agent' | 'agent-readonly';
  };
  // Pitfall 28 — agency_id from session, never body.
  try {
    await createSubAgent({
      agencyId: session.user.agency_id,
      email: body.email,
      firstName: body.firstName,
      lastName: body.lastName,
      role: body.role,
    });
  } catch {
    return new Response(null, { status: 502 });
  }
  return new Response(null, { status: 202 });
}
```

---

### 14. Invoice PDF stream-through proxy

**Analog:** `C:\Users\zhdishaq\source\repos\TBE\src\portals\b2c-web\app\api\bookings\[id]\receipt.pdf\route.ts`

**Full 54-line shape — mirror verbatim** (Pitfall 11 awaited `params`, Pitfall 14 `new Response(upstream.body, …)`). Only change: upstream path `/api/bookings/${id}/invoice.pdf` and default filename `invoice-{id}.pdf`.

```ts
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function GET(_request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  let upstream: Response;
  try {
    upstream = await gatewayFetch(`/api/bookings/${encodeURIComponent(id)}/invoice.pdf`);
  } catch (err) {
    if (err instanceof UnauthenticatedError) return new Response(null, { status: 401 });
    throw err;
  }
  if (!upstream.ok) return new Response(null, { status: upstream.status });
  // Pitfall 14 — stream through, never buffer.
  return new Response(upstream.body, {
    headers: {
      'content-type': upstream.headers.get('content-type') ?? 'application/pdf',
      'content-disposition':
        upstream.headers.get('content-disposition') ?? `attachment; filename="invoice-${id}.pdf"`,
    },
  });
}
```

**E-ticket proxy (`e-ticket.pdf/route.ts`)** — identical shape; secondary analog `app/api/hotels/[offerId]/voucher.pdf/route.ts` (which also proxies a Notification-originated PDF through BookingService, exactly the flow B2B e-ticket uses).

---

### 15. `b2b-web/auth.config.ts` — edge-safe config

**Analog:** `C:\Users\zhdishaq\source\repos\TBE\src\portals\b2c-web\auth.config.ts`

**Full 43-line shape — mirror verbatim with 2-line delta:**

```ts
// Line 18: clientId rename
clientId:     process.env.KEYCLOAK_B2B_CLIENT_ID!,
clientSecret: process.env.KEYCLOAK_B2B_CLIENT_SECRET!,
issuer:       process.env.KEYCLOAK_B2B_ISSUER!,

// Lines 30-36: protected-path list update
const isProtected =
  pathname.startsWith('/dashboard') ||
  pathname.startsWith('/bookings')  ||
  pathname.startsWith('/checkout')  ||
  pathname.startsWith('/admin')     ||
  pathname.startsWith('/search');
```

---

### 16. `b2b-web/middleware.ts` — edge gate

**Analog:** `C:\Users\zhdishaq\source\repos\TBE\src\portals\b2c-web\middleware.ts`

**Full 51-line shape — mirror verbatim; replace the `email_verified` B2C gate (lines 33-40) with an `/admin/*` role gate:**

```ts
// D-32 / B2BAdminPolicy — /admin/* is agent-admin only. Non-admin agents
// bounce back to /dashboard (never 403 — softer UX per UI-SPEC).
if (pathname.startsWith('/admin') && !session.roles?.includes('agent-admin')) {
  const url = req.nextUrl.clone();
  url.pathname = '/dashboard';
  return Response.redirect(url);
}

// matcher (line 46):
matcher: ['/dashboard/:path*', '/bookings/:path*', '/checkout/:path*', '/admin/:path*', '/search/:path*'],
```

---

### 17. `b2b-web/next.config.mjs` — CSP per-route

**Analog:** `C:\Users\zhdishaq\source\repos\TBE\src\portals\b2c-web\next.config.mjs`

**Full 39-line shape** — mirror but **tighten Stripe whitelist to `/admin/wallet/*` only** (UI-SPEC Interaction Contract §Checkout Debit: "`/checkout/confirm` contains no `<Elements>` mount"):

```js
// Two CSP strings:
const walletCsp = [   // /admin/wallet/* — allows Stripe.js
  "default-src 'self'",
  "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://js.stripe.com",
  "frame-src https://js.stripe.com https://hooks.stripe.com",
  "connect-src 'self' https://api.stripe.com https://js.stripe.com",
  // …
].join('; ');

const standardCsp = [   // every other route — NO Stripe whitelist
  "default-src 'self'",
  "script-src 'self' 'unsafe-inline'",
  "connect-src 'self'",
  // …
].join('; ');

async headers() {
  return [
    { source: '/admin/wallet/:path*', headers: [{ key: 'Content-Security-Policy', value: walletCsp }, /* … */] },
    { source: '/:path*',               headers: [{ key: 'Content-Security-Policy', value: standardCsp }, /* … */] },
  ];
}
```

---

### 18. Dual-pricing row component

**Analog:** `C:\Users\zhdishaq\source\repos\TBE\src\portals\b2c-web\components\results\flight-card.tsx`

**Existing selected-state ring pattern** (line 69 — adapt `blue-600` → `indigo-600` for B2B):

```tsx
className={cn(
  'flex flex-col gap-3 rounded-lg border bg-background p-4 transition-shadow',
  selected
    ? 'border-border border-l-4 border-l-indigo-600 ring-1 ring-indigo-600/30'  // B2B delta
    : 'border-border hover:shadow-md',
  className,
)}
```

**Money formatter pattern** (lines 28-38 — reuse verbatim; the B2C helper already uses `Intl.NumberFormat` + graceful fallback).

**Add 4-column pricing row** — UI-SPEC §5 defines the column layout (Net muted-tint / Markup `--background` / Gross semibold / Commission `green-700` text). Mount `class="tabular-nums"` on every numeric cell per UI-SPEC §Typography.

---

### 19. Wallet chip (client component with polling)

**Reference source:** RESEARCH.md Example 3, lines 654-663 (not a file on disk — it's the research stub):

```tsx
export function WalletBalanceBadge({ walletId }: { walletId: string }) {
  const { data } = useQuery({
    queryKey: ['wallet-balance', walletId],
    queryFn: () => gatewayFetch(`/api/wallets/${walletId}/balance`).then(r => r.json()),
    refetchInterval: 30_000,
    staleTime: 25_000,
  });
  return <span>{data?.balance?.toFixed(2)} {data?.currency}</span>;
}
```

**RSC server-render pattern** (for no-flicker initial load) — mirror `src/portals/b2c-web/app/bookings/page.tsx` lines 24-48 (RSC `auth()` + `gatewayFetch` + pass initial value to client component):

```tsx
// src/portals/b2b-web/app/(portal)/layout.tsx
const session = await auth();
if (!session) redirect('/login');

let initialBalance: number | null = null;
try {
  const res = await gatewayFetch(`/api/wallets/${session.user.walletId}/balance`);
  if (res.ok) initialBalance = (await res.json()).balance;
} catch { /* gateway logged */ }

return (
  <>
    <Header initialBalance={initialBalance} walletId={session.user.walletId} />
    {children}
  </>
);
```

---

## Shared Patterns

### Authentication (all B2B API routes)

**Source:** `C:\Users\zhdishaq\source\repos\TBE\src\services\PaymentService\PaymentService.API\Controllers\WalletController.cs` lines 47-56
**Apply to:** `AgentBookingsController`, `InvoicesController`, any new `/api/b2b/*` endpoint.

```csharp
[Authorize(Policy = "B2BPolicy")]   // research Pattern 2
// Or for admin-only endpoints:
[Authorize(Policy = "B2BAdminPolicy")]

// Read agency_id from claim — NEVER from request body / query string (Pitfall 28 / 26):
var agencyClaim = User.FindFirst("agency_id")?.Value;
if (!Guid.TryParse(agencyClaim, out var agencyId))
    return Unauthorized();   // missing claim → 401, not 403
```

---

### Agency tenancy filter (Pitfall 26)

**Source:** `C:\Users\zhdishaq\source\repos\TBE\src\services\BookingService\BookingService.API\Controllers\BookingsController.cs` lines 122-132 (per-user pattern to adapt).
**Apply to:** every `WHERE` in `AgentBookingsController`, `InvoicesController.BuildAsync`, and any future B2B query.

```csharp
// REQUIRED on every B2B repo query. No opt-out. Null-check returns 401, not 403.
.Where(s => s.AgencyId == agencyId)
```

**Inversion also locked (D-34):** NEVER add `&& s.UserId == requester`. A comment citing D-34 must appear above every agency-scoped query so a future reader doesn't "fix" it.

---

### QuestPDF generator scaffold

**Source:** `C:\Users\zhdishaq\source\repos\TBE\src\services\BookingService\BookingService.Infrastructure\Pdf\QuestPdfBookingReceiptGenerator.cs` (static ctor + Document.Create shape — lines 17-72).
**Apply to:** `AgencyInvoiceDocument.cs`.

```csharp
static XxxDocument()
{
    QuestPDF.Settings.License = LicenseType.Community;
    // TODO(prod): switch to LicenseType.Commercial before production launch.
}
```

---

### Stream-through PDF proxy (Pitfall 11 + 14)

**Source:** `C:\Users\zhdishaq\source\repos\TBE\src\portals\b2c-web\app\api\bookings\[id]\receipt.pdf\route.ts` (full 54-line file).
**Apply to:** `b2b-web/app/api/bookings/[id]/invoice.pdf/route.ts`, `b2b-web/app/api/bookings/[id]/e-ticket.pdf/route.ts`.

Key non-negotiables:
- `export const runtime = 'nodejs'`
- `export const dynamic = 'force-dynamic'`
- `const { id } = await params;` — awaited Promise (Pitfall 11)
- `return new Response(upstream.body, { … })` — **never** `await upstream.arrayBuffer()` (Pitfall 14)
- `UnauthenticatedError` → 401; non-OK upstream → pass-through status

---

### Keycloak admin token cache (30s-skew, Node-only)

**Source:** `C:\Users\zhdishaq\source\repos\TBE\src\portals\b2c-web\lib\keycloak-admin.ts` lines 21-85.
**Apply to:** `b2b-web/lib/keycloak-b2b-admin.ts`.

Verbatim rules:
1. Top-of-file runtime guard `if (typeof window !== 'undefined') throw new Error(…)`
2. Module-scope `cached: CachedToken | null = null` (one cache per Node process)
3. 30s skew: `cached.expiresAtMs > now + 30_000`
4. Never log error body (Keycloak can echo secret back)
5. `requireEnv()` helper — fail-loud on missing env

---

### Memoised `loadStripe` (Pitfall 5)

**Source:** `C:\Users\zhdishaq\source\repos\TBE\src\portals\b2c-web\lib\stripe.ts` (full 26 lines).
**Apply to:** `b2b-web/lib/stripe.ts` — **verbatim copy**, only mounted under `/admin/wallet/*`.

```ts
let _p: Promise<Stripe | null> | undefined;
export const getStripe = (): Promise<Stripe | null> =>
  (_p ??= loadStripe(process.env.NEXT_PUBLIC_STRIPE_PK!));
```

**Critical:** never import from any `b2b-web/app/checkout/**` file. CSP in `next.config.mjs` enforces at the browser edge too.

---

### Email idempotency (for any future notification consumer)

**Source:** `C:\Users\zhdishaq\source\repos\TBE\src\services\NotificationService\NotificationService.Application\Consumers\TicketingDeadlineApproachingConsumer.cs` lines 45-62.
**Apply to:** any new consumer in Phase 5 (none planned per RESEARCH — `WalletLowBalanceConsumer` + `TicketingDeadlineApproachingConsumer` already ship). If a Phase 5 plan does add one, follow:

```csharp
var idemp = new EmailIdempotencyLog { EventId = eventId, EmailType = …, … };
_db.EmailIdempotencyLogs.Add(idemp);
try { await _db.SaveChangesAsync(ct); }
catch (DbUpdateException ex) when (IdempotencyHelpers.IsUniqueViolation(ex))
{
    _log.LogInformation("NOTF-06: duplicate … skipped");
    return;
}
```

---

### Booking-on-behalf checkout-ref reuse

**Source:** `C:\Users\zhdishaq\source\repos\TBE\src\portals\b2c-web\lib\checkout-ref.ts` (full 45 lines — verbatim reuse).
**Apply to:** `b2b-web/lib/checkout-ref.ts` + both B2B checkout pages.

The regex at line 34 already allows `flight|hotel|basket|car` — covers B2B's POST `/api/b2b/bookings` body with `offerId` derived from `ref.id`. No contract change.

---

### Saga command idempotency keys

**Source:** `C:\Users\zhdishaq\source\repos\TBE\src\services\PaymentService\PaymentService.Infrastructure\Wallet\WalletRepository.cs` line 36 — `"booking-{bookingId}-reserve"` pattern.
**Apply to:** every new `WalletReserveCommand` / `WalletCommitCommand` / `WalletReleaseCommand` publish from the saga — already handled by `WalletRepository.ReserveAsync` (no new code needed; saga just passes through).

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `src/portals/b2b-web/components/layout/agent-portal-badge.tsx` | component | — | Entirely new UI element — 1px indigo-600 outline pill per UI-SPEC §Portal Differentiation. No existing badge component to copy. Build from `cn()` + Tailwind directly per UI-SPEC §14. |
| `src/portals/b2b-web/components/checkout/insufficient-funds-panel.tsx` | component | — | UI-SPEC §7 mandates a **replacement-in-place** render (Confirm CTA removed from DOM, not disabled) — no existing "blocking panel" component. Build fresh from Radix `Alert` primitive per UI-SPEC §15. |
| `src/services/PricingService/PricingService.Application/Consumers/AgencyPriceRequestedConsumer.cs` | consumer | event-driven | No pricing-service consumer exists yet (PricingService is HTTP-only today). `WalletReserveConsumer.cs` is the shape-match for MassTransit consumer scaffold (ctor injection + try/catch publish-reply), but the PricingService has no equivalent in tree. Planner should reference RESEARCH.md Pattern 3 as the contract + `WalletReserveConsumer` as the ceremony template. |

---

## Metadata

**Analog search scope:**
- `src/portals/b2c-web/**` — RSC pages, route handlers, lib utilities, middleware, CSP
- `src/services/BookingService/**` — saga, state, migrations, controllers, PDF generator
- `src/services/PaymentService/**` — wallet controller, repository, consumers
- `src/services/PricingService/**` — existing markup engine + migration + DbContext
- `src/services/NotificationService/**` — PDF docs (Car, Hotel), Razor templates, consumers
- `src/shared/TBE.Contracts/**` — saga commands, events, enums
- `infra/keycloak/**` — delta patch + verify-audience smoke script

**Files scanned:** ~55 C# source files, ~30 TypeScript files, 2 JSON realm patches, 11 Razor templates.

**Pattern extraction date:** 2026-04-17

**Cross-references verified against:**
- `.planning/phases/04-b2c-portal-customer-facing/04-CONTEXT.md` — D-01..D-19 locks (portal fork, Auth.js split, gatewayFetch, checkout-ref, Pitfall 17)
- `.planning/phases/03-core-flight-booking-saga-b2c/03-CONTEXT.md` — D-14 append-only, D-15 SQL hints, D-17/D-18 RazorLight + QuestPDF, D-19 email idempotency
- `.planning/research/PITFALLS.md` — Pitfalls 11, 14, 17 (inherited); 19-28 introduced in this phase
