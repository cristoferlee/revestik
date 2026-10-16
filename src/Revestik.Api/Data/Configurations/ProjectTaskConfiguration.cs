using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable("ProjectTasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.AssignedToUserId).HasMaxLength(450);
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.UpdatedByUserId).HasMaxLength(450);
        builder.Property(x => x.CompletedByUserId).HasMaxLength(450);
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectId, x.IsCompleted, x.DueOn });
        builder.HasIndex(x => x.AssignedToUserId);
        builder.ToTable(t => t.HasCheckConstraint("CK_ProjectTasks_Completion",
            "([IsCompleted] = 0 AND [CompletedAtUtc] IS NULL AND [CompletedByUserId] IS NULL) OR " +
            "([IsCompleted] = 1 AND [CompletedAtUtc] IS NOT NULL AND [CompletedByUserId] IS NOT NULL)"));
    }
}
