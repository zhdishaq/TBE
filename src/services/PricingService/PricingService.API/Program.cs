using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.PricingService.Application.Agency;
using TBE.PricingService.Application.Rules;
using TBE.PricingService.Infrastructure;
using TBE.PricingService.Infrastructure.Agency;
using TBE.PricingService.Infrastructure.Rules;
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
                     .Enrich.WithProperty("Service", "PricingService"));

    builder.Services.AddDbContext<PricingDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("PricingDb"),
            sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

    builder.Services.AddTbeMassTransitWithRabbitMq(
        builder.Configuration,
        // Plan 05-02 Task 1: register AgencyPriceRequestedConsumer via the
        // Application-layer extension (keeps consumer wiring adjacent to the
        // consumer type rather than scattered across Program.cs).
        configureConsumers: cfg => cfg.AddAgencyPricingConsumers(),
        configureOutbox: x =>
        {
            x.AddEntityFrameworkOutbox<PricingDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromSeconds(5);
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });
        });

    builder.Services.AddScoped<IPricingRulesEngine, MarkupRulesEngine>();
    // Plan 05-02 Task 1: D-36 per-agency markup resolver.
    builder.Services.AddScoped<IAgencyMarkupRulesEngine, AgencyMarkupRulesEngine>();

    // Keycloak JWT + FallbackPolicy (COMP-05).
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

    // Shared OTel + AES-GCM primitives.
    builder.Services.AddTbeOpenTelemetry(builder.Configuration, "PricingService");
    builder.Services.Configure<EncryptionOptions>(builder.Configuration.GetSection("Encryption"));
    builder.Services.AddSingleton<IEncryptionKeyProvider, EnvEncryptionKeyProvider>();
    builder.Services.AddSingleton<AesGcmFieldEncryptor>();

    builder.Services.AddControllers();
    builder.Services.AddTbeSwagger("PricingService");

    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("PricingDb")!,
            name: "pricing-db",
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
    app.MapHealthChecks("/health").AllowAnonymous();
    app.MapControllers();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "PricingService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
