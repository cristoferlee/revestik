using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseAccountsPayable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreditTermDays",
                table: "Purchases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "Purchases",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentType",
                table: "Purchases",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Cash");

            migrationBuilder.CreateTable(
                name: "PurchasePayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    VoidedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchasePayments", x => x.Id);
                    table.CheckConstraint("CK_PurchasePayments_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_PurchasePayments_ExchangeRate", "[ExchangeRate] IS NULL OR [ExchangeRate] > 0");
                    table.ForeignKey(
                        name: "FK_PurchasePayments_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchasePayments_AspNetUsers_VoidedByUserId",
                        column: x => x.VoidedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchasePayments_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_DueDate",
                table: "Purchases",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_PaymentType",
                table: "Purchases",
                column: "PaymentType");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Purchases_PaymentTerms",
                table: "Purchases",
                sql: "([PaymentType] = 'Cash' AND [CreditTermDays] IS NULL AND [DueDate] IS NULL) OR ([PaymentType] = 'Credit' AND [CreditTermDays] IS NOT NULL AND [CreditTermDays] > 0 AND [DueDate] IS NOT NULL AND [DueDate] = DATEADD(day, [CreditTermDays], [PurchaseDate]))");

            migrationBuilder.CreateIndex(
                name: "IX_PurchasePayments_CreatedByUserId",
                table: "PurchasePayments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchasePayments_PaidAtUtc",
                table: "PurchasePayments",
                column: "PaidAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PurchasePayments_PurchaseId",
                table: "PurchasePayments",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchasePayments_Status",
                table: "PurchasePayments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PurchasePayments_VoidedByUserId",
                table: "PurchasePayments",
                column: "VoidedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchasePayments");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_DueDate",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_PaymentType",
                table: "Purchases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Purchases_PaymentTerms",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "CreditTermDays",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "Purchases");
        }
    }
}
