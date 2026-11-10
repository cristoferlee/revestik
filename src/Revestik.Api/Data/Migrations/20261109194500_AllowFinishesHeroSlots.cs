using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Revestik.Api.Data;

#nullable disable
namespace Revestik.Api.Data.Migrations;

// Slots 1..3 remain Home. Slots 4..6 are reserved exclusively for Acabados.
// Deliberate data-only check-constraint expansion: no new EF entity or snapshot
// shape is required. Both route families enforce their own (1..3) public slots.
[DbContext(typeof(RevestikDbContext))]
[Migration("20261109194500_AllowFinishesHeroSlots")]
public partial class AllowFinishesHeroSlots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_PublicHomeHeroSlides_Slot", table: "PublicHomeHeroSlides");
        migrationBuilder.AddCheckConstraint(
            name: "CK_PublicHomeHeroSlides_Slot", table: "PublicHomeHeroSlides",
            sql: "[Slot] BETWEEN 1 AND 6");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Protect the Acabados data against accidental loss on rollback.
        migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM dbo.PublicHomeHeroSlides WHERE Slot BETWEEN 4 AND 6)
            THROW 51001, 'Remove Acabados hero slides before rolling back this migration.', 1;");
        migrationBuilder.DropCheckConstraint(
            name: "CK_PublicHomeHeroSlides_Slot", table: "PublicHomeHeroSlides");
        migrationBuilder.AddCheckConstraint(
            name: "CK_PublicHomeHeroSlides_Slot", table: "PublicHomeHeroSlides",
            sql: "[Slot] BETWEEN 1 AND 3");
    }
}
