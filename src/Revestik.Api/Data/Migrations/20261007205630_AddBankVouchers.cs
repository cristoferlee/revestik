using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBankVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankVouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Bank = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MerchantName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TransactionDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CardBrand = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CardLastFour = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    AuthorizationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GmailMessageId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MatchedElectronicDocumentId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankVouchers", x => x.Id);
                    table.CheckConstraint("CK_BankVouchers_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_BankVouchers_ElectronicDocuments_MatchedElectronicDocumentId",
                        column: x => x.MatchedElectronicDocumentId,
                        principalTable: "ElectronicDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankVouchers_MatchedElectronicDocumentId",
                table: "BankVouchers",
                column: "MatchedElectronicDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_BankVouchers_ReferenceNumber",
                table: "BankVouchers",
                column: "ReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_BankVouchers_Status_TransactionDate",
                table: "BankVouchers",
                columns: new[] { "Status", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankVouchers_TransactionDate",
                table: "BankVouchers",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "UX_BankVouchers_GmailMessageId",
                table: "BankVouchers",
                column: "GmailMessageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankVouchers");
        }
    }
}
