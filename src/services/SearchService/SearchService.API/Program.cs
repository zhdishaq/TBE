using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Serilog.Formatting.Compact;
using StackExchange.Redis;
using TBE.Common.Messaging;
using TBE.SearchService.Application.Airports;
using TBE.SearchService.Application.Cache;
using TBE.Common.Telemetry;
using Microsoft.AspNetCore.Authentication.JwtBearer;

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

    builder.Services.AddTbeOpenTelemetry(builder.Configuration, "SearchService");
    builder.Services.AddTbeMassTransitWithRabbitMq(builder.Configuration);

    // ── Keycloak JWT authentication ─────────────────────────────────────────
    // SearchService accepts B2C tokens (default scheme) — gateway routes B2B
    // traffic for /api/b2b/search/* but the gateway has already validated those
    // tokens with the B2BPolicy. Gateway always forwards Bearer header upstream;
    // the search service revalidates as a defence-in-depth layer.
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
        {
            o.Authority = builder.Configuration["Keycloak:Authority"];
            o.Audience  = builder.Configuration["Keycloak:Audience"];
            o.RequireHttpsMetadata = builder.Environment.IsProduction();
        });
    builder.Services.AddAuthorization();

    // Named HttpClient for FlightConnectorService — D-08: no project reference, HTTP only
    builder.Services.AddHttpClient("flight-connector", c =>
    {
        c.BaseAddress = new Uri(
            builder.Configuration["Services:FlightConnector:BaseUrl"]
            ?? "http://flight-connector-service:8080");
    });

    // Pricing service named client
    builder.Services.AddHttpClient("pricing-service", c =>
    {
        c.BaseAddress = new Uri(
            builder.Configuration["Services:PricingService:BaseUrl"]
            ?? "http://pricing-service:8080");
    });

    // Redis distributed cache
    builder.Services.AddStackExchangeRedisCache(opts =>
    {
        opts.Configuration = builder.Configuration["Redis:ConnectionString"]
            ?? builder.Configuration.GetConnectionString("Redis")
            ?? "localhost:6378";
    });

    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var cs = builder.Configuration["Redis:ConnectionString"]
            ?? builder.Configuration.GetConnectionString("Redis")
            ?? "localhost:6378";
        var options = ConfigurationOptions.Parse(cs);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 3;
        options.ConnectTimeout = 5000;
        return ConnectionMultiplexer.Connect(options);
    });

    builder.Services.AddSingleton<IAirportLookup, RedisAirportLookup>();
    builder.Services.AddHostedService<IataAirportSeeder>();

    builder.Services.AddHybridCache(opts =>
    {
        opts.MaximumPayloadBytes = 1024 * 1024;
        opts.MaximumKeyLength = 512;
    });

    builder.Services.AddSingleton<ISearchCacheService, SearchCacheService>();

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
            limiterOpts.QueueLimit        = 0;
        });

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

    builder.Services.AddResponseCaching();
    builder.Services.AddTbeSwagger("SearchService");
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
    app.UseResponseCaching();
    app.UseRateLimiter();
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
    Log.Fatal(ex, "SearchService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
