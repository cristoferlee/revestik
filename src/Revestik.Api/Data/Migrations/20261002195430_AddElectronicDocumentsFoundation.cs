using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddElectronicDocumentsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Suppliers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CommercialName",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IdentificationNumber",
                table: "Suppliers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentificationType",
                table: "Suppliers",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "PurchaseLines",
                type: "decimal(18,5)",
                precision: 18,
                scale: 5,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentCost",
                table: "Products",
                type: "decimal(18,5)",
                precision: 18,
                scale: 5,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "InventoryMovements",
                type: "decimal(18,5)",
                precision: 18,
                scale: 5,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "InventoryCostLayers",
                type: "decimal(18,5)",
                precision: 18,
                scale: 5,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ResolvedUnitCost",
                table: "InventoryCostLayers",
                type: "decimal(18,5)",
                precision: 18,
                scale: 5,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ElectronicDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Clave = table.Column<string>(type: "nchar(50)", fixedLength: true, maxLength: 50, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NumeroConsecutivo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaEmision = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IssuerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IssuerCommercialName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IssuerIdentificationType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IssuerIdentification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IssuerPhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IssuerEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    IssuerAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReceiverName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReceiverIdentificationType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ReceiverIdentification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SaleConditionCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CreditTermDays = table.Column<int>(type: "int", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalTaxedServices = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalExemptServices = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalExoneratedServices = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalNonSubjectServices = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalTaxedGoods = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalExemptGoods = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalExoneratedGoods = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalNonSubjectGoods = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalTaxed = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalExempt = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalExonerated = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalNonSubject = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalSale = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalDiscounts = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalNetSale = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalTax = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalVatReturned = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalOtherCharges = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalDocument = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    ProcessingStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    PurchaseId = table.Column<int>(type: "int", nullable: true),
                    OriginalXml = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ImportedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectronicDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectronicDocuments_AspNetUsers_ImportedByUserId",
                        column: x => x.ImportedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElectronicDocuments_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElectronicDocuments_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ElectronicDocumentLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ElectronicDocumentId = table.Column<int>(type: "int", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    CabysCode = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    CommercialCodeType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CommercialCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CommercialUnitOfMeasure = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TaxableBase = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    NetTax = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalLine = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectronicDocumentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectronicDocumentLines_ElectronicDocuments_ElectronicDocumentId",
                        column: x => x.ElectronicDocumentId,
                        principalTable: "ElectronicDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HaciendaResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Clave = table.Column<string>(type: "nchar(50)", fixedLength: true, maxLength: 50, nullable: false),
                    IssuerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IssuerIdentificationType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IssuerIdentification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReceiverName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReceiverIdentificationType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ReceiverIdentification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MessageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MessageStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MessageDetail = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TotalTax = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TotalInvoice = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    OriginalXml = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ElectronicDocumentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HaciendaResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HaciendaResponses_ElectronicDocuments_ElectronicDocumentId",
                        column: x => x.ElectronicDocumentId,
                        principalTable: "ElectronicDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ElectronicDocumentLineDiscounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ElectronicDocumentLineId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Nature = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectronicDocumentLineDiscounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectronicDocumentLineDiscounts_ElectronicDocumentLines_ElectronicDocumentLineId",
                        column: x => x.ElectronicDocumentLineId,
                        principalTable: "ElectronicDocumentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ElectronicDocumentLineTaxes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ElectronicDocumentLineId = table.Column<int>(type: "int", nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    VatRateCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectronicDocumentLineTaxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectronicDocumentLineTaxes_ElectronicDocumentLines_ElectronicDocumentLineId",
                        column: x => x.ElectronicDocumentLineId,
                        principalTable: "ElectronicDocumentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Suppliers_Identification",
                table: "Suppliers",
                columns: new[] { "IdentificationType", "IdentificationNumber" },
                unique: true,
                filter: "[IdentificationNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocumentLineDiscounts_ElectronicDocumentLineId",
                table: "ElectronicDocumentLineDiscounts",
                column: "ElectronicDocumentLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocumentLines_CabysCode",
                table: "ElectronicDocumentLines",
                column: "CabysCode");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocumentLines_ElectronicDocumentId_LineNumber",
                table: "ElectronicDocumentLines",
                columns: new[] { "ElectronicDocumentId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocumentLineTaxes_ElectronicDocumentLineId",
                table: "ElectronicDocumentLineTaxes",
                column: "ElectronicDocumentLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocuments_DocumentType",
                table: "ElectronicDocuments",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocuments_FechaEmision",
                table: "ElectronicDocuments",
                column: "FechaEmision");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocuments_ImportedByUserId",
                table: "ElectronicDocuments",
                column: "ImportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocuments_IssuerIdentification",
                table: "ElectronicDocuments",
                column: "IssuerIdentification");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocuments_NumeroConsecutivo",
                table: "ElectronicDocuments",
                column: "NumeroConsecutivo");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocuments_ProcessingStatus",
                table: "ElectronicDocuments",
                column: "ProcessingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocuments_PurchaseId",
                table: "ElectronicDocuments",
                column: "PurchaseId",
                unique: true,
                filter: "[PurchaseId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicDocuments_SupplierId",
                table: "ElectronicDocuments",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "UX_ElectronicDocuments_Clave",
                table: "ElectronicDocuments",
                column: "Clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HaciendaResponses_ElectronicDocumentId",
                table: "HaciendaResponses",
                column: "ElectronicDocumentId",
                unique: true,
                filter: "[ElectronicDocumentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_HaciendaResponses_Clave",
                table: "HaciendaResponses",
                column: "Clave",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElectronicDocumentLineDiscounts");

            migrationBuilder.DropTable(
                name: "ElectronicDocumentLineTaxes");

            migrationBuilder.DropTable(
                name: "HaciendaResponses");

            migrationBuilder.DropTable(
                name: "ElectronicDocumentLines");

            migrationBuilder.DropTable(
                name: "ElectronicDocuments");

            migrationBuilder.DropIndex(
                name: "UX_Suppliers_Identification",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CommercialName",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IdentificationNumber",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IdentificationType",
                table: "Suppliers");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "PurchaseLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,5)",
                oldPrecision: 18,
                oldScale: 5);

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentCost",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,5)",
                oldPrecision: 18,
                oldScale: 5);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "InventoryMovements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,5)",
                oldPrecision: 18,
                oldScale: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                table: "InventoryCostLayers",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,5)",
                oldPrecision: 18,
                oldScale: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ResolvedUnitCost",
                table: "InventoryCostLayers",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,5)",
                oldPrecision: 18,
                oldScale: 5,
                oldNullable: true);
        }
    }
}
