using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddElectronicDocumentFiscalReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DocumentType",
                table: "ElectronicDocuments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "AdjustmentStatus",
                table: "ElectronicDocuments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "ElectronicDocuments",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Received");

            migrationBuilder.CreateTable(
                name: "ElectronicDocumentReferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ElectronicDocumentId = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ReferencedDocumentTypeCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReferencedIssueDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReferenceCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RelatedElectronicDocumentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectronicDocumentReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectronicDocumentReferences_ElectronicDocuments_ElectronicDocumentId",
                        column: x => x.ElectronicDocumentId,
                        principalTable: "ElectronicDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ElectronicDocumentReferences_ElectronicDocuments_RelatedElectronicDocumentId",
                        column: x => x.RelatedElectronicDocumentId,
                        principalTable: "ElectronicDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocuments_Direction",
                table: "ElectronicDocuments",
                column: "Direction");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocumentReferences_ElectronicDocumentId_Sequence",
                table: "ElectronicDocumentReferences",
                columns: new[] { "ElectronicDocumentId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocumentReferences_ReferenceNumber",
                table: "ElectronicDocumentReferences",
                column: "ReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocumentReferences_RelatedElectronicDocumentId",
                table: "ElectronicDocumentReferences",
                column: "RelatedElectronicDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElectronicDocumentReferences");

            migrationBuilder.DropIndex(
                name: "IX_ElectronicDocuments_Direction",
                table: "ElectronicDocuments");

            migrationBuilder.DropColumn(
                name: "AdjustmentStatus",
                table: "ElectronicDocuments");

            migrationBuilder.DropColumn(
                name: "Direction",
                table: "ElectronicDocuments");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentType",
                table: "ElectronicDocuments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);
        }
    }
}
