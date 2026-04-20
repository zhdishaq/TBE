using System.Security.Claims;
using System.Text.Json;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.Common.Messaging;
using TBE.CrmService.Infrastructure;
using TBE.CrmService.Infrastructure.Consumers;

// Plan 06-04 Task 1 — CrmService bootstrap.
//
// Mirrors BackofficeService.API/Program.cs with three deltas:
//   1. DbContext targets the CrmDb connection (crm schema).
//   2. MassTransit registers 6 Phase-6 consumers + EF outbox for
//      InboxState dedup on MessageId (D-51 event-sourced projections).
//   3. BackofficeService-style 4 policies (ops-read / ops-cs /
//      ops-finance / ops-admin) with AddAuthenticationSchemes("Backoffice")
//      pin on every policy (Pitfall 4).
//
// No B2C / B2B schemes — CRM is ops-only. Payments' credit-limit
// fan-out and BookingService's erasure fan-out are orchestrated via
// TBE.Contracts.Events; no HTTP client to peer services.

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
                     .Enrich.WithProperty("Service", "CrmService"));

    builder.Services.AddDbContext<CrmDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("CrmDb"),
            sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

    // Keycloak JWT (tbe-backoffice realm) — CRM is an ops-only surface.
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

    builder.Services.AddControllers();

    builder.Services.AddTbeMassTransitWithRabbitMq(
        builder.Configuration,
        configureConsumers: x =>
        {
            // 6 Task-1 consumers + 1 Task-3 consumer (COMP-03 erasure fan-out).
            x.AddConsumer<BookingConfirmedConsumer>();
            x.AddConsumer<BookingCancelledConsumer>();
            x.AddConsumer<TicketIssuedConsumer>();
            x.AddConsumer<UserRegisteredConsumer>();
            x.AddConsumer<WalletTopUpConsumer>();
            x.AddConsumer<CustomerCommunicationLoggedConsumer>();
            x.AddConsumer<CustomerErasureRequestedConsumer>();
        },
        configureOutbox: x =>
        {
            x.AddEntityFrameworkOutbox<CrmDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromSeconds(5);
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });
        });

    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("CrmDb")!,
            name: "crm-db",
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
    Log.Fatal(ex, "CrmService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
