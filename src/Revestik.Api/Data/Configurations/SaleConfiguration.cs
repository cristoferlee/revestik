using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");
        builder.HasKey(sale => sale.Id);
        builder.Property(sale => sale.SaleNumber).HasMaxLength(20);
        builder.HasIndex(sale => sale.SaleNumber).IsUnique().HasFilter("[SaleNumber] IS NOT NULL");
        builder.HasIndex(sale => sale.SourceQuotationId).IsUnique().HasFilter("[SourceQuotationId] IS NOT NULL");
        builder.HasIndex(sale => sale.ReplacesSaleId).IsUnique().HasFilter("[ReplacesSaleId] IS NOT NULL");
        builder.Property(sale => sale.CustomerNameSnapshot).HasMaxLength(150).IsRequired();
        builder.Property(sale => sale.CustomerIdentificationNumberSnapshot).HasMaxLength(12).IsRequired();
        builder.Property(sale => sale.CustomerEmailSnapshot).HasMaxLength(254).IsRequired();
        builder.Property(sale => sale.CustomerPhoneNumberSnapshot).HasMaxLength(20).IsRequired();
        builder.Property(sale => sale.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(sale => sale.IssuedByUserId).HasMaxLength(450);
        builder.Property(sale => sale.VoidedByUserId).HasMaxLength(450);
        builder.Property(sale => sale.Currency).HasConversion<string>().HasMaxLength(3).IsRequired();
        builder.Property(sale => sale.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(sale => sale.GeneralDiscountType).HasConversion<string>().HasMaxLength(20);
        builder.Property(sale => sale.GeneralDiscountValue).HasPrecision(18, 2).IsRequired();
        builder.Property(sale => sale.VoidReason).HasMaxLength(1000).IsRequired();
        builder.Property(sale => sale.Observations).HasMaxLength(2000).IsRequired();
        builder.HasOne(sale => sale.SourceQuotation).WithMany().HasForeignKey(sale => sale.SourceQuotationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(sale => sale.Customer).WithMany().HasForeignKey(sale => sale.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(sale => sale.CreatedByUser).WithMany().HasForeignKey(sale => sale.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(sale => sale.IssuedByUser).WithMany().HasForeignKey(sale => sale.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(sale => sale.VoidedByUser).WithMany().HasForeignKey(sale => sale.VoidedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(sale => sale.ReplacesSale).WithOne(sale => sale.ReplacementSale).HasForeignKey<Sale>(sale => sale.ReplacesSaleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(sale => sale.Lines).WithOne(line => line.Sale).HasForeignKey(line => line.SaleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(sale => sale.Charges).WithOne(charge => charge.Sale).HasForeignKey(charge => charge.SaleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(sale => sale.Payments).WithOne(payment => payment.Sale).HasForeignKey(payment => payment.SaleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(sale => sale.CustomerId);
        builder.HasIndex(sale => sale.Status);
        builder.HasIndex(sale => sale.IssuedAtUtc);
    }
}