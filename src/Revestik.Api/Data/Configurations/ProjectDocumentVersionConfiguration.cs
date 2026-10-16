using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectDocumentVersionConfiguration : IEntityTypeConfiguration<ProjectDocumentVersion>
{
    public void Configure(EntityTypeBuilder<ProjectDocumentVersion> builder)
    {
        builder.ToTable("ProjectDocumentVersions", table =>
        {
            table.HasCheckConstraint("CK_ProjectDocumentVersions_Number", "[Number] > 0");
            table.HasCheckConstraint("CK_ProjectDocumentVersions_SizeBytes", "[SizeBytes] >= 0");
            table.HasCheckConstraint("CK_ProjectDocumentVersions_State", "[State] IN ('Pending', 'Ready', 'Failed')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OriginalFileName).HasMaxLength(250).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Sha256Hex).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(160).IsUnicode(false).IsRequired();
        builder.Property(x => x.ThumbnailStorageKey).HasMaxLength(160).IsUnicode(false);
        builder.Property(x => x.State).HasMaxLength(16).IsUnicode(false).IsRequired();
        builder.Property(x => x.UploadedByUserId).HasMaxLength(450).IsRequired();
        builder.HasOne(x => x.Document).WithMany(x => x.Versions)
            .HasForeignKey(x => x.ProjectDocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasAlternateKey(x => new { x.Id, x.ProjectDocumentId });
        builder.HasIndex(x => new { x.ProjectDocumentId, x.Number }).IsUnique();
        builder.HasIndex(x => x.StorageKey).IsUnique();
    }
}
