using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Revestik.Api.Models;

namespace Revestik.Api.Data.Configurations;

public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable(
            "Expenses",
            tableBuilder =>
                tableBuilder.HasCheckConstraint(
                    "CK_Expenses_TotalAmount",
                    "[TotalAmount] > 0"));

        builder.HasKey(expense => expense.Id);

        builder.Property(expense => expense.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(expense => expense.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(expense => expense.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(expense => expense.ExpenseDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(expense => expense.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasIndex(expense => expense.ExpenseDate);
        builder.HasIndex(expense => expense.Name);
    }
}
