using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCabysFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IssuerEconomicActivityCode",
                table: "ElectronicDocuments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceiverEconomicActivityCode",
                table: "ElectronicDocuments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CabysCatalogVersions",
                columns: table => new
                {
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    ItemCount = table.Column<int>(type: "int", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CabysCatalogVersions", x => x.Version);
                });

            migrationBuilder.CreateTable(
                name: "CabysItems",
                columns: table => new
                {
                    CatalogVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "nchar(13)", fixedLength: true, maxLength: 13, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    TaxReference = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Category1Code = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Category1Description = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Category2Code = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Category2Description = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Category3Code = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Category3Description = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Category4Code = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Category4Description = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Category5Code = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Category5Description = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Category6Code = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Category6Description = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Category7Code = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Category7Description = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Category8Code = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Category8Description = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Includes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Excludes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CabysItems", x => new { x.CatalogVersion, x.Code });
                    table.ForeignKey(
                        name: "FK_CabysItems_CabysCatalogVersions_CatalogVersion",
                        column: x => x.CatalogVersion,
                        principalTable: "CabysCatalogVersions",
                        principalColumn: "Version",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_CabysCatalogVersions_Current",
                table: "CabysCatalogVersions",
                column: "IsCurrent",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CabysItems_Code",
                table: "CabysItems",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_CabysItems_Version_Category4",
                table: "CabysItems",
                columns: new[] { "CatalogVersion", "Category4Code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CabysItems");

            migrationBuilder.DropTable(
                name: "CabysCatalogVersions");

            migrationBuilder.DropColumn(
                name: "IssuerEconomicActivityCode",
                table: "ElectronicDocuments");

            migrationBuilder.DropColumn(
                name: "ReceiverEconomicActivityCode",
                table: "ElectronicDocuments");
        }
    }
}
