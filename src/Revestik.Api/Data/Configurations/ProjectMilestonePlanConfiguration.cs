using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectMilestonePlanConfiguration : IEntityTypeConfiguration<ProjectMilestonePlan>
{
    public void Configure(EntityTypeBuilder<ProjectMilestonePlan> builder)
    {
        builder.ToTable("ProjectMilestonePlans");
        builder.HasKey(x => x.ProjectId);
        builder.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(1L);
        builder.HasOne(x => x.Project).WithOne()
            .HasForeignKey<ProjectMilestonePlan>(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Milestones).WithOne(x => x.Plan)
            .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
