using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ElectronicDocumentLineTaxConfiguration
    : IEntityTypeConfiguration<ElectronicDocumentLineTax>
{
    public void Configure(EntityTypeBuilder<ElectronicDocumentLineTax> builder)
    {
        builder.ToTable("ElectronicDocumentLineTaxes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TaxCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.VatRateCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.Rate).HasPrecision(7, 4);
        builder.Property(x => x.Amount).HasPrecision(18, 5);
        builder.HasIndex(x => x.ElectronicDocumentLineId);
    }
}
