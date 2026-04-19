using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TBE.BackofficeService.Infrastructure;

#nullable disable

namespace TBE.BackofficeService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 06-01 Task 3 — D-39 manual wallet-credit request table.
    /// 4-eyes state machine: PendingApproval → Approved | Denied | Expired.
    /// Amount CHECK (0.01, 100000); ReasonCode CHECK in D-53 enum; Status
    /// CHECK in 4-eyes enum. 72h ExpiresAt TTL per D-48.
    /// </summary>
    [DbContext(typeof(BackofficeDbContext))]
    [Migration("20260601000001_CreateWalletCreditRequests")]
    public partial class CreateWalletCreditRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "backoffice");

            migrationBuilder.CreateTable(
                name: "WalletCreditRequests",
                schema: "backoffice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LinkedBookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "PendingApproval"),
                    ApprovedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletCreditRequests", x => x.Id);
                    table.CheckConstraint(
                        "CK_WalletCreditRequests_Amount",
                        "[Amount] > 0 AND [Amount] <= 100000");
                    table.CheckConstraint(
                        "CK_WalletCreditRequests_ReasonCode",
                        "[ReasonCode] IN ('RefundedBooking','GoodwillCredit','DisputeResolution','SupplierRefundPassthrough')");
                    table.CheckConstraint(
                        "CK_WalletCreditRequests_Status",
                        "[Status] IN ('PendingApproval','Approved','Denied','Expired')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_WalletCreditRequests_Status_RequestedAt",
                schema: "backoffice",
                table: "WalletCreditRequests",
                columns: new[] { "Status", "RequestedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "WalletCreditRequests", schema: "backoffice");
        }
    }
}
