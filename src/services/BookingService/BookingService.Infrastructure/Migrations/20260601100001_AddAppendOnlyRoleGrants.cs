using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.BookingService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 06-01 Task 5 (BO-04 / D-49) — SQL Server-level append-only
    /// enforcement for <c>dbo.BookingEvents</c>.
    ///
    /// <para>
    /// Creates a database role <c>booking_events_writer</c> with
    /// <c>INSERT + SELECT</c> granted and <c>UPDATE + DELETE</c> DENY'd.
    /// The application login <c>tbe_booking_app</c> is added as a member
    /// so every statement the BookingService runs against this table is
    /// evaluated against the DENY permissions — UPDATE and DELETE throw
    /// <c>SqlException</c> with error number 229 (permission denied)
    /// regardless of any T-SQL author. This is the PRIMARY BO-04 control;
    /// the writer-only DbContext is defence-in-depth above it.
    /// </para>
    /// <para>
    /// Fallback idempotent CREATE USER block at the top: when the
    /// integration-test Testcontainers fixture starts a fresh MsSql the
    /// <c>tbe_booking_app</c> login does not yet exist. We create a
    /// login-less user in that case so the role membership assignment
    /// still succeeds. Production deploys use a real SQL login mapped
    /// via ops provisioning.
    /// </para>
    /// </summary>
    public partial class AddAppendOnlyRoleGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'tbe_booking_app')
BEGIN
    CREATE USER tbe_booking_app WITHOUT LOGIN;
END;

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'booking_events_writer' AND type = 'R')
BEGIN
    CREATE ROLE booking_events_writer;
END;

GRANT INSERT, SELECT ON dbo.BookingEvents TO booking_events_writer;
DENY UPDATE, DELETE ON dbo.BookingEvents TO booking_events_writer;

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members rm
    JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
    JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
    WHERE r.name = 'booking_events_writer' AND m.name = 'tbe_booking_app')
BEGIN
    ALTER ROLE booking_events_writer ADD MEMBER tbe_booking_app;
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'booking_events_writer' AND type = 'R')
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.database_role_members rm
        JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
        JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
        WHERE r.name = 'booking_events_writer' AND m.name = 'tbe_booking_app')
    BEGIN
        ALTER ROLE booking_events_writer DROP MEMBER tbe_booking_app;
    END;

    REVOKE INSERT, SELECT ON dbo.BookingEvents FROM booking_events_writer;
    REVOKE UPDATE, DELETE ON dbo.BookingEvents FROM booking_events_writer;

    DROP ROLE booking_events_writer;
END;
");
        }
    }
}
