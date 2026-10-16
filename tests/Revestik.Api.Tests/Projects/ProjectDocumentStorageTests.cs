using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Projects.Documents;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.Customers;
using Revestik.Shared.Projects;
using Revestik.Api.Services.Projects;
namespace Revestik.Api.Tests.Projects;
public sealed class ProjectDocumentStorageTests(SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    private const string Owner = "b04-storage-owner";
    [Fact]
    public async Task LocalStorage_RoundTripAndDeletion_PreservesBytes()
    {
        var root = NewRoot();
        try
        {
            var storage = LocalStorage(root);
            var bytes = RandomNumberGenerator.GetBytes(4096);
            await using var input = new MemoryStream(bytes);
            var key = await storage.SaveAsync(input, default);
            Assert.Equal(32, key.Length);
            await using (var stream = await storage.OpenReadAsync(key, default))
            {
                using var read = new MemoryStream();
                await stream.CopyToAsync(read);
                Assert.Equal(bytes, read.ToArray());
            }
            await storage.DeleteAsync(key, default);
            await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            {
                await using var missing = await storage.OpenReadAsync(key, default);
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
    [Fact]
    public async Task LocalStorage_RejectsTraversalAndAbsolutePaths()
    {
        var root = NewRoot();
        try
        {
            var storage = LocalStorage(root);
            await Assert.ThrowsAsync<ArgumentException>(() => storage.OpenReadAsync("../outside", default));
            await Assert.ThrowsAsync<ArgumentException>(() => storage.DeleteAsync("../outside", default));
            await Assert.ThrowsAsync<ArgumentException>(() => storage.OpenReadAsync("C:\\Windows\\win.ini", default));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
    [Fact]
    public async Task LocalStorage_FailedRead_CleansPartialFile()
    {
        var root = NewRoot();
        try
        {
            var storage = LocalStorage(root);
            await using var failingStream = new ThrowAfterReadStream();
            await Assert.ThrowsAsync<IOException>(() => storage.SaveAsync(failingStream, default));
            Assert.Empty(Directory.GetFiles(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
    [Fact]
    public async Task DocumentService_CreateAndVersion_PreserveBothPhysicalFiles()
    {
        var root = NewRoot();
        try
        {
            var projectId = await SeedProjectAsync();
            var storage = LocalStorage(root);
            ProjectDocumentSnapshot created;
            await using (var db = fixture.CreateDbContext())
            {
                var service = new ProjectDocumentService(db, storage);
                await using var content = Bytes("primera versión");
                created = await service.CreateAsync(projectId, "Plano", "Plan", "plano.pdf",
                    "application/pdf", content, Owner, false, default);
            }
            ProjectDocumentSnapshot updated;
            await using (var db = fixture.CreateDbContext())
            {
                var service = new ProjectDocumentService(db, storage);
                await using var content = Bytes("segunda versión");
                updated = await service.AddVersionAsync(projectId, created.Id, created.Version,
                    "plano-v2.pdf", "application/pdf", content, Owner, false, default);
            }
            Assert.Equal(2, updated.VersionNumber);
            await using var verify = fixture.CreateDbContext();
            var versions = await verify.Set<ProjectDocumentVersion>().AsNoTracking()
                .Where(x => x.ProjectDocumentId == created.Id).OrderBy(x => x.Number).ToListAsync();
            Assert.Equal(2, versions.Count);
            Assert.NotEqual(versions[0].StorageKey, versions[1].StorageKey);
            Assert.Equal(updated.CurrentVersionId, versions[1].Id);
            Assert.Equal(2, Directory.GetFiles(root).Length);
            Assert.Equal("primera versión", await ReadTextAsync(storage, versions[0].StorageKey));
            Assert.Equal("segunda versión", await ReadTextAsync(storage, versions[1].StorageKey));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
    [Fact]
    public async Task DocumentService_FailedStorageWrite_LeavesNoDocument()
    {
        var projectId = await SeedProjectAsync();
        await using var db = fixture.CreateDbContext();
        var service = new ProjectDocumentService(db, new FailingStorage());
        await using var content = Bytes("fallo de carga");
        await Assert.ThrowsAsync<IOException>(() => service.CreateAsync(projectId, "Foto", "Photo",
            "foto.jpg", "image/jpeg", content, Owner, false, default));
        Assert.False(await db.Set<ProjectDocument>().AnyAsync(x => x.ProjectId == projectId));
    }
    [Fact]
    public async Task DocumentService_NonOwner_CannotCreateFile()
    {
        var root = NewRoot();
        try
        {
            var projectId = await SeedProjectAsync();
            await using var db = fixture.CreateDbContext();
            var service = new ProjectDocumentService(db, LocalStorage(root));
            await using var content = Bytes("privado");
            var error = await Assert.ThrowsAsync<ProjectOperationException>(() =>
                service.CreateAsync(projectId, "Foto", "Photo", "foto.jpg", "image/jpeg",
                    content, "intruder", false, default));
            Assert.Equal(ProjectFailureReason.Forbidden, error.Reason);
            Assert.False(await db.Set<ProjectDocument>().AnyAsync(x => x.ProjectId == projectId));
            Assert.False(Directory.Exists(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
    private async Task<int> SeedProjectAsync()
    {
        await using var db = fixture.CreateDbContext();
        if (!await db.Users.AnyAsync(x => x.Id == Owner))
        {
            db.Users.Add(new ApplicationUser
            {
                Id = Owner, UserName = Owner, NormalizedUserName = Owner.ToUpperInvariant(),
                Email = "b04-storage-owner@example.com", NormalizedEmail = "B04-STORAGE-OWNER@EXAMPLE.COM",
                DisplayName = "Owner", SecurityStamp = Guid.NewGuid().ToString()
            });
        }
        var customer = new Customer
        {
            Name = "Cliente almacenamiento", IdentificationType = IdentificationType.PhysicalPerson,
            IdentificationNumber = Guid.NewGuid().ToString("N")[..9], Email = "storage@example.com",
            PhoneNumber = "88888888", ProvinceCode = "4", CantonCode = "01", DistrictCode = "01",
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
    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "revestik-b04-" + Guid.NewGuid().ToString("N"));
    private static LocalProjectFileStorage LocalStorage(string root) =>
        new(Options.Create(new ProjectFileStorageOptions { RootPath = root }));
    private static MemoryStream Bytes(string content) => new(System.Text.Encoding.UTF8.GetBytes(content));
    private static async Task<string> ReadTextAsync(IProjectFileStorage storage, string key)
    {
        await using var stream = await storage.OpenReadAsync(key, default);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
    private sealed class FailingStorage : IProjectFileStorage
    {
        public Task<string> SaveAsync(Stream source, CancellationToken cancellationToken) =>
            Task.FromException<string>(new IOException("Simulated write failure"));
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
    private sealed class ThrowAfterReadStream : Stream
    {
        private bool readOnce;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (readOnce) return ValueTask.FromException<int>(new IOException("Simulated interrupted source"));
            readOnce = true;
            buffer.Span[0] = 42;
            return ValueTask.FromResult(1);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}