using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ElectronicDocumentCategoryConfiguration
    : IEntityTypeConfiguration<ElectronicDocumentCategory>
{
    public void Configure(EntityTypeBuilder<ElectronicDocumentCategory> builder)
    {
        builder.ToTable("ElectronicDocumentCategories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SystemKey).HasMaxLength(100);
        builder.Property(x => x.AccountingNature)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(Revestik.Shared.ElectronicDocuments.AccountingNature.Other)
            .HasSentinel((Revestik.Shared.ElectronicDocuments.AccountingNature)0)
            .IsRequired();
        builder.Property(x => x.DefaultOperationalDestination)
            .HasConversion<string>()
            .HasMaxLength(30);
        builder.Property(x => x.IsDefaultDestinationInitialized).HasDefaultValue(false);
        builder.Property(x => x.SortOrder).HasDefaultValue(0);
        builder.Property(x => x.IsSystemDefault).HasDefaultValue(false);
        builder.Property(x => x.AllowsAutomaticSuggestion).HasDefaultValue(true);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

        builder.HasIndex(x => x.Name)
            .IsUnique()
            .HasDatabaseName("UX_ElectronicDocumentCategories_Name");

        builder.HasIndex(x => x.SystemKey)
            .IsUnique()
            .HasFilter("[SystemKey] IS NOT NULL")
            .HasDatabaseName("UX_ElectronicDocumentCategories_SystemKey");

        builder.HasIndex(x => new { x.AccountingNature, x.SortOrder });
    }
}
