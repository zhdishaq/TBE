using System.Security.Claims;
using System.Text.Json;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.Common.Messaging;
using TBE.Common.Security;
using TBE.Common.Telemetry;
using TBE.PaymentService.Application.Consumers;
using TBE.PaymentService.Application.Reconciliation;
using TBE.PaymentService.Application.Stripe;
using TBE.PaymentService.Application.Wallet;
using TBE.PaymentService.Infrastructure;
using TBE.PaymentService.Infrastructure.Keycloak;
using TBE.PaymentService.Infrastructure.Reconciliation;
using TBE.PaymentService.Infrastructure.Wallet;

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
                     .Enrich.WithProperty("Service", "PaymentService"));

    builder.Services.AddDbContext<PaymentDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("PaymentDb"),
            sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

    builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection("Stripe"));
    builder.Services.Configure<WalletOptions>(builder.Configuration.GetSection("Wallet"));
    builder.Services.Configure<KeycloakB2BAdminOptions>(
        builder.Configuration.GetSection("KeycloakB2B"));
    builder.Services.AddSingleton<IStripePaymentGateway, StripePaymentGateway>();
    builder.Services.AddScoped<IWalletRepository, WalletRepository>();
    builder.Services.AddScoped<IWalletTopUpService, WalletTopUpService>();

    // Plan 05-03 Task 2 — low-balance monitor + consumer surface.
    // Monitor is a BackgroundService; consumer registered with MassTransit below.
    // TimeProvider.System is injected as a singleton so tests can swap it with
    // FakeTimeProvider and step cooldowns deterministically.
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddScoped<IAgencyWalletRepository, AgencyWalletRepository>();
    builder.Services.AddScoped<IWalletLowBalanceEmailSender, WalletLowBalanceEmailSender>();
    builder.Services.AddHttpClient<IKeycloakB2BAdminClient, KeycloakB2BAdminClient>();
    builder.Services.AddHostedService<WalletLowBalanceMonitor>();

    builder.Services.AddControllers();
    builder.Services.AddTbeSwagger("PaymentService");

    // Plan 06-02 Task 3 (BO-06) — two JWT schemes:
    //  1. Default scheme: tbe-b2b realm (for /api/wallet/* endpoints).
    //  2. "Backoffice" scheme: tbe-backoffice realm (for /api/backoffice/
    //     reconciliation). Pinned per Pitfall 4 so a b2b token can't
    //     reach the ops-finance surface.
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
                // Flatten Keycloak realm_access.roles into ClaimTypes.Role
                // so RequireRole works on the "Backoffice" scheme.
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

        // Plan 05-03: named policies mirroring the B2B portal role model.
        // B2BPolicy: any authenticated B2B user (admin, agent, readonly).
        // B2BAdminPolicy: agent-admin role only — required for mutation endpoints
        // (/api/wallet/top-up/intent, /api/wallet/transactions, /api/agents/*).
        opt.AddPolicy("B2BPolicy", p => p.RequireAuthenticatedUser());
        opt.AddPolicy("B2BAdminPolicy", p =>
            p.RequireAuthenticatedUser().RequireRole("agent-admin"));

        // Plan 06-02 Task 3 (BO-06) — reconciliation surface policies
        // pinned to the Backoffice scheme (Pitfall 4). Mirror the four
        // ops-* tiers from BackofficeService so role taxonomy stays flat.
        opt.AddPolicy("BackofficeReadPolicy", p =>
            p.AddAuthenticationSchemes("Backoffice")
             .RequireAuthenticatedUser()
             .RequireRole("ops-read", "ops-cs", "ops-finance", "ops-admin"));
        opt.AddPolicy("BackofficeFinancePolicy", p =>
            p.AddAuthenticationSchemes("Backoffice")
             .RequireAuthenticatedUser()
             .RequireRole("ops-finance", "ops-admin"));
    });

    // Plan 06-02 Task 3 (BO-06) — reconciliation service + nightly
    // BackgroundService driver. TimeProvider.System was already
    // registered by Plan 05-03; scoping the service lets it share the
    // request-scoped DbContext during portal rescans (e.g. from a
    // manual-trigger endpoint if we add one later).
    builder.Services.AddScoped<IPaymentReconciliationService, PaymentReconciliationService>();
    builder.Services.AddHostedService<ReconciliationJob>();

    // Shared OTel + AES-GCM primitives.
    builder.Services.AddTbeOpenTelemetry(builder.Configuration, "PaymentService");
    builder.Services.Configure<EncryptionOptions>(builder.Configuration.GetSection("Encryption"));
    builder.Services.AddSingleton<IEncryptionKeyProvider, EnvEncryptionKeyProvider>();
    builder.Services.AddSingleton<AesGcmFieldEncryptor>();

    builder.Services.AddTbeMassTransitWithRabbitMq(
        builder.Configuration,
        configureConsumers: x =>
        {
            x.AddConsumer<AuthorizePaymentConsumer>();
            x.AddConsumer<CapturePaymentConsumer>();
            x.AddConsumer<CancelAuthorizationConsumer>();
            x.AddConsumer<RefundPaymentConsumer>();
            x.AddConsumer<StripeWebhookConsumer>();
            x.AddConsumer<StripeTopUpConsumer>();
            x.AddConsumer<WalletReserveConsumer>();
            x.AddConsumer<WalletCommitConsumer>();
            x.AddConsumer<WalletReleaseConsumer>();
            // Plan 05-03 Task 2 — consumes WalletLowBalanceDetected published by
            // WalletLowBalanceMonitor; resolves agent-admin recipients via
            // Keycloak (tbe-b2b realm) and flips LowBalanceEmailSent = 1.
            x.AddConsumer<WalletLowBalanceConsumer>();
            // Plan 06-01 Task 6 (D-39) — consumes WalletCreditApproved
            // published by BackofficeService after a 4-eyes approval on
            // backoffice.WalletCreditRequests; writes a single
            // payment.WalletTransactions row of Kind=ManualCredit.
            // Idempotency is MassTransit InboxState + unique IdempotencyKey.
            x.AddConsumer<WalletCreditApprovedConsumer>();
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

    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("PaymentDb")!,
            name: "payment-db",
            tags: new[] { "db", "sql" })
        .AddRabbitMQ(
            factory: sp =>
            {
                var cf = new RabbitMQ.Client.ConnectionFactory
                {
                    HostName = builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq",
                    UserName = builder.Configuration["RabbitMQ:Username"] ?? "guest",
                    Password = builder.Configuration["RabbitMQ:Password"] ?? "guest"
                };
                return cf.CreateConnectionAsync();
            },
            name: "rabbitmq",
            tags: new[] { "messaging" })
        .AddRedis(
            builder.Configuration["Redis:ConnectionString"]!,
            name: "redis",
            tags: new[] { "cache" });

    var app = builder.Build();
    app.UseSerilogRequestLogging();
    if (app.Environment.IsDevelopment())
    {
        app.UseTbeSwagger();
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapHealthChecks("/health");
    app.MapControllers();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "PaymentService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
