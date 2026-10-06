using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ElectronicDocumentLineDiscountConfiguration
    : IEntityTypeConfiguration<ElectronicDocumentLineDiscount>
{
    public void Configure(EntityTypeBuilder<ElectronicDocumentLineDiscount> builder)
    {
        builder.ToTable("ElectronicDocumentLineDiscounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 5);
        builder.Property(x => x.Code).HasMaxLength(2).IsRequired();
        builder.Property(x => x.Nature).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.ElectronicDocumentLineId);
    }
}
