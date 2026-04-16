using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Serilog.Formatting.Compact;
using StackExchange.Redis;
using TBE.Common.Messaging;
using TBE.SearchService.Application.Airports;
using TBE.SearchService.Application.Cache;

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

    // Pricing service named client
    builder.Services.AddHttpClient("pricing-service", c =>
    {
        c.BaseAddress = new Uri(
            builder.Configuration["Services:PricingService:BaseUrl"]
            ?? "http://pricingservice");
    });

    // Redis connection for distributed cache (HybridCache L2 + booking tokens)
    builder.Services.AddStackExchangeRedisCache(opts =>
    {
        opts.Configuration = builder.Configuration.GetConnectionString("Redis")
            ?? "localhost:6379";
    });

    // Shared IConnectionMultiplexer — used by IataAirportSeeder and RedisAirportLookup
    // (StackExchangeRedisCache uses its own internal multiplexer, so we keep this one
    // explicitly registered as a singleton for the airports subsystem.)
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var cs = builder.Configuration.GetConnectionString("Redis")
            ?? builder.Configuration["Redis:ConnectionString"]
            ?? "localhost:6379";
        return ConnectionMultiplexer.Connect(cs);
    });

    // IATA airport typeahead (CONTEXT D-18). Seeder runs at startup and populates
    // iata:airports + iata:idx:prefix; RedisAirportLookup serves /airports queries.
    builder.Services.AddSingleton<IAirportLookup, RedisAirportLookup>();
    builder.Services.AddHostedService<IataAirportSeeder>();

    // HybridCache (L1 in-process + L2 Redis)
    builder.Services.AddHybridCache(opts =>
    {
        opts.MaximumPayloadBytes = 1024 * 1024;  // 1 MB max per cache entry
        opts.MaximumKeyLength = 512;
    });

    // SearchCacheService
    builder.Services.AddSingleton<ISearchCacheService, SearchCacheService>();

    // GDS rate-limit guard: in-process sliding window 8 req/sec (upgrade to distributed in Phase 7)
    // T-02-04-03: enforced in SearchController before FlightConnector HTTP call
    builder.Services.AddRateLimiter(opts =>
    {
        opts.OnRejected = async (context, ct) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.Headers["Retry-After"] = "1";
            await context.HttpContext.Response.WriteAsync("GDS rate limit reached. Retry in 1 second.", ct);
        };

        opts.AddSlidingWindowLimiter("gds-rate-limit", limiterOpts =>
        {
            limiterOpts.PermitLimit       = 8;
            limiterOpts.Window            = TimeSpan.FromSeconds(1);
            limiterOpts.SegmentsPerWindow = 4;
            limiterOpts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOpts.QueueLimit        = 0;  // no queue — reject immediately
        });

        // Airports typeahead: 60 req/min/IP fixed window (T-04-02-04). Partitioned
        // by client IP so a single abusive caller cannot starve the endpoint for
        // other users. Anonymous endpoint — no Authorization header to key off.
        opts.AddPolicy("airports", httpContext =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString()
                     ?? httpContext.Request.Headers["X-Forwarded-For"].ToString()
                     ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = 60,
                Window               = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0,
            });
        });
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
    app.UseRateLimiter();
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
