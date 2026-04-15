using Refit;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.Common.Messaging;

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
    builder.Services.AddControllers();

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
    app.MapHealthChecks("/health");
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
