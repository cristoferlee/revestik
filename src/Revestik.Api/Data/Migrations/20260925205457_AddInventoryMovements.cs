using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    QuantityChange = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    StockBefore = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    StockAfter = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                    table.CheckConstraint("CK_InventoryMovements_Balance", "[StockAfter] = [StockBefore] + [QuantityChange]");
                    table.CheckConstraint("CK_InventoryMovements_InitialStock", "[Type] <> 'InitialStock' OR ([StockBefore] = 0 AND [QuantityChange] >= 0 AND [StockAfter] = [QuantityChange] AND [UnitCost] IS NOT NULL AND [UnitCost] > 0)");
                    table.CheckConstraint("CK_InventoryMovements_StockAfter", "[StockAfter] >= 0");
                    table.CheckConstraint("CK_InventoryMovements_StockBefore", "[StockBefore] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryMovements_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_CreatedAtUtc",
                table: "InventoryMovements",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_CreatedByUserId",
                table: "InventoryMovements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_Type",
                table: "InventoryMovements",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryMovements_Product_InitialStock",
                table: "InventoryMovements",
                column: "ProductId",
                unique: true,
                filter: "[Type] = 'InitialStock'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryMovements");
        }
    }
}
