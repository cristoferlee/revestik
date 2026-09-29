using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovements_InitialStock",
                table: "InventoryMovements");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Products",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "AdjustmentReason",
                table: "InventoryMovements",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_AdjustmentReason",
                table: "InventoryMovements",
                column: "AdjustmentReason");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovements_AdjustmentDecrease",
                table: "InventoryMovements",
                sql: "[Type] <> 'AdjustmentDecrease' OR ([QuantityChange] < 0 AND [UnitCost] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovements_AdjustmentIncrease",
                table: "InventoryMovements",
                sql: "[Type] <> 'AdjustmentIncrease' OR ([QuantityChange] > 0 AND [UnitCost] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovements_AdjustmentReason",
                table: "InventoryMovements",
                sql: "[Type] NOT IN ('AdjustmentIncrease', 'AdjustmentDecrease') OR [AdjustmentReason] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovements_InitialStock",
                table: "InventoryMovements",
                sql: "[Type] <> 'InitialStock' OR ([StockBefore] = 0 AND [QuantityChange] >= 0 AND [StockAfter] = [QuantityChange] AND [UnitCost] IS NOT NULL AND [UnitCost] > 0 AND [AdjustmentReason] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_AdjustmentReason",
                table: "InventoryMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovements_AdjustmentDecrease",
                table: "InventoryMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovements_AdjustmentIncrease",
                table: "InventoryMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovements_AdjustmentReason",
                table: "InventoryMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovements_InitialStock",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AdjustmentReason",
                table: "InventoryMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovements_InitialStock",
                table: "InventoryMovements",
                sql: "[Type] <> 'InitialStock' OR ([StockBefore] = 0 AND [QuantityChange] >= 0 AND [StockAfter] = [QuantityChange] AND [UnitCost] IS NOT NULL AND [UnitCost] > 0)");
        }
    }
}
