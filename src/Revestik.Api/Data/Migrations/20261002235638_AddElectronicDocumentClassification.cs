using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddElectronicDocumentClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "ElectronicDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedAtUtc",
                table: "ElectronicDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessedByUserId",
                table: "ElectronicDocuments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ElectronicDocumentCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectronicDocumentCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ElectronicDocumentClassificationRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IssuerIdentificationType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IssuerIdentification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectronicDocumentClassificationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectronicDocumentClassificationRules_ElectronicDocumentCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ElectronicDocumentCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocuments_CategoryId",
                table: "ElectronicDocuments",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocuments_ProcessedByUserId",
                table: "ElectronicDocuments",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_ElectronicDocumentCategories_Name",
                table: "ElectronicDocumentCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocumentClassificationRules_CategoryId",
                table: "ElectronicDocumentClassificationRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "UX_ElectronicDocumentClassificationRules_Issuer",
                table: "ElectronicDocumentClassificationRules",
                columns: new[] { "IssuerIdentificationType", "IssuerIdentification" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ElectronicDocuments_AspNetUsers_ProcessedByUserId",
                table: "ElectronicDocuments",
                column: "ProcessedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ElectronicDocuments_ElectronicDocumentCategories_CategoryId",
                table: "ElectronicDocuments",
                column: "CategoryId",
                principalTable: "ElectronicDocumentCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ElectronicDocuments_AspNetUsers_ProcessedByUserId",
                table: "ElectronicDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_ElectronicDocuments_ElectronicDocumentCategories_CategoryId",
                table: "ElectronicDocuments");

            migrationBuilder.DropTable(
                name: "ElectronicDocumentClassificationRules");

            migrationBuilder.DropTable(
                name: "ElectronicDocumentCategories");

            migrationBuilder.DropIndex(
                name: "IX_ElectronicDocuments_CategoryId",
                table: "ElectronicDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ElectronicDocuments_ProcessedByUserId",
                table: "ElectronicDocuments");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "ElectronicDocuments");

            migrationBuilder.DropColumn(
                name: "ProcessedAtUtc",
                table: "ElectronicDocuments");

            migrationBuilder.DropColumn(
                name: "ProcessedByUserId",
                table: "ElectronicDocuments");
        }
    }
}
