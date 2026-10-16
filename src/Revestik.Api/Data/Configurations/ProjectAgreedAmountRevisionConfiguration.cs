using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectAgreedAmountRevisionConfiguration : IEntityTypeConfiguration<ProjectAgreedAmountRevision>
{
    public void Configure(EntityTypeBuilder<ProjectAgreedAmountRevision> builder)
    {
        builder.ToTable("ProjectAgreedAmountRevisions", table =>
        {
            table.HasCheckConstraint("CK_ProjectAgreedAmountRevisions_PreviousAmount", "[PreviousAmount] IS NULL OR [PreviousAmount] >= 0");
            table.HasCheckConstraint("CK_ProjectAgreedAmountRevisions_NewAmount", "[NewAmount] IS NULL OR [NewAmount] >= 0");
            table.HasCheckConstraint("CK_ProjectAgreedAmountRevisions_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();
        builder.Property(x => x.PreviousAmount).HasPrecision(18, 2);
        builder.Property(x => x.NewAmount).HasPrecision(18, 2);
        builder.Property(x => x.PreviousCurrencyCode).HasMaxLength(3);
        builder.Property(x => x.NewCurrencyCode).HasMaxLength(3);
        builder.Property(x => x.ChangedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ChangedAtUtc).HasColumnType("datetime2");
        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectId, x.PreviousProjectVersion }).IsUnique();
    }
}
