using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectEventDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectEventAgreementRevisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectEventId = table.Column<int>(type: "int", nullable: false),
                    PreviousVersion = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AcceptedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AcceptedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    AcceptanceMedium = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectEventAgreementRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectEventAgreementRevisions_ProjectEvents_ProjectEventId",
                        column: x => x.ProjectEventId,
                        principalTable: "ProjectEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectEventAgreements",
                columns: table => new
                {
                    ProjectEventId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AcceptedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AcceptedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    AcceptanceMedium = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectEventAgreements", x => x.ProjectEventId);
                    table.CheckConstraint("CK_ProjectEventAgreements_Status", "[Status] IN ('Pending', 'Fulfilled', 'Modified', 'Unfulfilled')");
                    table.ForeignKey(
                        name: "FK_ProjectEventAgreements_ProjectEvents_ProjectEventId",
                        column: x => x.ProjectEventId,
                        principalTable: "ProjectEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectEventComments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectEventId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectEventComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectEventComments_ProjectEvents_ProjectEventId",
                        column: x => x.ProjectEventId,
                        principalTable: "ProjectEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectEventAgreementRevisions_ProjectEventId_PreviousVersion",
                table: "ProjectEventAgreementRevisions",
                columns: new[] { "ProjectEventId", "PreviousVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectEventComments_ProjectEventId_CreatedAtUtc_Id",
                table: "ProjectEventComments",
                columns: new[] { "ProjectEventId", "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectEventAgreementRevisions");

            migrationBuilder.DropTable(
                name: "ProjectEventAgreements");

            migrationBuilder.DropTable(
                name: "ProjectEventComments");
        }
    }
}
