using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryFifo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryCostLayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    SourceMovementId = table.Column<int>(type: "int", nullable: false),
                    OriginalQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCostLayers", x => x.Id);
                    table.CheckConstraint("CK_InventoryCostLayers_OriginalQuantity", "[OriginalQuantity] > 0");
                    table.CheckConstraint("CK_InventoryCostLayers_RemainingQuantity", "[RemainingQuantity] >= 0 AND [RemainingQuantity] <= [OriginalQuantity]");
                    table.CheckConstraint("CK_InventoryCostLayers_UnitCost", "[UnitCost] IS NULL OR [UnitCost] > 0");
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_InventoryMovements_SourceMovementId",
                        column: x => x.SourceMovementId,
                        principalTable: "InventoryMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostLayers_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryCostConsumptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryMovementId = table.Column<int>(type: "int", nullable: false),
                    InventoryCostLayerId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCostSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCostConsumptions", x => x.Id);
                    table.CheckConstraint("CK_InventoryCostConsumptions_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_InventoryCostConsumptions_UnitCostSnapshot", "[UnitCostSnapshot] IS NULL OR [UnitCostSnapshot] > 0");
                    table.ForeignKey(
                        name: "FK_InventoryCostConsumptions_InventoryCostLayers_InventoryCostLayerId",
                        column: x => x.InventoryCostLayerId,
                        principalTable: "InventoryCostLayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCostConsumptions_InventoryMovements_InventoryMovementId",
                        column: x => x.InventoryMovementId,
                        principalTable: "InventoryMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostConsumptions_InventoryCostLayerId",
                table: "InventoryCostConsumptions",
                column: "InventoryCostLayerId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostConsumptions_InventoryMovementId",
                table: "InventoryCostConsumptions",
                column: "InventoryMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostConsumptions_InventoryMovementId_InventoryCostLayerId",
                table: "InventoryCostConsumptions",
                columns: new[] { "InventoryMovementId", "InventoryCostLayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_ProductId",
                table: "InventoryCostLayers",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_ProductId_CreatedAtUtc_Id",
                table: "InventoryCostLayers",
                columns: new[] { "ProductId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCostLayers_SourceMovementId",
                table: "InventoryCostLayers",
                column: "SourceMovementId",
                unique: true);

            // Rebuild FIFO history from the immutable inventory movement ledger.
            // InitialStock creates a known-cost layer when quantity > 0.
            // AdjustmentIncrease creates an unknown-cost layer.
            // AdjustmentDecrease consumes the oldest remaining layers first.
            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                DECLARE
                    @MovementId int,
                    @ProductId int,
                    @Type nvarchar(30),
                    @QuantityChange decimal(18,4),
                    @UnitCost decimal(18,2),
                    @CreatedAtUtc datetime2,
                    @RemainingToConsume decimal(18,4),
                    @LayerId int,
                    @LayerRemaining decimal(18,4),
                    @LayerUnitCost decimal(18,2),
                    @Consumed decimal(18,4);

                DECLARE movement_cursor CURSOR LOCAL FAST_FORWARD FOR
                    SELECT
                        [Id],
                        [ProductId],
                        [Type],
                        [QuantityChange],
                        [UnitCost],
                        [CreatedAtUtc]
                    FROM [InventoryMovements]
                    WHERE [Type] IN
                        ('InitialStock', 'AdjustmentIncrease', 'AdjustmentDecrease')
                    ORDER BY [ProductId], [CreatedAtUtc], [Id];

                OPEN movement_cursor;

                FETCH NEXT FROM movement_cursor INTO
                    @MovementId,
                    @ProductId,
                    @Type,
                    @QuantityChange,
                    @UnitCost,
                    @CreatedAtUtc;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    IF @Type = 'InitialStock' AND @QuantityChange > 0
                    BEGIN
                        INSERT INTO [InventoryCostLayers]
                        (
                            [ProductId],
                            [SourceMovementId],
                            [OriginalQuantity],
                            [RemainingQuantity],
                            [UnitCost],
                            [CreatedAtUtc]
                        )
                        VALUES
                        (
                            @ProductId,
                            @MovementId,
                            @QuantityChange,
                            @QuantityChange,
                            @UnitCost,
                            @CreatedAtUtc
                        );
                    END
                    ELSE IF @Type = 'AdjustmentIncrease' AND @QuantityChange > 0
                    BEGIN
                        INSERT INTO [InventoryCostLayers]
                        (
                            [ProductId],
                            [SourceMovementId],
                            [OriginalQuantity],
                            [RemainingQuantity],
                            [UnitCost],
                            [CreatedAtUtc]
                        )
                        VALUES
                        (
                            @ProductId,
                            @MovementId,
                            @QuantityChange,
                            @QuantityChange,
                            NULL,
                            @CreatedAtUtc
                        );
                    END
                    ELSE IF @Type = 'AdjustmentDecrease' AND @QuantityChange < 0
                    BEGIN
                        SET @RemainingToConsume = ABS(@QuantityChange);

                        WHILE @RemainingToConsume > 0
                        BEGIN
                            SET @LayerId = NULL;
                            SET @LayerRemaining = NULL;
                            SET @LayerUnitCost = NULL;

                            SELECT TOP (1)
                                @LayerId = [Id],
                                @LayerRemaining = [RemainingQuantity],
                                @LayerUnitCost = [UnitCost]
                            FROM [InventoryCostLayers]
                            WHERE
                                [ProductId] = @ProductId
                                AND [RemainingQuantity] > 0
                            ORDER BY [CreatedAtUtc], [Id];

                            IF @LayerId IS NULL
                            BEGIN
                                THROW 51000,
                                    'FIFO backfill failed: inventory movements consume more quantity than available cost layers.',
                                    1;
                            END;

                            SET @Consumed =
                                CASE
                                    WHEN @LayerRemaining <= @RemainingToConsume
                                        THEN @LayerRemaining
                                    ELSE @RemainingToConsume
                                END;

                            UPDATE [InventoryCostLayers]
                            SET [RemainingQuantity] =
                                [RemainingQuantity] - @Consumed
                            WHERE [Id] = @LayerId;

                            INSERT INTO [InventoryCostConsumptions]
                            (
                                [InventoryMovementId],
                                [InventoryCostLayerId],
                                [Quantity],
                                [UnitCostSnapshot],
                                [CreatedAtUtc]
                            )
                            VALUES
                            (
                                @MovementId,
                                @LayerId,
                                @Consumed,
                                @LayerUnitCost,
                                @CreatedAtUtc
                            );

                            SET @RemainingToConsume =
                                @RemainingToConsume - @Consumed;
                        END;
                    END;

                    FETCH NEXT FROM movement_cursor INTO
                        @MovementId,
                        @ProductId,
                        @Type,
                        @QuantityChange,
                        @UnitCost,
                        @CreatedAtUtc;
                END;

                CLOSE movement_cursor;
                DEALLOCATE movement_cursor;

                IF EXISTS
                (
                    SELECT 1
                    FROM [Products] p
                    OUTER APPLY
                    (
                        SELECT
                            COALESCE(SUM(l.[RemainingQuantity]), 0) AS [LayerStock]
                        FROM [InventoryCostLayers] l
                        WHERE l.[ProductId] = p.[Id]
                    ) fifo
                    WHERE p.[StockQuantity] <> fifo.[LayerStock]
                )
                BEGIN
                    THROW 51001,
                        'FIFO backfill failed: product stock does not match reconstructed cost-layer stock.',
                        1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryCostConsumptions");

            migrationBuilder.DropTable(
                name: "InventoryCostLayers");
        }
    }
}