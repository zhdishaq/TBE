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
                     .Enrich.WithProperty("Service", "SearchService"));

    // SearchService is Redis-only (no DB) — uses Redis for search result caching
    builder.Services.AddTbeMassTransitWithRabbitMq(builder.Configuration);

    // Named HttpClient for FlightConnectorService — D-08: no project reference, HTTP only
    builder.Services.AddHttpClient("flight-connector", c =>
    {
        c.BaseAddress = new Uri(
            builder.Configuration["Services:FlightConnector:BaseUrl"]
            ?? "http://flightconnectorservice");
    });

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
    Log.Fatal(ex, "SearchService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
