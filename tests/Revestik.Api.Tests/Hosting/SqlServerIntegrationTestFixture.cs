using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Revestik.Api.Data;
using Testcontainers.MsSql;

namespace Revestik.Api.Tests.Hosting;

public sealed class SqlServerIntegrationTestFixture : IAsyncLifetime
{
    private static readonly MsSqlContainer Container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04")
            .WithDatabase("RevestikIntegrationTestsHost")
            .Build();

    private static readonly SemaphoreSlim StartupLock = new(1, 1);
    private static readonly SemaphoreSlim TemplateLock = new(1, 1);
    private static readonly object TimingFileLock = new();
    private static readonly string TemplateSuffix = Guid.NewGuid().ToString("N");
    private static readonly string TemplateDatabase = $"RevestikTestTemplate_{TemplateSuffix}";
    private static readonly string BackupPath = $"/var/opt/mssql/data/revestik_test_{TemplateSuffix}.bak";

    private static bool containerStarted;
    private static bool templatePrepared;
    private static string? logicalDataFile;
    private static string? logicalLogFile;

    private readonly string databaseName = $"RevestikIntegrationTests_{Guid.NewGuid():N}";
    private readonly string timingId = Guid.NewGuid().ToString("N")[..8];

    // Set REVESTIK_SQL_USE_MIGRATIONS=1 to restore the original per-fixture migration path.
    private static bool UseMigrations =>
        string.Equals(Environment.GetEnvironmentVariable("REVESTIK_SQL_USE_MIGRATIONS"),
            "1", StringComparison.Ordinal);

    public string ConnectionString
    {
        get
        {
            var builder = new SqlConnectionStringBuilder(Container.GetConnectionString())
            {
                InitialCatalog = databaseName
            };
            return builder.ConnectionString;
        }
    }

    public async Task InitializeAsync()
    {
        var startupWatch = Stopwatch.StartNew();
        var startupSucceeded = false;
        try
        {
            await EnsureContainerStartedAsync();
            startupSucceeded = true;
        }
        finally
        {
            RecordTiming("EnsureContainerStarted", startupWatch.Elapsed, startupSucceeded);
        }

        if (UseMigrations)
        {
            var watch = Stopwatch.StartNew();
            var succeeded = false;
            try
            {
                await using var context = CreateDbContext();
                await context.Database.MigrateAsync();
                succeeded = true;
            }
            finally
            {
                RecordTiming("Migrate", watch.Elapsed, succeeded);
            }
            return;
        }

        await EnsureTemplatePreparedAsync();
        var restoreWatch = Stopwatch.StartNew();
        var restored = false;
        try
        {
            await RestoreDatabaseAsync(databaseName);
            restored = true;
        }
        finally
        {
            RecordTiming("CopyRestore", restoreWatch.Elapsed, restored);
        }
    }

    public async Task DisposeAsync()
    {
        var watch = Stopwatch.StartNew();
        var succeeded = false;
        try
        {
            await using var dbContext = CreateDbContext();
            await dbContext.Database.EnsureDeletedAsync();
            succeeded = true;
        }
        finally
        {
            RecordTiming("EnsureDeleted", watch.Elapsed, succeeded);
        }
    }

    public RevestikDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RevestikDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new RevestikDbContext(options);
    }

    private static async Task EnsureContainerStartedAsync()
    {
        if (containerStarted) return;
        await StartupLock.WaitAsync();
        try
        {
            if (containerStarted) return;
            var watch = Stopwatch.StartNew();
            var succeeded = false;
            try
            {
                await Container.StartAsync();
                containerStarted = true;
                succeeded = true;
            }
            finally
            {
                RecordTiming("ContainerStart", watch.Elapsed, succeeded, "shared");
            }
        }
        finally
        {
            StartupLock.Release();
        }
    }

    private static async Task EnsureTemplatePreparedAsync()
    {
        if (templatePrepared) return;
        await TemplateLock.WaitAsync();
        try
        {
            if (templatePrepared) return;

            var watch = Stopwatch.StartNew();
            var migrated = false;
            try
            {
                var connection = new SqlConnectionStringBuilder(Container.GetConnectionString())
                {
                    InitialCatalog = TemplateDatabase
                }.ConnectionString;
                var options = new DbContextOptionsBuilder<RevestikDbContext>()
                    .UseSqlServer(connection).Options;
                await using var context = new RevestikDbContext(options);
                await context.Database.MigrateAsync();
                migrated = true;
            }
            finally
            {
                RecordTiming("TemplateMigrate", watch.Elapsed, migrated, "shared");
            }

            await using (var sql = new SqlConnection(MasterConnectionString))
            {
                await sql.OpenAsync();
                await using var command = new SqlCommand(
                    "SELECT name, type FROM sys.master_files WHERE database_id = DB_ID(@database)", sql);
                command.Parameters.AddWithValue("@database", TemplateDatabase);
                await using var reader = await command.ExecuteReaderAsync();
                var files = new List<(string Name, byte Type)>();
                while (await reader.ReadAsync())
                {
                    files.Add((reader.GetString(0), reader.GetByte(1)));
                }
                logicalDataFile = files.Single(file => file.Type == 0).Name;
                logicalLogFile = files.Single(file => file.Type == 1).Name;
            }

            watch.Restart();
            var backedUp = false;
            try
            {
                await ExecuteMasterAsync(
                    $"BACKUP DATABASE {QuoteIdentifier(TemplateDatabase)} " +
                    $"TO DISK = N'{EscapeSqlLiteral(BackupPath)}' WITH COPY_ONLY, INIT;");
                backedUp = true;
                templatePrepared = true;
            }
            finally
            {
                RecordTiming("TemplateBackup", watch.Elapsed, backedUp, "shared");
            }
        }
        finally
        {
            TemplateLock.Release();
        }
    }

    private static async Task RestoreDatabaseAsync(string name)
    {
        var sql = $"RESTORE DATABASE {QuoteIdentifier(name)} " +
                  $"FROM DISK = N'{EscapeSqlLiteral(BackupPath)}' WITH " +
                  $"MOVE N'{EscapeSqlLiteral(logicalDataFile!)}' TO N'/var/opt/mssql/data/{name}.mdf', " +
                  $"MOVE N'{EscapeSqlLiteral(logicalLogFile!)}' TO N'/var/opt/mssql/data/{name}_log.ldf', RECOVERY;";
        await ExecuteMasterAsync(sql);
    }

    private static string MasterConnectionString =>
        new SqlConnectionStringBuilder(Container.GetConnectionString())
        {
            InitialCatalog = "master"
        }.ConnectionString;

    private static async Task ExecuteMasterAsync(string sql)
    {
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 180 };
        await command.ExecuteNonQueryAsync();
    }

    private static string QuoteIdentifier(string value) =>
        $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static string EscapeSqlLiteral(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);

    private void RecordTiming(string operation, TimeSpan duration, bool succeeded) =>
        RecordTiming(operation, duration, succeeded, timingId);

    private static void RecordTiming(string operation, TimeSpan duration, bool succeeded, string fixtureId)
    {
        var path = Environment.GetEnvironmentVariable("REVESTIK_SQL_TIMINGS_PATH");
        if (string.IsNullOrWhiteSpace(path)) return;

        var line = string.Join(",",
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            fixtureId,
            operation,
            duration.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture),
            succeeded ? "Success" : "Failure");
        try
        {
            lock (TimingFileLock)
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                if (!File.Exists(path) || new FileInfo(path).Length == 0)
                    File.AppendAllText(path,
                        "TimestampUtc,FixtureId,Operation,Milliseconds,Outcome" + Environment.NewLine);
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch (IOException) { /* Best-effort diagnostics. */ }
        catch (UnauthorizedAccessException) { /* Best-effort diagnostics. */ }
    }
}
