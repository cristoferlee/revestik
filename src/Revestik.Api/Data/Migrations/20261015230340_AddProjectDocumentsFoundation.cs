using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectDocumentsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_ProjectEvents_Id_ProjectId",
                table: "ProjectEvents",
                columns: new[] { "Id", "ProjectId" });

            migrationBuilder.CreateTable(
                name: "ProjectDocumentEventLinks",
                columns: table => new
                {
                    ProjectDocumentId = table.Column<int>(type: "int", nullable: false),
                    ProjectEventId = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    LinkedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LinkedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDocumentEventLinks", x => new { x.ProjectDocumentId, x.ProjectEventId });
                    table.ForeignKey(
                        name: "FK_ProjectDocumentEventLinks_ProjectEvents_ProjectEventId_ProjectId",
                        columns: x => new { x.ProjectEventId, x.ProjectId },
                        principalTable: "ProjectEvents",
                        principalColumns: new[] { "Id", "ProjectId" });
                });

            migrationBuilder.CreateTable(
                name: "ProjectDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CurrentVersionId = table.Column<int>(type: "int", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDocuments", x => x.Id);
                    table.UniqueConstraint("AK_ProjectDocuments_Id_ProjectId", x => new { x.Id, x.ProjectId });
                    table.ForeignKey(
                        name: "FK_ProjectDocuments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectDocumentVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectDocumentId = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hex = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    StorageKey = table.Column<string>(type: "varchar(160)", unicode: false, maxLength: 160, nullable: false),
                    ThumbnailStorageKey = table.Column<string>(type: "varchar(160)", unicode: false, maxLength: 160, nullable: true),
                    State = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDocumentVersions", x => x.Id);
                    table.UniqueConstraint("AK_ProjectDocumentVersions_Id_ProjectDocumentId", x => new { x.Id, x.ProjectDocumentId });
                    table.CheckConstraint("CK_ProjectDocumentVersions_Number", "[Number] > 0");
                    table.CheckConstraint("CK_ProjectDocumentVersions_SizeBytes", "[SizeBytes] >= 0");
                    table.CheckConstraint("CK_ProjectDocumentVersions_State", "[State] IN ('Pending', 'Ready', 'Failed')");
                    table.ForeignKey(
                        name: "FK_ProjectDocumentVersions_ProjectDocuments_ProjectDocumentId",
                        column: x => x.ProjectDocumentId,
                        principalTable: "ProjectDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDocumentEventLinks_ProjectDocumentId_ProjectId",
                table: "ProjectDocumentEventLinks",
                columns: new[] { "ProjectDocumentId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDocumentEventLinks_ProjectEventId",
                table: "ProjectDocumentEventLinks",
                column: "ProjectEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDocumentEventLinks_ProjectEventId_ProjectId",
                table: "ProjectDocumentEventLinks",
                columns: new[] { "ProjectEventId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDocuments_CurrentVersionId_Id",
                table: "ProjectDocuments",
                columns: new[] { "CurrentVersionId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDocuments_ProjectId_DeletedAtUtc",
                table: "ProjectDocuments",
                columns: new[] { "ProjectId", "DeletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDocumentVersions_ProjectDocumentId_Number",
                table: "ProjectDocumentVersions",
                columns: new[] { "ProjectDocumentId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDocumentVersions_StorageKey",
                table: "ProjectDocumentVersions",
                column: "StorageKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectDocumentEventLinks_ProjectDocuments_ProjectDocumentId_ProjectId",
                table: "ProjectDocumentEventLinks",
                columns: new[] { "ProjectDocumentId", "ProjectId" },
                principalTable: "ProjectDocuments",
                principalColumns: new[] { "Id", "ProjectId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectDocuments_ProjectDocumentVersions_CurrentVersionId_Id",
                table: "ProjectDocuments",
                columns: new[] { "CurrentVersionId", "Id" },
                principalTable: "ProjectDocumentVersions",
                principalColumns: new[] { "Id", "ProjectDocumentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectDocumentVersions_ProjectDocuments_ProjectDocumentId",
                table: "ProjectDocumentVersions");

            migrationBuilder.DropTable(
                name: "ProjectDocumentEventLinks");

            migrationBuilder.DropTable(
                name: "ProjectDocuments");

            migrationBuilder.DropTable(
                name: "ProjectDocumentVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ProjectEvents_Id_ProjectId",
                table: "ProjectEvents");
        }
    }
}
