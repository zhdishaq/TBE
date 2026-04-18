using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.PaymentService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 05-03 Task 2 — adds <c>payment.AgencyWallets</c> one-row-per-agency
    /// metadata table: configured low-balance threshold + hysteresis flag +
    /// last-email timestamp. The append-only <c>payment.WalletTransactions</c>
    /// ledger itself is untouched (shipped by the 20260417000000 migration).
    /// </summary>
    /// <remarks>
    /// Hysteresis lives on this table: <c>LowBalanceEmailSent</c> is flipped
    /// to <c>1</c> by <c>WalletLowBalanceConsumer</c> after a successful
    /// advisory, and reset to <c>0</c> by <c>WalletTopUpService</c> on
    /// balance-cross-up OR <c>PUT /api/wallet/threshold</c>. Combined with
    /// the UNIQUE index on <c>AgencyId</c>, cross-tenant mis-writes are a
    /// loud failure mode (T-05-03-05).
    /// </remarks>
    public partial class AddAgencyWallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "payment");

            migrationBuilder.CreateTable(
                name: "AgencyWallets",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AgencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Currency = table.Column<string>(type: "char(3)", nullable: false),
                    LowBalanceThresholdAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 500m),
                    LowBalanceEmailSent = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastLowBalanceEmailAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgencyWallets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgencyWallets_AgencyId",
                schema: "payment",
                table: "AgencyWallets",
                column: "AgencyId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AgencyWallets", schema: "payment");
        }
    }
}
