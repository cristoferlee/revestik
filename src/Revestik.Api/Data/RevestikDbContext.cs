using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;

namespace Revestik.Api.Data;

public sealed class RevestikDbContext(
    DbContextOptions<RevestikDbContext> options)
    : IdentityDbContext<
        ApplicationUser,
        IdentityRole,
        string>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Quotation> Quotations => Set<Quotation>();

    public DbSet<QuotationLine> QuotationLines => Set<QuotationLine>();

    public DbSet<QuotationCharge> QuotationCharges => Set<QuotationCharge>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasSequence<long>("QuotationNumberSequence")
            .StartsAt(1);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RevestikDbContext).Assembly);
    }
}