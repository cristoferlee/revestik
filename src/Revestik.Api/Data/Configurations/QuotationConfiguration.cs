using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> builder)
    {
        builder.ToTable("Quotations");

        builder.HasKey(quotation => quotation.Id);

        builder.Property(quotation => quotation.QuotationNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(quotation => quotation.QuotationNumber)
            .IsUnique();

        builder.Property(quotation => quotation.Currency)
            .HasConversion<string>()
            .HasMaxLength(3)
            .IsRequired();

        builder.HasOne(quotation => quotation.Customer)
            .WithMany()
            .HasForeignKey(quotation => quotation.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(quotation => quotation.Lines)
            .WithOne(line => line.Quotation)
            .HasForeignKey(line => line.QuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(quotation => quotation.Charges)
            .WithOne(charge => charge.Quotation)
            .HasForeignKey(charge => charge.QuotationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}