using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Revestik.Api.Data;

#nullable disable

namespace Revestik.Api.Data.Migrations;

[DbContext(typeof(RevestikDbContext))]
[Migration("20261104120000_AddPublicHomeHeroSlides")]
public partial class AddPublicHomeHeroSlides : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PublicHomeHeroSlides",
            columns: table => new
            {
                Slot = table.Column<int>(type: "int", nullable: false),
                HeadingEs = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                HeadingEn = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                HeadingDe = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                DescriptionEs = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                DescriptionEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                DescriptionDe = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                ImageStorageKey = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                ImageContentType = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PublicHomeHeroSlides", x => x.Slot);
                table.CheckConstraint("CK_PublicHomeHeroSlides_Slot", "[Slot] BETWEEN 1 AND 3");
                table.CheckConstraint("CK_PublicHomeHeroSlides_Image", "([ImageStorageKey] IS NULL AND [ImageContentType] IS NULL) OR ([ImageStorageKey] IS NOT NULL AND [ImageContentType] IN ('image/jpeg','image/png','image/webp'))");
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "PublicHomeHeroSlides");
}
