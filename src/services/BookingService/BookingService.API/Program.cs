using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.BookingService.Application.Consumers;
using TBE.BookingService.Application.Consumers.CompensationConsumers;
using TBE.BookingService.Application.Pdf;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Application.Ttl;
using TBE.BookingService.Application.Ttl.Adapters;
using TBE.BookingService.Infrastructure;
using TBE.BookingService.Infrastructure.Pdf;
using TBE.BookingService.Infrastructure.Ttl;
using TBE.Common.Messaging;
using TBE.Common.Security;
using TBE.Common.Telemetry;

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

    // Dead-letter store (SagaDeadLetterSink depends on ISagaDeadLetterStore)
    builder.Services.AddScoped<ISagaDeadLetterStore, SagaDeadLetterStore>();

    // JWT Bearer — Keycloak authority. Enforced globally via class-level [Authorize].
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

    // B2C receipt PDF generator (Plan 04-01 / D-15). Scoped lifetime mirrors the
    // DbContext that the controller injects alongside it.
    builder.Services.AddScoped<IBookingReceiptPdfGenerator, QuestPdfBookingReceiptGenerator>();

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
