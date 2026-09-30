using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class InventoryPhysicalCountConfiguration
    : IEntityTypeConfiguration<InventoryPhysicalCount>
{
    public void Configure(
        EntityTypeBuilder<InventoryPhysicalCount> builder)
    {
        builder.ToTable(
            "InventoryPhysicalCounts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_InventoryPhysicalCounts_Completion",
                    "([Status] = 'Draft' AND " +
                    "[CompletedAtUtc] IS NULL AND " +
                    "[CompletedByUserId] IS NULL) OR " +
                    "([Status] = 'Completed' AND " +
                    "[CompletedAtUtc] IS NOT NULL AND " +
                    "[CompletedByUserId] IS NOT NULL)");
            });

        builder.HasKey(count => count.Id);

        builder.Property(count => count.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(count => count.Notes)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(count => count.StartedByUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(count => count.StartedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(count => count.CompletedByUserId)
            .HasMaxLength(450);

        builder.Property(count => count.CompletedAtUtc)
            .HasColumnType("datetime2");

        builder.HasOne(count => count.StartedByUser)
            .WithMany()
            .HasForeignKey(count => count.StartedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(count => count.CompletedByUser)
            .WithMany()
            .HasForeignKey(count => count.CompletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(count => count.Lines)
            .WithOne(line => line.PhysicalCount)
            .HasForeignKey(line => line.PhysicalCountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(count => count.Status);
        builder.HasIndex(count => count.StartedAtUtc);
    }
}