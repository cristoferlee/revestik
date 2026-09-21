using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationCustomerSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerEmailSnapshot",
                table: "Quotations",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomerIdentificationNumberSnapshot",
                table: "Quotations",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomerNameSnapshot",
                table: "Quotations",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomerPhoneNumberSnapshot",
                table: "Quotations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerEmailSnapshot",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "CustomerIdentificationNumberSnapshot",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "CustomerNameSnapshot",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "CustomerPhoneNumberSnapshot",
                table: "Quotations");
        }
    }
}
