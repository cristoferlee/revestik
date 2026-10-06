using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ElectronicDocumentCabysClassificationRuleConfiguration
    : IEntityTypeConfiguration<ElectronicDocumentCabysClassificationRule>
{
    public void Configure(EntityTypeBuilder<ElectronicDocumentCabysClassificationRule> builder)
    {
        builder.ToTable("ElectronicDocumentCabysClassificationRules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RuleKey).HasMaxLength(180).IsRequired();
        builder.Property(x => x.IssuerIdentificationType).HasMaxLength(2).IsRequired();
        builder.Property(x => x.IssuerIdentification).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CabysCode).HasMaxLength(13);
        builder.Property(x => x.CabysCategory4Code).HasMaxLength(13);
        builder.Property(x => x.OperationalDestination)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.ConfirmationCount).HasDefaultValue(1);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RuleKey)
            .IsUnique()
            .HasDatabaseName("UX_ElectronicDocumentCabysClassificationRules_RuleKey");
        builder.HasIndex(x => x.CategoryId);
        builder.HasIndex(x => new
        {
            x.IssuerIdentificationType,
            x.IssuerIdentification,
            x.CabysCode
        });
        builder.HasIndex(x => new
        {
            x.IssuerIdentificationType,
            x.IssuerIdentification,
            x.CabysCategory4Code
        });
    }
}
