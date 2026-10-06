using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class CabysItemConfiguration : IEntityTypeConfiguration<CabysItem>
{
    public void Configure(EntityTypeBuilder<CabysItem> builder)
    {
        builder.ToTable("CabysItems");
        builder.HasKey(x => new { x.CatalogVersion, x.Code });

        builder.Property(x => x.CatalogVersion).HasMaxLength(20);
        builder.Property(x => x.Code).HasMaxLength(13).IsFixedLength();
        builder.Property(x => x.Description).HasMaxLength(600);
        builder.Property(x => x.TaxReference).HasMaxLength(30);

        ConfigureLevel(builder, x => x.Category1Code, x => x.Category1Description);
        ConfigureLevel(builder, x => x.Category2Code, x => x.Category2Description);
        ConfigureLevel(builder, x => x.Category3Code, x => x.Category3Description);
        ConfigureLevel(builder, x => x.Category4Code, x => x.Category4Description);
        ConfigureLevel(builder, x => x.Category5Code, x => x.Category5Description);
        ConfigureLevel(builder, x => x.Category6Code, x => x.Category6Description);
        ConfigureLevel(builder, x => x.Category7Code, x => x.Category7Description);
        ConfigureLevel(builder, x => x.Category8Code, x => x.Category8Description);

        builder.Property(x => x.Includes).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Excludes).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => x.Code)
            .HasDatabaseName("IX_CabysItems_Code");

        builder.HasIndex(x => new { x.CatalogVersion, x.Category4Code })
            .HasDatabaseName("IX_CabysItems_Version_Category4");

        builder.HasOne(x => x.Catalog)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.CatalogVersion)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureLevel(
        EntityTypeBuilder<CabysItem> builder,
        System.Linq.Expressions.Expression<Func<CabysItem, string>> code,
        System.Linq.Expressions.Expression<Func<CabysItem, string>> description)
    {
        builder.Property(code).HasMaxLength(13);
        builder.Property(description).HasMaxLength(1000);
    }
}
