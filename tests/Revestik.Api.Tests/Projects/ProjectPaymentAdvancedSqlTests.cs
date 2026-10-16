using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Projects;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;
using Revestik.Shared.Projects;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectPaymentAdvancedSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string Owner = "b05d-sql-owner";

    private async Task<int> SeedProjectAsync(decimal amount = 100m)
    {
        await using var db = fixture.CreateDbContext();
        if (!await db.Users.AnyAsync(x => x.Id == Owner))
            db.Users.Add(new ApplicationUser
            {
                Id = Owner, UserName = Owner, NormalizedUserName = Owner.ToUpperInvariant(),
                Email = "b05d-sql-owner@example.com", NormalizedEmail = "B05D-SQL-OWNER@EXAMPLE.COM",
                DisplayName = Owner, SecurityStamp = Guid.NewGuid().ToString()
            });
        var customer = new Customer
        {
            Name = "B05D SQL", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9],
            Email = "b05d@example.com", PhoneNumber = "88888888",
            ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var name = Guid.NewGuid().ToString("N");
        var project = new Project
        {
            CustomerId = customer.Id, ResponsibleUserId = Owner,
            Name = name, NormalizedName = name.ToUpperInvariant(),
            AgreedAmount = amount, CurrencyCode = "CRC"
        };
        db.Set<Project>().Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private static ProjectPaymentUpsertRequest Payment(decimal amount, long? version = null, string? reason = null) =>
        new(amount, "CRC", new DateOnly(2026, 10, 16), "SINPE", "B05D", version, reason);

    [Fact]
    public async Task ConcurrentAbonos_CannotExceedAgreedAmount()
    {
        var projectId = await SeedProjectAsync();
        using var start = new ManualResetEventSlim(false);
        async Task<bool> AttemptAsync()
        {
            await using var db = fixture.CreateDbContext();
            await Task.Run(() => start.Wait(TimeSpan.FromSeconds(15)));
            try
            {
                await new ProjectPaymentService(db).CreateAsync(projectId, Payment(80), Owner, false, default);
                return true;
            }
            catch (ProjectOperationException e) when (e.Reason is ProjectFailureReason.Invalid or ProjectFailureReason.ConcurrencyConflict)
            {
                return false;
            }
        }
        var first = Task.Run(AttemptAsync);
        var second = Task.Run(AttemptAsync);
        start.Set();
        var results = await Task.WhenAll(first, second);
        Assert.Single(results, x => x);
        await using var verify = fixture.CreateDbContext();
        Assert.Equal(80m, await verify.Set<ProjectPayment>()
            .Where(x => x.ProjectId == projectId && x.Status == "Active")
            .SumAsync(x => x.Amount));
    }

    [Fact]
    public async Task Correction_RetainsPriorValuesAndRejectsStaleVersion()
    {
        var id = await SeedProjectAsync();
        int paymentId;
        await using (var db = fixture.CreateDbContext())
            paymentId = (await new ProjectPaymentService(db).CreateAsync(id, Payment(50), Owner, false, default)).Id;
        await using (var db = fixture.CreateDbContext())
        {
            var corrected = await new ProjectPaymentService(db).CorrectAsync(id, paymentId,
                Payment(40, 1, "Monto del recibo corregido"), Owner, false, default);
            Assert.Equal(2, corrected!.Version);
        }
        await using (var db = fixture.CreateDbContext())
        {
            var service = new ProjectPaymentService(db);
            var revision = Assert.Single((await service.HistoryAsync(id, paymentId, default))!);
            Assert.Equal(50m, revision.PreviousAmount);
            Assert.Equal(1, revision.PreviousVersion);
            Assert.Equal(Owner, revision.ChangedByUserId);
            await Assert.ThrowsAsync<ProjectOperationException>(() => service.CorrectAsync(id, paymentId,
                Payment(45, 1, "Cambio obsoleto"), Owner, false, default));
            Assert.Equal(60m, (await service.SummaryAsync(id, default))!.RemainingBalance);
        }
    }

    [Fact]
    public async Task AmountChange_AuditsPreviousValueAndRejectsCurrencyChangeAfterPayment()
    {
        var id = await SeedProjectAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectPaymentService(db);
        await service.CreateAsync(id, Payment(20), Owner, false, default);
        var summary = await service.ChangeAgreedAmountAsync(id,
            new ProjectAgreedAmountChangeRequest(120, "CRC", 1, "Cambio de alcance"), Owner, false, default);
        Assert.Equal(100m, summary.RemainingBalance);
        var revision = Assert.Single((await service.AmountHistoryAsync(id, default))!);
        Assert.Equal(100m, revision.PreviousAmount);
        Assert.Equal(120m, revision.NewAmount);
        Assert.Equal(Owner, revision.ChangedByUserId);
        await Assert.ThrowsAsync<ProjectOperationException>(() => service.ChangeAgreedAmountAsync(id,
            new ProjectAgreedAmountChangeRequest(120, "USD", 2, "Cambiar moneda"), Owner, false, default));
    }

    [Fact]
    public async Task ProjectDeletion_WithPaymentHistory_IsRejectedWithoutDeletingCustomer()
    {
        var id = await SeedProjectAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectPaymentService(db);
        await service.CreateAsync(id, Payment(10), Owner, false, default);
        await Assert.ThrowsAsync<ProjectOperationException>(() =>
            new ProjectService(db).DeleteAsync(id, Owner, false, 1, default));
        Assert.True(await db.Set<Project>().AnyAsync(x => x.Id == id));
        Assert.True(await db.Set<ProjectPayment>().AnyAsync(x => x.ProjectId == id));
    }

    [Fact]
    public async Task PaymentDocumentLinks_RejectCrossProjectAndPreserveDocument()
    {
        var firstId = await SeedProjectAsync();
        var secondId = await SeedProjectAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectPaymentService(db);
        var payment = await service.CreateAsync(firstId, Payment(10), Owner, false, default);
        var document = new ProjectDocument
        {
            ProjectId = secondId, Category = "Receipt", DisplayName = "Comprobante externo",
            CreatedByUserId = Owner
        };
        db.Set<ProjectDocument>().Add(document);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ProjectOperationException>(() =>
            service.LinkDocumentAsync(firstId, payment.Id, document.Id, Owner, false, default));
        Assert.False(await db.Set<ProjectPaymentDocumentLink>().AnyAsync(x => x.ProjectPaymentId == payment.Id));
        Assert.True(await db.Set<ProjectDocument>().AnyAsync(x => x.Id == document.Id));
    }
}
