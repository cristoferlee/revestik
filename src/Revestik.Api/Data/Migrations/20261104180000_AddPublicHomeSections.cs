using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Revestik.Api.Data;

#nullable disable
namespace Revestik.Api.Data.Migrations;

[DbContext(typeof(RevestikDbContext))]
[Migration("20261104180000_AddPublicHomeSections")]
public partial class AddPublicHomeSections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PublicHomeSections",
            columns: table => new
            {
                Key = table.Column<string>(type: "varchar(24)", unicode: false, maxLength: 24, nullable: false),
                ContentEs = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: false),
                ContentEn = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: true),
                ContentDe = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: true),
                FeaturedSlugsJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                ImageStorageKey = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                ImageContentType = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PublicHomeSections", x => x.Key);
                table.CheckConstraint("CK_PublicHomeSections_Key", "[Key] IN ('solutions','advantages','contact','products')");
                table.CheckConstraint("CK_PublicHomeSections_Image", "([ImageStorageKey] IS NULL AND [ImageContentType] IS NULL) OR ([Key] = 'solutions' AND [ImageStorageKey] IS NOT NULL AND [ImageContentType] IN ('image/jpeg','image/png','image/webp'))");
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "PublicHomeSections");
}
