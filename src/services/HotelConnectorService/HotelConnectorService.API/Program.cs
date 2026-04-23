using Refit;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.Common.Messaging;
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
                     .Enrich.WithProperty("Service", "HotelConnectorService"));

    // Shared OTel + PII/PCI scrubbing (COMP-05 / COMP-06).
    builder.Services.AddTbeOpenTelemetry(builder.Configuration, "HotelConnectorService");

    // HotelConnectorService is stateless — no DB or outbox
    builder.Services.AddTbeMassTransitWithRabbitMq(builder.Configuration);

    // Hotelbeds adapter
    builder.Services.Configure<TBE.HotelConnectorService.Application.Hotelbeds.HotelbedsOptions>(
        builder.Configuration.GetSection("Hotelbeds"));
    builder.Services.AddTransient<TBE.HotelConnectorService.Application.Hotelbeds.HotelbedsHmacHandler>();
    builder.Services
        .AddRefitClient<TBE.HotelConnectorService.Application.Hotelbeds.IHotelbedsApi>()
        .ConfigureHttpClient(c => c.BaseAddress = new Uri(
            builder.Configuration["Hotelbeds:BaseUrl"]
            ?? "https://api.test.hotelbeds.com/hotel-api/1.0"))
        .AddHttpMessageHandler<TBE.HotelConnectorService.Application.Hotelbeds.HotelbedsHmacHandler>()
        .AddStandardResilienceHandler();
    builder.Services.AddSingleton<TBE.Contracts.Inventory.IHotelAvailabilityProvider,
        TBE.HotelConnectorService.Application.Hotelbeds.HotelbedsProvider>();

    // Amadeus Transfer (car hire) adapter
    builder.Services.Configure<TBE.HotelConnectorService.Application.Car.AmadeusCarOptions>(
        builder.Configuration.GetSection("Amadeus")); // reuse same Amadeus config section
    builder.Services.AddHttpClient("amadeus-car-auth");
    builder.Services.AddTransient<TBE.HotelConnectorService.Application.Car.AmadeusCarAuthHandler>();
    builder.Services
        .AddRefitClient<TBE.HotelConnectorService.Application.Car.IAmadeusTransferApi>()
        .ConfigureHttpClient(c => c.BaseAddress = new Uri(
            builder.Configuration["Amadeus:CarBaseUrl"]
            ?? "https://test.api.amadeus.com/v1"))
        .AddHttpMessageHandler<TBE.HotelConnectorService.Application.Car.AmadeusCarAuthHandler>()
        .AddStandardResilienceHandler();
    builder.Services.AddSingleton<TBE.Contracts.Inventory.ICarAvailabilityProvider,
        TBE.HotelConnectorService.Application.Car.AmadeusCarProvider>();

    builder.Services.AddControllers();
    builder.Services.AddTbeSwagger("HotelConnectorService");

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
    app.MapHealthChecks("/health");
    app.MapControllers();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "HotelConnectorService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
