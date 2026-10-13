using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectEventAgreementRevisionConfiguration : IEntityTypeConfiguration<ProjectEventAgreementRevision>
{
    public void Configure(EntityTypeBuilder<ProjectEventAgreementRevision> builder)
    {
        builder.ToTable("ProjectEventAgreementRevisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.AcceptedBy).HasMaxLength(150);
        builder.Property(x => x.AcceptanceMedium).HasMaxLength(80);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.ChangedByUserId).HasMaxLength(450).IsRequired();
        builder.HasOne(x => x.ProjectEvent).WithMany().HasForeignKey(x => x.ProjectEventId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectEventId, x.PreviousVersion }).IsUnique();
    }
}
