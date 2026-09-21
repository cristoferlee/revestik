using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class QuotationChargeConfiguration : IEntityTypeConfiguration<QuotationCharge>
{
    public void Configure(EntityTypeBuilder<QuotationCharge> builder)
    {
        builder.ToTable("QuotationCharges");

        builder.HasKey(charge => charge.Id);

        builder.Property(charge => charge.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(charge => charge.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(charge => charge.Amount)
            .HasPrecision(18, 2);
    }
}