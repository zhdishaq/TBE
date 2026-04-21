using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Refit;
using Serilog;
using Serilog.Formatting.Compact;
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
                     .Enrich.WithProperty("Service", "FlightConnectorService"));

    // FlightConnectorService is stateless — no DB or outbox
    builder.Services.AddTbeMassTransitWithRabbitMq(builder.Configuration);

    // Amadeus adapter
    builder.Services.Configure<TBE.FlightConnectorService.Application.Amadeus.AmadeusOptions>(
        builder.Configuration.GetSection("Amadeus"));
    builder.Services.AddHttpClient("amadeus-auth");
    builder.Services.AddTransient<TBE.FlightConnectorService.Application.Amadeus.AmadeusAuthHandler>();
    builder.Services
        .AddRefitClient<TBE.FlightConnectorService.Application.Amadeus.IAmadeusFlightApi>()
        .ConfigureHttpClient(c => c.BaseAddress = new Uri(
            builder.Configuration["Amadeus:BaseUrl"] ?? "https://test.api.amadeus.com/v2"))
        .AddHttpMessageHandler<TBE.FlightConnectorService.Application.Amadeus.AmadeusAuthHandler>()
        .AddStandardResilienceHandler();
    builder.Services.AddKeyedSingleton<TBE.Contracts.Inventory.IFlightAvailabilityProvider,
        TBE.FlightConnectorService.Application.Amadeus.AmadeusFlightProvider>("amadeus");

    // Sabre adapter
    builder.Services.Configure<TBE.FlightConnectorService.Application.Sabre.SabreOptions>(
        builder.Configuration.GetSection("Sabre"));
    builder.Services.AddHttpClient("sabre-auth");
    builder.Services.AddTransient<TBE.FlightConnectorService.Application.Sabre.SabreAuthHandler>();
    builder.Services
        .AddRefitClient<TBE.FlightConnectorService.Application.Sabre.ISabreFlightApi>()
        .ConfigureHttpClient(c => c.BaseAddress = new Uri(
            builder.Configuration["Sabre:BaseUrl"] ?? "https://api.havail.sabre.com"))
        .AddHttpMessageHandler<TBE.FlightConnectorService.Application.Sabre.SabreAuthHandler>()
        .AddStandardResilienceHandler();
    builder.Services.AddKeyedSingleton<TBE.Contracts.Inventory.IFlightAvailabilityProvider,
        TBE.FlightConnectorService.Application.Sabre.SabreFlightProvider>("sabre");

    // Expose all providers as IEnumerable for the multi-provider controller action
    builder.Services.AddSingleton<IEnumerable<TBE.Contracts.Inventory.IFlightAvailabilityProvider>>(sp => [
        sp.GetRequiredKeyedService<TBE.Contracts.Inventory.IFlightAvailabilityProvider>("amadeus"),
        sp.GetRequiredKeyedService<TBE.Contracts.Inventory.IFlightAvailabilityProvider>("sabre"),
    ]);

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
    builder.Services.AddTbeOpenTelemetry(builder.Configuration, "FlightConnectorService");
    builder.Services.Configure<EncryptionOptions>(builder.Configuration.GetSection("Encryption"));
    builder.Services.AddSingleton<IEncryptionKeyProvider, EnvEncryptionKeyProvider>();
    builder.Services.AddSingleton<AesGcmFieldEncryptor>();

    builder.Services.AddControllers();
    builder.Services.AddTbeSwagger("FlightConnectorService");

    builder.Services.AddHealthChecks()
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
    Log.Fatal(ex, "FlightConnectorService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
