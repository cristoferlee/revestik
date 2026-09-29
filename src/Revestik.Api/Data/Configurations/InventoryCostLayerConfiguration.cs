using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class InventoryCostLayerConfiguration
    : IEntityTypeConfiguration<InventoryCostLayer>
{
    public void Configure(EntityTypeBuilder<InventoryCostLayer> builder)
    {
        builder.ToTable("InventoryCostLayers", table =>
        {
            table.HasCheckConstraint(
                "CK_InventoryCostLayers_OriginalQuantity",
                "[OriginalQuantity] > 0");
            table.HasCheckConstraint(
                "CK_InventoryCostLayers_RemainingQuantity",
                "[RemainingQuantity] >= 0 AND [RemainingQuantity] <= [OriginalQuantity]");
            table.HasCheckConstraint(
                "CK_InventoryCostLayers_UnitCost",
                "[UnitCost] IS NULL OR [UnitCost] > 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.OriginalQuantity).HasPrecision(18, 4);
        builder.Property(x => x.RemainingQuantity).HasPrecision(18, 4);
        builder.Property(x => x.UnitCost).HasPrecision(18, 2);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SourceMovement)
            .WithOne()
            .HasForeignKey<InventoryCostLayer>(x => x.SourceMovementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => new { x.ProductId, x.CreatedAtUtc, x.Id });
        builder.HasIndex(x => x.SourceMovementId).IsUnique();
    }
}