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
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseLine> PurchaseLines => Set<PurchaseLine>();
    public DbSet<PurchasePayment> PurchasePayments => Set<PurchasePayment>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<BankVoucher> BankVouchers => Set<BankVoucher>();
    public DbSet<CabysCatalogVersion> CabysCatalogVersions => Set<CabysCatalogVersion>();
    public DbSet<CabysItem> CabysItems => Set<CabysItem>();
    public DbSet<ElectronicDocument> ElectronicDocuments => Set<ElectronicDocument>();
    public DbSet<ElectronicDocumentReference> ElectronicDocumentReferences => Set<ElectronicDocumentReference>();
    public DbSet<ElectronicDocumentLine> ElectronicDocumentLines => Set<ElectronicDocumentLine>();
    public DbSet<ElectronicDocumentLineDiscount> ElectronicDocumentLineDiscounts => Set<ElectronicDocumentLineDiscount>();
    public DbSet<ElectronicDocumentLineTax> ElectronicDocumentLineTaxes => Set<ElectronicDocumentLineTax>();
    public DbSet<HaciendaResponse> HaciendaResponses => Set<HaciendaResponse>();
    public DbSet<ElectronicDocumentCategory> ElectronicDocumentCategories => Set<ElectronicDocumentCategory>();
    public DbSet<ElectronicDocumentClassificationRule> ElectronicDocumentClassificationRules => Set<ElectronicDocumentClassificationRule>();
    public DbSet<ElectronicDocumentCabysClassificationRule> ElectronicDocumentCabysClassificationRules => Set<ElectronicDocumentCabysClassificationRule>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<InventoryCostLayer> InventoryCostLayers => Set<InventoryCostLayer>();
    public DbSet<InventoryCostConsumption> InventoryCostConsumptions => Set<InventoryCostConsumption>();
    public DbSet<InventoryPhysicalCount> InventoryPhysicalCounts => Set<InventoryPhysicalCount>();
    public DbSet<InventoryPhysicalCountLine> InventoryPhysicalCountLines => Set<InventoryPhysicalCountLine>();
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
