using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectTaskRevisionConfiguration : IEntityTypeConfiguration<ProjectTaskRevision>
{
    public void Configure(EntityTypeBuilder<ProjectTaskRevision> builder)
    {
        builder.ToTable("ProjectTaskRevisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.AssignedToUserId).HasMaxLength(450);
        builder.Property(x => x.PreviousCompletedByUserId).HasMaxLength(450);
        builder.Property(x => x.ChangedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.HasOne(x => x.ProjectTask).WithMany(x => x.Revisions).HasForeignKey(x => x.ProjectTaskId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectTaskId, x.PreviousVersion }).IsUnique();
    }
}
