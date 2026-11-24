using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Revestik.Api.Data;

#nullable disable
namespace Revestik.Api.Data.Migrations;

[DbContext(typeof(RevestikDbContext))]
[Migration("20261123181500_AddPublicSolutionsSections")]
public partial class AddPublicSolutionsSections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PublicSolutionsSections",
            columns: table => new
            {
                Key = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                ContentEs = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: false),
                ContentEn = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: true),
                ContentDe = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: true),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PublicSolutionsSections", x => x.Key);
                table.CheckConstraint("CK_PublicSolutionsSections_Key",
                    "[Key] IN ('hero','approach','applications','interior-flooring','exterior-flooring','walls-facades','cta')");
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "PublicSolutionsSections");
}
