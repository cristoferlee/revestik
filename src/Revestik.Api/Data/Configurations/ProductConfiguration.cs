using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProductConfiguration
    : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable(
            "Products",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "CK_Products_TaxRate",
                "[TaxRate] IN (0, 13)"));

        builder.HasKey(product => product.Id);

        builder.Property(product => product.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(product => product.CabysCode)
            .HasMaxLength(13);

        builder.Property(product => product.Unit)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(product => product.SalePrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(product => product.TaxRate)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(product => product.StockQuantity)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(product => product.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(product => product.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(product => product.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.HasIndex(product => product.Description);

        builder.HasIndex(product => product.CabysCode);
    }
}