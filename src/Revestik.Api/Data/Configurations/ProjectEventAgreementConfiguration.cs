using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectEventAgreementConfiguration : IEntityTypeConfiguration<ProjectEventAgreement>
{
    public void Configure(EntityTypeBuilder<ProjectEventAgreement> builder)
    {
        builder.ToTable("ProjectEventAgreements", t => t.HasCheckConstraint(
            "CK_ProjectEventAgreements_Status", "[Status] IN ('Pending', 'Fulfilled', 'Modified', 'Unfulfilled')"));
        builder.HasKey(x => x.ProjectEventId);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.AcceptedBy).HasMaxLength(150);
        builder.Property(x => x.AcceptanceMedium).HasMaxLength(80);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.UpdatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(1L);
        builder.HasOne(x => x.ProjectEvent).WithOne().HasForeignKey<ProjectEventAgreement>(x => x.ProjectEventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
