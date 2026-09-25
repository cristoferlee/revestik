using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;

namespace Revestik.Api.Data;

public sealed class RevestikDbContext(DbContextOptions<RevestikDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationLine> QuotationLines => Set<QuotationLine>();
    public DbSet<QuotationCharge> QuotationCharges => Set<QuotationCharge>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();
    public DbSet<SaleCharge> SaleCharges => Set<SaleCharge>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasSequence<long>("QuotationNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("SaleNumberSequence").StartsAt(1);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RevestikDbContext).Assembly);
    }
}