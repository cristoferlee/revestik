using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ElectronicDocumentClassificationRuleConfiguration
    : IEntityTypeConfiguration<ElectronicDocumentClassificationRule>
{
    public void Configure(EntityTypeBuilder<ElectronicDocumentClassificationRule> builder)
    {
        builder.ToTable("ElectronicDocumentClassificationRules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IssuerIdentificationType).HasMaxLength(2).IsRequired();
        builder.Property(x => x.IssuerIdentification).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.IssuerIdentificationType, x.IssuerIdentification })
            .IsUnique()
            .HasDatabaseName("UX_ElectronicDocumentClassificationRules_Issuer");
        builder.HasIndex(x => x.CategoryId);
    }
}
