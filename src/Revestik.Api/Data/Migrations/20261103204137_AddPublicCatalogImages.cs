using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicCatalogImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicCatalogImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    Role = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    StorageKey = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    AltEs = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AltEn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AltDe = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FallbackImageId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicCatalogImages", x => x.Id);
                    table.CheckConstraint("CK_PublicCatalogImages_ContentType", "[ContentType] IN ('image/jpeg','image/png','image/webp') OR ([ContentType] = 'image/gif' AND [Role] = 'CategoryHeroPrimary')");
                    table.CheckConstraint("CK_PublicCatalogImages_Dimensions", "[Width] > 0 AND [Height] > 0 AND [Width] <= 8192 AND [Height] <= 8192");
                    table.CheckConstraint("CK_PublicCatalogImages_DisplayOrder", "[DisplayOrder] >= 0");
                    table.CheckConstraint("CK_PublicCatalogImages_FallbackRole", "[FallbackImageId] IS NULL OR [Role] = 'CategoryHeroPrimary'");
                    table.CheckConstraint("CK_PublicCatalogImages_FileSize", "[FileSizeBytes] > 0 AND [FileSizeBytes] <= 5242880");
                    table.CheckConstraint("CK_PublicCatalogImages_GifFallback", "[ContentType] <> 'image/gif' OR [FallbackImageId] IS NOT NULL");
                    table.CheckConstraint("CK_PublicCatalogImages_Owner", "([ProductId] IS NOT NULL AND [CategoryId] IS NULL) OR ([ProductId] IS NULL AND [CategoryId] IS NOT NULL)");
                    table.CheckConstraint("CK_PublicCatalogImages_ValidRole", "([ProductId] IS NOT NULL AND [Role] IN ('ProductCover','ProductGallery')) OR ([CategoryId] IS NOT NULL AND [Role] IN ('CategoryHeroPrimary','CategoryHeroFallback','CategoryGallery'))");
                    table.ForeignKey(
                        name: "FK_PublicCatalogImages_PublicCatalogCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "PublicCatalogCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicCatalogImages_PublicCatalogImages_FallbackImageId",
                        column: x => x.FallbackImageId,
                        principalTable: "PublicCatalogImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicCatalogImages_PublicCatalogProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "PublicCatalogProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicCatalogImages_CategoryId_DisplayOrder",
                table: "PublicCatalogImages",
                columns: new[] { "CategoryId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicCatalogImages_CategoryId_Role",
                table: "PublicCatalogImages",
                columns: new[] { "CategoryId", "Role" },
                unique: true,
                filter: "[CategoryId] IS NOT NULL AND [Role] IN ('CategoryHeroPrimary', 'CategoryHeroFallback')");

            migrationBuilder.CreateIndex(
                name: "IX_PublicCatalogImages_FallbackImageId",
                table: "PublicCatalogImages",
                column: "FallbackImageId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicCatalogImages_ProductId_DisplayOrder",
                table: "PublicCatalogImages",
                columns: new[] { "ProductId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicCatalogImages_ProductId_Role",
                table: "PublicCatalogImages",
                columns: new[] { "ProductId", "Role" },
                unique: true,
                filter: "[ProductId] IS NOT NULL AND [Role] = 'ProductCover'");

            migrationBuilder.CreateIndex(
                name: "IX_PublicCatalogImages_StorageKey",
                table: "PublicCatalogImages",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicCatalogImages");
        }
    }
}
