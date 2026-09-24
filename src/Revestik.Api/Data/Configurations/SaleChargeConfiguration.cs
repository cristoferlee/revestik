using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class SaleChargeConfiguration : IEntityTypeConfiguration<SaleCharge>
{
    public void Configure(EntityTypeBuilder<SaleCharge> builder)
    {
        builder.ToTable("SaleCharges");
        builder.HasKey(charge => charge.Id);
        builder.Property(charge => charge.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(charge => charge.Description).HasMaxLength(500).IsRequired();
        builder.Property(charge => charge.Amount).HasPrecision(18, 2);
    }
}