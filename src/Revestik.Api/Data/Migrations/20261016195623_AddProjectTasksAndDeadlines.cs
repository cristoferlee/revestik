using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectTasksAndDeadlines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectEventDeadlineRevisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectEventId = table.Column<int>(type: "int", nullable: false),
                    PreviousVersion = table.Column<long>(type: "bigint", nullable: false),
                    PreviousDueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectEventDeadlineRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectEventDeadlineRevisions_ProjectEvents_ProjectEventId",
                        column: x => x.ProjectEventId,
                        principalTable: "ProjectEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectEventDeadlines",
                columns: table => new
                {
                    ProjectEventId = table.Column<int>(type: "int", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectEventDeadlines", x => x.ProjectEventId);
                    table.ForeignKey(
                        name: "FK_ProjectEventDeadlines_ProjectEvents_ProjectEventId",
                        column: x => x.ProjectEventId,
                        principalTable: "ProjectEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AssignedToUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTasks", x => x.Id);
                    table.CheckConstraint("CK_ProjectTasks_Completion", "([IsCompleted] = 0 AND [CompletedAtUtc] IS NULL AND [CompletedByUserId] IS NULL) OR ([IsCompleted] = 1 AND [CompletedAtUtc] IS NOT NULL AND [CompletedByUserId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ProjectTasks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTaskRevisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectTaskId = table.Column<int>(type: "int", nullable: false),
                    PreviousVersion = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AssignedToUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    WasCompleted = table.Column<bool>(type: "bit", nullable: false),
                    PreviousCompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PreviousCompletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTaskRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTaskRevisions_ProjectTasks_ProjectTaskId",
                        column: x => x.ProjectTaskId,
                        principalTable: "ProjectTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectEventDeadlineRevisions_ProjectEventId_PreviousVersion",
                table: "ProjectEventDeadlineRevisions",
                columns: new[] { "ProjectEventId", "PreviousVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectEventDeadlines_DueOn",
                table: "ProjectEventDeadlines",
                column: "DueOn");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTaskRevisions_ProjectTaskId_PreviousVersion",
                table: "ProjectTaskRevisions",
                columns: new[] { "ProjectTaskId", "PreviousVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_AssignedToUserId",
                table: "ProjectTasks",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_ProjectId_IsCompleted_DueOn",
                table: "ProjectTasks",
                columns: new[] { "ProjectId", "IsCompleted", "DueOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectEventDeadlineRevisions");

            migrationBuilder.DropTable(
                name: "ProjectEventDeadlines");

            migrationBuilder.DropTable(
                name: "ProjectTaskRevisions");

            migrationBuilder.DropTable(
                name: "ProjectTasks");
        }
    }
}
