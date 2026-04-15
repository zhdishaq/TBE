using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TBE.PricingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarkupRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarkupRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AirlineCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    RouteOrigin = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkupRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarkupRules_ProductType_Channel_IsActive",
                table: "MarkupRules",
                columns: new[] { "ProductType", "Channel", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MarkupRules");
        }
    }
}
