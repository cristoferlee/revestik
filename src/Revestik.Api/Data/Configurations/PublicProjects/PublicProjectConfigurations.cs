using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models.PublicProjects;

namespace Revestik.Api.Data.Configurations.PublicProjects;

public sealed class PublicProjectConfiguration : IEntityTypeConfiguration<PublicProject>
{
    public void Configure(EntityTypeBuilder<PublicProject> b)
    {
        b.ToTable("PublicProjects", t =>
        {
            t.HasCheckConstraint("CK_PublicProjects_DisplayOrder",
                "[DisplayOrder] IS NULL OR [DisplayOrder] >= 0");
            t.HasCheckConstraint("CK_PublicProjects_FeaturedOrder",
                "[FeaturedOrder] IS NULL OR [FeaturedOrder] BETWEEN 1 AND 3");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Slug).HasMaxLength(160).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.TypeCode).HasMaxLength(40).IsUnicode(false).IsRequired();
        b.Property(x => x.Location).HasMaxLength(240);
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        b.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.IsPublished, x.DisplayOrder, x.CreatedAtUtc });
        b.HasIndex(x => new { x.IsPublished, x.TypeCode });
        b.HasIndex(x => x.FeaturedOrder).IsUnique()
            .HasFilter("[FeaturedOrder] IS NOT NULL");
    }
}

public sealed class PublicProjectTranslationConfiguration : IEntityTypeConfiguration<PublicProjectTranslation>
{
    public void Configure(EntityTypeBuilder<PublicProjectTranslation> b)
    {
        b.ToTable("PublicProjectTranslations", t =>
            t.HasCheckConstraint("CK_PublicProjectTranslations_Language",
                "[LanguageCode] IN ('es','en','de')"));
        b.HasKey(x => new { x.ProjectId, x.LanguageCode });
        b.Property(x => x.LanguageCode).HasMaxLength(2).IsUnicode(false).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        b.Property(x => x.SeoTitle).HasMaxLength(180);
        b.Property(x => x.SeoDescription).HasMaxLength(320);
        b.HasOne(x => x.Project).WithMany(x => x.Translations)
            .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PublicProjectSpaceConfiguration : IEntityTypeConfiguration<PublicProjectSpace>
{
    public void Configure(EntityTypeBuilder<PublicProjectSpace> b)
    {
        b.ToTable("PublicProjectSpaces");
        b.HasKey(x => new { x.ProjectId, x.SpaceCode });
        b.Property(x => x.SpaceCode).HasMaxLength(48).IsUnicode(false).IsRequired();
        b.HasOne(x => x.Project).WithMany(x => x.Spaces)
            .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.SpaceCode, x.ProjectId });
    }
}

public sealed class PublicProjectImageConfiguration : IEntityTypeConfiguration<PublicProjectImage>
{
    public void Configure(EntityTypeBuilder<PublicProjectImage> b)
    {
        b.ToTable("PublicProjectImages", t =>
        {
            t.HasCheckConstraint("CK_PublicProjectImages_Role",
                "[Role] IN ('ProjectCover','ProjectGallery')");
            t.HasCheckConstraint("CK_PublicProjectImages_ContentType",
                "[ContentType] IN ('image/jpeg','image/png','image/webp')");
            t.HasCheckConstraint("CK_PublicProjectImages_FileSize",
                "[FileSizeBytes] > 0 AND [FileSizeBytes] <= 5242880");
            t.HasCheckConstraint("CK_PublicProjectImages_Dimensions",
                "[Width] BETWEEN 1 AND 8192 AND [Height] BETWEEN 1 AND 8192");
            t.HasCheckConstraint("CK_PublicProjectImages_DisplayOrder", "[DisplayOrder] >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Role).HasMaxLength(32).IsUnicode(false).IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(64).IsUnicode(false).IsRequired();
        b.HasIndex(x => x.StorageKey).IsUnique();
        b.Property(x => x.ContentType).HasMaxLength(40).IsUnicode(false).IsRequired();
        b.Property(x => x.AltEs).HasMaxLength(300);
        b.Property(x => x.AltEn).HasMaxLength(300);
        b.Property(x => x.AltDe).HasMaxLength(300);
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.Project).WithMany(x => x.Images)
            .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProjectId, x.Role }).IsUnique()
            .HasFilter("[Role] = 'ProjectCover'");
        b.HasIndex(x => new { x.ProjectId, x.DisplayOrder });
    }
}

public sealed class PublicProjectImageTagConfiguration : IEntityTypeConfiguration<PublicProjectImageTag>
{
    public void Configure(EntityTypeBuilder<PublicProjectImageTag> b)
    {
        b.ToTable("PublicProjectImageTags", t =>
        {
            t.HasCheckConstraint("CK_PublicProjectImageTags_Coordinates",
                "[AnchorX] BETWEEN 0 AND 1 AND [AnchorY] BETWEEN 0 AND 1 AND " +
                "[LabelX] BETWEEN 0 AND 1 AND [LabelY] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_PublicProjectImageTags_DisplayOrder", "[DisplayOrder] >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.AnchorX).HasPrecision(5, 4);
        b.Property(x => x.AnchorY).HasPrecision(5, 4);
        b.Property(x => x.LabelX).HasPrecision(5, 4);
        b.Property(x => x.LabelY).HasPrecision(5, 4);
        b.Property(x => x.CustomLabelEs).HasMaxLength(120);
        b.Property(x => x.CustomLabelEn).HasMaxLength(120);
        b.Property(x => x.CustomLabelDe).HasMaxLength(120);
        b.Property(x => x.ProductNameSnapshotEs).HasMaxLength(200).IsRequired();
        b.Property(x => x.ProductNameSnapshotEn).HasMaxLength(200);
        b.Property(x => x.ProductNameSnapshotDe).HasMaxLength(200);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.Image).WithMany(x => x.Tags)
            .HasForeignKey(x => x.ImageId).OnDelete(DeleteBehavior.Cascade);
        // A removed public catalog product never destroys the editorial hotspot.
        b.HasOne(x => x.Product).WithMany()
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.ImageId, x.DisplayOrder });
        b.HasIndex(x => x.ProductId);
    }
}
