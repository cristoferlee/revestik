using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleInventoryReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReversesInventoryMovementId",
                table: "InventoryMovements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SaleId",
                table: "InventoryMovements",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE inventoryMovement
                SET inventoryMovement.SaleId = sale.Id
                FROM InventoryMovements AS inventoryMovement
                INNER JOIN Sales AS sale
                    ON inventoryMovement.Notes LIKE
                        'Venta ' + sale.SaleNumber + '.%'
                WHERE inventoryMovement.Type = 'Sale'
                    AND inventoryMovement.SaleId IS NULL;

                IF EXISTS
                (
                    SELECT 1
                    FROM InventoryMovements
                    WHERE Type = 'Sale'
                        AND SaleId IS NULL
                )
                BEGIN
                    THROW 51000,
                        'Cannot backfill SaleId for one or more historical sale inventory movements.',
                        1;
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_SaleId",
                table: "InventoryMovements",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryMovements_ReversesInventoryMovementId",
                table: "InventoryMovements",
                column: "ReversesInventoryMovementId",
                unique: true,
                filter: "[ReversesInventoryMovementId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_InventoryMovements_ReversesInventoryMovementId",
                table: "InventoryMovements",
                column: "ReversesInventoryMovementId",
                principalTable: "InventoryMovements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_Sales_SaleId",
                table: "InventoryMovements",
                column: "SaleId",
                principalTable: "Sales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_InventoryMovements_ReversesInventoryMovementId",
                table: "InventoryMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_Sales_SaleId",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_SaleId",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "UX_InventoryMovements_ReversesInventoryMovementId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "ReversesInventoryMovementId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "SaleId",
                table: "InventoryMovements");
        }
    }
}
