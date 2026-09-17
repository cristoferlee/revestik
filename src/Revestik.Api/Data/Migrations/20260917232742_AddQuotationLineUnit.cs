using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotationLineUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "QuotationLines",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Unit",
                table: "QuotationLines");
        }
    }
}
