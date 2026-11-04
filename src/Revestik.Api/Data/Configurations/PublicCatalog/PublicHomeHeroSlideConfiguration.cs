using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models.PublicCatalog;

namespace Revestik.Api.Data.Configurations.PublicCatalog;

public sealed class PublicHomeHeroSlideConfiguration : IEntityTypeConfiguration<PublicHomeHeroSlide>
{
    public void Configure(EntityTypeBuilder<PublicHomeHeroSlide> builder)
    {
        builder.ToTable("PublicHomeHeroSlides", t =>
        {
            t.HasCheckConstraint("CK_PublicHomeHeroSlides_Slot", "[Slot] BETWEEN 1 AND 3");
            t.HasCheckConstraint("CK_PublicHomeHeroSlides_Image", "([ImageStorageKey] IS NULL AND [ImageContentType] IS NULL) OR ([ImageStorageKey] IS NOT NULL AND [ImageContentType] IN ('image/jpeg','image/png','image/webp'))");
        });
        builder.HasKey(x => x.Slot);
        builder.Property(x => x.Slot).ValueGeneratedNever();
        builder.Property(x => x.HeadingEs).HasMaxLength(180).IsRequired();
        builder.Property(x => x.HeadingEn).HasMaxLength(180);
        builder.Property(x => x.HeadingDe).HasMaxLength(180);
        builder.Property(x => x.DescriptionEs).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.DescriptionEn).HasMaxLength(1000);
        builder.Property(x => x.DescriptionDe).HasMaxLength(1000);
        builder.Property(x => x.ImageStorageKey).HasMaxLength(64).IsUnicode(false);
        builder.Property(x => x.ImageContentType).HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
