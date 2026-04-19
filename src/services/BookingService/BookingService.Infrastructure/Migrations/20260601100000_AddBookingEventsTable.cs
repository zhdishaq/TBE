using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.BookingService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 06-01 Task 5 (BO-04 / BO-05 / D-50) — creates
    /// <c>dbo.BookingEvents</c>, the append-only audit log for every
    /// BookingSaga state transition.
    ///
    /// Schema <c>dbo</c> (not <c>Saga</c>) so ops tooling can scan the
    /// default schema for audit data without schema qualification, and
    /// so the DENY role grant in migration
    /// <c>20260601100001_AddAppendOnlyRoleGrants</c> targets a stable
    /// object name.
    ///
    /// No outbox columns — this context is writer-only by design
    /// (Pitfall 1): the ChangeTracker cannot accidentally issue UPDATE
    /// because there's no foreign <c>DbSet</c> to traverse into.
    /// </summary>
    /// <remarks>
    /// Columns (matches BookingEventsDbContext.OnModelCreating):
    /// <list type="bullet">
    ///   <item><c>EventId uniqueidentifier PK</c> — caller-supplied GUID v4 (value never auto-generated).</item>
    ///   <item><c>BookingId uniqueidentifier</c> — saga correlation id.</item>
    ///   <item><c>EventType nvarchar(64)</c> — one of BookingInitiated / PriceReconfirmed / PnrCreated / PaymentAuthorized / TicketIssued / PaymentCaptured / BookingConfirmed / BookingCancelled / StaffCancellationRequested / StaffCancellationApproved.</item>
    ///   <item><c>OccurredAt datetime2</c> — UTC timestamp from BookingEventsWriter.</item>
    ///   <item><c>Actor nvarchar(128)</c> — "system:BookingSaga" or preferred_username.</item>
    ///   <item><c>CorrelationId uniqueidentifier</c> — MT ConsumeContext correlation.</item>
    ///   <item><c>Snapshot nvarchar(max)</c> — serialized JSON envelope per D-50.</item>
    /// </list>
    ///
    /// Indexes:
    /// <list type="bullet">
    ///   <item><c>IX_BookingEvents_BookingId</c> — ad-hoc timeline lookup.</item>
    ///   <item><c>IX_BookingEvents_BookingId_OccurredAt</c> — ORDER BY OccurredAt covered.</item>
    /// </list>
    /// </remarks>
    public partial class AddBookingEventsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingEvents",
                schema: "dbo",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Snapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingEvents", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingEvents_BookingId",
                schema: "dbo",
                table: "BookingEvents",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingEvents_BookingId_OccurredAt",
                schema: "dbo",
                table: "BookingEvents",
                columns: new[] { "BookingId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BookingEvents", schema: "dbo");
        }
    }
}
