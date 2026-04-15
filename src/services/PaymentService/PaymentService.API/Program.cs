using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.Common.Messaging;
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
    builder.Services.AddSingleton<IStripePaymentGateway, StripePaymentGateway>();
    builder.Services.AddScoped<IWalletRepository, WalletRepository>();

    builder.Services.AddControllers();
    builder.Services.AddAuthentication();
    builder.Services.AddAuthorization();

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
