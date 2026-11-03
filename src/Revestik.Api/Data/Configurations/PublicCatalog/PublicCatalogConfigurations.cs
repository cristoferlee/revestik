using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models.PublicCatalog;

namespace Revestik.Api.Data.Configurations.PublicCatalog;

public sealed class PublicCatalogCategoryConfiguration : IEntityTypeConfiguration<PublicCatalogCategory>
{
    public void Configure(EntityTypeBuilder<PublicCatalogCategory> builder)
    {
        builder.ToTable("PublicCatalogCategories", t => t.HasCheckConstraint("CK_PublicCatalogCategories_DisplayOrder", "[DisplayOrder] >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Slug).HasMaxLength(160).IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.Property(x => x.NameEs).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(150);
        builder.Property(x => x.NameDe).HasMaxLength(150);
        builder.Property(x => x.DescriptionEs).HasMaxLength(2000);
        builder.Property(x => x.DescriptionEn).HasMaxLength(2000);
        builder.Property(x => x.DescriptionDe).HasMaxLength(2000);
        builder.Property(x => x.HeroTitleEs).HasMaxLength(180);
        builder.Property(x => x.HeroTitleEn).HasMaxLength(180);
        builder.Property(x => x.HeroTitleDe).HasMaxLength(180);
        builder.Property(x => x.HeroDescriptionEs).HasMaxLength(2000);
        builder.Property(x => x.HeroDescriptionEn).HasMaxLength(2000);
        builder.Property(x => x.HeroDescriptionDe).HasMaxLength(2000);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.IsActive, x.DisplayOrder });
    }
}

public sealed class PublicCatalogSubcategoryConfiguration : IEntityTypeConfiguration<PublicCatalogSubcategory>
{
    public void Configure(EntityTypeBuilder<PublicCatalogSubcategory> builder)
    {
        builder.ToTable("PublicCatalogSubcategories", t => t.HasCheckConstraint("CK_PublicCatalogSubcategories_DisplayOrder", "[DisplayOrder] >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Slug).HasMaxLength(160).IsRequired();
        builder.HasIndex(x => new { x.CategoryId, x.Slug }).IsUnique();
        builder.Property(x => x.NameEs).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(150);
        builder.Property(x => x.NameDe).HasMaxLength(150);
        builder.Property(x => x.DescriptionEs).HasMaxLength(2000);
        builder.Property(x => x.DescriptionEn).HasMaxLength(2000);
        builder.Property(x => x.DescriptionDe).HasMaxLength(2000);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.Category).WithMany(x => x.Subcategories)
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CategoryId, x.IsActive, x.DisplayOrder });
    }
}

public sealed class PublicCatalogProductConfiguration : IEntityTypeConfiguration<PublicCatalogProduct>
{
    public void Configure(EntityTypeBuilder<PublicCatalogProduct> builder)
    {
        builder.ToTable("PublicCatalogProducts", t =>
        {
            t.HasCheckConstraint("CK_PublicCatalogProducts_Price", "[Price] IS NULL OR [Price] >= 0");
            t.HasCheckConstraint("CK_PublicCatalogProducts_PriceFields", "([Price] IS NULL AND [Currency] IS NULL) OR ([Price] IS NOT NULL AND [Currency] IS NOT NULL AND [Currency] IN ('CRC','USD') AND [SalesUnit] IS NOT NULL AND LEN(LTRIM(RTRIM([SalesUnit]))) > 0)");
            t.HasCheckConstraint("CK_PublicCatalogProducts_DisplayOrder", "[DisplayOrder] >= 0");
            t.HasCheckConstraint("CK_PublicCatalogProducts_PublishedArchived", "NOT ([IsPublished] = 1 AND [IsArchived] = 1)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Slug).HasMaxLength(160).IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.Property(x => x.Brand).HasMaxLength(150);
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsUnicode(false);
        builder.Property(x => x.SalesUnit).HasMaxLength(40);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.IsPublished, x.IsArchived, x.DisplayOrder });
    }
}

public sealed class PublicCatalogProductSubcategoryConfiguration : IEntityTypeConfiguration<PublicCatalogProductSubcategory>
{
    public void Configure(EntityTypeBuilder<PublicCatalogProductSubcategory> builder)
    {
        builder.ToTable("PublicCatalogProductSubcategories");
        builder.HasKey(x => new { x.ProductId, x.SubcategoryId });
        builder.HasOne(x => x.Product).WithMany(x => x.SubcategoryLinks)
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Subcategory).WithMany(x => x.ProductLinks)
            .HasForeignKey(x => x.SubcategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SubcategoryId, x.ProductId });
    }
}

public sealed class PublicCatalogProductTranslationConfiguration : IEntityTypeConfiguration<PublicCatalogProductTranslation>
{
    public void Configure(EntityTypeBuilder<PublicCatalogProductTranslation> builder)
    {
        builder.ToTable("PublicCatalogProductTranslations", t =>
            t.HasCheckConstraint("CK_PublicCatalogProductTranslations_Language", "[LanguageCode] IN ('es','en','de')"));
        builder.HasKey(x => new { x.ProductId, x.LanguageCode });
        builder.Property(x => x.LanguageCode).HasMaxLength(2).IsUnicode(false).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.SeoTitle).HasMaxLength(180);
        builder.Property(x => x.SeoDescription).HasMaxLength(320);
        builder.HasOne(x => x.Product).WithMany(x => x.Translations)
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
