using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.PaymentService.Infrastructure.Migrations;

/// <summary>
/// Plan 06-01 Task 6 — extends <c>payment.WalletTransactions</c> with two
/// optional audit columns (<c>ApprovedBy</c>, <c>ApprovalNotes</c>) required
/// by D-39 manual wallet credits (4-eyes approval).
///
/// <para>
/// The <c>EntryType</c> TINYINT column is untyped by a CHECK constraint
/// in the original 20260417000000 migration, so values 5 (ManualCredit)
/// and 6 (CommissionPayout, reserved for Plan 06-03) slot in without
/// altering the schema beyond adding audit columns. The existing
/// SignedAmount computed column <c>CASE WHEN [EntryType] IN (1,2) THEN
/// -[Amount] ELSE [Amount] END</c> already returns a positive value for
/// 5 and 6.
/// </para>
///
/// <para>
/// Down-migration drops both audit columns. Any ManualCredit rows written
/// during an up window stay in place — EntryType=5 is still valid data,
/// we just lose the ApprovedBy/ApprovalNotes audit metadata. This is the
/// correct append-only posture (D-14): we never erase ledger entries, we
/// only walk back the audit surface.
/// </para>
/// </summary>
public partial class AddManualCreditKind : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ApprovedBy",
            schema: "payment",
            table: "WalletTransactions",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ApprovalNotes",
            schema: "payment",
            table: "WalletTransactions",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ApprovalNotes",
            schema: "payment",
            table: "WalletTransactions");

        migrationBuilder.DropColumn(
            name: "ApprovedBy",
            schema: "payment",
            table: "WalletTransactions");
    }
}
