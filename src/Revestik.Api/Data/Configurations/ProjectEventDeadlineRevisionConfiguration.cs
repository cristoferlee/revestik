using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectEventDeadlineRevisionConfiguration : IEntityTypeConfiguration<ProjectEventDeadlineRevision>
{
    public void Configure(EntityTypeBuilder<ProjectEventDeadlineRevision> builder)
    {
        builder.ToTable("ProjectEventDeadlineRevisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ChangedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.HasOne(x => x.ProjectEvent).WithMany().HasForeignKey(x => x.ProjectEventId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectEventId, x.PreviousVersion }).IsUnique();
    }
}
