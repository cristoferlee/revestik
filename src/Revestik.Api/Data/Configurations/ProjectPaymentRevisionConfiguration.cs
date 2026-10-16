using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectPaymentRevisionConfiguration : IEntityTypeConfiguration<ProjectPaymentRevision>
{
    public void Configure(EntityTypeBuilder<ProjectPaymentRevision> builder)
    {
        builder.ToTable("ProjectPaymentRevisions", table =>
        {
            table.HasCheckConstraint("CK_ProjectPaymentRevisions_PreviousAmount", "[PreviousAmount] > 0");
            table.HasCheckConstraint("CK_ProjectPaymentRevisions_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();
        builder.Property(x => x.PreviousAmount).HasPrecision(18, 2);
        builder.Property(x => x.PreviousCurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.PreviousReference).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PreviousNotes).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ChangedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ChangedAtUtc).HasColumnType("datetime2");
        builder.HasOne(x => x.ProjectPayment).WithMany(x => x.Revisions)
            .HasForeignKey(x => x.ProjectPaymentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ProjectPaymentId, x.PreviousVersion }).IsUnique();
    }
}
