using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.PricingService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 06-03 Task 1 / D-52 — creates <c>pricing.MarkupRuleAuditLog</c>.
    /// Every mutation to <c>pricing.AgencyMarkupRules</c> writes one row to
    /// this table inside the same DbTransaction as the rule change. D-38
    /// ops-finance CRUD of markup rules happens through
    /// <see cref="TBE.PricingService.API.Controllers.MarkupRulesController"/>
    /// with hard server-side bounds (FlatAmount [£0..£500], PercentOfNet
    /// [0%..25%]) + full Before/After/Actor/Reason audit trail.
    /// </summary>
    /// <remarks>
    /// Migration timestamp <c>20260603000000</c> lands AFTER Plan 05-02's
    /// <c>20260416000000_AddAgencyMarkupRules</c>, preserving migration history.
    /// </remarks>
    public partial class AddMarkupRuleAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "pricing");

            migrationBuilder.CreateTable(
                name: "MarkupRuleAuditLog",
                schema: "pricing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkupRuleAuditLog", x => x.Id);
                    table.CheckConstraint(
                        "CK_MarkupRuleAuditLog_Action",
                        "[Action] IN ('Created','Updated','Deactivated','Deleted')");
                });

            // Primary UI access path: "show every change to Agency X, newest first".
            migrationBuilder.CreateIndex(
                name: "IX_MarkupRuleAuditLog_AgencyId_ChangedAt",
                schema: "pricing",
                table: "MarkupRuleAuditLog",
                columns: new[] { "AgencyId", "ChangedAt" },
                descending: new[] { false, true });

            // Per-rule history (drill-down from rule row).
            migrationBuilder.CreateIndex(
                name: "IX_MarkupRuleAuditLog_RuleId_ChangedAt",
                schema: "pricing",
                table: "MarkupRuleAuditLog",
                columns: new[] { "RuleId", "ChangedAt" },
                descending: new[] { false, true });

            // Who-did-what compliance queries.
            migrationBuilder.CreateIndex(
                name: "IX_MarkupRuleAuditLog_Actor_ChangedAt",
                schema: "pricing",
                table: "MarkupRuleAuditLog",
                columns: new[] { "Actor", "ChangedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarkupRuleAuditLog",
                schema: "pricing");
        }
    }
}
