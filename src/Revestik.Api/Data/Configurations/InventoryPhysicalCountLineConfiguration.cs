using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class InventoryPhysicalCountLineConfiguration
    : IEntityTypeConfiguration<InventoryPhysicalCountLine>
{
    public void Configure(
        EntityTypeBuilder<InventoryPhysicalCountLine> builder)
    {
        builder.ToTable(
            "InventoryPhysicalCountLines",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_InventoryPhysicalCountLines_ExpectedQuantity",
                    "[ExpectedQuantity] >= 0");

                table.HasCheckConstraint(
                    "CK_InventoryPhysicalCountLines_CountedQuantity",
                    "[CountedQuantity] IS NULL OR [CountedQuantity] >= 0");
            });

        builder.HasKey(line => line.Id);

        builder.Property(line => line.ExpectedQuantity)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(line => line.CountedQuantity)
            .HasPrecision(18, 4);

        builder.HasOne(line => line.Product)
            .WithMany()
            .HasForeignKey(line => line.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(line => line.ProductId);

        builder.HasIndex(
                line => new
                {
                    line.PhysicalCountId,
                    line.ProductId
                })
            .IsUnique();
    }
}