using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.BookingService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 06-04 Task 3 / COMP-03 / D-57 — adds three filtered indexes to
    /// <c>Saga.BookingSagaState</c> that back the GDPR "right to erasure"
    /// fan-out in <c>CustomerErasureRequestedConsumer</c> and the Customer 360
    /// "recent bookings" lookup in the BackofficeService portal:
    ///
    /// <list type="bullet">
    ///   <item>
    ///     <c>IX_BookingSagaState_CustomerId</c> WHERE <c>CustomerId IS NOT NULL</c>
    ///     — the erasure consumer's primary lookup key ("null PII on every saga
    ///     row for this customer"). Filtered because only manual (BO-02) and
    ///     walk-in bookings carry a <c>CustomerId</c>; B2C / B2B rows do not.
    ///   </item>
    ///   <item>
    ///     <c>IX_BookingSagaState_GdsPnr</c> WHERE <c>GdsPnr IS NOT NULL</c>
    ///     — backs the portal Global Search "find by PNR" flow and the ops
    ///     cancellation / re-ticket lookup. Filtered because the PNR is only
    ///     minted once the GDS <c>CreatePnrConsumer</c> returns, i.e. after
    ///     ~state 3/4 of the saga; all initiated-but-not-yet-PNR'd rows stay
    ///     out of the index.
    ///   </item>
    ///   <item>
    ///     <c>IX_BookingSagaState_CustomerEmail</c> WHERE <c>CustomerEmail IS NOT NULL</c>
    ///     — backs the erasure controller's "same email → look up CustomerId"
    ///     lookup (the erasure request from ops carries the email, and we need
    ///     to find every saga row associated with that email, not just those
    ///     that already have a <c>CustomerId</c> from a prior registration).
    ///     Filtered because B2C rows pre-05-02 did not capture the customer
    ///     email snapshot.
    ///   </item>
    /// </list>
    ///
    /// <para>
    /// Column name is <c>GdsPnr</c> (not <c>Pnr</c> as the plan outline
    /// suggested); index name reflects the actual column so the filter clause
    /// stays aligned with the schema. Plan-vs-repo mismatch documented in the
    /// Plan 06-04 SUMMARY.
    /// </para>
    ///
    /// <para>
    /// Hand-authored per Plan 03-01 Deviation #2 precedent — BookingService
    /// has no design-time <c>DbContextFactory</c> wired, so every migration
    /// in this project is written by hand.
    /// </para>
    /// </summary>
    public partial class AddPnrCustomerEmailIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_BookingSagaState_CustomerId",
                schema: "Saga",
                table: "BookingSagaState",
                column: "CustomerId",
                filter: "[CustomerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSagaState_GdsPnr",
                schema: "Saga",
                table: "BookingSagaState",
                column: "GdsPnr",
                filter: "[GdsPnr] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookingSagaState_CustomerEmail",
                schema: "Saga",
                table: "BookingSagaState",
                column: "CustomerEmail",
                filter: "[CustomerEmail] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookingSagaState_CustomerEmail",
                schema: "Saga",
                table: "BookingSagaState");

            migrationBuilder.DropIndex(
                name: "IX_BookingSagaState_GdsPnr",
                schema: "Saga",
                table: "BookingSagaState");

            migrationBuilder.DropIndex(
                name: "IX_BookingSagaState_CustomerId",
                schema: "Saga",
                table: "BookingSagaState");
        }
    }
}
