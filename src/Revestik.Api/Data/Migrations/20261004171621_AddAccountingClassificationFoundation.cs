using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingClassificationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperationalDestination",
                table: "ElectronicDocuments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountingNature",
                table: "ElectronicDocumentCategories",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.AddColumn<bool>(
                name: "AllowsAutomaticSuggestion",
                table: "ElectronicDocumentCategories",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemDefault",
                table: "ElectronicDocumentCategories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "ElectronicDocumentCategories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SystemKey",
                table: "ElectronicDocumentCategories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocumentCategories_AccountingNature_SortOrder",
                table: "ElectronicDocumentCategories",
                columns: new[] { "AccountingNature", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_ElectronicDocumentCategories_SystemKey",
                table: "ElectronicDocumentCategories",
                column: "SystemKey",
                unique: true,
                filter: "[SystemKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ElectronicDocumentCategories_AccountingNature_SortOrder",
                table: "ElectronicDocumentCategories");

            migrationBuilder.DropIndex(
                name: "UX_ElectronicDocumentCategories_SystemKey",
                table: "ElectronicDocumentCategories");

            migrationBuilder.DropColumn(
                name: "OperationalDestination",
                table: "ElectronicDocuments");

            migrationBuilder.DropColumn(
                name: "AccountingNature",
                table: "ElectronicDocumentCategories");

            migrationBuilder.DropColumn(
                name: "AllowsAutomaticSuggestion",
                table: "ElectronicDocumentCategories");

            migrationBuilder.DropColumn(
                name: "IsSystemDefault",
                table: "ElectronicDocumentCategories");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "ElectronicDocumentCategories");

            migrationBuilder.DropColumn(
                name: "SystemKey",
                table: "ElectronicDocumentCategories");
        }
    }
}
