using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations;

public partial class AddPublicCartItems : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PublicCartItems",
            columns: table => new
            {
                UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ProductId = table.Column<int>(type: "int", nullable: false),
                Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PublicCartItems", x => new { x.UserId, x.ProductId });
                table.CheckConstraint("CK_PublicCartItems_Quantity", "[Quantity] > 0 AND [Quantity] <= 10000");
                table.ForeignKey(
                    name: "FK_PublicCartItems_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PublicCartItems_PublicCatalogProducts_ProductId",
                    column: x => x.ProductId,
                    principalTable: "PublicCatalogProducts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PublicCartItems_ProductId",
            table: "PublicCartItems",
            column: "ProductId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PublicCartItems");
    }
}
