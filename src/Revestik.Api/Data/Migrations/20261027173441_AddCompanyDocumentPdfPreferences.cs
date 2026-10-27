using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyDocumentPdfPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyPdfPreferencesSnapshotJson",
                table: "Sales",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyPdfPreferencesSnapshotJson",
                table: "Quotations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanyDocumentPdfSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    FooterMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    QuotationDefaultTerms = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ShowWebsite = table.Column<bool>(type: "bit", nullable: false),
                    ShowQuotationSeller = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyDocumentPdfSettings", x => x.Id);
                    table.CheckConstraint("CK_CompanyDocumentPdfSettings_Id", "[Id] = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyDocumentPdfSettings");

            migrationBuilder.DropColumn(
                name: "CompanyPdfPreferencesSnapshotJson",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CompanyPdfPreferencesSnapshotJson",
                table: "Quotations");
        }
    }
}
