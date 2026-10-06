using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeReceivedDocumentClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClassificationCategoryId",
                table: "ElectronicDocumentLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationalDestination",
                table: "ElectronicDocumentLines",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultOperationalDestination",
                table: "ElectronicDocumentCategories",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultDestinationInitialized",
                table: "ElectronicDocumentCategories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ElectronicDocumentCabysClassificationRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleKey = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    IssuerIdentificationType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IssuerIdentification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CabysCode = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    CabysCategory4Code = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    OperationalDestination = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ConfirmationCount = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    LastConfirmedElectronicDocumentId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectronicDocumentCabysClassificationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectronicDocumentCabysClassificationRules_ElectronicDocumentCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ElectronicDocumentCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocumentLines_ClassificationCategoryId",
                table: "ElectronicDocumentLines",
                column: "ClassificationCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocumentCabysClassificationRules_CategoryId",
                table: "ElectronicDocumentCabysClassificationRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocumentCabysClassificationRules_IssuerIdentificationType_IssuerIdentification_CabysCategory4Code",
                table: "ElectronicDocumentCabysClassificationRules",
                columns: new[] { "IssuerIdentificationType", "IssuerIdentification", "CabysCategory4Code" });

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocumentCabysClassificationRules_IssuerIdentificationType_IssuerIdentification_CabysCode",
                table: "ElectronicDocumentCabysClassificationRules",
                columns: new[] { "IssuerIdentificationType", "IssuerIdentification", "CabysCode" });

            migrationBuilder.CreateIndex(
                name: "UX_ElectronicDocumentCabysClassificationRules_RuleKey",
                table: "ElectronicDocumentCabysClassificationRules",
                column: "RuleKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ElectronicDocumentLines_ElectronicDocumentCategories_ClassificationCategoryId",
                table: "ElectronicDocumentLines",
                column: "ClassificationCategoryId",
                principalTable: "ElectronicDocumentCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ElectronicDocumentLines_ElectronicDocumentCategories_ClassificationCategoryId",
                table: "ElectronicDocumentLines");

            migrationBuilder.DropTable(
                name: "ElectronicDocumentCabysClassificationRules");

            migrationBuilder.DropIndex(
                name: "IX_ElectronicDocumentLines_ClassificationCategoryId",
                table: "ElectronicDocumentLines");

            migrationBuilder.DropColumn(
                name: "ClassificationCategoryId",
                table: "ElectronicDocumentLines");

            migrationBuilder.DropColumn(
                name: "OperationalDestination",
                table: "ElectronicDocumentLines");

            migrationBuilder.DropColumn(
                name: "DefaultOperationalDestination",
                table: "ElectronicDocumentCategories");

            migrationBuilder.DropColumn(
                name: "IsDefaultDestinationInitialized",
                table: "ElectronicDocumentCategories");
        }
    }
}
