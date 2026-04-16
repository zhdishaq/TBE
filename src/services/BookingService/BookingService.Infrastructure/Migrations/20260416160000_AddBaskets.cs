using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.BookingService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 04-04 Task 1 / PKG-01..04 — creates <c>Booking.Basket</c> aggregate table plus
    /// the <c>Booking.BasketEventLog</c> inbox table backing BasketPaymentOrchestrator
    /// idempotency (T-04-04-04). CONTEXT D-08: <c>StripePaymentIntentId</c> is a SINGLE
    /// nullable column — no FlightPaymentIntentId / HotelPaymentIntentId split is permitted.
    /// Hand-authored migration timestamp is strictly later than 04-03's
    /// <c>20260416120000_AddHotelBookingSagaState</c> so EF applies this last.
    /// </summary>
    public partial class AddBaskets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "Booking");

            migrationBuilder.CreateTable(
                name: "Basket",
                schema: "Booking",
                columns: table => new
                {
                    BasketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FlightBookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HotelBookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CarBookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StripePaymentIntentId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    FlightSubtotal = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    HotelSubtotal = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ChargedAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    FlightCaptured = table.Column<bool>(type: "bit", nullable: false),
                    HotelCaptured = table.Column<bool>(type: "bit", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    GuestEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    GuestFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Basket", x => x.BasketId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Basket_UserId",
                schema: "Booking",
                table: "Basket",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Basket_Status",
                schema: "Booking",
                table: "Basket",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Basket_FlightBookingId",
                schema: "Booking",
                table: "Basket",
                column: "FlightBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Basket_HotelBookingId",
                schema: "Booking",
                table: "Basket",
                column: "HotelBookingId");

            migrationBuilder.CreateTable(
                name: "BasketEventLog",
                schema: "Booking",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BasketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HandledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasketEventLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BasketEventLog_BasketId_EventId",
                schema: "Booking",
                table: "BasketEventLog",
                columns: new[] { "BasketId", "EventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BasketEventLog", schema: "Booking");
            migrationBuilder.DropTable(name: "Basket", schema: "Booking");
        }
    }
}
