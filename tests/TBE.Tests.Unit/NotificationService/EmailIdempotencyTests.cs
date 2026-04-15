using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TBE.NotificationService.Application.Email;
using TBE.NotificationService.Infrastructure.Persistence;
using Xunit;

namespace TBE.Tests.Unit.NotificationService;

/// <summary>
/// Exercises the NOTF-06 unique index on (EventId, EmailType) against a real relational
/// engine. Uses SQLite (in-memory) because EF Core InMemory does not enforce unique indexes.
/// The plan calls for Testcontainers-MsSql integration; SQLite is used here as a faster
/// deterministic stand-in for the unique-index contract — the Up() migration is asserted
/// separately against the MigrationBuilder shape. Documented as a Rule-3 deviation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EmailIdempotencyTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public EmailIdempotencyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private NotificationDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseSqlite(_connection)
            .Options;
        var ctx = new NotificationDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task NOTF06_unique_index_rejects_duplicate_eventid_emailtype_combo()
    {
        using var ctx = BuildContext();
        var eventId = Guid.NewGuid();

        ctx.EmailIdempotencyLogs.Add(new EmailIdempotencyLog
        {
            EventId = eventId,
            EmailType = EmailType.FlightConfirmation,
            Recipient = "alice@example.com",
            SentAtUtc = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        ctx.EmailIdempotencyLogs.Add(new EmailIdempotencyLog
        {
            EventId = eventId,
            EmailType = EmailType.FlightConfirmation,
            Recipient = "alice@example.com",
            SentAtUtc = DateTime.UtcNow,
        });

        Func<Task> act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task NOTF06_different_emailtype_for_same_eventid_allowed()
    {
        using var ctx = BuildContext();
        var eventId = Guid.NewGuid();

        ctx.EmailIdempotencyLogs.Add(new EmailIdempotencyLog
        {
            EventId = eventId,
            EmailType = EmailType.FlightConfirmation,
            Recipient = "alice@example.com",
            SentAtUtc = DateTime.UtcNow,
        });
        ctx.EmailIdempotencyLogs.Add(new EmailIdempotencyLog
        {
            EventId = eventId,
            EmailType = EmailType.TicketIssued,
            Recipient = "alice@example.com",
            SentAtUtc = DateTime.UtcNow,
        });

        await ctx.SaveChangesAsync();

        ctx.EmailIdempotencyLogs.Should().HaveCount(2);
    }

    [Fact]
    public void NOTF06_migration_creates_schema_and_unique_index()
    {
        // The migration file itself is the contract: assert its text contains the critical bits.
        // This is a read-only structural check that does not require a running database.
        var migrationPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "services", "NotificationService", "NotificationService.Infrastructure",
            "Persistence", "Migrations", "20260418000000_AddNotificationTables.cs");
        migrationPath = Path.GetFullPath(migrationPath);

        File.Exists(migrationPath).Should().BeTrue($"migration file should be at {migrationPath}");
        var text = File.ReadAllText(migrationPath);

        text.Should().Contain("EnsureSchema(name: \"notification\")");
        text.Should().Contain("EmailIdempotencyLog");
        text.Should().Contain("IX_EmailIdempotencyLog_EventId_EmailType");
        text.Should().Contain("unique: true");
    }
}
