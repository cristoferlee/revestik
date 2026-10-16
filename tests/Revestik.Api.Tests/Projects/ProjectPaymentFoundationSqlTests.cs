using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectPaymentFoundationSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private async Task<int> ProjectAsync()
    {
        await using var db = fixture.CreateDbContext();
        var userId = "b05a-" + Guid.NewGuid().ToString("N");
        db.Users.Add(new ApplicationUser { Id = userId, UserName = userId,
            NormalizedUserName = userId.ToUpperInvariant(), DisplayName = "B05 test",
            SecurityStamp = Guid.NewGuid().ToString() });
        var customer = new Customer { Name = "B05 Test", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9], Email = "b05@example.com",
            PhoneNumber = "88888888", ProvinceCode = "4", CantonCode = "01",
            DistrictCode = "01", OtherSigns = "Heredia", IsActive = true,
            CreatedAtUtc = DateTime.UtcNow };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var project = new Project { CustomerId = customer.Id, ResponsibleUserId = userId,
            Name = "B05 " + Guid.NewGuid().ToString("N"), NormalizedName = Guid.NewGuid().ToString("N").ToUpperInvariant(),
            AgreedAmount = 100m, CurrencyCode = "CRC" };
        db.Set<Project>().Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private static ProjectPayment Payment(int projectId, decimal amount = 10m) => new()
    {
        ProjectId = projectId, Amount = amount, CurrencyCode = "CRC",
        PaidOn = new DateOnly(2026, 10, 16), CreatedByUserId = "b05-test"
    };

    [Fact]
    public async Task Payment_Persists_WithGeneratedIdAndVersion()
    {
        var projectId = await ProjectAsync();
        await using var db = fixture.CreateDbContext();
        var payment = Payment(projectId);
        db.Set<ProjectPayment>().Add(payment);
        await db.SaveChangesAsync();
        Assert.True(payment.Id > 0);
        Assert.Equal(1, payment.Version);
        Assert.Equal(10m, (await db.Set<ProjectPayment>().AsNoTracking()
            .SingleAsync(x => x.Id == payment.Id)).Amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Payment_NonPositiveAmountRejected(decimal amount)
    {
        var projectId = await ProjectAsync();
        await using var db = fixture.CreateDbContext();
        db.Set<ProjectPayment>().Add(Payment(projectId, amount));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Payment_UnsupportedCurrencyRejected()
    {
        var projectId = await ProjectAsync();
        await using var db = fixture.CreateDbContext();
        var payment = Payment(projectId);
        payment.CurrencyCode = "EUR";
        db.Set<ProjectPayment>().Add(payment);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task PaymentDocumentLink_ForeignProjectRejected()
    {
        var firstId = await ProjectAsync();
        var secondId = await ProjectAsync();
        int paymentId, documentId;
        await using (var db = fixture.CreateDbContext())
        {
            var payment = Payment(firstId);
            var document = new ProjectDocument { ProjectId = secondId,
                DisplayName = "Comprobante", Category = "Receipts", CreatedByUserId = "b05-test" };
            db.Set<ProjectPayment>().Add(payment);
            db.Set<ProjectDocument>().Add(document);
            await db.SaveChangesAsync();
            paymentId = payment.Id;
            documentId = document.Id;
        }
        await using var check = fixture.CreateDbContext();
        check.Set<ProjectPaymentDocumentLink>().Add(new ProjectPaymentDocumentLink
        { ProjectId = firstId, ProjectPaymentId = paymentId, ProjectDocumentId = documentId,
          LinkedByUserId = "b05-test" });
        await Assert.ThrowsAsync<DbUpdateException>(() => check.SaveChangesAsync());
    }

    [Fact]
    public async Task PaymentRevision_DuplicatePreviousVersionRejected()
    {
        var projectId = await ProjectAsync();
        int paymentId;
        await using (var db = fixture.CreateDbContext())
        {
            var payment = Payment(projectId);
            db.Set<ProjectPayment>().Add(payment);
            await db.SaveChangesAsync();
            paymentId = payment.Id;
        }
        static ProjectPaymentRevision Revision(int id) => new()
        { ProjectPaymentId = id, PreviousVersion = 1, PreviousAmount = 10m,
          PreviousCurrencyCode = "CRC", PreviousPaidOn = new DateOnly(2026, 10, 16),
          ChangedByUserId = "b05-test", Reason = "Correction" };
        await using (var db = fixture.CreateDbContext())
        {
            db.Set<ProjectPaymentRevision>().Add(Revision(paymentId));
            await db.SaveChangesAsync();
        }
        await using var other = fixture.CreateDbContext();
        other.Set<ProjectPaymentRevision>().Add(Revision(paymentId));
        await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());
    }
}
