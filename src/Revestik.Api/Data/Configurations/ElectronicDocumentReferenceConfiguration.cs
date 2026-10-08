using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ElectronicDocumentReferenceConfiguration
    : IEntityTypeConfiguration<ElectronicDocumentReference>
{
    public void Configure(EntityTypeBuilder<ElectronicDocumentReference> builder)
    {
        builder.ToTable("ElectronicDocumentReferences");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReferencedDocumentTypeCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.ReferenceNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ReferencedIssueDate).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(x => x.ReferenceCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();

        builder.HasOne(x => x.ElectronicDocument)
            .WithMany(x => x.References)
            .HasForeignKey(x => x.ElectronicDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RelatedElectronicDocument)
            .WithMany()
            .HasForeignKey(x => x.RelatedElectronicDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ElectronicDocumentId, x.Sequence }).IsUnique();
        builder.HasIndex(x => x.ReferenceNumber);
        builder.HasIndex(x => x.RelatedElectronicDocumentId);
    }
}
