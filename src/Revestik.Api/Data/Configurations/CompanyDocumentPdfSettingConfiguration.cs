using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class CompanyDocumentPdfSettingConfiguration : IEntityTypeConfiguration<CompanyDocumentPdfSetting>
{
    public void Configure(EntityTypeBuilder<CompanyDocumentPdfSetting> builder)
    {
        builder.ToTable("CompanyDocumentPdfSettings", t => t.HasCheckConstraint("CK_CompanyDocumentPdfSettings_Id", "[Id] = 1"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FooterMessage).HasMaxLength(500).IsRequired();
        builder.Property(x => x.QuotationDefaultTerms).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
