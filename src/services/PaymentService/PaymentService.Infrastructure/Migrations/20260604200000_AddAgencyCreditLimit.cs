using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.PaymentService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 06-04 Task 2 / CRM-02 / D-61 — adds the overdraft allowance
    /// column <c>payment.AgencyWallets.CreditLimit</c> and the
    /// <c>payment.CreditLimitAuditLog</c> non-repudiation table (T-6-59).
    ///
    /// <para>
    /// The column is NOT NULL with <c>DEFAULT 0</c> so existing wallets
    /// are a transparent no-op (no agency gets overdraft until
    /// ops-finance explicitly raises it via
    /// <c>AgencyCreditLimitController.PATCH</c>).
    /// </para>
    /// </summary>
    public partial class AddAgencyCreditLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CreditLimit",
                schema: "payment",
                table: "AgencyWallets",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CreditLimitAuditLog",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AgencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OldLimit = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NewLimit = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditLimitAuditLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditLimitAuditLog_AgencyId_ChangedAtUtc",
                schema: "payment",
                table: "CreditLimitAuditLog",
                columns: new[] { "AgencyId", "ChangedAtUtc" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CreditLimitAuditLog", schema: "payment");

            migrationBuilder.DropColumn(
                name: "CreditLimit",
                schema: "payment",
                table: "AgencyWallets");
        }
    }
}
