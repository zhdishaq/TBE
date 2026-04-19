# Phase 6: Backoffice & CRM - Pattern Map

**Mapped:** 2026-04-19
**Files analyzed:** 62 new / modified files (4 plans, 2 new services + 1 new portal + 2 service extensions)
**Analogs found:** 58 / 62 (4 files have NO in-repo analog — flagged below)

CONTEXT D-45 locks 4 plans + 18 requirements. RESEARCH §Recommended Project Structure enumerates every file. Every pattern in this phase already has a Phase 1-5 precedent — the three genuine gaps are:
1. **SQL Server role-grant DENY for BookingEvents** (new enforcement pattern; partial precedent in `payment.WalletTransactions` append-only convention)
2. **MassTransit `_error` queue consumer for DLQ surfacing** (no in-repo consumer tails `_error` queues today; closest analog is `SagaDeadLetterSink` which consumes a typed event not a raw envelope)
3. **ClosedXML multi-sheet workbook export** (library is net-new to Wave 0; no Excel export exists anywhere in the repo)

The 4th "new pattern" is the CRM projection-rebuild endpoint; no projection store currently exists.

---

## File Classification

### Plan 1 — Unified booking management + audit + DLQ + 4-eyes (BO-01/03/04/05/09/10 + D-39)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/services/BackofficeService/BackofficeService.API/Program.cs` | service-bootstrap | startup | `src/services/PaymentService/PaymentService.API/Program.cs` | role-match (PaymentService adds JWT + AuthZ + consumers; Backoffice skeleton currently lacks those) |
| `src/services/BackofficeService/BackofficeService.Application/Consumers/ErrorQueueConsumer.cs` | consumer | event-driven (raw JSON envelope) | `src/services/BookingService/BookingService.Application/Consumers/CompensationConsumers/SagaDeadLetterSink.cs` | role-match (SagaDeadLetterSink consumes a typed event; ErrorQueueConsumer needs raw JSON — NEW `UseRawJsonDeserializer` wiring) |
| `src/services/BackofficeService/BackofficeService.Application/Controllers/DlqController.cs` | controller | request-response (list/requeue/resolve) | `src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs` (B2BWalletController) | exact (same `[Authorize(Policy=...)]` + `ControllerBase` + problem+json shape) |
| `src/services/BackofficeService/BackofficeService.Application/Controllers/BookingsController.cs` (unified list / BO-01) | controller | CRUD (read-heavy) | `src/services/BookingService/BookingService.API/Controllers/AgentBookingsController.cs` | exact (agency-filtered list + server-side paging + 404-not-403 pitfall) |
| `src/services/BackofficeService/BackofficeService.Application/Controllers/FourEyesController.cs` | controller | CRUD + workflow | `src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs` (B2BWalletController.UpdateThresholdAsync) | role-match (problem+json + role policy + `preferred_username` extraction is new) |
| `src/services/BackofficeService/BackofficeService.Infrastructure/BackofficeDbContext.cs` (replace skeleton) | infrastructure | EF Core DbSets + outbox | `src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs` | exact |
| `src/services/BackofficeService/BackofficeService.Infrastructure/Migrations/202606xx_CreateDeadLetterQueue.cs` | migration | schema | `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260525000000_AddAgencyWallet.cs` | exact (schema + single table + index + defaults) |
| `src/services/BookingService/BookingService.Infrastructure/Migrations/202606xx_AddBookingEventsTable.cs` | migration | schema | `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260417000000_AddWalletAndStripe.cs` (append-only wallet ledger) | role-match (tables + PK + indexes; DENY/GRANT roles are NEW) |
| `src/services/BookingService/BookingService.Infrastructure/Migrations/202606xx_AddAppendOnlyRoleGrants.cs` | migration | DB role grants | **NO ANALOG** | new pattern (raw SQL `CREATE ROLE` + `GRANT/DENY`) |
| `src/services/BookingService/BookingService.Infrastructure/Migrations/202606xx_AddBookingChannelManual.cs` | migration | enum extension | `src/services/BookingService/BookingService.Infrastructure/Migrations/20260520000000_AddB2BBookingColumns.cs` | exact (enum persisted as int — no data change needed) |
| `src/services/BookingService/BookingService.Infrastructure/Migrations/202606xx_AddCancellationColumns.cs` | migration | schema extension | `src/services/BookingService/BookingService.Infrastructure/Migrations/20260520000000_AddB2BBookingColumns.cs` | exact |
| `src/services/BookingService/BookingService.Infrastructure/BookingEventsDbContext.cs` | infrastructure | EF Core (writer-only) | `src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs` | role-match (second DbContext + separate connection to avoid ChangeTracker-vs-DENY conflict — Pitfall 1) |
| `src/services/BookingService/BookingService.Application/Saga/BookingEvent.cs` | domain | value-object | `src/services/BookingService/BookingService.Application/Saga/SagaDeadLetter.cs` | exact (immutable ledger row entity) |
| `src/services/BookingService/BookingService.Application/BookingEventsWriter.cs` | service | write-only (INSERT) | `src/services/BookingService/BookingService.Infrastructure/SagaDeadLetterStore.cs` | exact |
| `src/services/BookingService/BookingService.Application/Saga/BookingChannel.cs` (enum extend) | domain | enum | `TBE.Contracts.Enums/Channel.cs` | exact |
| `src/services/PaymentService/PaymentService.Infrastructure/Migrations/202606xx_AddWalletCreditRequests.cs` | migration | schema | `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260525000000_AddAgencyWallet.cs` | exact |
| `src/services/PaymentService/PaymentService.Application/Wallet/WalletCreditRequestWorkflow.cs` | service | state machine (simple) | `src/services/PaymentService/PaymentService.Application/Wallet/WalletTopUpService.cs` | role-match (D-40 range-validation pattern repurposed for D-53 reason-code enum) |
| `src/portals/backoffice-web/` (all files — forked) | portal-scaffold | — | `src/portals/b2b-web/` | exact |
| `src/portals/backoffice-web/auth.config.ts` | auth-edge-config | — | `src/portals/b2b-web/auth.config.ts` | exact |
| `src/portals/backoffice-web/lib/auth.ts` | auth-node-config | — | `src/portals/b2b-web/lib/auth.ts` | exact |
| `src/portals/backoffice-web/middleware.ts` | edge-middleware | request filter | `src/portals/b2b-web/middleware.ts` | exact |
| `src/portals/backoffice-web/next.config.mjs` | config | Next.js config | `src/portals/b2b-web/next.config.mjs` | exact (remove Stripe CSP block — backoffice has no wallet top-up surface) |
| `src/portals/backoffice-web/lib/api-client.ts` | helper | server-side fetch | `src/portals/b2b-web/lib/api-client.ts` | exact |
| `src/portals/backoffice-web/lib/rbac.ts` | helper | role predicate | **NO direct analog** — compose helpers around b2b-web session shape | new pattern (4 role predicates: `isOpsAdmin`, `isOpsCs`, `isOpsFinance`, `isOpsRead`) |
| `src/portals/backoffice-web/components/BackofficePortalBadge.tsx` | component | presentation | `src/portals/b2b-web/components/layout/agent-portal-badge.tsx` | exact (swap indigo-600 → slate-900) |
| `src/portals/backoffice-web/components/FourEyesApprovalBadge.tsx` | component | presentation | `src/portals/b2b-web/components/layout/agent-portal-badge.tsx` | role-match (amber chip variant — no exact predecessor) |
| `src/portals/backoffice-web/app/(portal)/layout.tsx` | layout | RSC auth gate | `src/portals/b2b-web/app/(portal)/layout.tsx` | exact |
| `src/portals/backoffice-web/app/(portal)/bookings/page.tsx` (BO-01) | page | RSC list | `src/portals/b2b-web/app/(portal)/bookings/page.tsx` | exact |
| `src/portals/backoffice-web/app/(portal)/bookings/[id]/page.tsx` (detail + events) | page | RSC detail | `src/portals/b2b-web/app/(portal)/bookings/[id]/page.tsx` | exact |
| `src/portals/backoffice-web/app/(portal)/dlq/page.tsx` (BO-09/10) | page | RSC list + polling | `src/portals/b2b-web/app/(portal)/admin/wallet/page.tsx` (transactions-table) | role-match (table + 30s TanStack Query poll) |
| `src/portals/backoffice-web/app/(portal)/approvals/page.tsx` (4-eyes queue) | page | RSC list | `src/portals/b2b-web/app/(portal)/admin/agents/page.tsx` | role-match |
| `infra/keycloak/realms/tbe-backoffice-realm.json` (REWRITE — wrong role names) | config | realm delta | `infra/keycloak/realm-tbe-b2b.json` | exact (copy mappers + client structure; swap 3 role names for 4) |

### Plan 2 — Manual booking + supplier contracts + payment reconciliation (BO-02/06/07)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/services/BookingService/BookingService.Application/ManualBookingCommand.cs` | command-handler | one-shot insert | `src/services/BookingService/BookingService.API/Controllers/BookingsController.cs` (PostAsync) | role-match (saga-bypass: insert `BookingSagaState` with `Channel=Manual, Status=Confirmed` directly, no `BookingInitiated` publish) |
| `src/services/BookingService/BookingService.API/Controllers/ManualBookingsController.cs` | controller | CRUD | `src/services/BookingService/BookingService.API/Controllers/BookingsController.cs` | exact |
| `src/services/BackofficeService/BackofficeService.Application/Controllers/SupplierContractsController.cs` (BO-07) | controller | CRUD | `src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs` (B2BWalletController) | exact |
| `src/services/BackofficeService/BackofficeService.Infrastructure/Migrations/202606xx_CreateSupplierContracts.cs` | migration | schema | `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260525000000_AddAgencyWallet.cs` | exact |
| `src/services/PaymentService/PaymentService.Infrastructure/Migrations/202606xx_ExtendStripeEventsWithRawPayload.cs` | migration | schema extension | `src/services/BookingService/BookingService.Infrastructure/Migrations/20260520000000_AddB2BBookingColumns.cs` | exact |
| `src/services/PaymentService/PaymentService.Infrastructure/Migrations/202606xx_AddReconciliationQueue.cs` | migration | schema | `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260525000000_AddAgencyWallet.cs` | exact |
| `src/services/PaymentService/PaymentService.Application/HostedServices/ReconciliationJob.cs` (BO-06 nightly diff) | hosted-service | cron/batch | `src/services/BookingService/BookingService.Infrastructure/Ttl/TtlMonitorHostedService.cs` | exact |
| Modify `src/services/PaymentService/PaymentService.API/Controllers/StripeWebhookController.cs` (D-55 persist full payload) | controller | extend | — | self-modification (add `RawPayload` persist; keep dedup + publish) |
| `src/portals/backoffice-web/app/(portal)/bookings/new/page.tsx` (BO-02) | page | form | `src/portals/b2b-web/app/(portal)/admin/agents/page.tsx` + its dialogs | role-match (react-hook-form + zod + POST to new endpoint) |
| `src/portals/backoffice-web/app/(portal)/contracts/page.tsx` (BO-07) | page | list | `src/portals/b2b-web/app/(portal)/admin/agents/page.tsx` | exact |
| `src/portals/backoffice-web/app/(portal)/reconciliation/page.tsx` (BO-06) | page | RSC list + polling | `src/portals/b2b-web/app/(portal)/admin/wallet/page.tsx` (transactions-table) | exact |

### Plan 3 — MIS reporting + markup CRUD + commission payout (BO-08 + D-38 + D-41)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/services/BackofficeService/BackofficeService.Application/HostedServices/MisDailyAggregateJob.cs` (D-60) | hosted-service | cron/batch | `src/services/BookingService/BookingService.Infrastructure/Ttl/TtlMonitorHostedService.cs` | exact |
| `src/services/BackofficeService/BackofficeService.Application/Exporters/MisExcelExporter.cs` | service | transform/stream | **NO ANALOG** (no Excel/ClosedXML usage anywhere in repo) | new pattern |
| `src/services/BackofficeService/BackofficeService.Application/Exporters/MisCsvExporter.cs` | service | transform/stream | **NO ANALOG** (no CSV exporter anywhere in repo) | new pattern |
| `src/services/BackofficeService/BackofficeService.Application/Controllers/MisController.cs` | controller | file-I/O (response stream) | `src/services/BookingService/BookingService.API/Controllers/InvoicesController.cs` (`File(bytes, "application/pdf", ...)`) | exact (same `File(...)` return shape; swap MIME + filename) |
| `src/services/BackofficeService/BackofficeService.Infrastructure/Migrations/202606xx_CreateMisDailyAggregates.cs` | migration | schema | `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260525000000_AddAgencyWallet.cs` | exact |
| `src/services/PricingService/.../MarkupRulesController.cs` (D-38, new or modify existing) | controller | CRUD | `src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs` (B2BWalletController — hard-bounded validation idiom) | exact (range-guard → problem+json) |
| `src/services/PricingService/.../Migrations/202606xx_AddMarkupRuleAuditLog.cs` | migration | schema | `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260525000000_AddAgencyWallet.cs` | exact |
| `src/services/PaymentService/PaymentService.Infrastructure/Migrations/202606xx_AddCommissionAccruals.cs` | migration | schema | `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260525000000_AddAgencyWallet.cs` | exact |
| `src/services/PaymentService/PaymentService.Application/HostedServices/CommissionAccrualJob.cs` | hosted-service | cron aggregator | `src/services/BookingService/BookingService.Infrastructure/Ttl/TtlMonitorHostedService.cs` | exact |
| `src/services/PaymentService/PaymentService.Application/HostedServices/MonthlyStatementJob.cs` | hosted-service | cron (monthly) | `src/services/BookingService/BookingService.Infrastructure/Ttl/TtlMonitorHostedService.cs` | role-match (Cronos for DST — new NuGet per research) |
| `src/services/PaymentService/PaymentService.Infrastructure/Pdf/QuestPdfAgencyStatementGenerator.cs` (D-54) | pdf-generator | file-I/O | `src/services/BookingService/BookingService.Infrastructure/Pdf/QuestPdfAgencyInvoiceGenerator.cs` | exact |
| `src/services/PaymentService/PaymentService.Application/Pdf/IAgencyStatementPdfGenerator.cs` | interface | DI | `src/services/BookingService/BookingService.Application/Pdf/IAgencyInvoicePdfGenerator.cs` | exact |
| `src/portals/backoffice-web/app/(portal)/mis/page.tsx` (BO-08) | page | RSC + charts + export | `src/portals/b2b-web/app/(portal)/admin/wallet/page.tsx` | role-match (ApexCharts + date-range + CSV/XLSX download buttons) |
| `src/portals/backoffice-web/app/(portal)/markup/page.tsx` (D-38) | page | form | `src/portals/b2b-web/app/(portal)/admin/agents/page.tsx` | exact |
| `src/portals/backoffice-web/app/(portal)/payouts/page.tsx` (D-41) | page | list + approval dialog | `src/portals/b2b-web/app/(portal)/admin/agents/page.tsx` | exact |

### Plan 4 — CRM service + GDPR (CRM-01..05 + COMP-03)

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/services/CrmService/CrmService.API/Program.cs` | service-bootstrap | startup | `src/services/PaymentService/PaymentService.API/Program.cs` | role-match (PaymentService shows the JWT + policy + consumer registration template; current CrmService skeleton lacks all of that) |
| `src/services/CrmService/CrmService.Application/Consumers/BookingConfirmedConsumer.cs` | consumer | event-driven (projection upsert) | `src/services/PaymentService/PaymentService.Application/Consumers/StripeWebhookConsumer.cs` | role-match (StripeWebhookConsumer republishes; BookingConfirmedConsumer must upsert projection + inbox dedup — a new pattern for the repo) |
| `src/services/CrmService/CrmService.Application/Consumers/BookingCancelledConsumer.cs` | consumer | event-driven | same | role-match |
| `src/services/CrmService/CrmService.Application/Consumers/UserRegisteredConsumer.cs` | consumer | event-driven | same | role-match |
| `src/services/CrmService/CrmService.Application/Consumers/WalletTopUpConsumer.cs` | consumer | event-driven | same | role-match |
| `src/services/CrmService/CrmService.Application/Consumers/TicketIssuedConsumer.cs` | consumer | event-driven | same | role-match |
| `src/services/CrmService/CrmService.Application/Consumers/CustomerCommunicationLoggedConsumer.cs` | consumer | event-driven | same | role-match |
| `src/services/CrmService/CrmService.Application/Consumers/CustomerErasureRequestedConsumer.cs` | consumer | event-driven (cross-service NULL PII) | same | role-match |
| `src/services/CrmService/CrmService.Infrastructure/CrmDbContext.cs` | infrastructure | EF Core | `src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs` | exact |
| `src/services/CrmService/CrmService.Infrastructure/Migrations/202606xx_CreateCrmProjections.cs` | migration | schema (multiple tables) | `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260417000000_AddWalletAndStripe.cs` | exact (multi-table migration + indexes) |
| `src/services/CrmService/CrmService.Application/Projections/CustomerProjection.cs` + `BookingProjection.cs` + `CommunicationLog.cs` + `UpcomingTrip.cs` + `AgencyProjection.cs` | domain | entity | `src/services/PaymentService/PaymentService.Infrastructure/Wallet/WalletTransaction.cs` | exact |
| `src/services/CrmService/CrmService.Infrastructure/Migrations/202606xx_CreateCustomerErasureTombstones.cs` | migration | schema | `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260525000000_AddAgencyWallet.cs` | exact |
| `src/services/CrmService/CrmService.API/Controllers/CrmController.cs` (CRM-01 customer 360, CRM-03 agencies) | controller | CRUD (read-heavy) | `src/services/BookingService/BookingService.API/Controllers/AgentBookingsController.cs` | exact |
| `src/services/CrmService/CrmService.API/Controllers/CommunicationLogController.cs` (CRM-04) | controller | CRUD | `src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs` (B2BWalletController) | exact |
| `src/services/BackofficeService/BackofficeService.Application/Controllers/ErasureController.cs` (COMP-03) | controller | command-publisher | `src/services/BookingService/BookingService.API/Controllers/BookingsController.cs` (PostAsync — fire-and-forget publish) | role-match |
| `src/services/BookingService/BookingService.Application/Consumers/CustomerErasureRequestedConsumer.cs` | consumer | event-driven (NULL PII) | `src/services/PaymentService/PaymentService.Application/Consumers/StripeWebhookConsumer.cs` | role-match |
| `src/services/PaymentService/PaymentService.Infrastructure/Migrations/202606xx_AddAgencyCreditLimit.cs` (CRM-02) | migration | schema extension | `src/services/BookingService/BookingService.Infrastructure/Migrations/20260520000000_AddB2BBookingColumns.cs` | exact |
| Modify `src/services/PaymentService/PaymentService.Application/Consumers/WalletReserveConsumer.cs` (CRM-02 credit-limit check) | consumer | extend | (same consumer, plus extend) | self-modify (add `(currentBalance + creditLimit) >= reserveAmount` check + 402 problem+json) |
| `src/portals/backoffice-web/app/(portal)/customers/page.tsx` (CRM-01) | page | RSC list | `src/portals/b2b-web/app/(portal)/bookings/page.tsx` | exact |
| `src/portals/backoffice-web/app/(portal)/customers/[id]/page.tsx` (Customer 360) | page | RSC detail (tabs) | `src/portals/b2b-web/app/(portal)/bookings/[id]/page.tsx` | exact |
| `src/portals/backoffice-web/app/(portal)/customers/[id]/erase/page.tsx` (COMP-03) | page | form + confirm | `src/portals/b2b-web/components/bookings/void-booking-button.tsx` | role-match |
| `src/portals/backoffice-web/app/(portal)/agencies/page.tsx` (CRM-03) | page | RSC list | `src/portals/b2b-web/app/(portal)/admin/agents/page.tsx` | exact |
| `src/shared/TBE.Contracts/Events/CrmEvents.cs` (new `CustomerCommunicationLogged`, `CustomerErasureRequested`, `CustomerErased`) | contract | record | `src/shared/TBE.Contracts/Events/SagaEvents.cs` | exact |

---

## Pattern Assignments

Each assignment is cross-referenced to the analog file with line numbers so the planner can cite it literally in `Action` sections.

### Pattern A — Service Program.cs (JWT + Policies + Outbox + Consumers)

**Analog:** `src/services/PaymentService/PaymentService.API/Program.cs`
**Apply to:** `src/services/BackofficeService/BackofficeService.API/Program.cs` (rewrite skeleton), `src/services/CrmService/CrmService.API/Program.cs` (rewrite skeleton)

**Imports pattern** (Program.cs lines 1-15):
```csharp
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.Common.Messaging;
using TBE.Common.Security;
using TBE.Common.Telemetry;
using TBE.<ServiceName>.Application.Consumers;
using TBE.<ServiceName>.Infrastructure;
```

**JWT Bearer + authz policy registration** (lines 57-77) — use as the template for `Backoffice<Role>Policy` x 4:
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
    {
        o.Authority = builder.Configuration["Keycloak:Authority"];
        o.Audience = builder.Configuration["Keycloak:Audience"];
        o.RequireHttpsMetadata = builder.Environment.IsProduction();
    });
builder.Services.AddAuthorization(opt =>
{
    opt.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    opt.AddPolicy("B2BPolicy", p => p.RequireAuthenticatedUser());
    opt.AddPolicy("B2BAdminPolicy", p =>
        p.RequireAuthenticatedUser().RequireRole("agent-admin"));
});
```
**Phase-6 delta:** add four named policies `BackofficeReadPolicy` / `BackofficeCsPolicy` / `BackofficeFinancePolicy` / `BackofficeAdminPolicy` each calling `RequireRole("ops-read" | "ops-cs" | "ops-finance" | "ops-admin")`. In the gateway the schemes pin — see Gateway pattern below.

**MassTransit with consumers + outbox** (lines 85-112):
```csharp
builder.Services.AddTbeMassTransitWithRabbitMq(
    builder.Configuration,
    configureConsumers: x =>
    {
        x.AddConsumer<AuthorizePaymentConsumer>();
        // ... one AddConsumer<T>() per consumer type
    },
    configureOutbox: x =>
    {
        x.AddEntityFrameworkOutbox<PaymentDbContext>(o =>
        {
            o.UseSqlServer();
            o.UseBusOutbox();
            o.QueryDelay = TimeSpan.FromSeconds(5);
            o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
        });
    });
```

**Current BackofficeService skeleton** (`Program.cs` lines 1-76) **already has**: Serilog JSON, DbContext+retry, MassTransit outbox, health checks. **Missing**: JWT bearer, Authorization policies, consumer registrations (`x.AddConsumer<ErrorQueueConsumer>()`), `AddControllers()`, `UseAuthentication/UseAuthorization`, `MapControllers()`. Planner must add all five.

---

### Pattern B — YARP Gateway JWT Scheme Pin (scheme-per-realm)

**Analog:** `src/gateway/TBE.Gateway/Program.cs` lines 95-107 + 132-134
```csharp
.AddJwtBearer("Backoffice", options =>
{
    options.Authority = $"{keycloakBaseUrl}/realms/tbe-backoffice";
    options.Audience = "tbe-gateway";
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = $"{keycloakBaseUrl}/realms/tbe-backoffice",
        ValidateAudience = false,
        ValidateLifetime = true
    };
});
// ...
options.AddPolicy("BackofficePolicy", policy =>
    policy.AddAuthenticationSchemes("Backoffice")
          .RequireAuthenticatedUser());
```

**Phase-6 delta:** the `"Backoffice"` scheme and `"BackofficePolicy"` already exist — **extend** the realm role names per D-46 (`ops-admin` / `ops-cs` / `ops-finance` / `ops-read`) and optionally add 4 named fine-grained policies mirroring the service-level policies. Also mirror the `OnTokenValidated` handler from the `tbe-b2b` scheme (Program.cs lines 69-93) so `realm_access.roles` is flattened into `roles` claims. **Pitfall 4** (audience confusion) must be echoed: every policy MUST call `.AddAuthenticationSchemes("Backoffice")` to pin the scheme.

---

### Pattern C — EF Core DbContext with Outbox

**Analog:** `src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs` (full file, 50 lines)
**Apply to:** `src/services/BackofficeService/BackofficeService.Infrastructure/BackofficeDbContext.cs` (replace skeleton), `src/services/CrmService/CrmService.Infrastructure/CrmDbContext.cs`, `src/services/BookingService/BookingService.Infrastructure/BookingEventsDbContext.cs` (NEW — writer-only, Pitfall 1)

**Key excerpt** (lines 27-35):
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    // MassTransit outbox tables
    modelBuilder.AddInboxStateEntity();
    modelBuilder.AddOutboxMessageEntity();
    modelBuilder.AddOutboxStateEntity();
    // Per-domain maps
    modelBuilder.ApplyConfiguration(new BookingSagaStateMap());
    modelBuilder.ApplyConfiguration(new SagaDeadLetterMap());
}
```

**Phase-6 delta for `BookingEventsDbContext.cs`:** **DO NOT** add `AddInboxStateEntity/AddOutboxMessageEntity`; this context ONLY tracks `BookingEvent`. Use a distinct connection string (`BookingEventsWriter`) bound to the `tbe_booking_events_app` SQL login. Avoids ChangeTracker issuing UPDATE against role-denied entities — **Pitfall 1 mitigation**.

---

### Pattern D — Append-Only Ledger Table Migration

**Analog:** `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260417000000_AddWalletAndStripe.cs` (WalletTransactions table definition) + `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260525000000_AddAgencyWallet.cs` (full file — clean small-table precedent)
**Apply to:** `BookingEvents` + `StripeEvents` + `DeadLetterQueue` + `MisDailyAggregates` + `CommissionAccruals` + `MarkupRuleAuditLog` + `WalletCreditRequests` + `CommunicationLog` + `CustomerErasureTombstones` + `PaymentReconciliationQueue`

**Idiom** (from `20260525000000_AddAgencyWallet.cs` lines 25-53):
```csharp
migrationBuilder.EnsureSchema(name: "payment");
migrationBuilder.CreateTable(
    name: "AgencyWallets",
    schema: "payment",
    columns: table => new
    {
        Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
        AgencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        Currency = table.Column<string>(type: "char(3)", nullable: false),
        // ... decimal(18,4) for money, bit for flags, datetime2 for timestamps,
        // default values via defaultValueSql: "SYSUTCDATETIME()".
    },
    constraints: table => { table.PrimaryKey("PK_AgencyWallets", x => x.Id); });
migrationBuilder.CreateIndex(name: "IX_AgencyWallets_AgencyId", schema: "payment", table: "AgencyWallets", column: "AgencyId", unique: true);
```

**Phase-6 deltas:**
- `BookingEvents` — add `Snapshot nvarchar(max) NOT NULL` column, `EventType nvarchar(64)`, non-unique index on `BookingId`.
- `DeadLetterQueue` — add `Payload nvarchar(max) NOT NULL`, `RequeueCount int NOT NULL DEFAULT 0`, filtered unique index on `MessageId` (planner: confirm or drop).
- `MisDailyAggregates` — composite PK `(Date, Product, Channel)` per D-60.
- `CommunicationLog` — `CHECK (EntityType IN ('Customer','Agency'))` per D-62.
- All reason-code columns — `nvarchar(64)` with `CHECK` constraint (reason codes locked as enums per Specific Ideas §"Reason codes as enums").

---

### Pattern E — SQL Server DENY Role Grant (NEW — no analog)

**Apply to:** `src/services/BookingService/BookingService.Infrastructure/Migrations/202606xx_AddAppendOnlyRoleGrants.cs` (separate migration after `AddBookingEventsTable`)

**Source:** RESEARCH.md Pattern 1 (lines 449-466). Raw SQL via `migrationBuilder.Sql(@"...")` — no EF-native equivalent.
```csharp
migrationBuilder.Sql(@"
    IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'booking_events_writer')
        CREATE ROLE booking_events_writer;
    GRANT INSERT, SELECT ON dbo.BookingEvents TO booking_events_writer;
    DENY UPDATE, DELETE ON dbo.BookingEvents TO booking_events_writer;
    IF NOT EXISTS (SELECT 1 FROM sys.database_role_members rm
        JOIN sys.database_principals p ON rm.member_principal_id = p.principal_id
        WHERE p.name = 'tbe_booking_app' AND
              rm.role_principal_id = (SELECT principal_id FROM sys.database_principals WHERE name='booking_events_writer'))
        ALTER ROLE booking_events_writer ADD MEMBER tbe_booking_app;
");
```
**Planner MUST note:** the SQL login name (`tbe_booking_app` or similar) is env-dependent. Derive from `BOOKING_EVENTS_WRITER_CONNECTION_STRING` per RESEARCH Runtime State Inventory. Down-migration must `DROP ROLE booking_events_writer`.

---

### Pattern F — Ledger Writer (Separate from Saga)

**Analog:** `src/services/BookingService/BookingService.Infrastructure/SagaDeadLetterStore.cs` (full file, 22 lines)
**Apply to:** `src/services/BookingService/BookingService.Application/BookingEventsWriter.cs`

**Full idiom:**
```csharp
public class SagaDeadLetterStore : ISagaDeadLetterStore
{
    private readonly BookingDbContext _db;
    public SagaDeadLetterStore(BookingDbContext db) => _db = db;
    public async Task AddAsync(SagaDeadLetter entry, CancellationToken ct)
    {
        await _db.SagaDeadLetters.AddAsync(entry, ct);
        await _db.SaveChangesAsync(ct);
    }
}
```

**Phase-6 delta:** constructor takes `BookingEventsDbContext` (NOT `BookingDbContext`) so `SaveChanges` only touches `BookingEvents`. **Pitfall 1 mitigation (RESEARCH line 894-899).** Writer publishes via `IPublishEndpoint` **inside the same DbContext transaction** (EF Core outbox pattern) when a corresponding integration event is required.

---

### Pattern G — Controller With Policy + Agency/User Scoping + problem+json

**Analog:** `src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs` (B2BWalletController — the full class starting at line 78) and `src/services/BookingService/BookingService.API/Controllers/InvoicesController.cs` (full file — 76 lines; 404-not-403 pitfall)
**Apply to:** all new backoffice controllers (DlqController, BookingsController [backoffice unified], FourEyesController, SupplierContractsController, MisController, ErasureController, MarkupRulesController) + CrmController + CommunicationLogController.

**Class attributes** (WalletController lines 76-78):
```csharp
[ApiController]
[Route("api/wallet")]
[Authorize(Policy = "B2BPolicy")]
public sealed class B2BWalletController : ControllerBase
```
**Phase-6 delta:** route prefix `"api/backoffice/..."` or `"api/crm/..."`; policy = tightest applicable `Backoffice<Role>Policy`.

**Actor extraction** (InvoicesController lines 48-54) — Pitfall 28 fail-closed:
```csharp
var agencyIdClaim = User.FindFirst("agency_id")?.Value;
if (string.IsNullOrWhiteSpace(agencyIdClaim) || !Guid.TryParse(agencyIdClaim, out var agencyId))
    return Unauthorized(new { error = "missing agency_id claim" });
```
**Phase-6 delta:** backoffice actors use `preferred_username` (Keycloak username) not `agency_id`, since ops staff are not agency-scoped. Extraction pattern is identical:
```csharp
var actor = User.FindFirst("preferred_username")?.Value
    ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
if (string.IsNullOrEmpty(actor)) return Unauthorized();
```

**RFC-7807 problem+json hand-serialization** (WalletController lines 132-150, 225-244) — re-use verbatim for every 4-eyes/reason-code/credit-limit problem response:
```csharp
var problem = new
{
    type = "/errors/wallet-topup-out-of-range",
    title = "Top-up amount out of range",
    status = StatusCodes.Status400BadRequest,
    detail = $"Requested {ex.Requested:N2} {ex.Currency} is outside allowed range.",
    allowedRange = new { min = ex.Min, max = ex.Max, currency = ex.Currency },
    requested = ex.Requested,
};
return new ContentResult
{
    Content = System.Text.Json.JsonSerializer.Serialize(problem),
    ContentType = "application/problem+json",
    StatusCode = StatusCodes.Status400BadRequest,
};
```
**Type-URI catalogue for Phase 6:**
- `/errors/wallet-credit-over-limit` (D-61 CRM-02 402 Payment Required)
- `/errors/markup-rule-out-of-range` (D-52)
- `/errors/markup-rule-too-many` (max-2-active-per-agency)
- `/errors/four-eyes-self-approval` (D-48 self-approval guard — 403)
- `/errors/four-eyes-already-decided` (conflict)
- `/errors/four-eyes-expired`
- `/errors/erasure-has-open-saga` (COMP-03 loud-fail)

---

### Pattern H — 4-Eyes Approval Controller Transition

**Analog:** Composite — `PaymentService/Controllers/WalletController.cs` (extraction + problem+json) + RESEARCH.md Pattern 6 (the logical template, lines 772-827).
**Apply to:** `BackofficeService/.../Controllers/FourEyesController.cs`

**Self-approval guard excerpt (RESEARCH Pattern 6, lines 798-825):**
```csharp
[HttpPost("wallet-credit-requests/{id}/approve")]
[Authorize(Policy = "BackofficeAdminPolicy")]
public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveReq req, CancellationToken ct)
{
    var actor = User.FindFirstValue("preferred_username")
        ?? throw new InvalidOperationException("No subject");
    var request = await _db.WalletCreditRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
    if (request == null) return NotFound();
    if (request.Status != "PendingApproval") return Conflict(Problem("Already decided"));
    if (request.ExpiresAt < DateTime.UtcNow) return Conflict(Problem("Expired"));
    // 4-eyes: actor cannot approve their own request
    if (string.Equals(request.RequestedBy, actor, StringComparison.OrdinalIgnoreCase))
        return StatusCode(403, Problem("Self-approval forbidden"));
    request.Status = "Approved";
    request.ApprovedBy = actor;
    request.ApprovedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync(ct);
    await _bus.Publish(new WalletCreditApproved(...), ct);
    return NoContent();
}
```
**Planner MUST:** use `IPublishEndpoint` + the EF outbox (registered at Program.cs per Pattern A) so the status flip and the published `WalletCreditApproved` commit atomically — prevents double-credit or lost-credit on crash.

---

### Pattern I — MassTransit `_error` Queue Consumer (NEW — no in-repo analog)

**Apply to:** `src/services/BackofficeService/BackofficeService.Application/Consumers/ErrorQueueConsumer.cs`

**Source:** RESEARCH.md Pattern 2 (lines 476-543). The **closest-but-not-quite** analog is `SagaDeadLetterSink` (`src/services/BookingService/BookingService.Application/Consumers/CompensationConsumers/SagaDeadLetterSink.cs`) which consumes a typed `SagaDeadLetterRequested` envelope — NOT what we need. The new pattern must consume a `JsonObject` (raw) via `UseRawJsonDeserializer()` so the consumer tolerates any upstream message shape.

**Key excerpt (RESEARCH lines 484-524):**
```csharp
public class ErrorQueueConsumer : IConsumer<JsonObject>
{
    public async Task Consume(ConsumeContext<JsonObject> context)
    {
        var faultMessage = context.Headers.Get<string>("MT-Fault-Message") ?? "(no message)";
        var originalQueue = context.Headers.Get<string>("MT-Fault-InputAddress") ?? "unknown";
        var messageType = context.Headers.Get<string>("MT-MessageType") ?? "unknown";
        var dlqRow = new DeadLetterQueueRow
        {
            Id = Guid.NewGuid(),
            MessageId = context.MessageId ?? Guid.NewGuid(),
            CorrelationId = context.CorrelationId,
            MessageType = messageType,
            OriginalQueue = originalQueue,
            Payload = context.Message.ToJsonString(),
            FailureReason = faultMessage.Length > 1000 ? faultMessage[..1000] : faultMessage,
            FirstFailedAt = DateTime.UtcNow,
            RequeueCount = 0,
            ResolvedAt = null
        };
        _db.DeadLetterQueue.Add(dlqRow);
        await _db.SaveChangesAsync();
    }
}
```
**Registration (lines 528-543) — bind an endpoint per `_error` queue:**
```csharp
x.AddConsumer<ErrorQueueConsumer>();
x.UsingRabbitMq((context, cfg) =>
{
    cfg.ReceiveEndpoint("booking-saga_error", e =>
    {
        e.UseRawJsonDeserializer();
        e.ConfigureConsumer<ErrorQueueConsumer>(context);
    });
    // one per upstream queue — see RESEARCH Open Question on whether to enumerate statically or discover dynamically at startup.
});
```
**Planner open question:** which `_error` queues to bind at startup? RESEARCH leaves this as Claude's discretion — enumerate the known MassTransit consumer endpoint names from Program.cs files across all services (BookingService has ~5; PaymentService has ~10; etc.). The auto-creation by MassTransit means the `_error` queues already exist — the consumer just needs to attach.

---

### Pattern J — Nightly / Monthly Hosted Service (BackgroundService)

**Analog:** `src/services/BookingService/BookingService.Infrastructure/Ttl/TtlMonitorHostedService.cs` (full file, 163 lines) and `src/services/PaymentService/PaymentService.Application/Wallet/WalletLowBalanceMonitor.cs` (lines 28-100)
**Apply to:** `MisDailyAggregateJob`, `ReconciliationJob`, `CommissionAccrualJob`, `MonthlyStatementJob`

**Idiom (TtlMonitorHostedService lines 31-72):**
```csharp
public sealed class TtlMonitorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<TtlMonitorOptions> _opts;
    private readonly ILogger<TtlMonitorHostedService> _log;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PollOnceAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogError(ex, "..."); }
            try { await Task.Delay(_opts.CurrentValue.PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
    internal async Task PollOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        // ... work
    }
}
```
**Phase-6 deltas:**
- **Cron-aware delay** (vs fixed poll interval) — add `Cronos` NuGet (new package) per RESEARCH Pattern 3 lines 602-609 for DST-safe next-occurrence computation.
- **Overlap prevention** — `SemaphoreSlim _runLock = new(1, 1)` + `WaitAsync(0, ct)` (RESEARCH lines 558-599). TtlMonitorHostedService does NOT have this — it's a genuine delta.
- **Test hook** — keep `internal async Task PollOnceAsync` public or internal so unit tests can call it without starting the host (WalletLowBalanceMonitor lines 78-102 show this pattern verbatim).

---

### Pattern K — QuestPDF Document Generator

**Analog:** `src/services/BookingService/BookingService.Infrastructure/Pdf/QuestPdfAgencyInvoiceGenerator.cs` (full file, 110 lines)
**Apply to:** `src/services/PaymentService/PaymentService.Infrastructure/Pdf/QuestPdfAgencyStatementGenerator.cs` (D-54 monthly statement)

**Static license init** (lines 39-42) — required per QuestPDF docs:
```csharp
static QuestPdfAgencyInvoiceGenerator()
{
    QuestPDF.Settings.License = LicenseType.Community;
}
```

**Fluent document pattern** (lines 58-106): `Document.Create(c => c.Page(p => { p.Size(PageSizes.A4); p.Margin(30); p.Header()...; p.Content()...; p.Footer()... }))`.

**Phase-6 delta (D-54 vs D-43):** AgencyMonthlyStatement shows NET/Markup/Commission columns PER-BOOKING (internal statement, NOT customer-facing); ensure the PdfPig negative-grep test is **inverted** — the statement MUST contain the words "NET" / "Markup" / "Commission", whereas the invoice MUST NOT. Planner: explicitly call this out in the test acceptance criteria so the reviewer doesn't confuse the two.

---

### Pattern L — Event-Sourced Projection Consumer with MessageId Dedup

**Apply to:** all 7 CRM consumers (`BookingConfirmedConsumer`, `BookingCancelledConsumer`, `UserRegisteredConsumer`, `WalletTopUpConsumer`, `TicketIssuedConsumer`, `CustomerCommunicationLoggedConsumer`, `CustomerErasureRequestedConsumer`) + the BookingService `CustomerErasureRequestedConsumer`.

**Source:** RESEARCH.md Pattern 5 (lines 705-767). The existing `StripeWebhookConsumer` in PaymentService (`PaymentService.Application/Consumers/StripeWebhookConsumer.cs`) shows a CONSUMER pattern but **republishes** rather than upserts a projection. The dedup mechanism in Phase 5 leans on MassTransit's EF outbox `InboxState` (automatic). **For CRM projections we should rely on the same `InboxState` table** rather than build a custom `InboxMessages` collection — the MT outbox already gives `MessageId` idempotence on `Consumed` consumers when `UseBusOutbox` is configured (which it is in every Phase 1 Program.cs).

**Idempotence WITHOUT duplicate code** — CrmService's `Program.cs` must register the outbox exactly like Pattern A's Program.cs excerpt:
```csharp
configureOutbox: x =>
{
    x.AddEntityFrameworkOutbox<CrmDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
        o.QueryDelay = TimeSpan.FromSeconds(5);
        o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
    });
}
```
The MassTransit outbox automatically dedups on `MessageId` for each consumer type — the consumer body can then focus purely on the projection upsert:
```csharp
public async Task Consume(ConsumeContext<BookingConfirmed> context)
{
    var msg = context.Message;
    var projection = await _db.BookingProjections.FirstOrDefaultAsync(
        b => b.BookingId == msg.BookingId, context.CancellationToken);
    if (projection == null) {
        _db.BookingProjections.Add(new BookingProjection { ... });
    } else {
        projection.Status = "Confirmed";
        projection.ConfirmedAt = msg.At;
    }
    await _db.SaveChangesAsync(context.CancellationToken);
}
```
**Planner: document this deviation from RESEARCH Pattern 5** — RESEARCH proposes a separate `InboxMessages` table. Using MT's native `InboxState` is simpler and matches the rest of the stack. Call this out in PLAN alternatives-considered.

---

### Pattern M — RSC Layout + Auth.js Edge-Split + gatewayFetch

**Analog:** `src/portals/b2b-web/auth.config.ts` (edge), `src/portals/b2b-web/lib/auth.ts` (node), `src/portals/b2b-web/middleware.ts`, `src/portals/b2b-web/lib/api-client.ts`, `src/portals/b2b-web/app/layout.tsx`, `src/portals/b2b-web/app/(portal)/layout.tsx`
**Apply to:** `src/portals/backoffice-web/` (byte-for-byte fork with deltas below)

**Edge-safe config deltas (`auth.config.ts`):**
```ts
// Phase 6 deltas vs b2b-web/auth.config.ts:
//   1. KEYCLOAK_B2B_* → KEYCLOAK_BACKOFFICE_* (tbe-backoffice realm per D-47)
//   2. PROTECTED_PREFIXES = all /*, since the whole portal is staff-only
//   3. sessionToken.name = '__Secure-tbe-backoffice.session-token' (Pitfall 19 — per-portal)
```
**All other structure is unchanged.** Same `NextAuth(authConfig)` export; same edge-safe footprint.

**Middleware role-gate delta (`middleware.ts` line 47-52):** b2b-web bounces non-admin to `/dashboard`. Backoffice mirrors this for each role area — `/approvals/*`, `/payouts/*`, `/markup/*`, `/dlq/*`, `/customers/{id}/erase` gate on `ops-admin`; `/contracts/*`, `/reconciliation/*` gate on `ops-finance`+`ops-admin`; `/bookings/new` gates on `ops-cs`+`ops-admin`; `/mis/*` and read-only list routes pass-through to any authenticated `ops-*` role. Use the existing session.roles array (b2b-web line 47 pattern):
```ts
const roles = (session as { roles?: string[] }).roles;
if (pathname.startsWith('/approvals') && !roles?.includes('ops-admin')) {
    const url = req.nextUrl.clone();
    url.pathname = '/';   // dashboard
    return Response.redirect(url);
}
```

**`next.config.mjs` delta (from b2b-web lines 55 + 62-79):**
- `basePath: '/b2b'` → `basePath: '/backoffice'`
- **Remove** the `/admin/wallet/:path*` Stripe-allowed `walletCsp` block entirely (no Stripe origin anywhere in backoffice — D-47 + Pitfall 5).
- Keep `standardCsp` as the only CSP rule.

**`lib/api-client.ts` — byte-for-byte copy.** Unchanged. Still reads `GATEWAY_URL` env + `session.access_token`.

**`app/(portal)/layout.tsx` delta (from b2b-web lines 38-49):**
- Remove `<LowBalanceBanner />` (no wallet in backoffice).
- Header component shows `<BackofficePortalBadge />` + role chip + user menu + global search + DLQ/approvals/reconciliation count badges (per UI-SPEC §Header).

---

### Pattern N — Portal Differentiation Badge

**Analog:** `src/portals/b2b-web/components/layout/agent-portal-badge.tsx` (full file, 34 lines)
**Apply to:** `src/portals/backoffice-web/components/BackofficePortalBadge.tsx`

**Full file:**
```tsx
export function AgentPortalBadge({ className }: { className?: string }) {
  return (
    <span
      aria-label="Agent portal"
      className={cn(
        'inline-flex h-8 items-center rounded-full border border-indigo-600 px-3',
        'text-xs font-semibold uppercase tracking-wide text-indigo-600',
        'dark:border-indigo-400 dark:text-indigo-400',
        className,
      )}
    >
      Agent portal
    </span>
  );
}
```
**Phase-6 delta:**
- `indigo-600` → `slate-900` (light), `indigo-400` → `slate-200` (dark) — D-47 accent palette.
- Label `"Agent portal"` → `"BACKOFFICE"` (uppercase is already inherited from `uppercase`).
- `aria-label="Agent portal"` → `aria-label="Backoffice portal"`.

---

### Pattern O — TanStack Query 30s-Poll for Live Panes

**Analog:** `src/portals/b2b-web/components/wallet/wallet-chip.tsx` (full file, 73 lines)
**Apply to:** DLQ count badge, Reconciliation queue count, 4-eyes pending count, Dashboard tile counts (per UI-SPEC §3 dashboard polling 60s, §2 header counts 60s).

**Idiom (lines 34-44):**
```ts
const { data } = useQuery<WalletBalancePayload>({
    queryKey: ['wallet', 'balance'],
    queryFn: async () => {
        const r = await fetch('/api/wallet/balance');
        if (!r.ok) throw new Error(`wallet balance ${r.status}`);
        return (await r.json()) as WalletBalancePayload;
    },
    initialData: { amount: initialBalance, currency },
    refetchInterval: 30_000,
    staleTime: 20_000,
});
```
**Phase-6 deltas per UI-SPEC:**
- DLQ / approvals / reconciliation counts: `refetchInterval: 60_000`, `staleTime: 45_000`.
- Dashboard MIS snapshot tile: daily — no poll, RSC fetch only.

---

### Pattern P — Keycloak Realm Delta

**Analog:** `infra/keycloak/realm-tbe-b2b.json` (80+ lines — full structure with mappers, service-account client, roles)
**Apply to:** `infra/keycloak/realms/tbe-backoffice-realm.json` (**REWRITE** — the existing file has WRONG role names per RESEARCH Runtime State Inventory; current file has `backoffice-admin`/`backoffice-operator`/`finance` which does not match D-46 `ops-admin`/`ops-cs`/`ops-finance`/`ops-read`).

**Key blocks to copy from `realm-tbe-b2b.json`:**
- `clients[0]` = staff OIDC client (`tbe-backoffice-ui`, redirectUris port 3002, `protocolMappers` with `tbe-api-audience` + any staff-id mapper).
- `clients[1]` = `tbe-backoffice-admin` service-account client (client_credentials grant) — mirror `tbe-b2b-admin` pattern for Keycloak Admin API access (erasure orchestration may need this).
- `roles.realm` = 4 roles with descriptions (NOT 3).
- `users` seed block (add test users per role for UAT — follow the structure from any existing realm import).

**Phase-6 deltas:**
- No `agency_id` attribute mapper (backoffice users are not agency-scoped).
- If backoffice needs to operate on b2b realm users (for agency admin or erasure), the Keycloak Admin API is already wired via `tbe-b2b-admin` service client — no new Keycloak client needed for that cross-realm operation.

---

### Pattern Q — Controller File-Stream Download (CSV / XLSX / PDF)

**Analog:** `src/services/BookingService/BookingService.API/Controllers/InvoicesController.cs` (line 74)
**Apply to:** `MisController.ExportCsv`, `MisController.ExportXlsx`, commission `StatementController.Download`

**Idiom:**
```csharp
return File(bytes, "application/pdf", $"invoice-{booking.BookingReference}.pdf");
```
**Phase-6 deltas — MIME + filename:**
- CSV: `File(bytes, "text/csv", $"mis-{from:yyyyMMdd}-{to:yyyyMMdd}.csv")`
- XLSX: `File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"mis-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx")`
- Statement PDF: `File(bytes, "application/pdf", $"statement-{agencyId}-{period:yyyyMM}.pdf")`

---

### Pattern R — Credit-Limit Check in WalletReserveConsumer (CRM-02 modify)

**Analog:** `src/services/PaymentService/PaymentService.Infrastructure/Wallet/AgencyWalletRepository.cs` lines 49-67 — `MERGE ... WITH (HOLDLOCK)` idiom (Phase 3 D-15 locking precedent) + `payment.WalletTransactions` append-only ledger.
**Apply to:** extend `src/services/PaymentService/PaymentService.Application/Consumers/WalletReserveConsumer.cs`

**Current pattern lives in raw Dapper (WalletRepository / AgencyWalletRepository). Extend the reserve SQL to read `AgencyWallets.CreditLimit`:**
```sql
-- BEFORE: reserve fails if balance < amount
-- AFTER (D-61): reserve fails if (balance + creditLimit) < amount
;WITH AgencyBalance AS (
    SELECT SUM(SignedAmount) AS Balance FROM payment.WalletTransactions WITH (UPDLOCK, HOLDLOCK)
    WHERE WalletId = @WalletId
)
SELECT AB.Balance, AW.CreditLimit
FROM AgencyBalance AB, payment.AgencyWallets AW WITH (HOLDLOCK)
WHERE AW.AgencyId = @AgencyId
-- reject with 402 Payment Required + /errors/wallet-credit-over-limit problem+json if (Balance + CreditLimit) < @Amount
```
**D-61 migration adds `Agencies.CreditLimit decimal(18,4) NOT NULL DEFAULT 0`** — but note the existing schema uses `payment.AgencyWallets`, not `Agencies`. Planner decides: (a) add the column to `payment.AgencyWallets` (consistent with Phase 5), or (b) create a new `Agencies` projection table in CRM (per D-51) and keep `CreditLimit` there but expose it to PaymentService via a cached read. Path (a) is simpler; path (b) follows the event-sourcing purity doctrine. **Recommend (a) for Phase 6 pragmatism; call out (b) as v2 alternative.**

---

## Shared Patterns (cross-cutting)

### Authentication
- **Edge middleware gate:** `src/portals/b2b-web/middleware.ts` (wrap `auth` from `@/auth.config`, redirect unauthenticated to `/login`, role-gate admin-only paths). Apply to every backoffice-web protected path.
- **Server JWT bearer:** `src/services/PaymentService/PaymentService.API/Program.cs` lines 57-77. Apply to `BackofficeService` + `CrmService` Program.cs.
- **Gateway scheme pin:** `src/gateway/TBE.Gateway/Program.cs` lines 95-107 + 118-134. `"Backoffice"` scheme + `"BackofficePolicy"` already exist; just extend role list.
- **`gatewayFetch` Bearer forwarding:** `src/portals/b2b-web/lib/api-client.ts` (byte-for-byte copy to backoffice-web).

### Error Handling (RFC-7807 problem+json)
- **Source shape:** `src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs` lines 132-150 + 225-244 (two identical problem+json serialization helpers).
- **Apply to:** every mutating controller in Plan 1/2/3/4 on validation-range / conflict / forbidden.

### Actor Attribution (Pitfall 28 fail-closed)
- **Source:** `src/services/BookingService/BookingService.API/Controllers/InvoicesController.cs` lines 48-54.
- **Apply to:** every backoffice controller. Use `preferred_username` (not `agency_id`) since ops staff are not agency-scoped. Fail-closed 401 when claim missing.

### Cross-Tenant 404 (Pitfall 10)
- **Source:** `InvoicesController.cs` lines 60-68.
- **Apply to:** backoffice cannot leak a cross-realm existence check (e.g., backoffice staff viewing agency-scoped resources). For backoffice-internal resources, a 404 for "not found" is sufficient. For resources that span tenants, 404 wherever 403 would leak existence.

### Outbox-Atomic Publish + DB Write
- **Source:** every Program.cs outbox registration (Pattern A). The EF Core outbox guarantees `_db.SaveChangesAsync()` commits the business row AND the MassTransit message atomically.
- **Apply to:** every mutating consumer + every 4-eyes approve controller + every projection consumer.

### 44px Touch Targets + Compact Tables (h-11) / Dense Audit (h-9)
- **Source:** UI-SPEC §Spacing Scale + §Information Density Contract.
- **Apply to:** every backoffice-web page. Audit timeline is the single exception — use h-9 for BookingEvents only.

### starterKit `.jsx` Byte-for-Byte (Pitfall 17)
- **Source:** `src/portals/b2b-web/components/ui/*.jsx` (every file — never converted to `.tsx`).
- **Apply to:** `src/portals/backoffice-web/components/ui/*` — copy UNCHANGED from b2b-web/ at fork time. Resolve TS friction via `types/ui.d.ts` ambient shim (mirror Phase 5).

### Per-Portal Cookie Scoping (Pitfall 19)
- **Source:** `src/portals/b2b-web/auth.config.ts` lines 40-53.
- **Apply to:** backoffice-web `auth.config.ts` with `sessionToken.name = '__Secure-tbe-backoffice.session-token'`.

### Auth.js v5 Edge-Split (Pitfall 3)
- **Source:** b2b-web `auth.config.ts` (edge) + `lib/auth.ts` (node) separation.
- **Apply to:** backoffice-web; `middleware.ts` MUST import from `@/auth.config` not `@/lib/auth` (lib/auth pulls Node `crypto` — would blow up at build if imported in edge context).

---

## Anti-Patterns to Avoid (from RESEARCH §Anti-Patterns + Phase commit history)

Planner MUST echo these into each relevant plan's "Anti-patterns" or "Out of scope" section:

1. **EF Core `TemporalTableBuilder` instead of role-grant DENY** — temporal tables allow UPDATE; D-49 requires true append-only. (RESEARCH line 832.)
2. **Shared DbContext for mutable tables + BookingEvents** — ChangeTracker will attempt UPDATE on BookingEvents → DENY error at SaveChanges. Mitigation: dedicated `BookingEventsDbContext` with its own SQL login. (RESEARCH lines 833 + 892-899 Pitfall 1.)
3. **Re-poisoning on DLQ requeue** — raw `IPublishEndpoint.Publish(payload)` drops tracing headers; use a `PublishContextCallback` to restore `MessageId`, `CorrelationId`, custom headers. (RESEARCH line 834.)
4. **Trigger-based append-only** — per-row trigger cost vs declarative role grant. (RESEARCH line 835.)
5. **Pending approval without expiry enforcement** — D-48 mandates 72h expiry; either BackgroundService transitions status to `Expired` OR CHECK constraint `ExpiresAt > GETUTCDATE()` enforced at read time. (RESEARCH line 836.)
6. **Calling Stripe API from nightly recon job** — D-55 recon diffs stored `StripeEvents` ONLY; no additional Stripe API calls. (RESEARCH line 837.)
7. **CRM consumer writes to BookingService DB** — D-51 service boundary forbids cross-DB; events only. (RESEARCH line 838.)
8. **Cascade delete on GDPR erasure** — D-57 NULL PII only; never DELETE `BookingSagaState`/`BookingEvents`. (RESEARCH line 839.)
9. **5-role RBAC** — D-46 locks at 4 roles. Do not add separate CS-vs-booking-edit split. (CONTEXT Deferred Ideas.)
10. **Agent self-service markup editing** — explicitly disallowed (unbounded self-margin). (CONTEXT Deferred Ideas.)
11. **Hard delete with FK cascade on erasure** — rejected (violates BO-04 audit). (CONTEXT Deferred Ideas.)
12. **Full event-store engine (Marten / EventStoreDB)** — rejected; MT outbox + `MessageId` dedup suffices. (D-51.)
13. **`.jsx` → `.tsx` rewrite** — Pitfall 17. Every `components/ui/*.jsx` file MUST remain byte-for-byte copy from b2b-web; fix TS friction with ambient shim.
14. **Cookie collision on shared domain** — Pitfall 19. Each portal's session cookie name MUST contain its portal slug (`__Secure-tbe-backoffice.session-token`).
15. **`agency_id` claim fall-back on missing** — Pitfall 28 inherit. Backoffice equivalent: on missing `preferred_username` / `ops-*` role, fail-closed 401, never default.

Carry forward from recent Phase-5 commit history (`git log --oneline`): the 05-05 review-fix cycle landed HI-01/HI-02 fixes (`GET /api/wallet/threshold` route + `return_url` absolute). Planner MUST ensure no similar gaps: every new endpoint has its route wired in BOTH the service controller AND the gateway `appsettings.json` ReverseProxy route list.

---

## No Analog Found (explicit "new pattern" tag)

Files with no close in-repo match. Planner should use RESEARCH.md's example blocks directly:

| File | Role | Data Flow | Reason | Source to copy |
|------|------|-----------|--------|----------------|
| `BookingService/Infrastructure/Migrations/202606xx_AddAppendOnlyRoleGrants.cs` | migration | raw SQL role grants | No existing CREATE ROLE / GRANT / DENY in any migration | RESEARCH §Pattern 1 lines 451-465 |
| `BackofficeService/.../Consumers/ErrorQueueConsumer.cs` | consumer | raw-JSON envelope | No consumer tails `_error` queues today; `SagaDeadLetterSink` consumes typed event | RESEARCH §Pattern 2 lines 484-543 |
| `BackofficeService/.../Exporters/MisExcelExporter.cs` | service | multi-sheet xlsx stream | ClosedXML is net-new to repo | RESEARCH §Pattern 4 lines 625-684 |
| `BackofficeService/.../Exporters/MisCsvExporter.cs` | service | CSV stream | No existing CSV writer | Hand-roll per RFC 4180 or use `CsvHelper` NuGet (planner's discretion — RESEARCH flagged as open question) |

**Net-new NuGet packages:** `ClosedXML` (for BackofficeService.Application), `Cronos` (for PaymentService + BackofficeService hosted services if DST-safe cron is wanted; can alternatively hand-roll fixed delays and accept DST drift per RESEARCH §Pattern 3). QuestPDF already in-stack from Phase 5.

---

## Metadata

**Analog search scope:**
- `src/services/BackofficeService/**` (skeleton — 3 files, Program.cs + DbContext + csproj)
- `src/services/CrmService/**` (skeleton — 3 files)
- `src/services/BookingService/**` (mature — ~50 files; QuestPDF / TtlMonitor / Saga / DeadLetterStore are the high-value analogs)
- `src/services/PaymentService/**` (mature — ~40 files; Wallet ledger / Stripe webhook / AgencyWallet repo / Program.cs template are the high-value analogs)
- `src/services/PricingService/**` (pattern reused; exact controller path TBD — planner to confirm existing file tree)
- `src/portals/b2b-web/**` (complete reference implementation — every auth / middleware / layout / component analog)
- `src/portals/b2c-web/**` (lineage only; deltas already traced through b2b-web)
- `src/gateway/TBE.Gateway/Program.cs` (YARP + JWT schemes + policies)
- `src/shared/TBE.Common/**`, `src/shared/TBE.Contracts/**`
- `infra/keycloak/realm-tbe-b2b.json`, `infra/keycloak/realms/tbe-backoffice-realm.json`
- `.planning/research/PITFALLS.md` (domain pitfalls; portal-numbering pitfalls (3/17/19/28) documented inline in b2b-web/auth.config.ts and b2b-web/next.config.mjs comments)

**Files scanned:** 62 code files + 3 phase docs (CONTEXT.md, RESEARCH.md, UI-SPEC.md) + 1 prior-phase realm file.

**Pattern extraction date:** 2026-04-19.
