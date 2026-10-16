using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;
namespace Revestik.Api.Data.Configurations;
public sealed class ProjectLifecycleChangeConfiguration : IEntityTypeConfiguration<ProjectLifecycleChange>
{
    public void Configure(EntityTypeBuilder<ProjectLifecycleChange> b)
    {
        b.ToTable("ProjectLifecycleChanges", x => x.HasCheckConstraint("CK_ProjectLifecycleChanges_Action", "[Action] IN ('Transition', 'Close', 'Reopen')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(24).IsRequired();
        b.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(24).IsRequired();
        b.Property(x => x.Action).HasMaxLength(16).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.ClosingObservations).HasMaxLength(2000);
        b.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.ChangedAtUtc).HasColumnType("datetime2");
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.PreviousProjectVersion }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.ChangedAtUtc });
        // Restrict closing evidence to documents belonging to the same project.
        // NoAction avoids additional cascade paths and protects physical deletion of referenced evidence.
        b.HasOne<ProjectDocument>()
            .WithMany()
            .HasForeignKey(x => new { x.EvidenceDocumentId, x.ProjectId })
            .HasPrincipalKey(x => new { x.Id, x.ProjectId })
            .OnDelete(DeleteBehavior.NoAction);
    }
}
