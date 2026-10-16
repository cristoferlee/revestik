using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectAgreedAmountRevisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    PreviousProjectVersion = table.Column<long>(type: "bigint", nullable: false),
                    PreviousAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NewAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PreviousCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    NewCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAgreedAmountRevisions", x => x.Id);
                    table.CheckConstraint("CK_ProjectAgreedAmountRevisions_NewAmount", "[NewAmount] IS NULL OR [NewAmount] >= 0");
                    table.CheckConstraint("CK_ProjectAgreedAmountRevisions_PreviousAmount", "[PreviousAmount] IS NULL OR [PreviousAmount] >= 0");
                    table.CheckConstraint("CK_ProjectAgreedAmountRevisions_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0");
                    table.ForeignKey(
                        name: "FK_ProjectAgreedAmountRevisions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VoidedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectPayments", x => x.Id);
                    table.UniqueConstraint("AK_ProjectPayments_Id_ProjectId", x => new { x.Id, x.ProjectId });
                    table.CheckConstraint("CK_ProjectPayments_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_ProjectPayments_Currency", "[CurrencyCode] IN ('CRC', 'USD')");
                    table.CheckConstraint("CK_ProjectPayments_Status", "[Status] IN ('Active', 'Voided')");
                    table.CheckConstraint("CK_ProjectPayments_VoidDetails", "([Status] = 'Active' AND [VoidedAtUtc] IS NULL AND [VoidedByUserId] IS NULL AND [VoidReason] IS NULL) OR ([Status] = 'Voided' AND [VoidedAtUtc] IS NOT NULL AND [VoidedByUserId] IS NOT NULL AND [VoidReason] IS NOT NULL AND LEN(LTRIM(RTRIM([VoidReason]))) > 0)");
                    table.ForeignKey(
                        name: "FK_ProjectPayments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPaymentDocumentLinks",
                columns: table => new
                {
                    ProjectPaymentId = table.Column<int>(type: "int", nullable: false),
                    ProjectDocumentId = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    LinkedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LinkedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectPaymentDocumentLinks", x => new { x.ProjectPaymentId, x.ProjectDocumentId });
                    table.ForeignKey(
                        name: "FK_ProjectPaymentDocumentLinks_ProjectDocuments_ProjectDocumentId_ProjectId",
                        columns: x => new { x.ProjectDocumentId, x.ProjectId },
                        principalTable: "ProjectDocuments",
                        principalColumns: new[] { "Id", "ProjectId" });
                    table.ForeignKey(
                        name: "FK_ProjectPaymentDocumentLinks_ProjectPayments_ProjectPaymentId_ProjectId",
                        columns: x => new { x.ProjectPaymentId, x.ProjectId },
                        principalTable: "ProjectPayments",
                        principalColumns: new[] { "Id", "ProjectId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPaymentRevisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectPaymentId = table.Column<int>(type: "int", nullable: false),
                    PreviousVersion = table.Column<long>(type: "bigint", nullable: false),
                    PreviousAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PreviousCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PreviousPaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PreviousReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreviousNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectPaymentRevisions", x => x.Id);
                    table.CheckConstraint("CK_ProjectPaymentRevisions_PreviousAmount", "[PreviousAmount] > 0");
                    table.CheckConstraint("CK_ProjectPaymentRevisions_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0");
                    table.ForeignKey(
                        name: "FK_ProjectPaymentRevisions_ProjectPayments_ProjectPaymentId",
                        column: x => x.ProjectPaymentId,
                        principalTable: "ProjectPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgreedAmountRevisions_ProjectId_PreviousProjectVersion",
                table: "ProjectAgreedAmountRevisions",
                columns: new[] { "ProjectId", "PreviousProjectVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentDocumentLinks_ProjectDocumentId",
                table: "ProjectPaymentDocumentLinks",
                column: "ProjectDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentDocumentLinks_ProjectDocumentId_ProjectId",
                table: "ProjectPaymentDocumentLinks",
                columns: new[] { "ProjectDocumentId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentDocumentLinks_ProjectPaymentId_ProjectId",
                table: "ProjectPaymentDocumentLinks",
                columns: new[] { "ProjectPaymentId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPaymentRevisions_ProjectPaymentId_PreviousVersion",
                table: "ProjectPaymentRevisions",
                columns: new[] { "ProjectPaymentId", "PreviousVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPayments_ProjectId_Status_PaidOn",
                table: "ProjectPayments",
                columns: new[] { "ProjectId", "Status", "PaidOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectAgreedAmountRevisions");

            migrationBuilder.DropTable(
                name: "ProjectPaymentDocumentLinks");

            migrationBuilder.DropTable(
                name: "ProjectPaymentRevisions");

            migrationBuilder.DropTable(
                name: "ProjectPayments");
        }
    }
}
