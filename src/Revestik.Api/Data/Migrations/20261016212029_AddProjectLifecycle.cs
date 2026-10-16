using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectLifecycleChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    PreviousProjectVersion = table.Column<long>(type: "bigint", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClosedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ClosingObservations = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EvidenceDocumentId = table.Column<int>(type: "int", nullable: true),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectLifecycleChanges", x => x.Id);
                    table.CheckConstraint("CK_ProjectLifecycleChanges_Action", "[Action] IN ('Transition', 'Close', 'Reopen')");
                    table.ForeignKey(
                        name: "FK_ProjectLifecycleChanges_ProjectDocuments_EvidenceDocumentId_ProjectId",
                        columns: x => new { x.EvidenceDocumentId, x.ProjectId },
                        principalTable: "ProjectDocuments",
                        principalColumns: new[] { "Id", "ProjectId" });
                    table.ForeignKey(
                        name: "FK_ProjectLifecycleChanges_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLifecycleChanges_EvidenceDocumentId_ProjectId",
                table: "ProjectLifecycleChanges",
                columns: new[] { "EvidenceDocumentId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLifecycleChanges_ProjectId_ChangedAtUtc",
                table: "ProjectLifecycleChanges",
                columns: new[] { "ProjectId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLifecycleChanges_ProjectId_PreviousProjectVersion",
                table: "ProjectLifecycleChanges",
                columns: new[] { "ProjectId", "PreviousProjectVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectLifecycleChanges");
        }
    }
}
