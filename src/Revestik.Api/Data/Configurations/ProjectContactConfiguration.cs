using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ProjectContactConfiguration : IEntityTypeConfiguration<ProjectContact>
{
    public void Configure(EntityTypeBuilder<ProjectContact> builder)
    {
        builder.ToTable("ProjectContacts");
        builder.HasKey(contact => contact.Id);
        builder.Property(contact => contact.Name).HasMaxLength(150).IsRequired();
        builder.Property(contact => contact.Role).HasMaxLength(100).IsRequired();
        builder.Property(contact => contact.PhoneNumber).HasMaxLength(20);
        builder.Property(contact => contact.Email).HasMaxLength(254);
        builder.HasIndex(contact => contact.ProjectId);
    }
}
