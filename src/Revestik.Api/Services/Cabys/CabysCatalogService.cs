using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Revestik.Api.Models;
using Revestik.Api.Services.ElectronicDocuments;
using Revestik.Shared.Cabys;
using Revestik.Shared.Common;

namespace Revestik.Api.Services.Cabys;

public sealed class CabysCatalogService(
    RevestikDbContext dbContext,
    IWebHostEnvironment environment,
    IElectronicDocumentXmlParser electronicDocumentXmlParser) : ICabysCatalogService
{
    private const string BundledVersion = "2025";
    private const string BundledFileName = "cabys-2025.json.gz";
    private const int ExpectedBundledItemCount = 20_506;
    private const int ImportBatchSize = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<CabysCatalogStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        var current = await dbContext.CabysCatalogVersions
            .AsNoTracking()
            .Where(x => x.IsCurrent)
            .Select(x => new CabysCatalogStatusResponse(
                true,
                x.Version,
                x.ItemCount,
                x.ImportedAtUtc,
                x.SourceFileName,
                x.SourceSha256))
            .SingleOrDefaultAsync(cancellationToken);

        return current ?? new CabysCatalogStatusResponse(
            false,
            null,
            0,
            null,
            null,
            null);
    }

    public async Task<CabysCatalogLoadResponse> LoadBundledCatalogAsync(
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.CabysCatalogVersions
            .SingleOrDefaultAsync(
                x => x.Version == BundledVersion,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.ItemCount != ExpectedBundledItemCount)
            {
                throw new InvalidOperationException(
                    "La versión CAByS 2025 existe, pero su carga está incompleta. " +
                    "Revisa la base de datos antes de continuar.");
            }

            if (!existing.IsCurrent)
            {
                await SetOnlyCurrentVersionAsync(existing, cancellationToken);
            }

            await BackfillEconomicActivitiesAsync(cancellationToken);

            return new CabysCatalogLoadResponse(
                existing.Version,
                existing.ItemCount,
                true);
        }

        var path = ResolveBundledCatalogPath();
        var sha256 = await ComputeSha256Async(path, cancellationToken);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var previousCurrent = await dbContext.CabysCatalogVersions
                .Where(x => x.IsCurrent)
                .ToListAsync(cancellationToken);

            foreach (var item in previousCurrent)
            {
                item.IsCurrent = false;
            }

            if (previousCurrent.Count > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var catalog = new CabysCatalogVersion
            {
                Version = BundledVersion,
                IsCurrent = true,
                ItemCount = 0,
                SourceFileName = BundledFileName,
                SourceSha256 = sha256,
                ImportedAtUtc = DateTime.UtcNow
            };

            dbContext.CabysCatalogVersions.Add(catalog);
            await dbContext.SaveChangesAsync(cancellationToken);

            var imported = await ImportRowsAsync(
                path,
                BundledVersion,
                cancellationToken);

            if (imported != ExpectedBundledItemCount)
            {
                throw new InvalidOperationException(
                    $"El catálogo CAByS incluido contiene {imported:N0} registros; " +
                    $"se esperaban {ExpectedBundledItemCount:N0}.");
            }

            catalog.ItemCount = imported;
            await dbContext.SaveChangesAsync(cancellationToken);
            await BackfillEconomicActivitiesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new CabysCatalogLoadResponse(
                catalog.Version,
                catalog.ItemCount,
                false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PaginatedResponse<CabysListItemResponse>> SearchAsync(
        CabysSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = dbContext.CabysItems
            .AsNoTracking()
            .Where(x => x.Catalog.IsCurrent);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            if (search.Length == 13 && search.All(char.IsDigit))
            {
                query = query.Where(x => x.Code == search);
            }
            else
            {
                const string searchCollation = "Modern_Spanish_CI_AI";

                query = query.Where(x =>
                    x.Code.Contains(search) ||
                    EF.Functions.Collate(x.Description, searchCollation).Contains(search) ||
                    EF.Functions.Collate(x.Category1Description, searchCollation).Contains(search) ||
                    EF.Functions.Collate(x.Category2Description, searchCollation).Contains(search) ||
                    EF.Functions.Collate(x.Category3Description, searchCollation).Contains(search) ||
                    EF.Functions.Collate(x.Category4Description, searchCollation).Contains(search) ||
                    EF.Functions.Collate(x.Category5Description, searchCollation).Contains(search) ||
                    EF.Functions.Collate(x.Category6Description, searchCollation).Contains(search) ||
                    EF.Functions.Collate(x.Category7Description, searchCollation).Contains(search) ||
                    EF.Functions.Collate(x.Category8Description, searchCollation).Contains(search) ||
                    EF.Functions.Collate(x.Includes, searchCollation).Contains(search) ||
                    EF.Functions.Collate(x.Excludes, searchCollation).Contains(search));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = ((long)request.Page - 1) * request.PageSize;

        IReadOnlyList<CabysListItemResponse> items = skip >= totalCount
            ? []
            : await query
                .OrderBy(x => x.Code)
                .Skip((int)skip)
                .Take(request.PageSize)
                .Select(x => new CabysListItemResponse(
                    x.Code,
                    x.Description,
                    x.TaxReference,
                    x.Category1Description,
                    x.Category4Description,
                    x.Category8Description,
                    x.CatalogVersion))
                .ToListAsync(cancellationToken);

        return new PaginatedResponse<CabysListItemResponse>(
            items,
            request.Page,
            request.PageSize,
            totalCount);
    }

    public async Task<CabysDetailResponse?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = code.Trim();

        return await dbContext.CabysItems
            .AsNoTracking()
            .Where(x => x.Catalog.IsCurrent && x.Code == normalized)
            .Select(x => new CabysDetailResponse(
                x.Code,
                x.Description,
                x.TaxReference,
                x.Category1Code,
                x.Category1Description,
                x.Category2Code,
                x.Category2Description,
                x.Category3Code,
                x.Category3Description,
                x.Category4Code,
                x.Category4Description,
                x.Category5Code,
                x.Category5Description,
                x.Category6Code,
                x.Category6Description,
                x.Category7Code,
                x.Category7Description,
                x.Category8Code,
                x.Category8Description,
                x.Includes,
                x.Excludes,
                x.CatalogVersion))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<int> ImportRowsAsync(
        string path,
        string version,
        CancellationToken cancellationToken)
    {
        await using var file = File.OpenRead(path);
        await using var gzip = new GZipStream(file, CompressionMode.Decompress);

        var batch = new List<CabysItem>(ImportBatchSize);
        var count = 0;

        await foreach (var row in JsonSerializer.DeserializeAsyncEnumerable<CabysSeedRow>(
            gzip,
            JsonOptions,
            cancellationToken))
        {
            if (row is null)
            {
                continue;
            }

            batch.Add(MapRow(row, version));

            if (batch.Count < ImportBatchSize)
            {
                continue;
            }

            dbContext.CabysItems.AddRange(batch);
            await dbContext.SaveChangesAsync(cancellationToken);
            count += batch.Count;
            DetachImportedItems();
            batch.Clear();
        }

        if (batch.Count > 0)
        {
            dbContext.CabysItems.AddRange(batch);
            await dbContext.SaveChangesAsync(cancellationToken);
            count += batch.Count;
            DetachImportedItems();
        }

        return count;
    }

    private async Task BackfillEconomicActivitiesAsync(
        CancellationToken cancellationToken)
    {
        var documents = await dbContext.ElectronicDocuments
            .Where(x =>
                x.IssuerEconomicActivityCode == string.Empty ||
                x.ReceiverEconomicActivityCode == string.Empty)
            .Select(x => new
            {
                x.Id,
                x.OriginalXml
            })
            .ToListAsync(cancellationToken);

        foreach (var stored in documents)
        {
            try
            {
                var parsed = electronicDocumentXmlParser.Parse(stored.OriginalXml);
                if (parsed is not ParsedReceivedElectronicDocument received)
                {
                    continue;
                }

                var document = await dbContext.ElectronicDocuments
                    .SingleAsync(x => x.Id == stored.Id, cancellationToken);

                document.IssuerEconomicActivityCode =
                    received.Document.IssuerEconomicActivityCode;
                document.ReceiverEconomicActivityCode =
                    received.Document.ReceiverEconomicActivityCode;
            }
            catch (ElectronicDocumentXmlException)
            {
                // The original fiscal XML remains untouched. Unsupported/malformed
                // historical documents are simply left without activity enrichment.
            }
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SetOnlyCurrentVersionAsync(
        CabysCatalogVersion selected,
        CancellationToken cancellationToken)
    {
        var currentVersions = await dbContext.CabysCatalogVersions
            .Where(x => x.IsCurrent && x.Version != selected.Version)
            .ToListAsync(cancellationToken);

        foreach (var item in currentVersions)
        {
            item.IsCurrent = false;
        }

        if (currentVersions.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        selected.IsCurrent = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private string ResolveBundledCatalogPath()
    {
        var contentRootPath = Path.Combine(
            environment.ContentRootPath,
            "Data",
            "Cabys",
            BundledFileName);

        if (File.Exists(contentRootPath))
        {
            return contentRootPath;
        }

        var outputPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "Cabys",
            BundledFileName);

        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        throw new FileNotFoundException(
            "No se encontró el catálogo CAByS 2025 incluido con Revestik.",
            contentRootPath);
    }

    private void DetachImportedItems()
    {
        foreach (var entry in dbContext.ChangeTracker.Entries<CabysItem>())
        {
            entry.State = EntityState.Detached;
        }
    }

    private static CabysItem MapRow(CabysSeedRow row, string version) =>
        new()
        {
            CatalogVersion = version,
            Code = row.Code,
            Description = row.Description,
            TaxReference = row.TaxReference,
            Category1Code = row.Category1Code,
            Category1Description = row.Category1Description,
            Category2Code = row.Category2Code,
            Category2Description = row.Category2Description,
            Category3Code = row.Category3Code,
            Category3Description = row.Category3Description,
            Category4Code = row.Category4Code,
            Category4Description = row.Category4Description,
            Category5Code = row.Category5Code,
            Category5Description = row.Category5Description,
            Category6Code = row.Category6Code,
            Category6Description = row.Category6Description,
            Category7Code = row.Category7Code,
            Category7Description = row.Category7Description,
            Category8Code = row.Category8Code,
            Category8Description = row.Category8Description,
            Includes = row.Includes,
            Excludes = row.Excludes
        };

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class CabysSeedRow
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        [JsonPropertyName("taxReference")]
        public string TaxReference { get; set; } = string.Empty;
        [JsonPropertyName("category1Code")]
        public string Category1Code { get; set; } = string.Empty;
        [JsonPropertyName("category1Description")]
        public string Category1Description { get; set; } = string.Empty;
        [JsonPropertyName("category2Code")]
        public string Category2Code { get; set; } = string.Empty;
        [JsonPropertyName("category2Description")]
        public string Category2Description { get; set; } = string.Empty;
        [JsonPropertyName("category3Code")]
        public string Category3Code { get; set; } = string.Empty;
        [JsonPropertyName("category3Description")]
        public string Category3Description { get; set; } = string.Empty;
        [JsonPropertyName("category4Code")]
        public string Category4Code { get; set; } = string.Empty;
        [JsonPropertyName("category4Description")]
        public string Category4Description { get; set; } = string.Empty;
        [JsonPropertyName("category5Code")]
        public string Category5Code { get; set; } = string.Empty;
        [JsonPropertyName("category5Description")]
        public string Category5Description { get; set; } = string.Empty;
        [JsonPropertyName("category6Code")]
        public string Category6Code { get; set; } = string.Empty;
        [JsonPropertyName("category6Description")]
        public string Category6Description { get; set; } = string.Empty;
        [JsonPropertyName("category7Code")]
        public string Category7Code { get; set; } = string.Empty;
        [JsonPropertyName("category7Description")]
        public string Category7Description { get; set; } = string.Empty;
        [JsonPropertyName("category8Code")]
        public string Category8Code { get; set; } = string.Empty;
        [JsonPropertyName("category8Description")]
        public string Category8Description { get; set; } = string.Empty;
        [JsonPropertyName("includes")]
        public string Includes { get; set; } = string.Empty;
        [JsonPropertyName("excludes")]
        public string Excludes { get; set; } = string.Empty;
    }
}
