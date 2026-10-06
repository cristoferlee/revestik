using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class CabysCatalogVersionConfiguration
    : IEntityTypeConfiguration<CabysCatalogVersion>
{
    public void Configure(EntityTypeBuilder<CabysCatalogVersion> builder)
    {
        builder.ToTable("CabysCatalogVersions");
        builder.HasKey(x => x.Version);

        builder.Property(x => x.Version)
            .HasMaxLength(20);

        builder.Property(x => x.SourceFileName)
            .HasMaxLength(200);

        builder.Property(x => x.SourceSha256)
            .HasMaxLength(64)
            .IsFixedLength();

        builder.HasIndex(x => x.IsCurrent)
            .IsUnique()
            .HasFilter("[IsCurrent] = 1")
            .HasDatabaseName("UX_CabysCatalogVersions_Current");
    }
}
