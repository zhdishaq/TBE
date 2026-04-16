using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.BookingService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 04-04 Task 3a — creates <c>Booking.CarBooking</c> table for the B2C car-hire
    /// surface (CARB-01..03). Hand-authored migration timestamp is strictly later than
    /// 04-04's <c>20260416160000_AddBaskets</c> so EF applies this last.
    /// </summary>
    public partial class AddCarBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "Booking");

            migrationBuilder.CreateTable(
                name: "CarBooking",
                schema: "Booking",
                columns: table => new
                {
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OfferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierRef = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BookingReference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PickupLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DropoffLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PickupAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DropoffAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DriverAge = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    GuestEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    GuestFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarBooking", x => x.BookingId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CarBooking_UserId",
                schema: "Booking",
                table: "CarBooking",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CarBooking_Status",
                schema: "Booking",
                table: "CarBooking",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarBooking",
                schema: "Booking");
        }
    }
}
