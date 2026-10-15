using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectMilestoneTemplateItemConfiguration : IEntityTypeConfiguration<ProjectMilestoneTemplateItem>
{
    public void Configure(EntityTypeBuilder<ProjectMilestoneTemplateItem> builder)
    {
        builder.ToTable("ProjectMilestoneTemplateItems", table =>
            table.HasCheckConstraint("CK_ProjectMilestoneTemplateItems_Weight", "[Weight] > 0 AND [Weight] <= 100"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Weight).HasPrecision(5, 2);
        builder.HasIndex(x => new { x.TemplateId, x.SortOrder }).IsUnique();
    }
}
