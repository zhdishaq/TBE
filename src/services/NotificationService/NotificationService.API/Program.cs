using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SendGrid;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.Common.Messaging;
using TBE.Common.Security;
using TBE.Common.Telemetry;
using TBE.NotificationService.Application.Consumers;
using TBE.NotificationService.Application.Contacts;
using TBE.NotificationService.Application.Email;
using TBE.NotificationService.Application.Pdf;
using TBE.NotificationService.Infrastructure.Contacts;
using TBE.NotificationService.Infrastructure.Email;
using TBE.NotificationService.Infrastructure.Pdf;
using TBE.NotificationService.Application.Persistence;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, configuration) =>
        configuration.ReadFrom.Configuration(builder.Configuration)
                     .ReadFrom.Services(services)
                     .Enrich.FromLogContext()
                     .Enrich.WithProperty("Service", "NotificationService"));

    // ---- Email delivery (NOTF-01 backbone) ----
    builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection("SendGrid"));
    builder.Services.AddSingleton<ISendGridClient>(_ =>
    {
        var key = builder.Configuration["SendGrid:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "SendGrid:ApiKey is missing — set SENDGRID__APIKEY in the .env before starting NotificationService.");
        }
        return new SendGridClient(key);
    });
    builder.Services.AddScoped<IEmailDelivery, SendGridEmailDelivery>();
    builder.Services.AddSingleton<IEmailTemplateRenderer, RazorLightEmailTemplateRenderer>();
    builder.Services.AddSingleton<IETicketPdfGenerator, QuestPdfETicketGenerator>();
    builder.Services.AddSingleton<IHotelVoucherPdfGenerator, HotelVoucherDocument>();
    builder.Services.Configure<BrandOptions>(builder.Configuration.GetSection("Branding"));

    // Keycloak JWT + FallbackPolicy (COMP-05). NotificationService exposes no public controllers
    // today, but if any are added they'll be auth-gated by default.
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
    builder.Services.AddTbeOpenTelemetry(builder.Configuration, "NotificationService");
    builder.Services.Configure<EncryptionOptions>(builder.Configuration.GetSection("Encryption"));
    builder.Services.AddSingleton<IEncryptionKeyProvider, EnvEncryptionKeyProvider>();
    builder.Services.AddSingleton<AesGcmFieldEncryptor>();

    // ---- Persistence (NOTF-06 EmailIdempotencyLog) ----
    builder.Services.AddDbContext<NotificationDbContext>(opt =>
        opt.UseSqlServer(
            builder.Configuration.GetConnectionString("NotificationDb")
                ?? throw new InvalidOperationException("ConnectionStrings:NotificationDb missing"),
            sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

    // ---- BookingService contact lookup clients ----
    builder.Services.AddHttpClient<IBookingContactClient, BookingContactClient>(c =>
    {
        var baseUrl = builder.Configuration["Services:BookingService:BaseUrl"]
            ?? throw new InvalidOperationException("Services:BookingService:BaseUrl missing");
        c.BaseAddress = new Uri(baseUrl);
    });
    builder.Services.AddHttpClient<IAgencyAdminContactClient, AgencyAdminContactClient>(c =>
    {
        var baseUrl = builder.Configuration["Services:BookingService:BaseUrl"]
            ?? throw new InvalidOperationException("Services:BookingService:BaseUrl missing");
        c.BaseAddress = new Uri(baseUrl);
    });

    // ---- MassTransit consumer host ----
    builder.Services.AddTbeMassTransitWithRabbitMq(
        builder.Configuration,
        configureConsumers: cfg =>
        {
            cfg.AddConsumer<BookingConfirmedConsumer>();
            cfg.AddConsumer<BookingCancelledConsumer>();
            cfg.AddConsumer<TicketIssuedConsumer>();
            cfg.AddConsumer<BookingExpiredConsumer>();
            cfg.AddConsumer<TicketingDeadlineApproachingConsumer>();
            cfg.AddConsumer<WalletLowBalanceConsumer>();
            cfg.AddConsumer<HotelBookingConfirmedConsumer>();
        },
        configureOutbox: x =>
        {
            x.AddEntityFrameworkOutbox<NotificationDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromSeconds(5);
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });
        });

    // ---- Health checks ----
    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("NotificationDb")!,
            name: "notification-db",
            tags: new[] { "db", "sql" })
        .AddRabbitMQ(
            factory: _ =>
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
            tags: new[] { "messaging" });

    var app = builder.Build();

    // Apply migrations on startup (idempotent).
    using (var scope = app.Services.CreateScope())
    {
        scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.Migrate();
    }

    app.MapHealthChecks("/health");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NotificationService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
