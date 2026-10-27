using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class SecurityAuditEntryConfiguration : IEntityTypeConfiguration<SecurityAuditEntry>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEntry> builder)
    {
        builder.ToTable("SecurityAuditEntries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.OccurredAtUtc).IsRequired();
        builder.Property(x => x.ActorUserId).HasMaxLength(450);
        builder.Property(x => x.Action).HasMaxLength(80).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(450);
        builder.Property(x => x.Details).HasMaxLength(2000);
        builder.HasIndex(x => new { x.OccurredAtUtc, x.Id });
        builder.HasIndex(x => new { x.ActorUserId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAtUtc });
    }
}
