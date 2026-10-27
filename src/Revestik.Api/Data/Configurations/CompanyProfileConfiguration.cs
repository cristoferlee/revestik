using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class CompanyProfileConfiguration : IEntityTypeConfiguration<CompanyProfile>
{
    public void Configure(EntityTypeBuilder<CompanyProfile> builder)
    {
        builder.ToTable("CompanyProfiles", table => table.HasCheckConstraint("CK_CompanyProfiles_Singleton", "[Id] = 1"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.LegalName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IdentificationNumber).HasMaxLength(32).IsRequired();
        builder.Property(x => x.TaxIdentificationType).HasMaxLength(8).IsRequired();
        builder.Property(x => x.TaxIdentificationNumber).HasMaxLength(32).IsRequired();
        builder.Property(x => x.CommercialName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(254).IsRequired();
        builder.Property(x => x.Website).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ProvinceCode).HasMaxLength(1).IsRequired();
        builder.Property(x => x.CantonCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.DistrictCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.OtherSigns).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(500).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedByUserId).HasMaxLength(450);
        builder.Property(x => x.LogoStorageKey).HasMaxLength(32);
        builder.Property(x => x.LogoContentType).HasMaxLength(32);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
