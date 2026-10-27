using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Revestik.Api.Configuration;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Company;
using Revestik.Shared.Quotations;
using SaleStatus = Revestik.Shared.Sales.SaleStatus;

namespace Revestik.Api.Tests.Company;

// Provider-level tests: EF InMemory and a private temp folder; no SQL container or HTTP host.
public sealed class CompanyPdfBrandingProviderTests
{
    private static readonly CompanyOptions Legacy = new()
    {
        LegalName = "Empresa Original", IdentificationNumber = "3101000000",
        Website = "original.test", Address = "Heredia"
    };

    [Fact]
    public async Task DraftQuotation_UsesCurrentProfile_NotLegacyIdentity()
    {
        await using var db = CreateDatabase();
        Seed(db);
        var provider = CreateProvider(db);
        var result = await provider.ForQuotationAsync(1, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("Empresa Actual", result.Identity.LegalName);
    }

    [Fact]
    public async Task IssuedQuotation_UsesHistoricalSnapshot_AfterProfileChanges()
    {
        await using var db = CreateDatabase();
        Seed(db);
        var provider = CreateProvider(db);
        var before = await provider.ForQuotationAsync(2, CancellationToken.None);
        Assert.NotNull(before);
        Assert.Equal("Empresa Historica", before.Identity.LegalName);
        var profile = await db.CompanyProfiles.SingleAsync();
        profile.LegalName = "Empresa Cambiada";
        await db.SaveChangesAsync();
        var after = await provider.ForQuotationAsync(2, CancellationToken.None);
        Assert.Equal("Empresa Historica", after!.Identity.LegalName);
        Assert.Equal(before.Identity, after.Identity);
    }

    [Fact]
    public async Task LegacyIssuedSale_UsesOriginalCompanyOptions()
    {
        await using var db = CreateDatabase();
        Seed(db);
        var result = await CreateProvider(db).ForSaleAsync(1, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("Empresa Original", result.Identity.LegalName);
        Assert.Null(result.Identity.LogoStorageKey);
    }

    [Fact]
    public async Task IssuedSale_UsesHistoricalIdentity_NotCurrentProfile()
    {
        await using var db = CreateDatabase();
        Seed(db);
        var result = await CreateProvider(db).ForSaleAsync(2, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("Empresa Historica", result.Identity.LegalName);
        Assert.NotEqual("Empresa Actual", result.Identity.LegalName);
    }

    [Fact]
    public async Task MissingHistoricalLogo_ThrowsInsteadOfUsingCurrentLogo()
    {
        await using var db = CreateDatabase();
        Seed(db);
        var historical = await db.Sales.SingleAsync(s => s.Id == 2);
        historical.CompanyIdentitySnapshotJson = CompanyDocumentIdentitySnapshotSerializer.Serialize(
            Historical with { LogoStorageKey = Guid.NewGuid().ToString("N"), LogoContentType = "image/png" });
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => CreateProvider(db).ForSaleAsync(2, CancellationToken.None));
        Assert.Contains("logotipo", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    public async Task InvalidHistoricalIdentity_UsesDistinctSnapshotException(string json)
    {
        await using var db = CreateDatabase();
        Seed(db);
        var sale = await db.Sales.SingleAsync(x => x.Id == 2);
        sale.CompanyIdentitySnapshotJson = json;
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<CompanyPdfSnapshotException>(
            () => CreateProvider(db).ForSaleAsync(2, CancellationToken.None));
        Assert.Contains("identidad", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<InvalidDataException>(exception.InnerException);
    }

    private static readonly CompanyDocumentIdentity Historical = new()
    {
        LegalName = "Empresa Historica", IdentificationNumber = "3101999999",
        CommercialName = "Marca Historica", Address = "San Jose", Website = "historica.test"
    };

    private static RevestikDbContext CreateDatabase() => new(
        new DbContextOptionsBuilder<RevestikDbContext>()
            .UseInMemoryDatabase("branding-" + Guid.NewGuid().ToString("N"))
            .Options);

    private static void Seed(RevestikDbContext db)
    {
        var user = new ApplicationUser { Id = "branding-user", UserName = "branding-user" };
        var customer = new Customer { Name = "Cliente", IdentificationNumber = "3101234567" };
        db.Users.Add(user);
        db.Customers.Add(customer);
        db.CompanyProfiles.Add(new CompanyProfile
        {
            Id = 1, LegalName = "Empresa Actual", IdentificationNumber = "3101111111",
            CommercialName = "Marca Actual", Address = "Heredia", RowVersion = []
        });
        db.Quotations.AddRange(
            new Quotation { Id = 1, QuotationNumber = "COT-1", Customer = customer, CreatedByUser = user, Status = QuotationStatus.Draft },
            new Quotation { Id = 2, QuotationNumber = "COT-2", Customer = customer, CreatedByUser = user,
                Status = QuotationStatus.Issued,
                CompanyIdentitySnapshotJson = CompanyDocumentIdentitySnapshotSerializer.Serialize(Historical) });
        db.Sales.AddRange(
            new Sale { Id = 1, SaleNumber = "VEN-1", Customer = customer, CreatedByUser = user, Status = SaleStatus.Issued },
            new Sale { Id = 2, SaleNumber = "VEN-2", Customer = customer, CreatedByUser = user, Status = SaleStatus.Issued,
                CompanyIdentitySnapshotJson = CompanyDocumentIdentitySnapshotSerializer.Serialize(Historical) });
        db.SaveChanges();
    }

    private static CompanyPdfBrandingProvider CreateProvider(RevestikDbContext db)
    {
        var env = new IsolatedHostEnvironment();
        return new CompanyPdfBrandingProvider(db, new CompanyLogoStorage(env), Options.Create(Legacy));
    }

    private sealed class IsolatedHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Revestik.Api.Tests";
        public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "revestik-branding-" + Guid.NewGuid().ToString("N"));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
