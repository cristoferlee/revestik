using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class PurchaseLineConfiguration : IEntityTypeConfiguration<PurchaseLine>
{
    public void Configure(EntityTypeBuilder<PurchaseLine> builder)
    {
        builder.ToTable("PurchaseLines", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_PurchaseLines_Quantity", "[Quantity] > 0");
            tableBuilder.HasCheckConstraint("CK_PurchaseLines_UnitCost", "[UnitCost] > 0");
        });
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Quantity).HasPrecision(18, 4).IsRequired();
        builder.Property(line => line.UnitCost).HasPrecision(18, 5).IsRequired();
        builder.HasOne(line => line.Purchase).WithMany(purchase => purchase.Lines).HasForeignKey(line => line.PurchaseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(line => line.Product).WithMany().HasForeignKey(line => line.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(line => line.ProductId);
        builder.HasIndex(line => new { line.PurchaseId, line.ProductId }).IsUnique().HasDatabaseName("UX_PurchaseLines_PurchaseId_ProductId");
    }
}
