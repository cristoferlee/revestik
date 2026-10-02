using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class SupplierConfiguration
    : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");

        builder.HasKey(supplier => supplier.Id);

        builder.Property(supplier => supplier.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(supplier => supplier.ContactName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(supplier => supplier.PhoneNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(supplier => supplier.Email)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(supplier => supplier.IsActive)
            .HasDefaultValue(true);

        builder.Property(supplier => supplier.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(supplier => supplier.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.HasIndex(supplier => supplier.Name)
            .IsUnique()
            .HasDatabaseName("UX_Suppliers_Name");
    }
}