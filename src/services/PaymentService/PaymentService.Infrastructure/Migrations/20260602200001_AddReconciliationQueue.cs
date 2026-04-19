using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TBE.PaymentService.Infrastructure;

#nullable disable

namespace TBE.PaymentService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 06-02 Task 3 (BO-06) — payment.PaymentReconciliationQueue.
    /// Discrepancy rows written by the nightly reconciliation job; resolved
    /// by ops-finance via the portal. CHECK constraints enforce the four
    /// discrepancy kinds, three severity buckets, and two-state lifecycle.
    /// </summary>
    [DbContext(typeof(PaymentDbContext))]
    [Migration("20260602200001_AddReconciliationQueue")]
    public partial class AddReconciliationQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "payment");

            migrationBuilder.CreateTable(
                name: "PaymentReconciliationQueue",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiscrepancyType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StripeEventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "Pending"),
                    ResolvedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReconciliationQueue", x => x.Id);
                    table.CheckConstraint(
                        "CK_PaymentReconciliationQueue_DiscrepancyType",
                        "[DiscrepancyType] IN ('OrphanStripeEvent','OrphanWalletRow','AmountDrift','UnprocessedEvent')");
                    table.CheckConstraint(
                        "CK_PaymentReconciliationQueue_Severity",
                        "[Severity] IN ('Low','Medium','High')");
                    table.CheckConstraint(
                        "CK_PaymentReconciliationQueue_Status",
                        "[Status] IN ('Pending','Resolved')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationQueue_Status_DetectedAt",
                schema: "payment",
                table: "PaymentReconciliationQueue",
                columns: new[] { "Status", "DetectedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationQueue_Type_StripeEventId",
                schema: "payment",
                table: "PaymentReconciliationQueue",
                columns: new[] { "DiscrepancyType", "StripeEventId" },
                filter: "[StripeEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReconciliationQueue_Type_BookingId",
                schema: "payment",
                table: "PaymentReconciliationQueue",
                columns: new[] { "DiscrepancyType", "BookingId" },
                filter: "[BookingId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentReconciliationQueue",
                schema: "payment");
        }
    }
}
