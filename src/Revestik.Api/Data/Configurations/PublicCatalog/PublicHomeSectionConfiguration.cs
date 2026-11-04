using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models.PublicCatalog;

namespace Revestik.Api.Data.Configurations.PublicCatalog;

public sealed class PublicHomeSectionConfiguration : IEntityTypeConfiguration<PublicHomeSection>
{
    public void Configure(EntityTypeBuilder<PublicHomeSection> b)
    {
        b.ToTable("PublicHomeSections", t =>
        {
            t.HasCheckConstraint("CK_PublicHomeSections_Key", "[Key] IN ('solutions','advantages','contact','products')");
            t.HasCheckConstraint("CK_PublicHomeSections_Image", "([ImageStorageKey] IS NULL AND [ImageContentType] IS NULL) OR ([Key] = 'solutions' AND [ImageStorageKey] IS NOT NULL AND [ImageContentType] IN ('image/jpeg','image/png','image/webp'))");
        });
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(24).IsUnicode(false).ValueGeneratedNever();
        b.Property(x => x.ContentEs).HasMaxLength(6000).IsRequired();
        b.Property(x => x.ContentEn).HasMaxLength(6000);
        b.Property(x => x.ContentDe).HasMaxLength(6000);
        b.Property(x => x.FeaturedSlugsJson).HasMaxLength(1000).IsRequired();
        b.Property(x => x.ImageStorageKey).HasMaxLength(64).IsUnicode(false);
        b.Property(x => x.ImageContentType).HasMaxLength(40).IsUnicode(false);
        b.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
