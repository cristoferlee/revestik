using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;
using Revestik.Shared.Purchases;

namespace Revestik.Api.Data.Configurations;

public sealed class PurchaseConfiguration
    : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable(
            "Purchases",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "CK_Purchases_Currency",
                    "[Currency] IN ('CRC', 'USD')");

                tableBuilder.HasCheckConstraint(
                    "CK_Purchases_ExchangeRate",
                    "([Currency] = 'CRC' AND [ExchangeRate] IS NULL) OR " +
                    "([Currency] = 'USD' AND [ExchangeRate] IS NOT NULL AND [ExchangeRate] > 0)");

                tableBuilder.HasCheckConstraint(
                    "CK_Purchases_PaymentTerms",
                    "([PaymentType] = 'Cash' AND [CreditTermDays] IS NULL AND [DueDate] IS NULL) OR " +
                    "([PaymentType] = 'Credit' AND [CreditTermDays] IS NOT NULL AND [CreditTermDays] > 0 AND " +
                    "[DueDate] IS NOT NULL AND [DueDate] = DATEADD(day, [CreditTermDays], [PurchaseDate]))");
            });

        builder.HasKey(purchase => purchase.Id);

        builder.Property(purchase => purchase.PurchaseDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(purchase => purchase.Currency)
            .HasConversion<string>()
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(purchase => purchase.ExchangeRate)
            .HasPrecision(18, 6);

        builder.Property(purchase => purchase.PaymentType)
            .HasConversion<string>()
            .HasMaxLength(10)
            .HasDefaultValue(PurchasePaymentType.Cash)
            .IsRequired();

        builder.Property(purchase => purchase.DueDate)
            .HasColumnType("date");

        builder.Property(purchase => purchase.Notes)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(purchase => purchase.CreatedByUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(purchase => purchase.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.HasOne(purchase => purchase.Supplier)
            .WithMany()
            .HasForeignKey(purchase => purchase.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(purchase => purchase.CreatedByUser)
            .WithMany()
            .HasForeignKey(purchase => purchase.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(purchase => purchase.Lines)
            .WithOne(line => line.Purchase)
            .HasForeignKey(line => line.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(purchase => purchase.Payments)
            .WithOne(payment => payment.Purchase)
            .HasForeignKey(payment => payment.PurchaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(purchase => purchase.SupplierId);
        builder.HasIndex(purchase => purchase.PurchaseDate);
        builder.HasIndex(purchase => purchase.Currency);
        builder.HasIndex(purchase => purchase.PaymentType);
        builder.HasIndex(purchase => purchase.DueDate);
        builder.HasIndex(purchase => purchase.CreatedByUserId);
    }
}