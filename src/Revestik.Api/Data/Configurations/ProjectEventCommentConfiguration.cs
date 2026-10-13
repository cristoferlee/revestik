using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectEventCommentConfiguration : IEntityTypeConfiguration<ProjectEventComment>
{
    public void Configure(EntityTypeBuilder<ProjectEventComment> builder)
    {
        builder.ToTable("ProjectEventComments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.HasOne(x => x.ProjectEvent).WithMany().HasForeignKey(x => x.ProjectEventId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectEventId, x.CreatedAtUtc, x.Id });
    }
}
