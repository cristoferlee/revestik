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
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "CK_Products_CabysCode",
                    "LEN([CabysCode]) = 13 AND [CabysCode] NOT LIKE '%[^0-9]%'");
                tableBuilder.HasCheckConstraint(
                    "CK_Products_Conversion",
                    "[CommercialUnitsPerInventoryUnit] > 0");
                tableBuilder.HasCheckConstraint(
                    "CK_Products_CurrentCost",
                    "[CurrentCost] > 0");
                tableBuilder.HasCheckConstraint(
                    "CK_Products_MinimumStock",
                    "[MinimumStock] >= 0");
                tableBuilder.HasCheckConstraint(
                    "CK_Products_SalePrice",
                    "[SalePrice] >= 0");
                tableBuilder.HasCheckConstraint(
                    "CK_Products_StockQuantity",
                    "[StockQuantity] >= 0 AND ([RequiresWholeInventoryUnits] = 0 OR [StockQuantity] = FLOOR([StockQuantity]))");
                tableBuilder.HasCheckConstraint(
                    "CK_Products_TaxRate",
                    "[TaxRate] IN (0, 13)");
            });

        builder.HasKey(product => product.Id);

        builder.Property(product => product.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(product => product.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(product => product.CabysCode)
            .HasMaxLength(13)
            .IsFixedLength()
            .IsRequired();

        builder.Property(product => product.CommercialUnitsPerInventoryUnit)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(product => product.RequiresWholeInventoryUnits)
            .IsRequired();

        builder.Property(product => product.SalePrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(product => product.CurrentCost)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(product => product.TaxRate)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(product => product.StockQuantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(product => product.MinimumStock)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(product => product.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(product => product.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(product => product.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.HasOne(product => product.Category)
            .WithMany(category => category.Products)
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(product => product.InventoryUnit)
            .WithMany()
            .HasForeignKey(product => product.InventoryUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(product => product.CommercialUnit)
            .WithMany()
            .HasForeignKey(product => product.CommercialUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(product => product.Name);

        builder.HasIndex(product => product.CabysCode);

        builder.HasIndex(product => product.CategoryId);

        builder.HasIndex(product => product.InventoryUnitId);

        builder.HasIndex(product => product.CommercialUnitId);
    }
}