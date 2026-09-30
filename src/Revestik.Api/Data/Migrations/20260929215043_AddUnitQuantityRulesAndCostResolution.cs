using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitQuantityRulesAndCostResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresWholeQuantity",
                table: "UnitsOfMeasure",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CostResolvedAtUtc",
                table: "InventoryCostLayers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostResolvedByUserId",
                table: "InventoryCostLayers",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ResolvedUnitCost",
                table: "InventoryCostLayers",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE UnitsOfMeasure
                SET RequiresWholeQuantity = 1
                WHERE Name IN (N'Caja', N'Unidad');
                """);

            migrationBuilder.Sql(
                """
                UPDATE p
                SET p.RequiresWholeInventoryUnits = u.RequiresWholeQuantity
                FROM Products AS p
                INNER JOIN UnitsOfMeasure AS u
                    ON u.Id = p.InventoryUnitId;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_CostResolvedByUserId",
                table: "InventoryCostLayers",
                column: "CostResolvedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryCostLayers_ResolvedCost",
                table: "InventoryCostLayers",
                sql:
                    "([ResolvedUnitCost] IS NULL AND " +
                    "[CostResolvedByUserId] IS NULL AND " +
                    "[CostResolvedAtUtc] IS NULL) OR " +
                    "([UnitCost] IS NULL AND " +
                    "[ResolvedUnitCost] > 0 AND " +
                    "[CostResolvedByUserId] IS NOT NULL AND " +
                    "[CostResolvedAtUtc] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCostLayers_AspNetUsers_CostResolvedByUserId",
                table: "InventoryCostLayers",
                column: "CostResolvedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryCostLayers_AspNetUsers_CostResolvedByUserId",
                table: "InventoryCostLayers");

            migrationBuilder.DropIndex(
                name: "IX_InventoryCostLayers_CostResolvedByUserId",
                table: "InventoryCostLayers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryCostLayers_ResolvedCost",
                table: "InventoryCostLayers");

            migrationBuilder.DropColumn(
                name: "RequiresWholeQuantity",
                table: "UnitsOfMeasure");

            migrationBuilder.DropColumn(
                name: "CostResolvedAtUtc",
                table: "InventoryCostLayers");

            migrationBuilder.DropColumn(
                name: "CostResolvedByUserId",
                table: "InventoryCostLayers");

            migrationBuilder.DropColumn(
                name: "ResolvedUnitCost",
                table: "InventoryCostLayers");
        }
    }
}