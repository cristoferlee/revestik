using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseInventoryReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PurchaseLineId",
                table: "InventoryMovements",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_InventoryMovements_PurchaseLineId",
                table: "InventoryMovements",
                column: "PurchaseLineId",
                unique: true,
                filter: "[PurchaseLineId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovements_Purchase",
                table: "InventoryMovements",
                sql: "[Type] <> 'Purchase' OR ([PurchaseLineId] IS NOT NULL AND [QuantityChange] > 0 AND [UnitCost] IS NOT NULL AND [UnitCost] > 0 AND [AdjustmentReason] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovements_PurchaseLine",
                table: "InventoryMovements",
                sql: "[PurchaseLineId] IS NULL OR [Type] = 'Purchase'");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_PurchaseLines_PurchaseLineId",
                table: "InventoryMovements",
                column: "PurchaseLineId",
                principalTable: "PurchaseLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_PurchaseLines_PurchaseLineId",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "UX_InventoryMovements_PurchaseLineId",
                table: "InventoryMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovements_Purchase",
                table: "InventoryMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovements_PurchaseLine",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "PurchaseLineId",
                table: "InventoryMovements");
        }
    }
}
