using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectMilestoneTemplateConfiguration : IEntityTypeConfiguration<ProjectMilestoneTemplate>
{
    public void Configure(EntityTypeBuilder<ProjectMilestoneTemplate> builder)
    {
        builder.ToTable("ProjectMilestoneTemplates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(x => x.NormalizedName).IsUnique();
        builder.HasMany(x => x.Items).WithOne(x => x.Template)
            .HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}
