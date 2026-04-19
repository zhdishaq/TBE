using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.CrmService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 06-04 Task 1 — seed the <c>crm</c> schema with five projection
    /// tables built from integration events (D-51).
    /// </summary>
    public partial class CreateCrmProjections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "crm");

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LifetimeBookingsCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LifetimeGross = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    LastBookingAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsErased = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ErasedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                schema: "crm",
                table: "Customers",
                column: "Email",
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Name",
                schema: "crm",
                table: "Customers",
                column: "Name",
                filter: "[Name] IS NOT NULL");

            migrationBuilder.CreateTable(
                name: "Agencies",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LifetimeBookingsCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LifetimeGross = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    LifetimeCommission = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    LastBookingAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agencies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Agencies_Name",
                schema: "crm",
                table: "Agencies",
                column: "Name");

            migrationBuilder.CreateTable(
                name: "BookingProjections",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Pnr = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AgencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TicketNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    TravelDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OriginIata = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    DestinationIata = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingProjections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingProjections_CustomerId",
                schema: "crm",
                table: "BookingProjections",
                column: "CustomerId",
                filter: "[CustomerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookingProjections_AgencyId",
                schema: "crm",
                table: "BookingProjections",
                column: "AgencyId",
                filter: "[AgencyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookingProjections_Pnr",
                schema: "crm",
                table: "BookingProjections",
                column: "Pnr",
                filter: "[Pnr] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookingProjections_BookingReference",
                schema: "crm",
                table: "BookingProjections",
                column: "BookingReference",
                filter: "[BookingReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookingProjections_TravelDate_Status",
                schema: "crm",
                table: "BookingProjections",
                columns: new[] { "TravelDate", "Status" });

            migrationBuilder.CreateTable(
                name: "CommunicationLog",
                schema: "crm",
                columns: table => new
                {
                    LogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationLog", x => x.LogId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationLog_Entity_CreatedAt",
                schema: "crm",
                table: "CommunicationLog",
                columns: new[] { "EntityType", "EntityId", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateTable(
                name: "UpcomingTrips",
                schema: "crm",
                columns: table => new
                {
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AgencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BookingReference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Pnr = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TravelDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    OriginIata = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    DestinationIata = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpcomingTrips", x => x.BookingId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UpcomingTrips_TravelDate_Status",
                schema: "crm",
                table: "UpcomingTrips",
                columns: new[] { "TravelDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UpcomingTrips_AgencyId_TravelDate",
                schema: "crm",
                table: "UpcomingTrips",
                columns: new[] { "AgencyId", "TravelDate" },
                filter: "[AgencyId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UpcomingTrips", schema: "crm");
            migrationBuilder.DropTable(name: "CommunicationLog", schema: "crm");
            migrationBuilder.DropTable(name: "BookingProjections", schema: "crm");
            migrationBuilder.DropTable(name: "Agencies", schema: "crm");
            migrationBuilder.DropTable(name: "Customers", schema: "crm");
        }
    }
}
