using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedProductTaxonomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubcategoryId",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductSubcategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSubcategories", x => x.Id);
                    table.UniqueConstraint("AK_ProductSubcategories_CategoryId_Id", x => new { x.CategoryId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProductSubcategories_ProductCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ProductCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PublicCategoryBindings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    PublicCategoryId = table.Column<int>(type: "int", nullable: false),
                    IsPublicVisible = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicCategoryBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicCategoryBindings_ProductCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ProductCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicCategoryBindings_PublicCatalogCategories_PublicCategoryId",
                        column: x => x.PublicCategoryId,
                        principalTable: "PublicCatalogCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PublicSubcategoryBindings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubcategoryId = table.Column<int>(type: "int", nullable: false),
                    PublicSubcategoryId = table.Column<int>(type: "int", nullable: false),
                    IsPublicVisible = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicSubcategoryBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicSubcategoryBindings_ProductSubcategories_SubcategoryId",
                        column: x => x.SubcategoryId,
                        principalTable: "ProductSubcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicSubcategoryBindings_PublicCatalogSubcategories_PublicSubcategoryId",
                        column: x => x.PublicSubcategoryId,
                        principalTable: "PublicCatalogSubcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId_SubcategoryId",
                table: "Products",
                columns: new[] { "CategoryId", "SubcategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_SubcategoryId",
                table: "Products",
                column: "SubcategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubcategories_CategoryId_IsActive",
                table: "ProductSubcategories",
                columns: new[] { "CategoryId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSubcategories_CategoryId_Name",
                table: "ProductSubcategories",
                columns: new[] { "CategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicCategoryBindings_CategoryId",
                table: "PublicCategoryBindings",
                column: "CategoryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicCategoryBindings_PublicCategoryId",
                table: "PublicCategoryBindings",
                column: "PublicCategoryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicSubcategoryBindings_PublicSubcategoryId",
                table: "PublicSubcategoryBindings",
                column: "PublicSubcategoryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicSubcategoryBindings_SubcategoryId",
                table: "PublicSubcategoryBindings",
                column: "SubcategoryId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_ProductSubcategories_CategoryId_SubcategoryId",
                table: "Products",
                columns: new[] { "CategoryId", "SubcategoryId" },
                principalTable: "ProductSubcategories",
                principalColumns: new[] { "CategoryId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_ProductSubcategories_CategoryId_SubcategoryId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "PublicCategoryBindings");

            migrationBuilder.DropTable(
                name: "PublicSubcategoryBindings");

            migrationBuilder.DropTable(
                name: "ProductSubcategories");

            migrationBuilder.DropIndex(
                name: "IX_Products_CategoryId_SubcategoryId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_SubcategoryId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SubcategoryId",
                table: "Products");
        }
    }
}
