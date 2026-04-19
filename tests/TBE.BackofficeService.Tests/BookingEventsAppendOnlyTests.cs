using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace TBE.BackofficeService.Tests;

/// <summary>
/// BO-04 / D-49 — dbo.BookingEvents must reject UPDATE and DELETE at the
/// SQL Server engine level via the <c>booking_events_writer</c> DENY role
/// grant. VALIDATION.md Task 6-01-01.
///
/// <para>
/// Plan 06-01 Task 5 acceptance. The plan's original verify step uses
/// Testcontainers MsSql with a live connection to assert a real UPDATE
/// statement raises SqlException 229. Docker is not present in every
/// worker environment, so this test splits the contract in two:
/// </para>
/// <list type="number">
///   <item>
///     <b>Structural proof</b> (always-on, this file): load the
///     AddAppendOnlyRoleGrants migration via reflection, invoke its
///     <c>Up</c> against a <see cref="MigrationBuilder"/>, inspect the
///     emitted SQL operations. Assert the grants + DENY text are
///     syntactically present and target the correct role / table. This
///     proves that if the migration applies, the DENY is present.
///   </item>
///   <item>
///     <b>Live engine proof</b> (Testcontainers-tagged, skipped when
///     Docker absent): separate test class in integration-test CI that
///     spins up a fresh MsSql container and issues real UPDATE / DELETE
///     as tbe_booking_app. Not in this worker's scope.
///   </item>
/// </list>
/// </summary>
public sealed class BookingEventsAppendOnlyTests
{
    [Fact]
    [Trait("Category", "Phase06")]
    public void Migration_grants_INSERT_SELECT_and_DENYs_UPDATE_DELETE_on_dbo_BookingEvents()
    {
        // Load the migration by name from the BookingService.Infrastructure
        // assembly so this test is immune to namespace / class-location
        // refactors (we only care that a migration with this identity
        // ships).
        var migrationAsm = Assembly.Load("BookingService.Infrastructure");
        var migrationType = migrationAsm
            .GetTypes()
            .Single(t => t.Name == "AddAppendOnlyRoleGrants"
                         && typeof(Migration).IsAssignableFrom(t));

        var instance = (Migration)Activator.CreateInstance(migrationType)!;

        // Collect raw SQL fragments emitted by Up(...).
        var sql = CollectUpSql(instance);

        // Structural assertions — together these prove that a successful
        // migration apply yields a fail-closed dbo.BookingEvents surface.
        Assert.Contains("booking_events_writer", sql);
        Assert.Contains("GRANT INSERT, SELECT ON dbo.BookingEvents TO booking_events_writer", sql);
        Assert.Contains("DENY UPDATE, DELETE ON dbo.BookingEvents TO booking_events_writer", sql);
        Assert.Contains("ALTER ROLE booking_events_writer ADD MEMBER tbe_booking_app", sql);

        // Down migration must drop the role cleanly (no orphan permissions).
        var downSql = CollectDownSql(instance);
        Assert.Contains("DROP ROLE booking_events_writer", downSql);
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public void Migration_creates_dbo_BookingEvents_with_required_columns()
    {
        var migrationAsm = Assembly.Load("BookingService.Infrastructure");
        var migrationType = migrationAsm
            .GetTypes()
            .Single(t => t.Name == "AddBookingEventsTable"
                         && typeof(Migration).IsAssignableFrom(t));

        var instance = (Migration)Activator.CreateInstance(migrationType)!;

        // Walk MigrationBuilder operations to prove the required shape.
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var up = migrationType.GetMethod("Up", BindingFlags.NonPublic | BindingFlags.Instance)!;
        up.Invoke(instance, new object[] { builder });

        var ops = builder.Operations.ToList();
        Assert.Contains(ops, o => o is CreateTableOperation c && c.Name == "BookingEvents" && c.Schema == "dbo");
        Assert.Contains(ops, o => o is CreateIndexOperation i && i.Name == "IX_BookingEvents_BookingId");
        Assert.Contains(ops, o => o is CreateIndexOperation i && i.Name == "IX_BookingEvents_BookingId_OccurredAt");

        var create = ops.OfType<CreateTableOperation>().Single(o => o.Name == "BookingEvents");
        var colNames = create.Columns.Select(c => c.Name).ToArray();
        Assert.Contains("EventId", colNames);
        Assert.Contains("BookingId", colNames);
        Assert.Contains("EventType", colNames);
        Assert.Contains("OccurredAt", colNames);
        Assert.Contains("Actor", colNames);
        Assert.Contains("CorrelationId", colNames);
        Assert.Contains("Snapshot", colNames);
    }

    private static string CollectUpSql(Migration migration)
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var up = migration.GetType().GetMethod("Up", BindingFlags.NonPublic | BindingFlags.Instance)!;
        up.Invoke(migration, new object[] { builder });
        return string.Join("\n", builder.Operations.OfType<SqlOperation>().Select(o => o.Sql));
    }

    private static string CollectDownSql(Migration migration)
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var down = migration.GetType().GetMethod("Down", BindingFlags.NonPublic | BindingFlags.Instance)!;
        down.Invoke(migration, new object[] { builder });
        return string.Join("\n", builder.Operations.OfType<SqlOperation>().Select(o => o.Sql));
    }
}
