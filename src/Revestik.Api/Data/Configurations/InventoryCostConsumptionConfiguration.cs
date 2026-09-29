using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class InventoryCostConsumptionConfiguration
    : IEntityTypeConfiguration<InventoryCostConsumption>
{
    public void Configure(EntityTypeBuilder<InventoryCostConsumption> builder)
    {
        builder.ToTable("InventoryCostConsumptions", table =>
        {
            table.HasCheckConstraint(
                "CK_InventoryCostConsumptions_Quantity",
                "[Quantity] > 0");
            table.HasCheckConstraint(
                "CK_InventoryCostConsumptions_UnitCostSnapshot",
                "[UnitCostSnapshot] IS NULL OR [UnitCostSnapshot] > 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.UnitCostSnapshot).HasPrecision(18, 2);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");

        builder.HasOne(x => x.InventoryMovement)
            .WithMany()
            .HasForeignKey(x => x.InventoryMovementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.InventoryCostLayer)
            .WithMany()
            .HasForeignKey(x => x.InventoryCostLayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.InventoryMovementId);
        builder.HasIndex(x => x.InventoryCostLayerId);
        builder.HasIndex(x => new { x.InventoryMovementId, x.InventoryCostLayerId })
            .IsUnique();
    }
}