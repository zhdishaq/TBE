using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TBE.BackofficeService.Infrastructure;

#nullable disable

namespace TBE.BackofficeService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 06-01 Task 3 — D-58 dead-letter capture table. Rows written by
    /// <c>ErrorQueueConsumer</c> when MassTransit's max-retry
    /// exceeded publishes an envelope to an <c>_error</c> queue.
    /// Backoffice portal (BO-09 / BO-10) exposes list + requeue + resolve.
    /// </summary>
    [DbContext(typeof(BackofficeDbContext))]
    [Migration("20260601000000_CreateDeadLetterQueue")]
    public partial class CreateDeadLetterQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "backoffice");

            migrationBuilder.CreateTable(
                name: "DeadLetterQueue",
                schema: "backoffice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OriginalQueue = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FirstFailedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastRequeuedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequeueCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ResolutionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeadLetterQueue", x => x.Id);
                });

            // Non-unique filtered index — one row per unresolved MessageId for fast lookup + requeue.
            migrationBuilder.CreateIndex(
                name: "IX_DeadLetterQueue_MessageId_Unresolved",
                schema: "backoffice",
                table: "DeadLetterQueue",
                column: "MessageId",
                filter: "[ResolvedAt] IS NULL");

            // DESC index on FirstFailedAt powers "newest unresolved first" ordering.
            migrationBuilder.CreateIndex(
                name: "IX_DeadLetterQueue_FirstFailedAt",
                schema: "backoffice",
                table: "DeadLetterQueue",
                column: "FirstFailedAt",
                descending: new[] { true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DeadLetterQueue", schema: "backoffice");
        }
    }
}
