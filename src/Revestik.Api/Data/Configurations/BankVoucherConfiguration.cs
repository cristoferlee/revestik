using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class BankVoucherConfiguration
    : IEntityTypeConfiguration<BankVoucher>
{
    public void Configure(EntityTypeBuilder<BankVoucher> builder)
    {
        builder.ToTable(
            "BankVouchers",
            tableBuilder =>
                tableBuilder.HasCheckConstraint(
                    "CK_BankVouchers_Amount",
                    "[Amount] > 0"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Bank)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.MerchantName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(x => x.TransactionDate)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.CardBrand)
            .HasMaxLength(30);

        builder.Property(x => x.CardLastFour)
            .HasMaxLength(4);

        builder.Property(x => x.AuthorizationNumber)
            .HasMaxLength(50);

        builder.Property(x => x.ReferenceNumber)
            .HasMaxLength(100);

        builder.Property(x => x.GmailMessageId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.HasOne(x => x.MatchedElectronicDocument)
            .WithMany()
            .HasForeignKey(x => x.MatchedElectronicDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.GmailMessageId)
            .IsUnique()
            .HasDatabaseName("UX_BankVouchers_GmailMessageId");

        builder.HasIndex(x => x.ReferenceNumber);
        builder.HasIndex(x => x.TransactionDate);
        builder.HasIndex(x => x.MatchedElectronicDocumentId);

        builder.HasIndex(x => new { x.Status, x.TransactionDate })
            .HasDatabaseName("IX_BankVouchers_Status_TransactionDate");
    }
}
