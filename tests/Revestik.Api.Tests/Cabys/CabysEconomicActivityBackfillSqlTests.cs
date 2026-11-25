using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Models.Identity;
using Revestik.Api.Services.Cabys;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Api.Tests.Hosting;
using Revestik.Shared.ElectronicDocuments;

namespace Revestik.Api.Tests.Cabys;

// REFACTOR-04D.1: one SQL scenario, using the existing shared container fixture.
// The catalog is preseeded as loaded: no 20,506-row import is executed.
public sealed class CabysEconomicActivityBackfillSqlTests(
    SqlServerIntegrationTestFixture fixture)
    : IClassFixture<SqlServerIntegrationTestFixture>
{
    [Fact]
    public async Task ExistingCatalog_BackfillsParsedDocumentsWithTwoReads()
    {
        var userId = Guid.NewGuid().ToString("N");
        await using (var seed = fixture.CreateDbContext())
        {
            seed.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = $"cabys-{userId}@example.test",
                NormalizedUserName = $"CABYS-{userId}@EXAMPLE.TEST",
                Email = $"cabys-{userId}@example.test",
                NormalizedEmail = $"CABYS-{userId}@EXAMPLE.TEST",
                EmailConfirmed = true,
                DisplayName = "CAByS backfill SQL test",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });

            seed.CabysCatalogVersions.Add(new CabysCatalogVersion
            {
                Version = "2025",
                ItemCount = 20_506,
                IsCurrent = true,
                SourceFileName = "test",
                SourceSha256 = "test",
                ImportedAtUtc = DateTime.UtcNow
            });

            seed.ElectronicDocuments.AddRange(
                CreateDocument(userId, 1, "", ""),
                CreateDocument(userId, 2, "EXISTING", ""),
                CreateDocument(userId, 3, "", ""),
                CreateDocument(userId, 4, "OLD", "DONE"));
            await seed.SaveChangesAsync();
        }

        var reads = new ElectronicDocumentReadCounter();
        var options = new DbContextOptionsBuilder<RevestikDbContext>()
            .UseSqlServer(fixture.ConnectionString)
            .AddInterceptors(reads)
            .Options;

        await using (var db = new RevestikDbContext(options))
        {
            var service = new CabysCatalogService(
                db, new StubEnvironment(), new StubParser());
            await service.LoadBundledCatalogAsync(CancellationToken.None);
        }

        // One narrow candidate SELECT + one batched tracked SELECT (not N SELECTs).
        Assert.Equal(2, reads.Count);

        await using var verify = fixture.CreateDbContext();
        var stored = await verify.ElectronicDocuments
            .AsNoTracking()
            .Select(x => new
            {
                x.OriginalXml,
                x.IssuerEconomicActivityCode,
                x.ReceiverEconomicActivityCode
            })
            .ToListAsync();
        var byMarker = stored.ToDictionary(x => x.OriginalXml[0]);

        Assert.Equal("ACT-1", byMarker[1].IssuerEconomicActivityCode);
        Assert.Equal("REC-1", byMarker[1].ReceiverEconomicActivityCode);
        Assert.Equal("ACT-2", byMarker[2].IssuerEconomicActivityCode);
        Assert.Equal("REC-2", byMarker[2].ReceiverEconomicActivityCode);
        Assert.Equal("", byMarker[3].IssuerEconomicActivityCode);
        Assert.Equal("", byMarker[3].ReceiverEconomicActivityCode);
        Assert.Equal("OLD", byMarker[4].IssuerEconomicActivityCode);
        Assert.Equal("DONE", byMarker[4].ReceiverEconomicActivityCode);
    }

    private static ElectronicDocument CreateDocument(
        string userId, byte marker, string issuerCode, string receiverCode) =>
        new()
        {
            Clave = Guid.NewGuid().ToString("N").PadRight(50, '0'),
            NumeroConsecutivo = "00100001010000000001",
            DocumentType = ElectronicDocumentType.Invoice,
            Direction = ElectronicDocumentDirection.Received,
            FechaEmision = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.FromHours(-6)),
            IssuerName = "Test issuer",
            IssuerIdentificationType = "02",
            IssuerIdentification = "3101000000",
            ReceiverName = "Test receiver",
            ReceiverIdentificationType = "02",
            ReceiverIdentification = "3102959852",
            IssuerEconomicActivityCode = issuerCode,
            ReceiverEconomicActivityCode = receiverCode,
            CurrencyCode = "CRC",
            ExchangeRate = 1m,
            SaleConditionCode = "01",
            ProcessingStatus = ElectronicDocumentProcessingStatus.Pending,
            OriginalXml = [marker],
            ImportedByUserId = userId,
            ImportedAtUtc = DateTime.UtcNow
        };

    private sealed class ElectronicDocumentReadCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("[ElectronicDocuments]", StringComparison.OrdinalIgnoreCase))
            {
                Count++;
            }

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class StubParser : IElectronicDocumentXmlParser
    {
        public ParsedReceivedXml Parse(ReadOnlyMemory<byte> xml)
        {
            var marker = xml.Span[0];
            if (marker == 3)
            {
                throw new ElectronicDocumentXmlException("Historical XML not supported");
            }

            // Only the economic activity fields are consumed by the backfill.
            var parsed = new ParsedElectronicDocument(
                ElectronicDocumentType.Invoice,
                string.Empty,
                string.Empty,
                DateTimeOffset.UtcNow,
                $"ACT-{marker}",
                $"REC-{marker}",
                null!,
                null!,
                "01",
                null,
                "CRC",
                1m,
                null!,
                []);
            return new ParsedReceivedElectronicDocument(parsed);
        }
    }

    private sealed class StubEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Revestik.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
