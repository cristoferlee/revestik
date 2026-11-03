using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models.PublicCatalog;

namespace Revestik.Api.Data.Configurations.PublicCatalog;

public sealed class PublicCatalogImageConfiguration : IEntityTypeConfiguration<PublicCatalogImage>
{
    public void Configure(EntityTypeBuilder<PublicCatalogImage> builder)
    {
        builder.ToTable("PublicCatalogImages", table =>
        {
            table.HasCheckConstraint("CK_PublicCatalogImages_Owner",
                "([ProductId] IS NOT NULL AND [CategoryId] IS NULL) OR ([ProductId] IS NULL AND [CategoryId] IS NOT NULL)");
            table.HasCheckConstraint("CK_PublicCatalogImages_ValidRole",
                "([ProductId] IS NOT NULL AND [Role] IN ('ProductCover','ProductGallery')) OR " +
                "([CategoryId] IS NOT NULL AND [Role] IN ('CategoryHeroPrimary','CategoryHeroFallback','CategoryGallery'))");
            table.HasCheckConstraint("CK_PublicCatalogImages_FileSize",
                "[FileSizeBytes] > 0 AND [FileSizeBytes] <= 5242880");
            table.HasCheckConstraint("CK_PublicCatalogImages_Dimensions",
                "[Width] > 0 AND [Height] > 0 AND [Width] <= 8192 AND [Height] <= 8192");
            table.HasCheckConstraint("CK_PublicCatalogImages_DisplayOrder", "[DisplayOrder] >= 0");
            table.HasCheckConstraint("CK_PublicCatalogImages_ContentType",
                "[ContentType] IN ('image/jpeg','image/png','image/webp') OR " +
                "([ContentType] = 'image/gif' AND [Role] = 'CategoryHeroPrimary')");
            table.HasCheckConstraint("CK_PublicCatalogImages_GifFallback",
                "[ContentType] <> 'image/gif' OR [FallbackImageId] IS NOT NULL");
            table.HasCheckConstraint("CK_PublicCatalogImages_FallbackRole",
                "[FallbackImageId] IS NULL OR [Role] = 'CategoryHeroPrimary'");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Role).HasMaxLength(32).IsUnicode(false).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.Property(x => x.ContentType).HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.Property(x => x.AltEs).HasMaxLength(300);
        builder.Property(x => x.AltEn).HasMaxLength(300);
        builder.Property(x => x.AltDe).HasMaxLength(300);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Product).WithMany()
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Category).WithMany()
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FallbackImage).WithMany()
            .HasForeignKey(x => x.FallbackImageId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ProductId, x.Role })
            .IsUnique().HasFilter("[ProductId] IS NOT NULL AND [Role] = 'ProductCover'");
        builder.HasIndex(x => new { x.CategoryId, x.Role })
            .IsUnique().HasFilter("[CategoryId] IS NOT NULL AND [Role] IN ('CategoryHeroPrimary', 'CategoryHeroFallback')");
        builder.HasIndex(x => new { x.ProductId, x.DisplayOrder });
        builder.HasIndex(x => new { x.CategoryId, x.DisplayOrder });
    }
}
