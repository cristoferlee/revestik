using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class QuotationLineConfiguration : IEntityTypeConfiguration<QuotationLine>
{
    public void Configure(EntityTypeBuilder<QuotationLine> builder)
    {
        builder.ToTable("QuotationLines");

        builder.HasKey(line => line.Id);

        builder.Property(line => line.CabysCode)
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(line => line.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(line => line.DiscountType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(line => line.Quantity)
            .HasPrecision(18, 5);

        builder.Property(line => line.UnitPrice)
            .HasPrecision(18, 5);

        builder.Property(line => line.DiscountValue)
            .HasPrecision(18, 5);

        builder.Property(line => line.TaxRate)
            .HasPrecision(5, 2);
    }
}