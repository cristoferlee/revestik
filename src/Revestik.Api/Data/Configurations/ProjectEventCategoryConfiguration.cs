using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectEventCategoryConfiguration : IEntityTypeConfiguration<ProjectEventCategory>
{
    public void Configure(EntityTypeBuilder<ProjectEventCategory> builder)
    {
        builder.ToTable("ProjectEventCategories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.NormalizedName).IsUnique();
    }
}
