using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class CustomerConfiguration
    : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(customer => customer.IdentificationType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(customer => customer.IdentificationNumber)
            .HasMaxLength(12);

        builder.Property(customer => customer.Email)
            .HasMaxLength(254);

        builder.Property(customer => customer.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(customer => customer.ProvinceCode)
            .HasMaxLength(1)
            .IsFixedLength();

        builder.Property(customer => customer.CantonCode)
            .HasMaxLength(2)
            .IsFixedLength();

        builder.Property(customer => customer.DistrictCode)
            .HasMaxLength(2)
            .IsFixedLength();

        builder.Property(customer => customer.OtherSigns)
            .HasMaxLength(160);

        builder.Property(customer => customer.IsActive)
            .HasDefaultValue(true);

        builder.Property(customer => customer.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(customer => customer.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.HasIndex(customer => customer.Name);

        builder.HasIndex(customer => customer.IdentificationNumber);
    }
}