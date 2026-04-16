using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.BookingService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 04-01 Task 1 — persist fare breakdown (base / YQ-YR / taxes) onto
    /// Saga.BookingSagaState so the QuestPDF receipt generator can render a
    /// FLTB-03-compliant breakdown after the booking has been ticketed.
    /// Hand-authored per 03-01 Deviation #2: ModelSnapshot is not usable on
    /// this project (no DbContext design-time factory wired for EF tooling).
    /// </summary>
    public partial class AddReceiptFareBreakdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BaseFareAmount",
                schema: "Saga",
                table: "BookingSagaState",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SurchargeAmount",
                schema: "Saga",
                table: "BookingSagaState",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                schema: "Saga",
                table: "BookingSagaState",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseFareAmount",
                schema: "Saga",
                table: "BookingSagaState");

            migrationBuilder.DropColumn(
                name: "SurchargeAmount",
                schema: "Saga",
                table: "BookingSagaState");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                schema: "Saga",
                table: "BookingSagaState");
        }
    }
}
