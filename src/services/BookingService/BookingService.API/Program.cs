using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.BookingService.Application.Consumers.CompensationConsumers;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Infrastructure;
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
                     .Enrich.WithProperty("Service", "BookingService"));

    // Database
    builder.Services.AddDbContext<BookingDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("BookingDb"),
            sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

    // Dead-letter store (SagaDeadLetterSink depends on ISagaDeadLetterStore)
    builder.Services.AddScoped<ISagaDeadLetterStore, SagaDeadLetterStore>();

    // JWT Bearer — Keycloak authority. Enforced globally via class-level [Authorize].
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
        {
            o.Authority = builder.Configuration["Keycloak:Authority"];
            o.Audience = builder.Configuration["Keycloak:Audience"];
            o.RequireHttpsMetadata = builder.Environment.IsProduction();
        });
    builder.Services.AddAuthorization();

    builder.Services.AddControllers();

    // MassTransit with RabbitMQ + BookingSaga + outbox
    builder.Services.AddTbeMassTransitWithRabbitMq(
        builder.Configuration,
        configureConsumers: x =>
        {
            x.AddSagaStateMachine<BookingSaga, BookingSagaState>(typeof(BookingSagaDefinition))
                .EntityFrameworkRepository(r =>
                {
                    r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                    r.ExistingDbContext<BookingDbContext>();
                    r.UseSqlServer();
                });
            x.AddConsumer<SagaDeadLetterSink>();
        },
        configureOutbox: x =>
        {
            x.AddEntityFrameworkOutbox<BookingDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromSeconds(5);
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });
        });

    // Health checks
    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("BookingDb")!,
            name: "booking-db",
            tags: new[] { "db", "sql" })
        .AddRabbitMQ(
            factory: async sp =>
            {
                var connectionFactory = new RabbitMQ.Client.ConnectionFactory
                {
                    HostName = builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq",
                    UserName = builder.Configuration["RabbitMQ:Username"] ?? "guest",
                    Password = builder.Configuration["RabbitMQ:Password"] ?? "guest"
                };
                return await connectionFactory.CreateConnectionAsync();
            },
            name: "rabbitmq",
            tags: new[] { "messaging" });

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "BookingService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Exposed so integration/controller tests (WebApplicationFactory) can reference the entry-point assembly.
/// </summary>
public partial class Program { }
