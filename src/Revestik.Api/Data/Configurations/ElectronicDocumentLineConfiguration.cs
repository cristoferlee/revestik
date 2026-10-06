using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ElectronicDocumentLineConfiguration
    : IEntityTypeConfiguration<ElectronicDocumentLine>
{
    public void Configure(EntityTypeBuilder<ElectronicDocumentLine> builder)
    {
        builder.ToTable("ElectronicDocumentLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CabysCode).HasMaxLength(13).IsRequired();
        builder.Property(x => x.CommercialCodeType).HasMaxLength(2).IsRequired();
        builder.Property(x => x.CommercialCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitOfMeasure).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CommercialUnitOfMeasure).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 5);
        builder.Property(x => x.GrossAmount).HasPrecision(18, 5);
        builder.Property(x => x.Subtotal).HasPrecision(18, 5);
        builder.Property(x => x.TaxableBase).HasPrecision(18, 5);
        builder.Property(x => x.NetTax).HasPrecision(18, 5);
        builder.Property(x => x.TotalLine).HasPrecision(18, 5);
        builder.Property(x => x.OperationalDestination)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasOne(x => x.ClassificationCategory)
            .WithMany()
            .HasForeignKey(x => x.ClassificationCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Discounts).WithOne(x => x.ElectronicDocumentLine)
            .HasForeignKey(x => x.ElectronicDocumentLineId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Taxes).WithOne(x => x.ElectronicDocumentLine)
            .HasForeignKey(x => x.ElectronicDocumentLineId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ElectronicDocumentId, x.LineNumber }).IsUnique();
        builder.HasIndex(x => x.CabysCode);
        builder.HasIndex(x => x.ClassificationCategoryId);
    }
}
