using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;
using Revestik.Shared.Inventory;

namespace Revestik.Api.Data.Configurations;

public sealed class InventoryMovementConfiguration
    : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable(
            "InventoryMovements",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "CK_InventoryMovements_StockBefore",
                    "[StockBefore] >= 0");

                tableBuilder.HasCheckConstraint(
                    "CK_InventoryMovements_StockAfter",
                    "[StockAfter] >= 0");

                tableBuilder.HasCheckConstraint(
                    "CK_InventoryMovements_Balance",
                    "[StockAfter] = [StockBefore] + [QuantityChange]");

                tableBuilder.HasCheckConstraint(
                    "CK_InventoryMovements_InitialStock",
                    "[Type] <> 'InitialStock' OR " +
                    "([StockBefore] = 0 AND " +
                    "[QuantityChange] >= 0 AND " +
                    "[StockAfter] = [QuantityChange] AND " +
                    "[UnitCost] IS NOT NULL AND [UnitCost] > 0 AND " +
                    "[AdjustmentReason] IS NULL)");

                tableBuilder.HasCheckConstraint(
                    "CK_InventoryMovements_AdjustmentReason",
                    "[Type] NOT IN ('AdjustmentIncrease', 'AdjustmentDecrease') OR " +
                    "[AdjustmentReason] IS NOT NULL");

                tableBuilder.HasCheckConstraint(
                    "CK_InventoryMovements_AdjustmentIncrease",
                    "[Type] <> 'AdjustmentIncrease' OR " +
                    "([QuantityChange] > 0 AND [UnitCost] IS NULL)");

                tableBuilder.HasCheckConstraint(
                    "CK_InventoryMovements_AdjustmentDecrease",
                    "[Type] <> 'AdjustmentDecrease' OR " +
                    "([QuantityChange] < 0 AND [UnitCost] IS NULL)");
            });

        builder.HasKey(movement => movement.Id);

        builder.Property(movement => movement.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(movement => movement.QuantityChange)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(movement => movement.StockBefore)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(movement => movement.StockAfter)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(movement => movement.UnitCost)
            .HasPrecision(18, 2);

        builder.Property(movement => movement.AdjustmentReason)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(movement => movement.Notes)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(movement => movement.CreatedByUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(movement => movement.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.HasOne(movement => movement.Product)
            .WithMany()
            .HasForeignKey(movement => movement.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.Sale)
            .WithMany()
            .HasForeignKey(movement => movement.SaleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.PhysicalCount)
            .WithMany(count => count.Movements)
            .HasForeignKey(movement => movement.PhysicalCountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.ReversesInventoryMovement)
            .WithOne(movement => movement.ReversalInventoryMovement)
            .HasForeignKey<InventoryMovement>(
                movement => movement.ReversesInventoryMovementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.CreatedByUser)
            .WithMany()
            .HasForeignKey(movement => movement.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(movement => movement.ProductId);

        builder.HasIndex(movement => movement.SaleId);

        builder.HasIndex(movement => movement.PhysicalCountId);

        builder.HasIndex(
                movement => movement.ReversesInventoryMovementId)
            .HasDatabaseName(
                "UX_InventoryMovements_ReversesInventoryMovementId")
            .IsUnique()
            .HasFilter(
                "[ReversesInventoryMovementId] IS NOT NULL");

        builder.HasIndex(movement => movement.CreatedAtUtc);

        builder.HasIndex(movement => movement.Type);

        builder.HasIndex(movement => movement.AdjustmentReason);

        builder.HasIndex(movement => movement.ProductId)
            .HasDatabaseName(
                "UX_InventoryMovements_Product_InitialStock")
            .IsUnique()
            .HasFilter(
                $"[Type] = '{InventoryMovementType.InitialStock}'");
    }
}