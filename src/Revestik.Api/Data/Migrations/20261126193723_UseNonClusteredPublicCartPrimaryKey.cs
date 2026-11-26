using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Revestik.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UseNonClusteredPublicCartPrimaryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PublicCartItems",
                table: "PublicCartItems");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PublicCartItems",
                table: "PublicCartItems",
                columns: new[] { "UserId", "ProductId" })
                .Annotation("SqlServer:Clustered", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM [PublicCartItems] WITH (TABLOCKX, HOLDLOCK)
                    WHERE DATALENGTH([UserId]) + DATALENGTH([ProductId]) > 900
                )
                BEGIN
                    THROW 51000,
                        'Cannot restore clustered cart key: existing keys exceed 900 bytes.',
                        1;
                END;
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_PublicCartItems",
                table: "PublicCartItems");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PublicCartItems",
                table: "PublicCartItems",
                columns: new[] { "UserId", "ProductId" });
        }
    }
}
