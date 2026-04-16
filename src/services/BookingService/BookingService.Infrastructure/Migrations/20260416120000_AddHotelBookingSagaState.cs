using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.BookingService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 04-03 / HOTB-01..05 — creates <c>Booking.HotelBookingSagaState</c> with
    /// RowVersion-mapped concurrency token and HasIndex on UserId / SupplierRef / Status.
    /// Hand-authored following the 03-02 migration precedent (see 03-02-SUMMARY).
    /// </summary>
    public partial class AddHotelBookingSagaState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "Booking");

            migrationBuilder.CreateTable(
                name: "HotelBookingSagaState",
                schema: "Booking",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BookingReference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SupplierRef = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CheckInDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckOutDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Rooms = table.Column<int>(type: "int", nullable: false),
                    Adults = table.Column<int>(type: "int", nullable: false),
                    Children = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    GuestEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    GuestFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FailureCause = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StripePaymentIntentId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InitiatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HotelBookingSagaState", x => x.CorrelationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HotelBookingSagaState_SupplierRef",
                schema: "Booking",
                table: "HotelBookingSagaState",
                column: "SupplierRef");

            migrationBuilder.CreateIndex(
                name: "IX_HotelBookingSagaState_UserId",
                schema: "Booking",
                table: "HotelBookingSagaState",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HotelBookingSagaState_Status",
                schema: "Booking",
                table: "HotelBookingSagaState",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "HotelBookingSagaState", schema: "Booking");
        }
    }
}
