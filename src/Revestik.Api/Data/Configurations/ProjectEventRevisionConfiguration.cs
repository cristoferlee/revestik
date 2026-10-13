using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectEventRevisionConfiguration : IEntityTypeConfiguration<ProjectEventRevision>
{
    public void Configure(EntityTypeBuilder<ProjectEventRevision> builder)
    {
        builder.ToTable("ProjectEventRevisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.PreviousVoidReason).HasMaxLength(500);
        builder.Property(x => x.ChangedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.ChangeKind).HasMaxLength(20).IsRequired();
        builder.HasOne(x => x.ProjectEvent).WithMany(x => x.Revisions)
            .HasForeignKey(x => x.ProjectEventId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectEventId, x.PreviousVersion }).IsUnique();
    }
}
