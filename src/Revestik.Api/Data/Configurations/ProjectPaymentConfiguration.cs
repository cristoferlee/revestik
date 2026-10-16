using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectPaymentConfiguration : IEntityTypeConfiguration<ProjectPayment>
{
    public void Configure(EntityTypeBuilder<ProjectPayment> builder)
    {
        builder.ToTable("ProjectPayments", table =>
        {
            table.HasCheckConstraint("CK_ProjectPayments_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_ProjectPayments_Currency", "[CurrencyCode] IN ('CRC', 'USD')");
            table.HasCheckConstraint("CK_ProjectPayments_Status", "[Status] IN ('Active', 'Voided')");
            table.HasCheckConstraint("CK_ProjectPayments_VoidDetails", "([Status] = 'Active' AND [VoidedAtUtc] IS NULL AND [VoidedByUserId] IS NULL AND [VoidReason] IS NULL) OR ([Status] = 'Voided' AND [VoidedAtUtc] IS NOT NULL AND [VoidedByUserId] IS NOT NULL AND [VoidReason] IS NOT NULL AND LEN(LTRIM(RTRIM([VoidReason]))) > 0)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();
        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Version).HasDefaultValue(1L).IsConcurrencyToken();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.VoidedByUserId).HasMaxLength(450);
        builder.Property(x => x.VoidReason).HasMaxLength(500);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.VoidedAtUtc).HasColumnType("datetime2");
        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasAlternateKey(x => new { x.Id, x.ProjectId });
        builder.HasIndex(x => new { x.ProjectId, x.Status, x.PaidOn });
    }
}
