using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.Common.Messaging;
using TBE.Common.Security;
using TBE.Common.Telemetry;
using TBE.PaymentService.Application.Consumers;
using TBE.PaymentService.Application.Stripe;
using TBE.PaymentService.Application.Wallet;
using TBE.PaymentService.Infrastructure;
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
    builder.Services.AddSingleton<IStripePaymentGateway, StripePaymentGateway>();
    builder.Services.AddScoped<IWalletRepository, WalletRepository>();
    builder.Services.AddScoped<IWalletTopUpService, WalletTopUpService>();

    builder.Services.AddControllers();

    // Keycloak JWT — enforced on every controller unless [AllowAnonymous] (StripeWebhookController).
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

        // Plan 05-03: named policies mirroring the B2B portal role model.
        // B2BPolicy: any authenticated B2B user (admin, agent, readonly).
        // B2BAdminPolicy: agent-admin role only — required for mutation endpoints
        // (/api/wallet/top-up/intent, /api/wallet/transactions, /api/agents/*).
        opt.AddPolicy("B2BPolicy", p => p.RequireAuthenticatedUser());
        opt.AddPolicy("B2BAdminPolicy", p =>
            p.RequireAuthenticatedUser().RequireRole("agent-admin"));
    });

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
