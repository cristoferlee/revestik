using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectPaymentDocumentLinkConfiguration : IEntityTypeConfiguration<ProjectPaymentDocumentLink>
{
    public void Configure(EntityTypeBuilder<ProjectPaymentDocumentLink> builder)
    {
        builder.ToTable("ProjectPaymentDocumentLinks");
        builder.HasKey(x => new { x.ProjectPaymentId, x.ProjectDocumentId });
        builder.Property(x => x.LinkedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.LinkedAtUtc).HasColumnType("datetime2");
        builder.HasOne(x => x.Payment).WithMany(x => x.DocumentLinks)
            .HasForeignKey(x => new { x.ProjectPaymentId, x.ProjectId })
            .HasPrincipalKey(x => new { x.Id, x.ProjectId })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Document).WithMany()
            .HasForeignKey(x => new { x.ProjectDocumentId, x.ProjectId })
            .HasPrincipalKey(x => new { x.Id, x.ProjectId })
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.ProjectDocumentId);
    }
}
