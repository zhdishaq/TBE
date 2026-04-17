using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.BookingService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 05-02 Task 2 — extends <c>Saga.BookingSagaState</c> with the B2B
    /// channel, agency pricing snapshot (D-36/D-41), per-booking markup
    /// override (D-37), and customer-contact snapshot (B2B-04).
    ///
    /// Hand-authored per 03-01 Deviation #2: the BookingService has no
    /// ModelSnapshot (no design-time DbContextFactory wired), so migrations
    /// are written by hand and validated via the BookingSagaStateMap EF Core
    /// mapping.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ChannelKind</c> (int, default 0 = <c>Channel.B2C</c>) is the typed
    /// sibling of the existing nvarchar <c>Channel</c> column. The string
    /// column is preserved so that Phase-3 rows (and the unchanged
    /// <c>BookingInitiated.Channel</c> contract) keep working; the saga's
    /// B2B/B2C IfElse reads <c>ChannelKind</c> via the <c>Channel</c> enum
    /// property on the domain model.
    /// </para>
    /// <para>
    /// <c>IX_BookingSagaState_AgencyId</c> supports D-34's agency-wide
    /// listing endpoint in <c>AgentBookingsController.ListForAgencyAsync</c>;
    /// <c>IX_BookingSagaState_Channel</c> supports fast B2B/B2C splits (ops
    /// dashboards, 05-04 metrics).
    /// </para>
    /// <para>
    /// All agency / customer columns are nullable so existing B2C rows remain
    /// valid after the migration applies (backfill-free migration).
    /// </para>
    /// </remarks>
    public partial class AddB2BBookingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Typed channel enum (default 0 = B2C so existing rows stay on the Stripe path).
            migrationBuilder.AddColumn<int>(
                name: "ChannelKind",
                schema: "Saga",
                table: "BookingSagaState",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // D-33 — agency identification (server-side stamp from JWT claim).
            migrationBuilder.AddColumn<Guid>(
                name: "AgencyId",
                schema: "Saga",
                table: "BookingSagaState",
                type: "uniqueidentifier",
                nullable: true);

            // D-36 — agency pricing snapshot (NET / Markup / GROSS / Commission).
            migrationBuilder.AddColumn<decimal>(
                name: "AgencyNetFare",
                schema: "Saga",
                table: "BookingSagaState",
                type: "decimal(18,4)",
                nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "AgencyMarkupAmount",
                schema: "Saga",
                table: "BookingSagaState",
                type: "decimal(18,4)",
                nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "AgencyGrossAmount",
                schema: "Saga",
                table: "BookingSagaState",
                type: "decimal(18,4)",
                nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "AgencyCommissionAmount",
                schema: "Saga",
                table: "BookingSagaState",
                type: "decimal(18,4)",
                nullable: true);

            // D-37 — per-booking markup override (admin-only; enforced in controller).
            migrationBuilder.AddColumn<decimal>(
                name: "AgencyMarkupOverride",
                schema: "Saga",
                table: "BookingSagaState",
                type: "decimal(18,4)",
                nullable: true);

            // B2B-04 — customer-contact snapshot captured at on-behalf booking time.
            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                schema: "Saga",
                table: "BookingSagaState",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "CustomerEmail",
                schema: "Saga",
                table: "BookingSagaState",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "CustomerPhone",
                schema: "Saga",
                table: "BookingSagaState",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            // Plan 05-02 Task 2 — B2B WalletReserveFailed reason captured for ops/UI.
            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                schema: "Saga",
                table: "BookingSagaState",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            // D-34 — agency-wide booking list query hits this index.
            migrationBuilder.CreateIndex(
                name: "IX_BookingSagaState_AgencyId",
                schema: "Saga",
                table: "BookingSagaState",
                column: "AgencyId");

            // Fast B2B/B2C split for ops dashboards and 05-04 metrics.
            migrationBuilder.CreateIndex(
                name: "IX_BookingSagaState_Channel",
                schema: "Saga",
                table: "BookingSagaState",
                column: "ChannelKind");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_BookingSagaState_Channel", schema: "Saga", table: "BookingSagaState");
            migrationBuilder.DropIndex(name: "IX_BookingSagaState_AgencyId", schema: "Saga", table: "BookingSagaState");
            migrationBuilder.DropColumn(name: "FailureReason", schema: "Saga", table: "BookingSagaState");
            migrationBuilder.DropColumn(name: "CustomerPhone", schema: "Saga", table: "BookingSagaState");
            migrationBuilder.DropColumn(name: "CustomerEmail", schema: "Saga", table: "BookingSagaState");
            migrationBuilder.DropColumn(name: "CustomerName", schema: "Saga", table: "BookingSagaState");
            migrationBuilder.DropColumn(name: "AgencyMarkupOverride", schema: "Saga", table: "BookingSagaState");
            migrationBuilder.DropColumn(name: "AgencyCommissionAmount", schema: "Saga", table: "BookingSagaState");
            migrationBuilder.DropColumn(name: "AgencyGrossAmount", schema: "Saga", table: "BookingSagaState");
            migrationBuilder.DropColumn(name: "AgencyMarkupAmount", schema: "Saga", table: "BookingSagaState");
            migrationBuilder.DropColumn(name: "AgencyNetFare", schema: "Saga", table: "BookingSagaState");
            migrationBuilder.DropColumn(name: "AgencyId", schema: "Saga", table: "BookingSagaState");
            migrationBuilder.DropColumn(name: "ChannelKind", schema: "Saga", table: "BookingSagaState");
        }
    }
}
