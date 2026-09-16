using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Quotations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quotations_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuotationCharges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationCharges_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotationLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    CabysCode = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    DiscountType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationLines_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationCharges_QuotationId",
                table: "QuotationCharges",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationLines_QuotationId",
                table: "QuotationLines",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_CustomerId",
                table: "Quotations",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_QuotationNumber",
                table: "Quotations",
                column: "QuotationNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuotationCharges");

            migrationBuilder.DropTable(
                name: "QuotationLines");

            migrationBuilder.DropTable(
                name: "Quotations");
        }
    }
}
