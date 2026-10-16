using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectDocumentConfiguration : IEntityTypeConfiguration<ProjectDocument>
{
    public void Configure(EntityTypeBuilder<ProjectDocument> builder)
    {
        builder.ToTable("ProjectDocuments");
        builder.HasKey(x => x.Id);
        // A document is inserted before its versions; SQL Server generates its identity.
        builder.Property(x => x.Id).ValueGeneratedOnAdd().UseIdentityColumn();
        builder.Property(x => x.Category).HasMaxLength(40).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(250).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.DeletedByUserId).HasMaxLength(450);
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectId, x.DeletedAtUtc });
        // SQL-enforced same-document current version. NoAction avoids cascade cycles.
        builder.HasAlternateKey(x => new { x.Id, x.ProjectId });
        builder.HasOne<ProjectDocumentVersion>().WithMany()
            .HasForeignKey(x => new { x.CurrentVersionId, x.Id })
            .HasPrincipalKey(x => new { x.Id, x.ProjectDocumentId })
            .OnDelete(DeleteBehavior.NoAction);
    }
}
