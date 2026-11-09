using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProductCategoryConfiguration
    : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("ProductCategories");

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(category => category.IsActive)
            .HasDefaultValue(true);

        builder.Property(category => category.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(category => category.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(category => category.PublicSlug).HasMaxLength(160);
        builder.HasIndex(category => category.PublicSlug).IsUnique()
            .HasFilter("[PublicSlug] IS NOT NULL");
        builder.Property(category => category.PublicNameEn).HasMaxLength(150);
        builder.Property(category => category.PublicNameDe).HasMaxLength(150);
        builder.Property(category => category.PublicDescriptionEs).HasMaxLength(2000);
        builder.Property(category => category.PublicDescriptionEn).HasMaxLength(2000);
        builder.Property(category => category.PublicDescriptionDe).HasMaxLength(2000);
        builder.Property(category => category.PublicHeroTitleEs).HasMaxLength(180);
        builder.Property(category => category.PublicHeroTitleEn).HasMaxLength(180);
        builder.Property(category => category.PublicHeroTitleDe).HasMaxLength(180);
        builder.Property(category => category.PublicHeroDescriptionEs).HasMaxLength(2000);
        builder.Property(category => category.PublicHeroDescriptionEn).HasMaxLength(2000);
        builder.Property(category => category.PublicHeroDescriptionDe).HasMaxLength(2000);
        builder.Property(category => category.RowVersion).IsRowVersion();
        builder.HasIndex(category => category.Name)
            .IsUnique();
    }
}