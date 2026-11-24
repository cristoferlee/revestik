using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models.PublicCatalog;

namespace Revestik.Api.Data.Configurations.PublicCatalog;

public sealed class PublicSolutionsSectionConfiguration : IEntityTypeConfiguration<PublicSolutionsSection>
{
    public void Configure(EntityTypeBuilder<PublicSolutionsSection> b)
    {
        b.ToTable("PublicSolutionsSections", t =>
        {
            t.HasCheckConstraint("CK_PublicSolutionsSections_Key",
                "[Key] IN ('hero','approach','applications','interior-flooring','exterior-flooring','walls-facades','cta')");
            // An uploaded photo is allowed only on the page's own hero row.
            t.HasCheckConstraint("CK_PublicSolutionsSections_HeroImage",
                "([ImageStorageKey] IS NULL AND [ImageContentType] IS NULL) OR " +
                "([Key] = 'hero' AND [ImageStorageKey] IS NOT NULL AND " +
                "[ImageContentType] IN ('image/jpeg','image/png','image/webp'))");
        });
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(32).IsUnicode(false).ValueGeneratedNever();
        b.Property(x => x.ContentEs).HasMaxLength(6000).IsRequired();
        b.Property(x => x.ContentEn).HasMaxLength(6000);
        b.Property(x => x.ContentDe).HasMaxLength(6000);
        b.Property(x => x.ImageStorageKey).HasMaxLength(64).IsUnicode(false);
        b.Property(x => x.ImageContentType).HasMaxLength(40).IsUnicode(false);
        b.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
