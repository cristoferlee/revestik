using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects", table =>
        {
            table.HasCheckConstraint("CK_Projects_AgreedAmount", "[AgreedAmount] IS NULL OR [AgreedAmount] >= 0");
            table.HasCheckConstraint("CK_Projects_BudgetCurrency", "([AgreedAmount] IS NULL AND [CurrencyCode] IS NULL) OR ([AgreedAmount] IS NOT NULL AND [CurrencyCode] IN ('CRC', 'USD'))");
        });
        builder.HasKey(project => project.Id);
        builder.Property(project => project.Version).IsConcurrencyToken().HasDefaultValue(1L);
        builder.Property(project => project.Name).HasMaxLength(150).IsRequired();
        builder.Property(project => project.NormalizedName).HasMaxLength(150).IsRequired();
        builder.HasIndex(project => new { project.CustomerId, project.NormalizedName })
            .IsUnique().HasDatabaseName("UX_Projects_Customer_NormalizedName");
        builder.Property(project => project.Description).HasMaxLength(2000).IsRequired();
        builder.Property(project => project.ResponsibleUserId).HasMaxLength(450).IsRequired();
        builder.Property(project => project.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(project => project.ProvinceCode).HasMaxLength(1).IsFixedLength();
        builder.Property(project => project.CantonCode).HasMaxLength(2).IsFixedLength();
        builder.Property(project => project.DistrictCode).HasMaxLength(2).IsFixedLength();
        builder.Property(project => project.OtherSigns).HasMaxLength(160);
        builder.Property(project => project.AgreedAmount).HasPrecision(18, 2);
        builder.Property(project => project.CurrencyCode).HasMaxLength(3);
        builder.Property(project => project.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(project => project.UpdatedAtUtc).HasColumnType("datetime2");
        builder.HasOne(project => project.Customer).WithMany()
            .HasForeignKey(project => project.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(project => project.ResponsibleUser).WithMany()
            .HasForeignKey(project => project.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(project => project.Contacts).WithOne(contact => contact.Project)
            .HasForeignKey(contact => contact.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(project => project.ResponsibleUserId);
        builder.HasIndex(project => project.Status);
    }
}
