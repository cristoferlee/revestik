using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ElectronicDocumentReferenceResolutionConfiguration
    : IEntityTypeConfiguration<ElectronicDocumentReferenceResolution>
{
    public void Configure(EntityTypeBuilder<ElectronicDocumentReferenceResolution> builder)
    {
        builder.ToTable("ElectronicDocumentReferenceResolutions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ResolutionType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Justification).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.CompatibilityReasons).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ResolvedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.ResolvedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.HasOne(x => x.Reference).WithMany()
            .HasForeignKey(x => x.ElectronicDocumentReferenceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OriginalDocument).WithMany()
            .HasForeignKey(x => x.OriginalDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResolvedByUser).WithMany()
            .HasForeignKey(x => x.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ElectronicDocumentReferenceId, x.ResolvedAtUtc });
        // Enforce at most one date-exception authorization for any one fiscal reference.
        builder.HasIndex(x => x.ElectronicDocumentReferenceId)
            .IsUnique()
            .HasDatabaseName("UX_ReferenceResolutions_ActiveDateException")
            .HasFilter("[ResolutionType] = 'DateExceptionAuthorized'");
    }
}
