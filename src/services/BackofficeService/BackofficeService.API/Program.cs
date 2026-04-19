using System.Security.Claims;
using System.Text.Json;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.BackofficeService.Application.Consumers;
using TBE.BackofficeService.Infrastructure;
using TBE.Common.Messaging;

// Plan 06-01 Task 3 — BackofficeService bootstrap.
//
// Mirrors PaymentService.API/Program.cs (Plan 05-01 shape) with three
// Phase 6 deltas:
//   1. JWT scheme "Backoffice" pinned to realm tbe-backoffice (audience
//      tbe-api, ValidateAudience=true, Pitfall 4 pin). Realm_access.roles
//      are projected into flat "roles" claims so RequireRole works.
//   2. Four named authorization policies (ops-read / ops-cs / ops-finance
//      / ops-admin) — each calls AddAuthenticationSchemes("Backoffice")
//      so a token from another realm cannot satisfy a Backoffice policy.
//   3. MassTransit binds ErrorQueueConsumer to the 10 known _error queues
//      (RESEARCH Pattern 2 §"which _error queues to bind"). The consumer
//      body (Task 4) persists dead-letters into backoffice.DeadLetterQueue
//      with MassTransit fault headers intact.

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
                     .Enrich.WithProperty("Service", "BackofficeService"));

    builder.Services.AddDbContext<BackofficeDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("BackofficeDb"),
            sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

    // AddApplicationPart registers the Infrastructure assembly with MVC so
    // DlqController + future Phase 6 controllers are discovered without
    // being in the API project itself (Clean Architecture: API hosts
    // Program.cs + shared middleware; controllers co-locate with the
    // DbContext they depend on).
    builder.Services.AddControllers()
        .AddApplicationPart(typeof(BackofficeDbContext).Assembly);

    // Keycloak JWT (tbe-backoffice realm). Pinned scheme name "Backoffice"
    // so every Phase 6 policy can close Pitfall 4 via AddAuthenticationSchemes.
    var keycloakBaseUrl = builder.Configuration["Keycloak:BaseUrl"]
        ?? "http://keycloak:8080";
    var backofficeIssuer = builder.Configuration["Keycloak:Backoffice:Authority"]
        ?? $"{keycloakBaseUrl}/realms/tbe-backoffice";

    builder.Services.AddAuthentication("Backoffice")
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
                // Keycloak nests realm roles inside realm_access.roles.
                // Expand into flat "roles" claims so RequireRole(...) works
                // without every policy having to crack the envelope.
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

    // Four ops-* policies — Pitfall 4 pin: every policy names the Backoffice
    // scheme so a B2B or B2C token never satisfies a backoffice policy.
    builder.Services.AddAuthorization(opt =>
    {
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
    });

    // MassTransit + RabbitMQ + EF outbox, mirroring Plan 03-01 BookingService.
    // The backoffice service is a LISTENER on every _error queue; Phase 6
    // Task 4 puts the real persistence logic behind ErrorQueueConsumer.
    builder.Services.AddTbeMassTransitWithRabbitMq(
        builder.Configuration,
        configureConsumers: x =>
        {
            x.AddConsumer<ErrorQueueConsumer>();
        },
        configureOutbox: x =>
        {
            x.AddEntityFrameworkOutbox<BackofficeDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromSeconds(5);
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });
        },
        configureBus: (ctx, cfg) =>
        {
            // Static list of _error queues per RESEARCH Pattern 2.
            // Add new entries here when a new MassTransit consumer ships.
            string[] errorQueues =
            {
                "booking-saga_error",
                "stripe-webhook_error",
                "wallet-low-balance_error",
                "wallet-top-up_error",
                "ticketing-deadline_error",
                "booking-confirmed_error",
                "booking-cancelled_error",
                "ticket-issued_error",
                "user-registered_error",
                "wallet-top-up-confirmed_error",
            };
            foreach (var q in errorQueues)
            {
                cfg.ReceiveEndpoint(q, e =>
                {
                    // UseRawJsonDeserializer: the envelope on _error queues is
                    // opaque JSON authored by the originating service —
                    // binding it to IConsumer<JsonObject> avoids a cross-service
                    // type registry.
                    e.UseRawJsonDeserializer();
                    e.ConfigureConsumer<ErrorQueueConsumer>(ctx);
                });
            }
        });

    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("BackofficeDb")!,
            name: "backoffice-db",
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
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapHealthChecks("/health");
    app.MapControllers();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "BackofficeService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
