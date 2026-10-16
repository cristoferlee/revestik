using Microsoft.EntityFrameworkCore;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;

namespace Revestik.Api.Tests.Projects;

public sealed class ProjectDocumentSqlTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string Owner = "b04-sql-owner";

    private async Task<int> SeedProjectAsync()
    {
        await using var db = fixture.CreateDbContext();
        if (!await db.Users.AnyAsync(x => x.Id == Owner))
        {
            db.Users.Add(new ApplicationUser
            {
                Id = Owner, UserName = Owner, NormalizedUserName = Owner.ToUpperInvariant(),
                Email = "b04-sql-owner@example.com",
                NormalizedEmail = "B04-SQL-OWNER@EXAMPLE.COM",
                DisplayName = "Owner", SecurityStamp = Guid.NewGuid().ToString()
            });
        }

        var customer = new Customer
        {
            Name = "Cliente documentos",
            IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9],
            Email = "b04@example.com", PhoneNumber = "88888888",
            ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
            OtherSigns = "Heredia", IsActive = true, CreatedAtUtc = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var project = new Project
        {
            CustomerId = customer.Id, ResponsibleUserId = Owner,
            Name = "Proyecto " + suffix, NormalizedName = suffix.ToUpperInvariant()
        };
        db.Set<Project>().Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private static ProjectDocument NewDocument(int projectId) => new()
    {
        ProjectId = projectId,
        Category = "Photo",
        DisplayName = "Evidencia",
        CreatedByUserId = Owner
    };

    private static ProjectDocumentVersion NewVersion(int documentId, int number,
        string? storageKey = null) => new()
    {
        ProjectDocumentId = documentId,
        Number = number,
        OriginalFileName = "imagen.jpg",
        ContentType = "image/jpeg",
        SizeBytes = 42,
        Sha256Hex = new string('a', 64),
        StorageKey = storageKey ?? Guid.NewGuid().ToString("N"),
        State = "Ready",
        UploadedByUserId = Owner
    };

    private async Task<int> SeedDocumentAsync(int projectId)
    {
        await using var db = fixture.CreateDbContext();
        var document = NewDocument(projectId);
        db.Set<ProjectDocument>().Add(document);
        await db.SaveChangesAsync();
        return document.Id;
    }

    private async Task<int> SeedEventAsync(int projectId)
    {
        await using var db = fixture.CreateDbContext();
        var suffix = Guid.NewGuid().ToString("N");
        var category = new ProjectEventCategory
        {
            Name = "Documentos " + suffix,
            NormalizedName = ("DOCUMENTOS " + suffix).ToUpperInvariant()
        };
        db.Set<ProjectEventCategory>().Add(category);
        await db.SaveChangesAsync();
        var item = new ProjectEvent
        {
            ProjectId = projectId, CategoryId = category.Id,
            Title = "Visita", Description = "Evidencia",
            OccurredOn = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedByUserId = Owner, CreatedAtUtc = DateTime.UtcNow
        };
        db.Set<ProjectEvent>().Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    [Fact]
    public async Task DocumentAndFirstVersion_GenerateIdsAndPersist()
    {
        var projectId = await SeedProjectAsync();
        var documentId = await SeedDocumentAsync(projectId);
        Assert.True(documentId > 0);

        int versionId;
        await using (var db = fixture.CreateDbContext())
        {
            var version = NewVersion(documentId, 1);
            db.Set<ProjectDocumentVersion>().Add(version);
            await db.SaveChangesAsync();
            versionId = version.Id;
            Assert.True(versionId > 0);
        }
        await using (var db = fixture.CreateDbContext())
        {
            var document = await db.Set<ProjectDocument>().SingleAsync(x => x.Id == documentId);
            document.CurrentVersionId = versionId;
            await db.SaveChangesAsync();
        }
        await using var verify = fixture.CreateDbContext();
        var persisted = await verify.Set<ProjectDocument>().AsNoTracking()
            .SingleAsync(x => x.Id == documentId);
        Assert.Equal(versionId, persisted.CurrentVersionId);
        Assert.Equal(1, await verify.Set<ProjectDocumentVersion>()
            .CountAsync(x => x.ProjectDocumentId == documentId));
    }

    [Fact]
    public async Task CurrentVersion_MustBelongToSameDocument()
    {
        var projectId = await SeedProjectAsync();
        var firstDocument = await SeedDocumentAsync(projectId);
        var secondDocument = await SeedDocumentAsync(projectId);
        int versionId;
        await using (var db = fixture.CreateDbContext())
        {
            var version = NewVersion(firstDocument, 1);
            db.Set<ProjectDocumentVersion>().Add(version);
            await db.SaveChangesAsync();
            versionId = version.Id;
        }
        await using (var db = fixture.CreateDbContext())
        {
            var wrongDocument = await db.Set<ProjectDocument>()
                .SingleAsync(x => x.Id == secondDocument);
            wrongDocument.CurrentVersionId = versionId;
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using var verify = fixture.CreateDbContext();
        Assert.Null((await verify.Set<ProjectDocument>().AsNoTracking()
            .SingleAsync(x => x.Id == secondDocument)).CurrentVersionId);
    }

    [Fact]
    public async Task EventLink_MustBelongToSameProject()
    {
        var firstProject = await SeedProjectAsync();
        var secondProject = await SeedProjectAsync();
        var documentId = await SeedDocumentAsync(firstProject);
        var matchingEventId = await SeedEventAsync(firstProject);
        var foreignEventId = await SeedEventAsync(secondProject);

        await using (var db = fixture.CreateDbContext())
        {
            db.Set<ProjectDocumentEventLink>().Add(new ProjectDocumentEventLink
            {
                ProjectId = firstProject,
                ProjectDocumentId = documentId,
                ProjectEventId = matchingEventId,
                LinkedByUserId = Owner
            });
            await db.SaveChangesAsync();
        }
        await using (var db = fixture.CreateDbContext())
        {
            db.Set<ProjectDocumentEventLink>().Add(new ProjectDocumentEventLink
            {
                ProjectId = firstProject,
                ProjectDocumentId = documentId,
                ProjectEventId = foreignEventId,
                LinkedByUserId = Owner
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using var verify = fixture.CreateDbContext();
        var links = await verify.Set<ProjectDocumentEventLink>().AsNoTracking()
            .Where(x => x.ProjectDocumentId == documentId).ToListAsync();
        Assert.Single(links);
        Assert.Equal(matchingEventId, links[0].ProjectEventId);
    }

    [Fact]
    public async Task DuplicateVersionNumberAndStorageKey_AreRejected()
    {
        var projectId = await SeedProjectAsync();
        var documentId = await SeedDocumentAsync(projectId);
        var secondDocumentId = await SeedDocumentAsync(projectId);
        var sharedKey = Guid.NewGuid().ToString("N");
        await using (var db = fixture.CreateDbContext())
        {
            db.Set<ProjectDocumentVersion>().Add(NewVersion(documentId, 1, sharedKey));
            await db.SaveChangesAsync();
        }
        await using (var db = fixture.CreateDbContext())
        {
            db.Set<ProjectDocumentVersion>().Add(NewVersion(documentId, 1));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var db = fixture.CreateDbContext())
        {
            db.Set<ProjectDocumentVersion>().Add(NewVersion(secondDocumentId, 1, sharedKey));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using var verify = fixture.CreateDbContext();
        Assert.Equal(1, await verify.Set<ProjectDocumentVersion>().CountAsync(
            x => x.StorageKey == sharedKey));
    }

    [Fact]
    public async Task DeletingProject_RemovesDocumentVersionsAndLinks()
    {
        var projectId = await SeedProjectAsync();
        var documentId = await SeedDocumentAsync(projectId);
        var eventId = await SeedEventAsync(projectId);
        await using (var db = fixture.CreateDbContext())
        {
            db.Set<ProjectDocumentVersion>().Add(NewVersion(documentId, 1));
            db.Set<ProjectDocumentEventLink>().Add(new ProjectDocumentEventLink
            {
                ProjectId = projectId,
                ProjectDocumentId = documentId,
                ProjectEventId = eventId,
                LinkedByUserId = Owner
            });
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateDbContext())
        {
            var project = await db.Set<Project>().SingleAsync(x => x.Id == projectId);
            db.Set<Project>().Remove(project);
            await db.SaveChangesAsync();
        }

        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.Set<ProjectDocument>().AnyAsync(x => x.Id == documentId));
        Assert.False(await verify.Set<ProjectDocumentVersion>()
            .AnyAsync(x => x.ProjectDocumentId == documentId));
        Assert.False(await verify.Set<ProjectDocumentEventLink>()
            .AnyAsync(x => x.ProjectDocumentId == documentId));
    }
}
