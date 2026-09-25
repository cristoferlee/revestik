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

        builder.HasIndex(category => category.Name)
            .IsUnique();
    }
}