using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TBE.PaymentService.Infrastructure;

#nullable disable

namespace TBE.PaymentService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 06-02 Task 3 (BO-06 / D-55) — extend payment.StripeWebhookEvents
    /// with the raw Stripe JSON envelope + a Processed flag so the nightly
    /// reconciliation job can compare Stripe's source-of-truth amounts /
    /// metadata against the wallet ledger.
    ///
    /// <para>
    /// Pre-existing rows are backfilled to <c>RawPayload='{}'</c> and
    /// <c>Processed=1</c> so reconciliation does not immediately flag
    /// every historical event as unprocessed.
    /// </para>
    /// </summary>
    [DbContext(typeof(PaymentDbContext))]
    [Migration("20260602200000_ExtendStripeEventsWithRawPayload")]
    public partial class ExtendStripeEventsWithRawPayload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RawPayload",
                schema: "payment",
                table: "StripeWebhookEvents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<bool>(
                name: "Processed",
                schema: "payment",
                table: "StripeWebhookEvents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Historical rows were handled by the pre-D-55 consumer. Mark
            // them Processed=1 so reconciliation only flags post-migration
            // rows that stall.
            migrationBuilder.Sql(
                "UPDATE [payment].[StripeWebhookEvents] SET [Processed] = 1 WHERE [Processed] = 0;");

            migrationBuilder.CreateIndex(
                name: "IX_StripeWebhookEvents_Processed_ReceivedAt",
                schema: "payment",
                table: "StripeWebhookEvents",
                columns: new[] { "Processed", "ReceivedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StripeWebhookEvents_Processed_ReceivedAt",
                schema: "payment",
                table: "StripeWebhookEvents");

            migrationBuilder.DropColumn(
                name: "Processed",
                schema: "payment",
                table: "StripeWebhookEvents");

            migrationBuilder.DropColumn(
                name: "RawPayload",
                schema: "payment",
                table: "StripeWebhookEvents");
        }
    }
}
