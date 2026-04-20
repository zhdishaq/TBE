using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.CrmService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 06-04 Task 3 / COMP-03 / D-57 — GDPR "right to erasure" proof-of-work.
    /// Creates <c>crm.CustomerErasureTombstones</c>:
    /// <list type="bullet">
    ///   <item><c>Id</c> uniqueidentifier PK.</item>
    ///   <item><c>OriginalCustomerId</c> uniqueidentifier NOT NULL — the pre-erasure projection id.</item>
    ///   <item><c>EmailHash</c> nvarchar(64) NOT NULL — SHA-256 hex of normalised email.</item>
    ///   <item><c>ErasedAt</c> datetime2 NOT NULL.</item>
    ///   <item><c>ErasedBy</c> nvarchar(128) NOT NULL — preferred_username of the ops-admin.</item>
    ///   <item><c>Reason</c> nvarchar(500) NOT NULL.</item>
    /// </list>
    /// <para>
    /// <c>UX_CustomerErasureTombstones_EmailHash</c> UNIQUE → D-57 "same person
    /// returns" dedup: a second erasure request for the same email is a no-op
    /// in the consumer (guarded by <c>AnyAsync(EmailHash)</c>) and would fail
    /// at the DB if somehow bypassed.
    /// </para>
    /// <para>
    /// <c>IX_CustomerErasureTombstones_ErasedAt DESC</c> → backs the Erasures
    /// archive list page (/customers/erasures) "most recent first" order.
    /// </para>
    /// <para>
    /// Hand-authored per Plan 06-02 Deviation #2 precedent — no design-time
    /// <c>DbContextFactory</c> is wired for CrmService, and the previous
    /// plan-6 migrations in this project follow the same hand-authored style.
    /// </para>
    /// </summary>
    public partial class CreateCustomerErasureTombstones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerErasureTombstones",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalCustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ErasedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErasedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerErasureTombstones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_CustomerErasureTombstones_EmailHash",
                schema: "crm",
                table: "CustomerErasureTombstones",
                column: "EmailHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerErasureTombstones_ErasedAt",
                schema: "crm",
                table: "CustomerErasureTombstones",
                column: "ErasedAt",
                descending: new[] { true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerErasureTombstones",
                schema: "crm");
        }
    }
}
