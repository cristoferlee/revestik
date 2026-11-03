using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicCatalogFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicCatalogCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    NameEs = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NameDe = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DescriptionEs = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DescriptionDe = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    HeroTitleEs = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    HeroTitleEn = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    HeroTitleDe = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    HeroDescriptionEs = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    HeroDescriptionEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    HeroDescriptionDe = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicCatalogCategories", x => x.Id);
                    table.CheckConstraint("CK_PublicCatalogCategories_DisplayOrder", "[DisplayOrder] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "PublicCatalogProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: true),
                    SalesUnit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicCatalogProducts", x => x.Id);
                    table.CheckConstraint("CK_PublicCatalogProducts_DisplayOrder", "[DisplayOrder] >= 0");
                    table.CheckConstraint("CK_PublicCatalogProducts_Price", "[Price] IS NULL OR [Price] >= 0");
                    table.CheckConstraint("CK_PublicCatalogProducts_PriceFields", "([Price] IS NULL AND [Currency] IS NULL) OR ([Price] IS NOT NULL AND [Currency] IS NOT NULL AND [Currency] IN ('CRC','USD') AND [SalesUnit] IS NOT NULL AND LEN(LTRIM(RTRIM([SalesUnit]))) > 0)");
                    table.CheckConstraint("CK_PublicCatalogProducts_PublishedArchived", "NOT ([IsPublished] = 1 AND [IsArchived] = 1)");
                });

            migrationBuilder.CreateTable(
                name: "PublicCatalogSubcategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    NameEs = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NameDe = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DescriptionEs = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DescriptionDe = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicCatalogSubcategories", x => x.Id);
                    table.CheckConstraint("CK_PublicCatalogSubcategories_DisplayOrder", "[DisplayOrder] >= 0");
                    table.ForeignKey(
                        name: "FK_PublicCatalogSubcategories_PublicCatalogCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "PublicCatalogCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PublicCatalogProductTranslations",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    LanguageCode = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SeoTitle = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    SeoDescription = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicCatalogProductTranslations", x => new { x.ProductId, x.LanguageCode });
                    table.CheckConstraint("CK_PublicCatalogProductTranslations_Language", "[LanguageCode] IN ('es','en','de')");
                    table.ForeignKey(
                        name: "FK_PublicCatalogProductTranslations_PublicCatalogProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "PublicCatalogProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PublicCatalogProductSubcategories",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    SubcategoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicCatalogProductSubcategories", x => new { x.ProductId, x.SubcategoryId });
                    table.ForeignKey(
                        name: "FK_PublicCatalogProductSubcategories_PublicCatalogProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "PublicCatalogProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicCatalogProductSubcategories_PublicCatalogSubcategories_SubcategoryId",
                        column: x => x.SubcategoryId,
                        principalTable: "PublicCatalogSubcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicCatalogCategories_IsActive_DisplayOrder",
                table: "PublicCatalogCategories",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicCatalogCategories_Slug",
                table: "PublicCatalogCategories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicCatalogProducts_IsPublished_IsArchived_DisplayOrder",
                table: "PublicCatalogProducts",
                columns: new[] { "IsPublished", "IsArchived", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicCatalogProducts_Slug",
                table: "PublicCatalogProducts",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicCatalogProductSubcategories_SubcategoryId_ProductId",
                table: "PublicCatalogProductSubcategories",
                columns: new[] { "SubcategoryId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicCatalogSubcategories_CategoryId_IsActive_DisplayOrder",
                table: "PublicCatalogSubcategories",
                columns: new[] { "CategoryId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicCatalogSubcategories_CategoryId_Slug",
                table: "PublicCatalogSubcategories",
                columns: new[] { "CategoryId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicCatalogProductSubcategories");

            migrationBuilder.DropTable(
                name: "PublicCatalogProductTranslations");

            migrationBuilder.DropTable(
                name: "PublicCatalogSubcategories");

            migrationBuilder.DropTable(
                name: "PublicCatalogProducts");

            migrationBuilder.DropTable(
                name: "PublicCatalogCategories");
        }
    }
}
