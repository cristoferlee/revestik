using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Projects.Documents;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectDocumentLifecycleSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    [Fact]
    public async Task DeletedProject_LeavesPhysicalFilesForExplicitReconciliation()
    {
        await WithStorageAsync(async (storage, root) =>
        {
            var (projectId, owner) = await SeedProjectAsync();
            string key;
            int documentId;
            await using (var db = fixture.CreateDbContext())
            {
                var service = new ProjectDocumentService(db, storage);
                await using var source = new MemoryStream([1, 2, 3, 4]);
                var snapshot = await service.CreateAsync(projectId, "Plano", "Plan", "plano.pdf",
                    "application/pdf", source, owner, false, default);
                documentId = snapshot.Id;
                key = await db.Set<ProjectDocumentVersion>().AsNoTracking()
                    .Where(x => x.ProjectDocumentId == documentId)
                    .Select(x => x.StorageKey).SingleAsync();
            }
            Assert.True(File.Exists(Path.Combine(root, key)));
            await using (var db = fixture.CreateDbContext())
            {
                // Explicitly load dependents to exercise the same relational constraints as production deletion.
                var project = await db.Set<Project>().SingleAsync(x => x.Id == projectId);
                db.Set<Project>().Remove(project);
                await db.SaveChangesAsync();
            }
            await using (var db = fixture.CreateDbContext())
            {
                Assert.False(await db.Set<ProjectDocument>().AnyAsync(x => x.Id == documentId));
                var reconciler = new ProjectFileReconciliationService(db, storage, storage);
                // The grace window prevents immediate deletion after a project is removed.
                var recent = await reconciler.ReconcileAsync(false, default);
                Assert.Equal(0, recent.Deleted);
                Assert.True(File.Exists(Path.Combine(root, key)));
                File.SetLastWriteTimeUtc(Path.Combine(root, key), DateTime.UtcNow.AddDays(-2));
                var dryRun = await reconciler.ReconcileAsync(true, default);
                Assert.Equal(1, dryRun.Unreferenced);
                Assert.Equal(0, dryRun.Deleted);
                Assert.True(File.Exists(Path.Combine(root, key)));
                var cleaned = await reconciler.ReconcileAsync(false, default);
                Assert.Equal(1, cleaned.Deleted);
                Assert.False(File.Exists(Path.Combine(root, key)));
            }
        });
    }

    [Fact]
    public async Task ActiveProject_ProtectsHistoricalVersionDuringReconciliation()
    {
        await WithStorageAsync(async (storage, root) =>
        {
            var (projectId, owner) = await SeedProjectAsync();
            await using (var db = fixture.CreateDbContext())
            {
                var service = new ProjectDocumentService(db, storage);
                await using var first = new MemoryStream([1, 2, 3]);
                var created = await service.CreateAsync(projectId, "Plano", "Plan", "v1.pdf",
                    "application/pdf", first, owner, false, default);
                await using var second = new MemoryStream([4, 5, 6]);
                await service.AddVersionAsync(projectId, created.Id, created.Version,
                    "v2.pdf", "application/pdf", second, owner, false, default);
            }
            await using (var db = fixture.CreateDbContext())
            {
                var versions = await db.Set<ProjectDocumentVersion>().AsNoTracking()
                    .Where(x => x.Document.ProjectId == projectId)
                    .OrderBy(x => x.Number).ToListAsync();
                Assert.Equal(2, versions.Count);
                foreach (var version in versions)
                    File.SetLastWriteTimeUtc(Path.Combine(root, version.StorageKey), DateTime.UtcNow.AddDays(-2));
                var result = await new ProjectFileReconciliationService(db, storage, storage)
                    .ReconcileAsync(false, default);
                Assert.Equal(0, result.Unreferenced);
                Assert.Equal(0, result.Deleted);
                Assert.All(versions, version => Assert.True(File.Exists(Path.Combine(root, version.StorageKey))));
            }
        });
    }

    private static async Task WithStorageAsync(Func<LocalProjectFileStorage, string, Task> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "revestik-b04-lifecycle-" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalProjectFileStorage(Options.Create(new ProjectFileStorageOptions { RootPath = root }));
            await action(storage, root);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private async Task<(int ProjectId, string Owner)> SeedProjectAsync()
    {
        const string owner = "b04-lifecycle-owner";
        await using var db = fixture.CreateDbContext();
        if (!await db.Users.AnyAsync(x => x.Id == owner))
        {
            db.Users.Add(new ApplicationUser
            {
                Id = owner, UserName = owner, NormalizedUserName = owner.ToUpperInvariant(),
                Email = "b04-lifecycle@example.com", NormalizedEmail = "B04-LIFECYCLE@EXAMPLE.COM",
                DisplayName = "Owner", SecurityStamp = Guid.NewGuid().ToString()
            });
        }
        var customer = new Customer
        {
            Name = "Cliente archivos", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9], Email = "b04@example.com",
            PhoneNumber = "88888888", ProvinceCode = "4", CantonCode = "01",
            DistrictCode = "01", OtherSigns = "Heredia", IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var unique = Guid.NewGuid().ToString("N");
        var project = new Project
        {
            CustomerId = customer.Id, ResponsibleUserId = owner,
            Name = "Proyecto " + unique, NormalizedName = unique.ToUpperInvariant()
        };
        db.Set<Project>().Add(project);
        await db.SaveChangesAsync();
        return (project.Id, owner);
    }
}
