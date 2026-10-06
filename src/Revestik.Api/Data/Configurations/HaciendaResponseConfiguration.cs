using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class HaciendaResponseConfiguration
    : IEntityTypeConfiguration<HaciendaResponse>
{
    public void Configure(EntityTypeBuilder<HaciendaResponse> builder)
    {
        builder.ToTable("HaciendaResponses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Clave).HasMaxLength(50).IsFixedLength().IsRequired();
        builder.Property(x => x.IssuerName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IssuerIdentificationType).HasMaxLength(2).IsRequired();
        builder.Property(x => x.IssuerIdentification).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ReceiverName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ReceiverIdentificationType).HasMaxLength(2).IsRequired();
        builder.Property(x => x.ReceiverIdentification).HasMaxLength(20).IsRequired();
        builder.Property(x => x.MessageCode).HasMaxLength(10).IsRequired();
        builder.Property(x => x.MessageStatus).HasMaxLength(50).IsRequired();
        builder.Property(x => x.MessageDetail).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.TotalTax).HasPrecision(18, 5);
        builder.Property(x => x.TotalInvoice).HasPrecision(18, 5);
        builder.Property(x => x.OriginalXml).HasColumnType("varbinary(max)").IsRequired();
        builder.Property(x => x.ReceivedAtUtc).HasColumnType("datetime2").IsRequired();

        builder.HasOne(x => x.ElectronicDocument)
            .WithOne(x => x.HaciendaResponse)
            .HasForeignKey<HaciendaResponse>(x => x.ElectronicDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Clave).IsUnique().HasDatabaseName("UX_HaciendaResponses_Clave");
        builder.HasIndex(x => x.ElectronicDocumentId).IsUnique().HasFilter("[ElectronicDocumentId] IS NOT NULL");
    }
}
