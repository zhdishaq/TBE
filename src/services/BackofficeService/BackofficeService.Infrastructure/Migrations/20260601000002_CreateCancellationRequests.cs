using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TBE.BackofficeService.Infrastructure;

#nullable disable

namespace TBE.BackofficeService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 06-01 Task 3 — BO-03 staff cancellation request table.
    /// 4-eyes state machine per D-48: PendingApproval → Approved | Denied |
    /// Expired. ReasonCode locked to CHECK enum; 72h ExpiresAt TTL.
    /// On approval a BookingCancellationApproved event is published via
    /// MassTransit outbox and the existing Plan 03-01 compensation
    /// pipeline runs the saga cancel path.
    /// </summary>
    [DbContext(typeof(BackofficeDbContext))]
    [Migration("20260601000002_CreateCancellationRequests")]
    public partial class CreateCancellationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "backoffice");

            migrationBuilder.CreateTable(
                name: "CancellationRequests",
                schema: "backoffice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "PendingApproval"),
                    ApprovedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancellationRequests", x => x.Id);
                    table.CheckConstraint(
                        "CK_CancellationRequests_ReasonCode",
                        "[ReasonCode] IN ('CustomerRequest','SupplierInitiated','FareRuleViolation','FraudSuspected','DuplicateBooking','Other')");
                    table.CheckConstraint(
                        "CK_CancellationRequests_Status",
                        "[Status] IN ('PendingApproval','Approved','Denied','Expired')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CancellationRequests_BookingId_Status",
                schema: "backoffice",
                table: "CancellationRequests",
                columns: new[] { "BookingId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CancellationRequests_Status_RequestedAt",
                schema: "backoffice",
                table: "CancellationRequests",
                columns: new[] { "Status", "RequestedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CancellationRequests", schema: "backoffice");
        }
    }
}
