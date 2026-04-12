using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.BookingService.Application.Consumers;
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

    // MassTransit with RabbitMQ and outbox
    builder.Services.AddTbeMassTransitWithRabbitMq(
        builder.Configuration,
        configureConsumers: x =>
        {
            x.AddConsumer<TestBookingConsumer>();
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
