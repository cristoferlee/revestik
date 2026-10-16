using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectDocumentEventLinkConfiguration : IEntityTypeConfiguration<ProjectDocumentEventLink>
{
    public void Configure(EntityTypeBuilder<ProjectDocumentEventLink> builder)
    {
        builder.ToTable("ProjectDocumentEventLinks");
        builder.HasKey(x => new { x.ProjectDocumentId, x.ProjectEventId });
        builder.Property(x => x.LinkedByUserId).HasMaxLength(450).IsRequired();
        // One shared ProjectId must match BOTH the document and event.
        builder.HasOne(x => x.Document).WithMany(x => x.EventLinks)
            .HasForeignKey(x => new { x.ProjectDocumentId, x.ProjectId })
            .HasPrincipalKey(x => new { x.Id, x.ProjectId })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Event).WithMany()
            .HasForeignKey(x => new { x.ProjectEventId, x.ProjectId })
            .HasPrincipalKey(x => new { x.Id, x.ProjectId })
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.ProjectEventId);
    }
}
