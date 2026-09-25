using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Revestik.Api.Data;

#nullable disable

namespace Revestik.Api.Data.Migrations;

[DbContext(typeof(RevestikDbContext))]
[Migration("20260924220000_AddInventoryCatalogFoundation")]
public partial class AddInventoryCatalogFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_QuotationLines_Products_ProductId",
            table: "QuotationLines");

        migrationBuilder.DropForeignKey(
            name: "FK_SaleLines_Products_ProductId",
            table: "SaleLines");

        migrationBuilder.Sql(
            """
            UPDATE [QuotationLines] SET [ProductId] = NULL WHERE [ProductId] IS NOT NULL;
            UPDATE [SaleLines] SET [ProductId] = NULL WHERE [ProductId] IS NOT NULL;
            """);

        migrationBuilder.DropTable(name: "Products");

        migrationBuilder.CreateTable(
            name: "ProductCategories",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductCategories", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "UnitsOfMeasure",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UnitsOfMeasure", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Products",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                CategoryId = table.Column<int>(type: "int", nullable: false),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                CabysCode = table.Column<string>(type: "nchar(13)", fixedLength: true, maxLength: 13, nullable: false),
                InventoryUnitId = table.Column<int>(type: "int", nullable: false),
                CommercialUnitId = table.Column<int>(type: "int", nullable: false),
                CommercialUnitsPerInventoryUnit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                RequiresWholeInventoryUnits = table.Column<bool>(type: "bit", nullable: false),
                SalePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                CurrentCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                TaxRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                StockQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                MinimumStock = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Products", x => x.Id);
                table.CheckConstraint("CK_Products_CabysCode", "LEN([CabysCode]) = 13 AND [CabysCode] NOT LIKE '%[^0-9]%'");
                table.CheckConstraint("CK_Products_Conversion", "[CommercialUnitsPerInventoryUnit] > 0");
                table.CheckConstraint("CK_Products_CurrentCost", "[CurrentCost] > 0");
                table.CheckConstraint("CK_Products_MinimumStock", "[MinimumStock] >= 0");
                table.CheckConstraint("CK_Products_SalePrice", "[SalePrice] >= 0");
                table.CheckConstraint("CK_Products_StockQuantity", "[StockQuantity] >= 0 AND ([RequiresWholeInventoryUnits] = 0 OR [StockQuantity] = FLOOR([StockQuantity]))");
                table.CheckConstraint("CK_Products_TaxRate", "[TaxRate] IN (0, 13)");
                table.ForeignKey(
                    name: "FK_Products_ProductCategories_CategoryId",
                    column: x => x.CategoryId,
                    principalTable: "ProductCategories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Products_UnitsOfMeasure_CommercialUnitId",
                    column: x => x.CommercialUnitId,
                    principalTable: "UnitsOfMeasure",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Products_UnitsOfMeasure_InventoryUnitId",
                    column: x => x.InventoryUnitId,
                    principalTable: "UnitsOfMeasure",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProductCategories_Name",
            table: "ProductCategories",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Products_CabysCode",
            table: "Products",
            column: "CabysCode");

        migrationBuilder.CreateIndex(
            name: "IX_Products_CategoryId",
            table: "Products",
            column: "CategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_Products_CommercialUnitId",
            table: "Products",
            column: "CommercialUnitId");

        migrationBuilder.CreateIndex(
            name: "IX_Products_InventoryUnitId",
            table: "Products",
            column: "InventoryUnitId");

        migrationBuilder.CreateIndex(
            name: "IX_Products_Name",
            table: "Products",
            column: "Name");

        migrationBuilder.CreateIndex(
            name: "IX_UnitsOfMeasure_Name",
            table: "UnitsOfMeasure",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UnitsOfMeasure_Symbol",
            table: "UnitsOfMeasure",
            column: "Symbol",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_QuotationLines_Products_ProductId",
            table: "QuotationLines",
            column: "ProductId",
            principalTable: "Products",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_SaleLines_Products_ProductId",
            table: "SaleLines",
            column: "ProductId",
            principalTable: "Products",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_QuotationLines_Products_ProductId",
            table: "QuotationLines");

        migrationBuilder.DropForeignKey(
            name: "FK_SaleLines_Products_ProductId",
            table: "SaleLines");

        migrationBuilder.Sql(
            """
            UPDATE [QuotationLines] SET [ProductId] = NULL WHERE [ProductId] IS NOT NULL;
            UPDATE [SaleLines] SET [ProductId] = NULL WHERE [ProductId] IS NOT NULL;
            """);

        migrationBuilder.DropTable(name: "Products");
        migrationBuilder.DropTable(name: "ProductCategories");
        migrationBuilder.DropTable(name: "UnitsOfMeasure");

        migrationBuilder.CreateTable(
            name: "Products",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                CabysCode = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                SalePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                TaxRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                StockQuantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Products", x => x.Id);
                table.CheckConstraint("CK_Products_TaxRate", "[TaxRate] IN (0, 13)");
            });

        migrationBuilder.CreateIndex(
            name: "IX_Products_CabysCode",
            table: "Products",
            column: "CabysCode");

        migrationBuilder.CreateIndex(
            name: "IX_Products_Description",
            table: "Products",
            column: "Description");

        migrationBuilder.AddForeignKey(
            name: "FK_QuotationLines_Products_ProductId",
            table: "QuotationLines",
            column: "ProductId",
            principalTable: "Products",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_SaleLines_Products_ProductId",
            table: "SaleLines",
            column: "ProductId",
            principalTable: "Products",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}