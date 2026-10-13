using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalReferenceResolutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ElectronicDocumentReferenceResolutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ElectronicDocumentReferenceId = table.Column<int>(type: "int", nullable: false),
                    OriginalDocumentId = table.Column<int>(type: "int", nullable: false),
                    ResolutionType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CompatibilityReasons = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ResolvedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectronicDocumentReferenceResolutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectronicDocumentReferenceResolutions_AspNetUsers_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElectronicDocumentReferenceResolutions_ElectronicDocumentReferences_ElectronicDocumentReferenceId",
                        column: x => x.ElectronicDocumentReferenceId,
                        principalTable: "ElectronicDocumentReferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElectronicDocumentReferenceResolutions_ElectronicDocuments_OriginalDocumentId",
                        column: x => x.OriginalDocumentId,
                        principalTable: "ElectronicDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocumentReferenceResolutions_ElectronicDocumentReferenceId_ResolvedAtUtc",
                table: "ElectronicDocumentReferenceResolutions",
                columns: new[] { "ElectronicDocumentReferenceId", "ResolvedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocumentReferenceResolutions_OriginalDocumentId",
                table: "ElectronicDocumentReferenceResolutions",
                column: "OriginalDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocumentReferenceResolutions_ResolvedByUserId",
                table: "ElectronicDocumentReferenceResolutions",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_ReferenceResolutions_ActiveDateException",
                table: "ElectronicDocumentReferenceResolutions",
                column: "ElectronicDocumentReferenceId",
                unique: true,
                filter: "[ResolutionType] = 'DateExceptionAuthorized'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElectronicDocumentReferenceResolutions");
        }
    }
}
