using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> builder)
    {
        builder.ToTable("SalePayments");
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(payment => payment.PaymentMethod).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(payment => payment.Reference).HasMaxLength(200).IsRequired();
        builder.Property(payment => payment.Notes).HasMaxLength(1000).IsRequired();
        builder.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(payment => payment.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(payment => payment.VoidedByUserId).HasMaxLength(450);
        builder.Property(payment => payment.VoidReason).HasMaxLength(1000).IsRequired();
        builder.HasOne(payment => payment.CreatedByUser).WithMany().HasForeignKey(payment => payment.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(payment => payment.VoidedByUser).WithMany().HasForeignKey(payment => payment.VoidedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(payment => payment.SaleId);
        builder.HasIndex(payment => payment.PaidAtUtc);
        builder.HasIndex(payment => payment.Status);
    }
}