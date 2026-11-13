using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicProjectsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    TypeCode = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: true),
                    FeaturedOrder = table.Column<int>(type: "int", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicProjects", x => x.Id);
                    table.CheckConstraint("CK_PublicProjects_DisplayOrder", "[DisplayOrder] IS NULL OR [DisplayOrder] >= 0");
                    table.CheckConstraint("CK_PublicProjects_FeaturedOrder", "[FeaturedOrder] IS NULL OR [FeaturedOrder] BETWEEN 1 AND 3");
                });

            migrationBuilder.CreateTable(
                name: "PublicProjectImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
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
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicProjectImages", x => x.Id);
                    table.CheckConstraint("CK_PublicProjectImages_ContentType", "[ContentType] IN ('image/jpeg','image/png','image/webp')");
                    table.CheckConstraint("CK_PublicProjectImages_Dimensions", "[Width] BETWEEN 1 AND 8192 AND [Height] BETWEEN 1 AND 8192");
                    table.CheckConstraint("CK_PublicProjectImages_DisplayOrder", "[DisplayOrder] >= 0");
                    table.CheckConstraint("CK_PublicProjectImages_FileSize", "[FileSizeBytes] > 0 AND [FileSizeBytes] <= 5242880");
                    table.CheckConstraint("CK_PublicProjectImages_Role", "[Role] IN ('ProjectCover','ProjectGallery')");
                    table.ForeignKey(
                        name: "FK_PublicProjectImages_PublicProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "PublicProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PublicProjectSpaces",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    SpaceCode = table.Column<string>(type: "varchar(48)", unicode: false, maxLength: 48, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicProjectSpaces", x => new { x.ProjectId, x.SpaceCode });
                    table.ForeignKey(
                        name: "FK_PublicProjectSpaces_PublicProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "PublicProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PublicProjectTranslations",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    LanguageCode = table.Column<string>(type: "varchar(2)", unicode: false, maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SeoTitle = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    SeoDescription = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicProjectTranslations", x => new { x.ProjectId, x.LanguageCode });
                    table.CheckConstraint("CK_PublicProjectTranslations_Language", "[LanguageCode] IN ('es','en','de')");
                    table.ForeignKey(
                        name: "FK_PublicProjectTranslations_PublicProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "PublicProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PublicProjectImageTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImageId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    AnchorX = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    AnchorY = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    LabelX = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    LabelY = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CustomLabelEs = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CustomLabelEn = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CustomLabelDe = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ProductNameSnapshotEs = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProductNameSnapshotEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProductNameSnapshotDe = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicProjectImageTags", x => x.Id);
                    table.CheckConstraint("CK_PublicProjectImageTags_Coordinates", "[AnchorX] BETWEEN 0 AND 1 AND [AnchorY] BETWEEN 0 AND 1 AND [LabelX] BETWEEN 0 AND 1 AND [LabelY] BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_PublicProjectImageTags_DisplayOrder", "[DisplayOrder] >= 0");
                    table.ForeignKey(
                        name: "FK_PublicProjectImageTags_PublicCatalogProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "PublicCatalogProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PublicProjectImageTags_PublicProjectImages_ImageId",
                        column: x => x.ImageId,
                        principalTable: "PublicProjectImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicProjectImages_ProjectId_DisplayOrder",
                table: "PublicProjectImages",
                columns: new[] { "ProjectId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicProjectImages_ProjectId_Role",
                table: "PublicProjectImages",
                columns: new[] { "ProjectId", "Role" },
                unique: true,
                filter: "[Role] = 'ProjectCover'");

            migrationBuilder.CreateIndex(
                name: "IX_PublicProjectImages_StorageKey",
                table: "PublicProjectImages",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicProjectImageTags_ImageId_DisplayOrder",
                table: "PublicProjectImageTags",
                columns: new[] { "ImageId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicProjectImageTags_ProductId",
                table: "PublicProjectImageTags",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicProjects_FeaturedOrder",
                table: "PublicProjects",
                column: "FeaturedOrder",
                unique: true,
                filter: "[FeaturedOrder] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PublicProjects_IsPublished_DisplayOrder_CreatedAtUtc",
                table: "PublicProjects",
                columns: new[] { "IsPublished", "DisplayOrder", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicProjects_IsPublished_TypeCode",
                table: "PublicProjects",
                columns: new[] { "IsPublished", "TypeCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicProjects_Slug",
                table: "PublicProjects",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicProjectSpaces_SpaceCode_ProjectId",
                table: "PublicProjectSpaces",
                columns: new[] { "SpaceCode", "ProjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicProjectImageTags");

            migrationBuilder.DropTable(
                name: "PublicProjectSpaces");

            migrationBuilder.DropTable(
                name: "PublicProjectTranslations");

            migrationBuilder.DropTable(
                name: "PublicProjectImages");

            migrationBuilder.DropTable(
                name: "PublicProjects");
        }
    }
}
