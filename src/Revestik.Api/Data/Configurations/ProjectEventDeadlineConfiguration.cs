using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectEventDeadlineConfiguration : IEntityTypeConfiguration<ProjectEventDeadline>
{
    public void Configure(EntityTypeBuilder<ProjectEventDeadline> builder)
    {
        builder.ToTable("ProjectEventDeadlines");
        builder.HasKey(x => x.ProjectEventId);
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.Property(x => x.ChangedByUserId).HasMaxLength(450).IsRequired();
        builder.HasOne(x => x.ProjectEvent).WithOne().HasForeignKey<ProjectEventDeadline>(x => x.ProjectEventId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.DueOn);
    }
}
