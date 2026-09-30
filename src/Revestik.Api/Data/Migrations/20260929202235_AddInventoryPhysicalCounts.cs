using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryPhysicalCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PhysicalCountId",
                table: "InventoryMovements",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryPhysicalCounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    StartedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryPhysicalCounts", x => x.Id);
                    table.CheckConstraint("CK_InventoryPhysicalCounts_Completion", "([Status] = 'Draft' AND [CompletedAtUtc] IS NULL AND [CompletedByUserId] IS NULL) OR ([Status] = 'Completed' AND [CompletedAtUtc] IS NOT NULL AND [CompletedByUserId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_InventoryPhysicalCounts_AspNetUsers_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryPhysicalCounts_AspNetUsers_StartedByUserId",
                        column: x => x.StartedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryPhysicalCountLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhysicalCountId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ExpectedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CountedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryPhysicalCountLines", x => x.Id);
                    table.CheckConstraint("CK_InventoryPhysicalCountLines_CountedQuantity", "[CountedQuantity] IS NULL OR [CountedQuantity] >= 0");
                    table.CheckConstraint("CK_InventoryPhysicalCountLines_ExpectedQuantity", "[ExpectedQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryPhysicalCountLines_InventoryPhysicalCounts_PhysicalCountId",
                        column: x => x.PhysicalCountId,
                        principalTable: "InventoryPhysicalCounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryPhysicalCountLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_PhysicalCountId",
                table: "InventoryMovements",
                column: "PhysicalCountId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPhysicalCountLines_PhysicalCountId_ProductId",
                table: "InventoryPhysicalCountLines",
                columns: new[] { "PhysicalCountId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPhysicalCountLines_ProductId",
                table: "InventoryPhysicalCountLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPhysicalCounts_CompletedByUserId",
                table: "InventoryPhysicalCounts",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPhysicalCounts_StartedAtUtc",
                table: "InventoryPhysicalCounts",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPhysicalCounts_StartedByUserId",
                table: "InventoryPhysicalCounts",
                column: "StartedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryPhysicalCounts_Status",
                table: "InventoryPhysicalCounts",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_InventoryPhysicalCounts_PhysicalCountId",
                table: "InventoryMovements",
                column: "PhysicalCountId",
                principalTable: "InventoryPhysicalCounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_InventoryPhysicalCounts_PhysicalCountId",
                table: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "InventoryPhysicalCountLines");

            migrationBuilder.DropTable(
                name: "InventoryPhysicalCounts");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_PhysicalCountId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "PhysicalCountId",
                table: "InventoryMovements");
        }
    }
}
