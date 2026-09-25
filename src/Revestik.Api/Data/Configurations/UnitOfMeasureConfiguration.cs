using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class UnitOfMeasureConfiguration
    : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.ToTable("UnitsOfMeasure");

        builder.HasKey(unit => unit.Id);

        builder.Property(unit => unit.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(unit => unit.Symbol)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(unit => unit.IsActive)
            .HasDefaultValue(true);

        builder.Property(unit => unit.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(unit => unit.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.HasIndex(unit => unit.Name)
            .IsUnique();

        builder.HasIndex(unit => unit.Symbol)
            .IsUnique();
    }
}