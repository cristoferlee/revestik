using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models.PublicCatalog;

namespace Revestik.Api.Data.Configurations.PublicCatalog;

public sealed class PublicCartItemConfiguration : IEntityTypeConfiguration<PublicCartItem>
{
    public void Configure(EntityTypeBuilder<PublicCartItem> builder)
    {
        builder.ToTable("PublicCartItems", table =>
            table.HasCheckConstraint("CK_PublicCartItems_Quantity", "[Quantity] > 0 AND [Quantity] <= 10000"));
        builder.HasKey(item => new { item.UserId, item.ProductId });
        builder.Property(item => item.UserId).HasMaxLength(450).IsRequired();
        builder.Property(item => item.Quantity).HasPrecision(18, 3);
        builder.Property(item => item.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(item => item.UpdatedAtUtc).HasColumnType("datetime2");
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => item.ProductId);
        builder.HasOne(item => item.User).WithMany()
            .HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        // Deleting a public-only product also deletes its cart references atomically.
        builder.HasOne(item => item.Product).WithMany()
            .HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
