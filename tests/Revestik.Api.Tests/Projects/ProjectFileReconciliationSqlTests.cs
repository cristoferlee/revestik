using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Projects.Documents;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectFileReconciliationSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string Owner = "b04-reconcile-owner";

    [Fact]
    public async Task ReferencedFileAndThumbnail_AreNeverDeleted()
    {
        await WithStorageAsync(async (db, storage, root) =>
        {
            var projectId = await SeedProjectAsync(db);
            var document = new ProjectDocument
            {
                ProjectId = projectId, Category = "Photo",
                DisplayName = "Fotografía", CreatedByUserId = Owner
            };
            db.Set<ProjectDocument>().Add(document);
            await db.SaveChangesAsync();

            var original = await SaveAsync(storage, "original");
            var thumbnail = await SaveAsync(storage, "thumbnail");
            Age(root, original);
            Age(root, thumbnail);
            db.Set<ProjectDocumentVersion>().Add(new ProjectDocumentVersion
            {
                ProjectDocumentId = document.Id, Number = 1,
                OriginalFileName = "foto.jpg", ContentType = "image/jpeg",
                SizeBytes = 8, Sha256Hex = new string('a', 64),
                StorageKey = original, ThumbnailStorageKey = thumbnail,
                State = "Ready", UploadedByUserId = Owner
            });
            await db.SaveChangesAsync();

            var result = await Reconciler(db, storage).ReconcileAsync(false, default);
            Assert.Equal(2, result.Examined);
            Assert.Equal(0, result.Unreferenced);
            Assert.Equal(0, result.Deleted);
            Assert.True(File.Exists(Path.Combine(root, original)));
            Assert.True(File.Exists(Path.Combine(root, thumbnail)));
        });
    }

    [Fact]
    public async Task RecentOrphan_IsExcludedFromInventoryAndCleanup()
    {
        await WithStorageAsync(async (db, storage, root) =>
        {
            var key = await SaveAsync(storage, "recent");
            var result = await Reconciler(db, storage).ReconcileAsync(false, default);
            Assert.Equal(0, result.Examined);
            Assert.Equal(0, result.Deleted);
            Assert.True(File.Exists(Path.Combine(root, key)));
        });
    }

    [Fact]
    public async Task OldOrphan_DryRunDoesNotDelete()
    {
        await WithStorageAsync(async (db, storage, root) =>
        {
            var key = await SaveAsync(storage, "orphan");
            Age(root, key);
            var result = await Reconciler(db, storage).ReconcileAsync(true, default);
            Assert.True(result.DryRun);
            Assert.Equal(1, result.Examined);
            Assert.Equal(1, result.Unreferenced);
            Assert.Equal(0, result.Deleted);
            Assert.True(File.Exists(Path.Combine(root, key)));
        });
    }

    [Fact]
    public async Task OldOrphan_RealReconciliationDeletesOnlyEligibleFile()
    {
        await WithStorageAsync(async (db, storage, root) =>
        {
            var old = await SaveAsync(storage, "old");
            var recent = await SaveAsync(storage, "recent");
            Age(root, old);
            var result = await Reconciler(db, storage).ReconcileAsync(false, default);
            Assert.False(result.DryRun);
            Assert.Equal(1, result.Examined);
            Assert.Equal(1, result.Unreferenced);
            Assert.Equal(1, result.Deleted);
            Assert.Equal(0, result.Failed);
            Assert.False(File.Exists(Path.Combine(root, old)));
            Assert.True(File.Exists(Path.Combine(root, recent)));
        });
    }

    [Fact]
    public async Task HistoricalVersion_AndCurrentVersionRemainProtected()
    {
        await WithStorageAsync(async (db, storage, root) =>
        {
            var projectId = await SeedProjectAsync(db);
            var document = new ProjectDocument
            {
                ProjectId = projectId, Category = "Plan",
                DisplayName = "Plano", CreatedByUserId = Owner
            };
            db.Set<ProjectDocument>().Add(document);
            await db.SaveChangesAsync();
            var historical = await SaveAsync(storage, "v1");
            var current = await SaveAsync(storage, "v2");
            var orphan = await SaveAsync(storage, "unused");
            foreach (var key in new[] { historical, current, orphan }) Age(root, key);
            var first = NewVersion(document.Id, 1, historical);
            var second = NewVersion(document.Id, 2, current);
            db.Set<ProjectDocumentVersion>().AddRange(first, second);
            await db.SaveChangesAsync();
            document.CurrentVersionId = second.Id;
            await db.SaveChangesAsync();

            var result = await Reconciler(db, storage).ReconcileAsync(false, default);
            Assert.Equal(3, result.Examined);
            Assert.Equal(1, result.Unreferenced);
            Assert.Equal(1, result.Deleted);
            Assert.True(File.Exists(Path.Combine(root, historical)));
            Assert.True(File.Exists(Path.Combine(root, current)));
            Assert.False(File.Exists(Path.Combine(root, orphan)));
        });
    }

    private static ProjectDocumentVersion NewVersion(int documentId, int number, string key) => new()
    {
        ProjectDocumentId = documentId, Number = number,
        OriginalFileName = "plano.pdf", ContentType = "application/pdf",
        SizeBytes = 2, Sha256Hex = new string('a', 64),
        StorageKey = key, State = "Ready", UploadedByUserId = Owner
    };

    private static ProjectFileReconciliationService Reconciler(
        Revestik.Api.Data.RevestikDbContext db, LocalProjectFileStorage storage) =>
        new(db, storage, storage);

    private async Task WithStorageAsync(Func<Revestik.Api.Data.RevestikDbContext,
        LocalProjectFileStorage, string, Task> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "revestik-b04-reconcile-" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalProjectFileStorage(
                Options.Create(new ProjectFileStorageOptions { RootPath = root }));
            await using var db = fixture.CreateDbContext();
            await body(db, storage, root);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<string> SaveAsync(LocalProjectFileStorage storage, string value)
    {
        await using var data = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(value));
        return await storage.SaveAsync(data, default);
    }

    private static void Age(string root, string key) =>
        File.SetLastWriteTimeUtc(Path.Combine(root, key), DateTime.UtcNow.AddDays(-2));

    private static async Task<int> SeedProjectAsync(Revestik.Api.Data.RevestikDbContext db)
    {
        if (!await db.Users.AnyAsync(x => x.Id == Owner))
        {
            db.Users.Add(new ApplicationUser
            {
                Id = Owner, UserName = Owner, NormalizedUserName = Owner.ToUpperInvariant(),
                Email = "b04-reconcile-owner@example.com",
                NormalizedEmail = "B04-RECONCILE-OWNER@EXAMPLE.COM",
                DisplayName = "Owner", SecurityStamp = Guid.NewGuid().ToString()
            });
        }
        var customer = new Customer
        {
            Name = "Cliente conciliación",
            IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9],
            Email = "reconcile@example.com", PhoneNumber = "88888888",
            ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var unique = Guid.NewGuid().ToString("N");
        var project = new Project
        {
            CustomerId = customer.Id, ResponsibleUserId = Owner,
            Name = "Proyecto " + unique, NormalizedName = unique.ToUpperInvariant()
        };
        db.Set<Project>().Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }
}
