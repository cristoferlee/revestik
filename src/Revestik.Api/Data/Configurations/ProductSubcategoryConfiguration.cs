using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProductSubcategoryConfiguration : IEntityTypeConfiguration<ProductSubcategory>
{
    public void Configure(EntityTypeBuilder<ProductSubcategory> builder)
    {
        builder.ToTable("ProductSubcategories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.Category).WithMany(x => x.Subcategories)
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.PublicSlug).HasMaxLength(160);
        builder.HasIndex(x => new { x.CategoryId, x.PublicSlug }).IsUnique()
            .HasFilter("[PublicSlug] IS NOT NULL");
        builder.Property(x => x.PublicNameEn).HasMaxLength(150);
        builder.Property(x => x.PublicNameDe).HasMaxLength(150);
        builder.Property(x => x.PublicDescriptionEs).HasMaxLength(2000);
        builder.Property(x => x.PublicDescriptionEn).HasMaxLength(2000);
        builder.Property(x => x.PublicDescriptionDe).HasMaxLength(2000);
        builder.HasIndex(x => new { x.CategoryId, x.Name }).IsUnique();
        // Composite principal key lets SQL reject category/subcategory mismatches.
        builder.HasAlternateKey(x => new { x.CategoryId, x.Id });
        builder.HasIndex(x => new { x.CategoryId, x.IsActive });
    }
}
