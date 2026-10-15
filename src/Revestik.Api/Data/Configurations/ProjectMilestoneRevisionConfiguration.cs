using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectMilestoneRevisionConfiguration : IEntityTypeConfiguration<ProjectMilestoneRevision>
{
    public void Configure(EntityTypeBuilder<ProjectMilestoneRevision> builder)
    {
        builder.ToTable("ProjectMilestoneRevisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Weight).HasPrecision(5, 2);
        builder.Property(x => x.PreviousCompletedByUserId).HasMaxLength(450);
        builder.Property(x => x.ChangedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.ChangeKind).HasMaxLength(24).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => new { x.MilestoneId, x.PreviousVersion }).IsUnique();
    }
}
