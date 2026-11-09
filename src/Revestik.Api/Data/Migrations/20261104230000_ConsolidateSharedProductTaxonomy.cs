using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations;

/// <summary>
/// One shared taxonomy. Preserves legacy public product classifications,
/// category media, translations and SEO slugs before removing duplicate tables.
/// Do not apply to production without a separately approved data review.
/// </summary>
public partial class ConsolidateSharedProductTaxonomy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Keep all historical records until the data backfill succeeds.
        migrationBuilder.DropForeignKey(
            name: "FK_PublicCatalogImages_PublicCatalogCategories_CategoryId",
            table: "PublicCatalogImages");
        migrationBuilder.DropForeignKey(
            name: "FK_PublicCatalogProductSubcategories_PublicCatalogSubcategories_SubcategoryId",
            table: "PublicCatalogProductSubcategories");
        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion", table: "ProductCategories",
            type: "rowversion", rowVersion: true, nullable: false);

        migrationBuilder.AddColumn<string>(
            name: "PublicNameEn", table: "ProductCategories",
            type: "nvarchar(150)", maxLength: 150, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicNameDe", table: "ProductCategories",
            type: "nvarchar(150)", maxLength: 150, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicDescriptionEs", table: "ProductCategories",
            type: "nvarchar(2000)", maxLength: 2000, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicDescriptionEn", table: "ProductCategories",
            type: "nvarchar(2000)", maxLength: 2000, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicDescriptionDe", table: "ProductCategories",
            type: "nvarchar(2000)", maxLength: 2000, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicHeroTitleEs", table: "ProductCategories",
            type: "nvarchar(180)", maxLength: 180, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicHeroTitleEn", table: "ProductCategories",
            type: "nvarchar(180)", maxLength: 180, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicHeroTitleDe", table: "ProductCategories",
            type: "nvarchar(180)", maxLength: 180, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicHeroDescriptionEs", table: "ProductCategories",
            type: "nvarchar(2000)", maxLength: 2000, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicHeroDescriptionEn", table: "ProductCategories",
            type: "nvarchar(2000)", maxLength: 2000, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicHeroDescriptionDe", table: "ProductCategories",
            type: "nvarchar(2000)", maxLength: 2000, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicSlug", table: "ProductCategories",
            type: "nvarchar(160)", maxLength: 160, nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PublicDisplayOrder", table: "ProductCategories", type: "int",
            nullable: false, defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "PublicNameEn", table: "ProductSubcategories",
            type: "nvarchar(150)", maxLength: 150, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicNameDe", table: "ProductSubcategories",
            type: "nvarchar(150)", maxLength: 150, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicDescriptionEs", table: "ProductSubcategories",
            type: "nvarchar(2000)", maxLength: 2000, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicDescriptionEn", table: "ProductSubcategories",
            type: "nvarchar(2000)", maxLength: 2000, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicDescriptionDe", table: "ProductSubcategories",
            type: "nvarchar(2000)", maxLength: 2000, nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicSlug", table: "ProductSubcategories",
            type: "nvarchar(160)", maxLength: 160, nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PublicDisplayOrder", table: "ProductSubcategories", type: "int",
            nullable: false, defaultValue: 0);

        migrationBuilder.Sql("""
-- Exact-name migration: no special treatment for TEST records and no guessed IDs.
-- All writes are part of EF Core's SQL Server migration transaction.
IF EXISTS (SELECT 1 FROM dbo.PublicCategoryBindings)
 OR EXISTS (SELECT 1 FROM dbo.PublicSubcategoryBindings)
BEGIN
    ;THROW 51401, 'Manual category bindings found. Review before migration.', 1;
END

-- A hidden legacy classification must never become public accidentally.
-- Local test data are active; nontrivial deployments require an explicit review.
IF EXISTS (SELECT 1 FROM dbo.PublicCatalogCategories WHERE IsActive = 0)
 OR EXISTS (SELECT 1 FROM dbo.PublicCatalogSubcategories WHERE IsActive = 0)
BEGIN
    ;THROW 51413, 'Inactive public taxonomy needs an explicit visibility migration.', 1;
END

IF EXISTS (SELECT 1 FROM dbo.PublicCatalogCategories
 WHERE LEN(LTRIM(RTRIM(NameEs))) NOT BETWEEN 1 AND 100)
BEGIN
    ;THROW 51402, 'A public category name cannot fit the shared catalog.', 1;
END
IF EXISTS (SELECT 1 FROM dbo.PublicCatalogSubcategories
 WHERE LEN(LTRIM(RTRIM(NameEs))) NOT BETWEEN 1 AND 100)
BEGIN
    ;THROW 51403, 'A public subcategory name cannot fit the shared catalog.', 1;
END
IF EXISTS (
 SELECT LTRIM(RTRIM(NameEs)) FROM dbo.PublicCatalogCategories
 GROUP BY LTRIM(RTRIM(NameEs)) HAVING COUNT(*) > 1)
BEGIN
    ;THROW 51404, 'Multiple public categories have the same shared name.', 1;
END

-- Preserve existing categories/IDs; add only genuinely missing names.
INSERT INTO dbo.ProductCategories (Name, IsActive, CreatedAtUtc)
SELECT DISTINCT LTRIM(RTRIM(old.NameEs)), 1, SYSUTCDATETIME()
FROM dbo.PublicCatalogCategories old
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductCategories shared
 WHERE shared.Name = LTRIM(RTRIM(old.NameEs)));

UPDATE shared SET
 PublicSlug = old.Slug, PublicNameEn = old.NameEn, PublicNameDe = old.NameDe,
 PublicDescriptionEs = old.DescriptionEs, PublicDescriptionEn = old.DescriptionEn,
 PublicDescriptionDe = old.DescriptionDe,
 PublicHeroTitleEs = old.HeroTitleEs, PublicHeroTitleEn = old.HeroTitleEn,
 PublicHeroTitleDe = old.HeroTitleDe,
 PublicHeroDescriptionEs = old.HeroDescriptionEs,
 PublicHeroDescriptionEn = old.HeroDescriptionEn,
 PublicHeroDescriptionDe = old.HeroDescriptionDe,
 PublicDisplayOrder = old.DisplayOrder
FROM dbo.ProductCategories shared
JOIN dbo.PublicCatalogCategories old ON shared.Name = LTRIM(RTRIM(old.NameEs));

IF EXISTS (SELECT 1 FROM dbo.PublicCatalogCategories old
 WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductCategories shared WHERE shared.PublicSlug = old.Slug))
BEGIN
    ;THROW 51405, 'Unable to preserve a public category slug.', 1;
END
IF EXISTS (SELECT PublicSlug FROM dbo.ProductCategories WHERE PublicSlug IS NOT NULL
 GROUP BY PublicSlug HAVING COUNT(*) > 1)
BEGIN
    ;THROW 51406, 'Duplicate public category slug.', 1;
END

IF EXISTS (SELECT 1 FROM dbo.PublicCatalogCategories old
 JOIN dbo.ProductCategories shared ON shared.PublicSlug = old.Slug
 WHERE shared.IsActive = 0)
BEGIN
    ;THROW 51414, 'An existing inactive internal category would hide published content.', 1;
END

-- Require each old subcategory to have exactly one canonical parent/name.
IF EXISTS (
 SELECT sharedCat.Id, LTRIM(RTRIM(oldSub.NameEs))
 FROM dbo.PublicCatalogSubcategories oldSub
 JOIN dbo.PublicCatalogCategories oldCat ON oldCat.Id = oldSub.CategoryId
 JOIN dbo.ProductCategories sharedCat ON sharedCat.PublicSlug = oldCat.Slug
 GROUP BY sharedCat.Id, LTRIM(RTRIM(oldSub.NameEs)) HAVING COUNT(*) > 1)
BEGIN
    ;THROW 51407, 'Ambiguous duplicate public subcategories.', 1;
END

INSERT INTO dbo.ProductSubcategories (CategoryId, Name, IsActive, CreatedAtUtc)
SELECT DISTINCT sharedCat.Id, LTRIM(RTRIM(oldSub.NameEs)), 1, SYSUTCDATETIME()
FROM dbo.PublicCatalogSubcategories oldSub
JOIN dbo.PublicCatalogCategories oldCat ON oldCat.Id = oldSub.CategoryId
JOIN dbo.ProductCategories sharedCat ON sharedCat.PublicSlug = oldCat.Slug
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductSubcategories sub
 WHERE sub.CategoryId = sharedCat.Id AND sub.Name = LTRIM(RTRIM(oldSub.NameEs)));

UPDATE sharedSub SET
 PublicSlug = oldSub.Slug, PublicNameEn = oldSub.NameEn,
 PublicNameDe = oldSub.NameDe,
 PublicDescriptionEs = oldSub.DescriptionEs,
 PublicDescriptionEn = oldSub.DescriptionEn,
 PublicDescriptionDe = oldSub.DescriptionDe,
 PublicDisplayOrder = oldSub.DisplayOrder
FROM dbo.ProductSubcategories sharedSub
JOIN dbo.ProductCategories sharedCat ON sharedCat.Id = sharedSub.CategoryId
JOIN dbo.PublicCatalogCategories oldCat ON oldCat.Slug = sharedCat.PublicSlug
JOIN dbo.PublicCatalogSubcategories oldSub ON oldSub.CategoryId = oldCat.Id
 AND sharedSub.Name = LTRIM(RTRIM(oldSub.NameEs));

IF EXISTS (SELECT 1 FROM dbo.PublicCatalogSubcategories oldSub
 JOIN dbo.PublicCatalogCategories oldCat ON oldCat.Id = oldSub.CategoryId
 JOIN dbo.ProductCategories sharedCat ON sharedCat.PublicSlug = oldCat.Slug
 WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductSubcategories sharedSub
   WHERE sharedSub.CategoryId = sharedCat.Id AND sharedSub.PublicSlug = oldSub.Slug))
BEGIN
    ;THROW 51408, 'Unable to preserve a public subcategory slug.', 1;
END
IF EXISTS (SELECT CategoryId, PublicSlug FROM dbo.ProductSubcategories
 WHERE PublicSlug IS NOT NULL GROUP BY CategoryId, PublicSlug HAVING COUNT(*) > 1)
BEGIN
    ;THROW 51409, 'Duplicate public subcategory slug.', 1;
END

IF EXISTS (SELECT 1 FROM dbo.PublicCatalogSubcategories oldSub
 JOIN dbo.PublicCatalogCategories oldCat ON oldCat.Id = oldSub.CategoryId
 JOIN dbo.ProductCategories sharedCat ON sharedCat.PublicSlug = oldCat.Slug
 JOIN dbo.ProductSubcategories sharedSub ON sharedSub.CategoryId = sharedCat.Id
  AND sharedSub.PublicSlug = oldSub.Slug
 WHERE sharedSub.IsActive = 0)
BEGIN
    ;THROW 51415, 'An inactive shared subcategory would change public visibility.', 1;
END

-- Remap each product association to the matching shared subcategory.
IF EXISTS (
 SELECT link.ProductId, sharedSub.Id
 FROM dbo.PublicCatalogProductSubcategories link
 JOIN dbo.PublicCatalogSubcategories oldSub ON oldSub.Id = link.SubcategoryId
 JOIN dbo.PublicCatalogCategories oldCat ON oldCat.Id = oldSub.CategoryId
 JOIN dbo.ProductCategories sharedCat ON sharedCat.PublicSlug = oldCat.Slug
 JOIN dbo.ProductSubcategories sharedSub ON sharedSub.CategoryId = sharedCat.Id
  AND sharedSub.PublicSlug = oldSub.Slug
 GROUP BY link.ProductId, sharedSub.Id HAVING COUNT(*) > 1)
BEGIN
    ;THROW 51410, 'Public product classification collision.', 1;
END

UPDATE link SET SubcategoryId = sharedSub.Id
FROM dbo.PublicCatalogProductSubcategories link
JOIN dbo.PublicCatalogSubcategories oldSub ON oldSub.Id = link.SubcategoryId
JOIN dbo.PublicCatalogCategories oldCat ON oldCat.Id = oldSub.CategoryId
JOIN dbo.ProductCategories sharedCat ON sharedCat.PublicSlug = oldCat.Slug
JOIN dbo.ProductSubcategories sharedSub ON sharedSub.CategoryId = sharedCat.Id
 AND sharedSub.PublicSlug = oldSub.Slug;

-- Category image metadata and file keys keep their original IDs.
UPDATE img SET CategoryId = sharedCat.Id
FROM dbo.PublicCatalogImages img
JOIN dbo.PublicCatalogCategories oldCat ON oldCat.Id = img.CategoryId
JOIN dbo.ProductCategories sharedCat ON sharedCat.PublicSlug = oldCat.Slug
WHERE img.CategoryId IS NOT NULL;

IF EXISTS (SELECT 1 FROM dbo.PublicCatalogProductSubcategories link
 LEFT JOIN dbo.ProductSubcategories sharedSub ON sharedSub.Id = link.SubcategoryId
 WHERE sharedSub.Id IS NULL)
BEGIN
    ;THROW 51411, 'Orphaned product classification after backfill.', 1;
END
IF EXISTS (SELECT 1 FROM dbo.PublicCatalogImages img
 LEFT JOIN dbo.ProductCategories sharedCat ON sharedCat.Id = img.CategoryId
 WHERE img.CategoryId IS NOT NULL AND sharedCat.Id IS NULL)
BEGIN
    ;THROW 51412, 'Orphaned category image after backfill.', 1;
END
""");

        migrationBuilder.DropTable(name: "PublicCategoryBindings");
        migrationBuilder.DropTable(name: "PublicSubcategoryBindings");
        migrationBuilder.DropTable(name: "PublicCatalogSubcategories");
        migrationBuilder.DropTable(name: "PublicCatalogCategories");

        migrationBuilder.CreateIndex(
            name: "IX_ProductCategories_PublicSlug", table: "ProductCategories",
            column: "PublicSlug", unique: true, filter: "[PublicSlug] IS NOT NULL");
        migrationBuilder.CreateIndex(
            name: "IX_ProductSubcategories_CategoryId_PublicSlug", table: "ProductSubcategories",
            columns: new[] { "CategoryId", "PublicSlug" }, unique: true,
            filter: "[PublicSlug] IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_PublicCatalogImages_ProductCategories_CategoryId",
            table: "PublicCatalogImages", column: "CategoryId",
            principalTable: "ProductCategories", principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_PublicCatalogProductSubcategories_ProductSubcategories_SubcategoryId",
            table: "PublicCatalogProductSubcategories", column: "SubcategoryId",
            principalTable: "ProductSubcategories", principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A reverse migration must explicitly reconstruct taxonomy/media associations.
        // Refuse implicit data-loss rollback; restore a verified backup instead.
        throw new NotSupportedException(
            "ConsolidateSharedProductTaxonomy cannot be rolled back automatically without data loss.");
    }
}
