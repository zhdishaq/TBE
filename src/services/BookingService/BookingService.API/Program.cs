using System.Security.Claims;
using System.Text.Json;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.BookingService.Application;
using TBE.BookingService.Application.Baskets;
using TBE.BookingService.Application.Consumers;
using TBE.BookingService.Application.Consumers.CompensationConsumers;
using TBE.BookingService.Application.Keycloak;
using TBE.BookingService.Application.Pdf;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Application.Ttl;
using TBE.BookingService.Application.Ttl.Adapters;
using TBE.BookingService.Infrastructure;
using TBE.BookingService.Infrastructure.Baskets;
using TBE.BookingService.Infrastructure.Consumers;
using TBE.BookingService.Infrastructure.Keycloak;
using TBE.BookingService.Infrastructure.Pdf;
using TBE.BookingService.Infrastructure.Ttl;
using TBE.Common.Messaging;
using TBE.Common.Security;
using TBE.Common.Telemetry;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration)
                     .ReadFrom.Services(services)
                     .Enrich.FromLogContext()
                     .Enrich.WithProperty("Service", "BookingService"));

    // Database
    builder.Services.AddDbContext<BookingDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("BookingDb"),
            sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

    // Plan 06-01 Task 5 (BO-04 / BO-05 / D-49 / D-50) — writer-only DbContext
    // for the append-only dbo.BookingEvents audit table. Distinct connection
    // string (BookingEventsWriter) that maps to the tbe_booking_app SQL login
    // so the engine DENY grant rejects UPDATE/DELETE. Falls back to the
    // primary BookingDb string in dev/test where a separate login is not
    // provisioned (Testcontainers fixtures use the sa login during apply).
    builder.Services.AddDbContext<BookingEventsDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("BookingEventsWriter")
                ?? builder.Configuration.GetConnectionString("BookingDb"),
            sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

    builder.Services.AddScoped<IBookingEventsWriter, BookingEventsWriter>();

    // Plan 06-02 Task 1 (BO-02) — manual booking direct-insert command
    // (saga-bypass). Consumed by ManualBookingsController under
    // BackofficeCsPolicy.
    builder.Services.AddScoped<ManualBookingCommand>();

    // Dead-letter store (SagaDeadLetterSink depends on ISagaDeadLetterStore)
    builder.Services.AddScoped<ISagaDeadLetterStore, SagaDeadLetterStore>();

    // JWT Bearer — primary scheme is the tbe-b2b realm for B2B endpoints.
    // Plan 06-02 Task 1 (BO-02) adds a second named scheme "Backoffice"
    // pinned to the tbe-backoffice realm so <c>ManualBookingsController</c>
    // (class-level [Authorize(Policy=..., AuthenticationSchemes="Backoffice")])
    // validates ops tokens against the correct issuer. Pitfall 4 pin:
    // every ops policy calls AddAuthenticationSchemes("Backoffice"); a
    // tbe-b2b token cannot satisfy it even if the roles match.
    var keycloakBaseUrl = builder.Configuration["Keycloak:BaseUrl"]
        ?? "http://keycloak:8080";
    var backofficeIssuer = builder.Configuration["Keycloak:Backoffice:Authority"]
        ?? $"{keycloakBaseUrl}/realms/tbe-backoffice";

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
        {
            o.Authority = builder.Configuration["Keycloak:Authority"];
            o.Audience = builder.Configuration["Keycloak:Audience"];
            o.RequireHttpsMetadata = builder.Environment.IsProduction();
        })
        .AddJwtBearer("Backoffice", options =>
        {
            options.Authority = backofficeIssuer;
            options.Audience = "tbe-api";
            options.RequireHttpsMetadata = builder.Environment.IsProduction();
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = backofficeIssuer,
                ValidateAudience = true,
                ValidAudience = "tbe-api",
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
            options.Events = new JwtBearerEvents
            {
                // Keycloak nests realm roles inside realm_access.roles — flatten
                // them into ClaimTypes.Role + "roles" so RequireRole(...) works.
                OnTokenValidated = ctx =>
                {
                    var realmAccess = ctx.Principal?.FindFirst("realm_access")?.Value;
                    if (!string.IsNullOrEmpty(realmAccess))
                    {
                        using var doc = JsonDocument.Parse(realmAccess);
                        if (doc.RootElement.TryGetProperty("roles", out var rolesEl))
                        {
                            var identity = (ClaimsIdentity)ctx.Principal!.Identity!;
                            foreach (var role in rolesEl.EnumerateArray())
                            {
                                var name = role.GetString();
                                if (!string.IsNullOrEmpty(name))
                                {
                                    identity.AddClaim(new Claim(ClaimTypes.Role, name));
                                    identity.AddClaim(new Claim("roles", name));
                                }
                            }
                        }
                    }
                    return Task.CompletedTask;
                },
            };
        });
    builder.Services.AddAuthorization(opt =>
    {
        opt.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();

        // Plan 06-02 Task 1 (BO-02) — four ops-* policies pinned to the
        // "Backoffice" JWT scheme. Duplicated from BackofficeService.API
        // so BookingService stays self-contained.
        opt.AddPolicy("BackofficeReadPolicy", p =>
            p.AddAuthenticationSchemes("Backoffice")
             .RequireAuthenticatedUser()
             .RequireRole("ops-read", "ops-cs", "ops-finance", "ops-admin"));
        opt.AddPolicy("BackofficeCsPolicy", p =>
            p.AddAuthenticationSchemes("Backoffice")
             .RequireAuthenticatedUser()
             .RequireRole("ops-cs", "ops-admin"));
        opt.AddPolicy("BackofficeFinancePolicy", p =>
            p.AddAuthenticationSchemes("Backoffice")
             .RequireAuthenticatedUser()
             .RequireRole("ops-finance", "ops-admin"));
        opt.AddPolicy("BackofficeAdminPolicy", p =>
            p.AddAuthenticationSchemes("Backoffice")
             .RequireAuthenticatedUser()
             .RequireRole("ops-admin"));

        // Plan 05-02 Task 2 — B2BPolicy gates the /agent/bookings endpoints.
        // Any agent role (agent | agent-admin | agent-readonly) satisfies the
        // authN gate; D-34 / D-35 / D-37 are enforced at the controller.
        opt.AddPolicy("B2BPolicy", p =>
            p.RequireAuthenticatedUser()
             .RequireAssertion(ctx =>
                 ctx.User.HasClaim(c =>
                     c.Type == "roles" &&
                     (c.Value == "agent" || c.Value == "agent-admin" || c.Value == "agent-readonly"))
                 || ctx.User.IsInRole("agent")
                 || ctx.User.IsInRole("agent-admin")
                 || ctx.User.IsInRole("agent-readonly")));

        // Plan 05-04 Task 1 (B2B-10) — B2BAdminPolicy gates write-side admin
        // endpoints (POST /agent/bookings/{id}/void). ONLY agent-admin may
        // void a booking; agent + agent-readonly are forbidden.
        opt.AddPolicy("B2BAdminPolicy", p =>
            p.RequireAuthenticatedUser()
             .RequireAssertion(ctx =>
                 ctx.User.HasClaim(c => c.Type == "roles" && c.Value == "agent-admin")
                 || ctx.User.IsInRole("agent-admin")));
    });

    // Shared OTel + AES-GCM primitives (COMP-05 / COMP-06).
    builder.Services.AddTbeOpenTelemetry(builder.Configuration, "BookingService");
    builder.Services.Configure<EncryptionOptions>(builder.Configuration.GetSection("Encryption"));
    builder.Services.AddSingleton<IEncryptionKeyProvider, EnvEncryptionKeyProvider>();
    builder.Services.AddSingleton<AesGcmFieldEncryptor>();

    // Fare-rule parser + per-GDS adapters (keyed DI) — FLTB-06.
    builder.Services.AddKeyedSingleton<IFareRuleAdapter, AmadeusFareRuleAdapter>("amadeus");
    builder.Services.AddKeyedSingleton<IFareRuleAdapter, SabreFareRuleAdapter>("sabre");
    builder.Services.AddKeyedSingleton<IFareRuleAdapter, GalileoFareRuleAdapter>("galileo");
    builder.Services.AddSingleton<IFareRuleParser, FareRuleParser>();

    // TTL monitor hosted service (FLTB-06) — advisory-only, hard-timeout is saga-Schedule-driven.
    builder.Services.Configure<TtlMonitorOptions>(builder.Configuration.GetSection("TtlMonitor"));
    builder.Services.AddHostedService<TtlMonitorHostedService>();

    // FlightConnectorService HTTP client for PNR creation.
    builder.Services.AddHttpClient("flight-connector", c =>
        c.BaseAddress = new Uri(
            builder.Configuration["Services:FlightConnector:BaseUrl"] ?? "http://flight-connector:8080"));

    // Plan 04-03 / D-16 — HotelBookingsController streams voucher.pdf from NotificationService.
    builder.Services.AddHttpClient(TBE.BookingService.API.Controllers.HotelBookingsController.NotificationClientName, c =>
        c.BaseAddress = new Uri(
            builder.Configuration["Services:NotificationService:BaseUrl"] ?? "http://notification-service:8080"));

    // B2C receipt PDF generator (Plan 04-01 / D-15). Scoped lifetime mirrors the
    // DbContext that the controller injects alongside it.
    builder.Services.AddScoped<IBookingReceiptPdfGenerator, QuestPdfBookingReceiptGenerator>();

    // Plan 05-04 Task 2 (B2B-08) — B2B agency invoice PDF generator (D-43 GROSS-only).
    // Deviation from plan: the generator lives in BookingService (not
    // NotificationService / "DocumentService") because BookingSagaState holds all
    // required fields (BookingReference, AgencyId, CustomerName/Email,
    // AgencyGrossAmount). A cross-service HTTP contract would only duplicate the
    // data. Route: GET /api/invoices/{bookingId}.pdf under B2BPolicy.
    builder.Services.AddScoped<IAgencyInvoicePdfGenerator, QuestPdfAgencyInvoiceGenerator>();

    // Plan 05-04 Task 1 (B2B-09) — TTL-deadline fan-out. TimeProvider.System lets
    // tests swap in FakeTimeProvider for deterministic Keycloak-token-cache
    // stepping. The Keycloak client typed-HttpClient is scoped; its options bind
    // to the "KeycloakB2BAdmin" configuration section (see appsettings.json).
    // The e-mail sender is the log-only MVP stub; a follow-up plan will replace
    // it with a SendGrid transport once the NotificationService template
    // contract for ticketing-deadline advisories is approved.
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.Configure<KeycloakB2BAdminOptions>(
        builder.Configuration.GetSection("KeycloakB2BAdmin"));
    builder.Services.AddHttpClient<IKeycloakB2BAdminClient, KeycloakB2BAdminClient>();
    builder.Services.AddScoped<ITicketingDeadlineEmailSender, LoggerTicketingDeadlineEmailSender>();

    // Plan 04-04 / D-08 — the basket single-PI gateway. A thin bus-command adapter
    // (NullBasketPaymentGateway) is bound by default so tests and local dev run
    // without PaymentService; production wires a real adapter forwarding to the
    // PaymentService.IStripePaymentGateway.CapturePartialAsync / VoidAsync API
    // via MassTransit request/response (PCI SAQ-A isolation — PAY-08).
    builder.Services.AddScoped<IBasketPaymentGateway, NullBasketPaymentGateway>();

    builder.Services.AddControllers();

    // MassTransit with RabbitMQ + BookingSaga + outbox
    builder.Services.AddTbeMassTransitWithRabbitMq(
        builder.Configuration,
        configureConsumers: x =>
        {
            x.AddSagaStateMachine<BookingSaga, BookingSagaState>(typeof(BookingSagaDefinition))
                .EntityFrameworkRepository(r =>
                {
                    r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                    r.ExistingDbContext<BookingDbContext>();
                    r.UseSqlServer();
                });
            x.AddConsumer<SagaDeadLetterSink>();
            x.AddConsumer<CreatePnrConsumer>();
            // Plan 04-04 / D-08 — sequential partial capture orchestrator on a single combined PI.
            x.AddConsumer<BasketPaymentOrchestrator>();
            // Plan 05-04 Task 1 (B2B-09) — fan out TTL-deadline advisories to the
            // agency's agent-admin + agent users via Keycloak-resolved recipients.
            // Both warn (24h) and urgent (2h) horizons land on the same class;
            // the audit note in deferred-items.md flagged that unconsumed events
            // were accumulating on the default exchange.
            x.AddConsumer<TicketingDeadlineConsumer>();
            // Plan 06-04 Task 3 (COMP-03 / D-57) — BookingService-side GDPR
            // erasure fan-out. Resolves via TBE.BookingService.Infrastructure.Consumers.
            x.AddConsumer<CustomerErasureRequestedConsumer>();
        },
        configureOutbox: x =>
        {
            x.AddEntityFrameworkOutbox<BookingDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromSeconds(5);
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });
        });

    // Health checks
    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("BookingDb")!,
            name: "booking-db",
            tags: new[] { "db", "sql" })
        .AddRabbitMQ(
            factory: async sp =>
            {
                var connectionFactory = new RabbitMQ.Client.ConnectionFactory
                {
                    HostName = builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq",
                    UserName = builder.Configuration["RabbitMQ:Username"] ?? "guest",
                    Password = builder.Configuration["RabbitMQ:Password"] ?? "guest"
                };
                return await connectionFactory.CreateConnectionAsync();
            },
            name: "rabbitmq",
            tags: new[] { "messaging" });

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "BookingService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Exposed so integration/controller tests (WebApplicationFactory) can reference the entry-point assembly.
/// </summary>
public partial class Program { }
