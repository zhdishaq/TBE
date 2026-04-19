using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TBE.BackofficeService.Infrastructure;

#nullable disable

namespace TBE.BackofficeService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan 06-02 Task 2 (BO-07) — supplier contract table with validity
    /// window + soft-delete. CHECK constraints enforce ProductType enum,
    /// commission range [0,100], non-negative NetRate, and a coherent
    /// ValidFrom/ValidTo window. Composite index powers the hottest
    /// read path: list filtered by ProductType ordered by ValidTo DESC.
    /// </summary>
    [DbContext(typeof(BackofficeDbContext))]
    [Migration("20260602100000_CreateSupplierContracts")]
    public partial class CreateSupplierContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "backoffice");

            migrationBuilder.CreateTable(
                name: "SupplierContracts",
                schema: "backoffice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ProductType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NetRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CommissionPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierContracts", x => x.Id);
                    table.CheckConstraint(
                        "CK_SupplierContracts_ProductType",
                        "[ProductType] IN ('Flight','Hotel','Car','Package')");
                    table.CheckConstraint(
                        "CK_SupplierContracts_NetRate",
                        "[NetRate] >= 0");
                    table.CheckConstraint(
                        "CK_SupplierContracts_CommissionPercent",
                        "[CommissionPercent] >= 0 AND [CommissionPercent] <= 100");
                    table.CheckConstraint(
                        "CK_SupplierContracts_Validity",
                        "[ValidTo] >= [ValidFrom]");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContracts_IsDeleted_ProductType_ValidTo",
                schema: "backoffice",
                table: "SupplierContracts",
                columns: new[] { "IsDeleted", "ProductType", "ValidTo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SupplierContracts", schema: "backoffice");
        }
    }
}
