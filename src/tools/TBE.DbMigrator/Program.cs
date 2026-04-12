using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.BookingService.Infrastructure;
using TBE.PaymentService.Infrastructure;
using TBE.PricingService.Infrastructure;
using TBE.NotificationService.Infrastructure;
using TBE.CrmService.Infrastructure;
using TBE.BackofficeService.Infrastructure;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    Log.Information("TBE.DbMigrator starting");

    var config = new ConfigurationBuilder()
        .AddEnvironmentVariables()
        .Build();

    async Task MigrateAsync<TContext>(string connectionStringKey) where TContext : DbContext
    {
        var connectionString = config[$"ConnectionStrings__{connectionStringKey}"]
            ?? config.GetConnectionString(connectionStringKey)
            ?? throw new InvalidOperationException($"Missing connection string: {connectionStringKey}");

        Log.Information("Migrating {DbContext} using key {Key}", typeof(TContext).Name, connectionStringKey);

        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null))
            .Options;

        using var ctx = (TContext)Activator.CreateInstance(typeof(TContext), options)!;
        await ctx.Database.MigrateAsync();
        Log.Information("{DbContext} migration complete", typeof(TContext).Name);
    }

    // Migrate each service database sequentially
    // Order does not matter — each DbContext targets a separate database
    await MigrateAsync<BookingDbContext>("BookingDb");
    await MigrateAsync<PaymentDbContext>("PaymentDb");
    await MigrateAsync<PricingDbContext>("PricingDb");
    await MigrateAsync<NotificationDbContext>("NotificationDb");
    await MigrateAsync<CrmDbContext>("CrmDb");
    await MigrateAsync<BackofficeDbContext>("BackofficeDb");

    // Stateless services (no DbContext): ensure the database exists via a plain connection
    // Uses EnsureCreated with minimal DbContext for SearchDb, FlightConnectorDb, HotelConnectorDb
    async Task EnsureDbExistsAsync(string connectionStringKey)
    {
        var connectionString = config[$"ConnectionStrings__{connectionStringKey}"]
            ?? config.GetConnectionString(connectionStringKey)
            ?? throw new InvalidOperationException($"Missing connection string: {connectionStringKey}");

        Log.Information("Ensuring database exists for {Key}", connectionStringKey);
        var options = new DbContextOptionsBuilder<EmptyDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        using var ctx = new EmptyDbContext(options);
        await ctx.Database.EnsureCreatedAsync();
        Log.Information("Database {Key} exists", connectionStringKey);
    }

    await EnsureDbExistsAsync("SearchDb");
    await EnsureDbExistsAsync("FlightConnectorDb");
    await EnsureDbExistsAsync("HotelConnectorDb");

    Log.Information("TBE.DbMigrator completed successfully — all databases ready");
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "TBE.DbMigrator failed — check MSSQL connection and credentials");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Minimal DbContext for stateless services that only need a database to exist
public class EmptyDbContext : DbContext
{
    public EmptyDbContext(DbContextOptions<EmptyDbContext> options) : base(options) { }
}
