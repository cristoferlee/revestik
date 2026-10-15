using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectMilestoneConfiguration : IEntityTypeConfiguration<ProjectMilestone>
{
    public void Configure(EntityTypeBuilder<ProjectMilestone> builder)
    {
        builder.ToTable("ProjectMilestones", table =>
            table.HasCheckConstraint("CK_ProjectMilestones_Weight", "[Weight] > 0 AND [Weight] <= 100"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Weight).HasPrecision(5, 2);
        builder.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(1L);
        builder.Property(x => x.CompletedByUserId).HasMaxLength(450);
        builder.HasIndex(x => new { x.ProjectId, x.IsExcluded, x.SortOrder });
        builder.HasIndex(x => new { x.ProjectId, x.NormalizedName })
            .IsUnique()
            .HasFilter("[IsExcluded] = 0")
            .HasDatabaseName("UX_ProjectMilestones_Project_NormalizedName_Active");
        builder.HasMany(x => x.Revisions).WithOne(x => x.Milestone)
            .HasForeignKey(x => x.MilestoneId).OnDelete(DeleteBehavior.Cascade);
    }
}
