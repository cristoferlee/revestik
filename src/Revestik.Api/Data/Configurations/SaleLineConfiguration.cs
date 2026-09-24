using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> builder)
    {
        builder.ToTable("SaleLines");
        builder.HasKey(line => line.Id);
        builder.Property(line => line.CabysCode).HasMaxLength(13).IsRequired(false);
        builder.Property(line => line.Description).HasMaxLength(500).IsRequired();
        builder.Property(line => line.Unit).HasMaxLength(50).IsRequired();
        builder.Property(line => line.DiscountType).HasConversion<string>().HasMaxLength(20);
        builder.Property(line => line.Quantity).HasPrecision(18, 2);
        builder.Property(line => line.UnitPrice).HasPrecision(18, 2);
        builder.Property(line => line.DiscountValue).HasPrecision(18, 2);
        builder.Property(line => line.TaxRate).HasPrecision(5, 2);
        builder.HasOne(line => line.Product).WithMany().HasForeignKey(line => line.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}