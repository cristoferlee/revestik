using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ElectronicDocumentConfiguration
    : IEntityTypeConfiguration<ElectronicDocument>
{
    public void Configure(EntityTypeBuilder<ElectronicDocument> builder)
    {
        builder.ToTable("ElectronicDocuments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Clave).HasMaxLength(50).IsFixedLength().IsRequired();
        builder.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.NumeroConsecutivo).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FechaEmision).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(x => x.IssuerEconomicActivityCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ReceiverEconomicActivityCode).HasMaxLength(20).IsRequired();

        builder.Property(x => x.IssuerName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IssuerCommercialName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IssuerIdentificationType).HasMaxLength(2).IsRequired();
        builder.Property(x => x.IssuerIdentification).HasMaxLength(20).IsRequired();
        builder.Property(x => x.IssuerPhoneNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.IssuerEmail).HasMaxLength(254).IsRequired();
        builder.Property(x => x.IssuerAddress).HasMaxLength(1000).IsRequired();

        builder.Property(x => x.ReceiverName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ReceiverIdentificationType).HasMaxLength(2).IsRequired();
        builder.Property(x => x.ReceiverIdentification).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SaleConditionCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.ExchangeRate).HasPrecision(18, 5);

        foreach (var property in new[]
        {
            nameof(ElectronicDocument.TotalTaxedServices), nameof(ElectronicDocument.TotalExemptServices),
            nameof(ElectronicDocument.TotalExoneratedServices), nameof(ElectronicDocument.TotalNonSubjectServices),
            nameof(ElectronicDocument.TotalTaxedGoods), nameof(ElectronicDocument.TotalExemptGoods),
            nameof(ElectronicDocument.TotalExoneratedGoods), nameof(ElectronicDocument.TotalNonSubjectGoods),
            nameof(ElectronicDocument.TotalTaxed), nameof(ElectronicDocument.TotalExempt),
            nameof(ElectronicDocument.TotalExonerated), nameof(ElectronicDocument.TotalNonSubject),
            nameof(ElectronicDocument.TotalSale), nameof(ElectronicDocument.TotalDiscounts),
            nameof(ElectronicDocument.TotalNetSale), nameof(ElectronicDocument.TotalTax),
            nameof(ElectronicDocument.TotalVatReturned), nameof(ElectronicDocument.TotalOtherCharges),
            nameof(ElectronicDocument.TotalDocument)
        })
        {
            builder.Property<decimal>(property).HasPrecision(18, 5);
        }

        builder.Property(x => x.ProcessingStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.OperationalDestination)
            .HasConversion<string>()
            .HasMaxLength(30);
        builder.Property(x => x.OriginalXml).HasColumnType("varbinary(max)").IsRequired();
        builder.Property(x => x.ImportedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.ImportedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(x => x.ProcessedByUserId).HasMaxLength(450);
        builder.Property(x => x.ProcessedAtUtc).HasColumnType("datetime2");

        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Purchase).WithMany().HasForeignKey(x => x.PurchaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ImportedByUser).WithMany().HasForeignKey(x => x.ImportedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProcessedByUser).WithMany().HasForeignKey(x => x.ProcessedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne(x => x.ElectronicDocument).HasForeignKey(x => x.ElectronicDocumentId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Clave).IsUnique().HasDatabaseName("UX_ElectronicDocuments_Clave");
        builder.HasIndex(x => x.NumeroConsecutivo);
        builder.HasIndex(x => x.FechaEmision);
        builder.HasIndex(x => x.IssuerIdentification);
        builder.HasIndex(x => x.DocumentType);
        builder.HasIndex(x => x.ProcessingStatus);
        builder.HasIndex(x => x.CategoryId);
        builder.HasIndex(x => x.SupplierId);
        builder.HasIndex(x => x.PurchaseId).IsUnique().HasFilter("[PurchaseId] IS NOT NULL");
    }
}
