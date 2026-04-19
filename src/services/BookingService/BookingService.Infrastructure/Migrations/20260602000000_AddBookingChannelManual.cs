using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.BookingService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 06-02 Task 1 (BO-02) — extends <c>Saga.BookingSagaState</c>
    /// with columns that back <see cref="TBE.Contracts.Enums.Channel.Manual"/>
    /// = 2 staff-entered bookings:
    ///
    /// <list type="bullet">
    ///   <item><c>SupplierReference</c> nvarchar(128) NULL — NDC PNR /
    ///     hotel confirmation code captured by the wizard.</item>
    ///   <item><c>ItineraryJson</c> nvarchar(max) NULL — passenger list
    ///     + segments blob captured by the wizard.</item>
    ///   <item><c>ConfirmedAtUtc</c> datetime2 NULL — timestamp when the
    ///     row flipped to the terminal Confirmed state. Null for B2C /
    ///     B2B rows because confirmation there is implicit in the
    ///     saga state machine.</item>
    ///   <item><c>CustomerId</c> uniqueidentifier NULL — opaque id for
    ///     walk-in customers who have a prior account.</item>
    /// </list>
    ///
    /// <para>
    /// The <c>ChannelKind</c> column already exists (Plan 05-02 migration
    /// <c>AddB2BBookingColumns</c>) and accepts any int value, so the
    /// widening of the enum from {0,1} to {0,1,2} is a schema no-op on
    /// the Channel column itself.
    /// </para>
    ///
    /// <para>
    /// Index <c>IX_BookingSagaState_SupplierReference_InitiatedAt</c>
    /// (filtered WHERE SupplierReference IS NOT NULL) supports the
    /// 24-hour duplicate-supplier-reference check in
    /// <c>ManualBookingCommand</c>.
    /// </para>
    ///
    /// <para>
    /// Hand-authored per 03-01 Deviation #2 (no design-time
    /// <c>DbContextFactory</c> wired for BookingService).
    /// </para>
    /// </summary>
    public partial class AddBookingChannelManual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupplierReference",
                schema: "Saga",
                table: "BookingSagaState",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItineraryJson",
                schema: "Saga",
                table: "BookingSagaState",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAtUtc",
                schema: "Saga",
                table: "BookingSagaState",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "Saga",
                table: "BookingSagaState",
                type: "uniqueidentifier",
                nullable: true);

            // Filtered index — only manual-booking rows carry a
            // non-null SupplierReference, so the index stays tiny on
            // the live table while making the duplicate probe an
            // index-seek.
            migrationBuilder.CreateIndex(
                name: "IX_BookingSagaState_SupplierReference_InitiatedAt",
                schema: "Saga",
                table: "BookingSagaState",
                columns: new[] { "SupplierReference", "InitiatedAtUtc" },
                filter: "[SupplierReference] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookingSagaState_SupplierReference_InitiatedAt",
                schema: "Saga",
                table: "BookingSagaState");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "Saga",
                table: "BookingSagaState");

            migrationBuilder.DropColumn(
                name: "ConfirmedAtUtc",
                schema: "Saga",
                table: "BookingSagaState");

            migrationBuilder.DropColumn(
                name: "ItineraryJson",
                schema: "Saga",
                table: "BookingSagaState");

            migrationBuilder.DropColumn(
                name: "SupplierReference",
                schema: "Saga",
                table: "BookingSagaState");
        }
    }
}
