using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Revestik.Api.Data;

#nullable disable
namespace Revestik.Api.Data.Migrations;

// Applies after AddPublicSolutionsSections, preserving existing translated content and row versions.
[DbContext(typeof(RevestikDbContext))]
[Migration("20261123201500_AddPublicSolutionsHeroImage")]
public partial class AddPublicSolutionsHeroImage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "ImageStorageKey", table: "PublicSolutionsSections",
            type: "varchar(64)", unicode: false, maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ImageContentType", table: "PublicSolutionsSections",
            type: "varchar(40)", unicode: false, maxLength: 40, nullable: true);
        migrationBuilder.AddCheckConstraint(name: "CK_PublicSolutionsSections_HeroImage",
            table: "PublicSolutionsSections",
            sql: "([ImageStorageKey] IS NULL AND [ImageContentType] IS NULL) OR " +
                 "([Key] = 'hero' AND [ImageStorageKey] IS NOT NULL AND " +
                 "[ImageContentType] IN ('image/jpeg','image/png','image/webp'))");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Do not silently discard published images on a schema rollback.
        migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM dbo.PublicSolutionsSections WHERE ImageStorageKey IS NOT NULL)
            THROW 51001, 'Remove the Solutions hero photograph before rolling back this migration.', 1;");
        migrationBuilder.DropCheckConstraint(name: "CK_PublicSolutionsSections_HeroImage", table: "PublicSolutionsSections");
        migrationBuilder.DropColumn(name: "ImageContentType", table: "PublicSolutionsSections");
        migrationBuilder.DropColumn(name: "ImageStorageKey", table: "PublicSolutionsSections");
    }
}
