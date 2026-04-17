using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.PricingService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 05-02 Task 1 — creates <c>pricing.AgencyMarkupRules</c>. D-36:
    /// max 2 active rows per agency (base row with <c>RouteClass</c> NULL +
    /// optional RouteClass override), enforced by a filtered UNIQUE index on
    /// (AgencyId, RouteClass) WHERE IsActive = 1.
    ///
    /// Timestamp <c>20260416000000</c> is chosen to land AFTER the existing
    /// <c>20260415000000_AddMarkupRules</c> (which seeds the legacy B2C
    /// markup table) so EF migration history preserves order.
    /// </summary>
    public partial class AddAgencyMarkupRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "pricing");

            migrationBuilder.CreateTable(
                name: "AgencyMarkupRules",
                schema: "pricing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteClass = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    FlatAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PercentOfNet = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgencyMarkupRules", x => x.Id);
                });

            // D-36: filtered UNIQUE index enforces "at most one active row per
            // (agency, routeclass) tuple". SQL Server treats NULLs in a unique
            // index as equal within the same filter, so this gives max one
            // active base (RouteClass NULL) + max one active override per
            // distinct RouteClass value per agency.
            migrationBuilder.CreateIndex(
                name: "IX_AgencyMarkupRules_Active",
                schema: "pricing",
                table: "AgencyMarkupRules",
                columns: new[] { "AgencyId", "RouteClass" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AgencyMarkupRules", schema: "pricing");
        }
    }
}
