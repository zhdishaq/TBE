using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.BookingService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 06-01 Task 5 (BO-03 staff cancel/modify) — extends
    /// <c>Saga.BookingSagaState</c> with the staff-initiated cancellation
    /// metadata needed for 4-eyes approval workflow (D-48) and for the
    /// BackofficeEvents BookingCancelledByStaff / BookingCancellationApproved
    /// contracts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Columns (all nullable so existing rows remain valid without
    /// backfill):
    /// </para>
    /// <list type="bullet">
    ///   <item><c>CancelledByStaff bit DEFAULT 0</c> — fast filter for
    ///     "which rows are in the staff-cancel flow"; bit-column chosen
    ///     over the enum discriminator so a BI query doesn't have to
    ///     touch CancellationRequestedBy.</item>
    ///   <item><c>CancellationReasonCode nvarchar(64) NULL</c> — one of
    ///     CustomerRequest / SupplierInitiated / FareRuleViolation /
    ///     FraudSuspected / DuplicateBooking / Other (CHECK constraint).</item>
    ///   <item><c>CancellationReason nvarchar(500) NULL</c> — free text
    ///     from the operator at request time.</item>
    ///   <item><c>CancellationRequestedBy nvarchar(128) NULL</c> — first-eye
    ///     preferred_username.</item>
    ///   <item><c>CancellationApprovedBy nvarchar(128) NULL</c> — second-eye
    ///     preferred_username.</item>
    ///   <item><c>CancellationApprovedAt datetime2 NULL</c> — UTC timestamp
    ///     of the flip to Approved.</item>
    /// </list>
    /// <para>
    /// CHECK constraint restricts CancellationReasonCode to the
    /// whitelisted set — anything else gets rejected at the engine so the
    /// API surface cannot be spoofed by a future migration that loosens
    /// the validator. 4-eyes separation is enforced at the controller
    /// layer (preferred_username of approver MUST differ from requester).
    /// </para>
    /// </remarks>
    public partial class AddCancellationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CancelledByStaff",
                schema: "Saga",
                table: "BookingSagaState",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReasonCode",
                schema: "Saga",
                table: "BookingSagaState",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                schema: "Saga",
                table: "BookingSagaState",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationRequestedBy",
                schema: "Saga",
                table: "BookingSagaState",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationApprovedBy",
                schema: "Saga",
                table: "BookingSagaState",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancellationApprovedAt",
                schema: "Saga",
                table: "BookingSagaState",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql(@"
ALTER TABLE Saga.BookingSagaState
ADD CONSTRAINT CK_BookingSagaState_CancellationReasonCode
    CHECK (CancellationReasonCode IS NULL
        OR CancellationReasonCode IN ('CustomerRequest','SupplierInitiated','FareRuleViolation','FraudSuspected','DuplicateBooking','Other'));
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_BookingSagaState_CancellationReasonCode')
    ALTER TABLE Saga.BookingSagaState DROP CONSTRAINT CK_BookingSagaState_CancellationReasonCode;
");

            migrationBuilder.DropColumn(
                name: "CancelledByStaff", schema: "Saga", table: "BookingSagaState");
            migrationBuilder.DropColumn(
                name: "CancellationReasonCode", schema: "Saga", table: "BookingSagaState");
            migrationBuilder.DropColumn(
                name: "CancellationReason", schema: "Saga", table: "BookingSagaState");
            migrationBuilder.DropColumn(
                name: "CancellationRequestedBy", schema: "Saga", table: "BookingSagaState");
            migrationBuilder.DropColumn(
                name: "CancellationApprovedBy", schema: "Saga", table: "BookingSagaState");
            migrationBuilder.DropColumn(
                name: "CancellationApprovedAt", schema: "Saga", table: "BookingSagaState");
        }
    }
}
