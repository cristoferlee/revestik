using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Revestik.Shared.Dashboard;

namespace Revestik.Api.Integrations.Bccr;

public sealed class BccrExchangeRateService(
    HttpClient httpClient,
    IMemoryCache cache,
    IConfiguration configuration,
    ILogger<BccrExchangeRateService> logger)
{
    private const string CacheKey = "dashboard:bccr:usd-crc";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(3);
    private static readonly TimeZoneInfo CostaRica = TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica");
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    public async Task<ExchangeRateResponse?> GetAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CostaRica));
        if (cache.TryGetValue(CacheKey, out CacheEntry? entry) && entry is not null && entry.NextRefreshUtc > DateTimeOffset.UtcNow)
            return Project(entry.Value, true, today);

        await RefreshLock.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(CacheKey, out entry) && entry is not null && entry.NextRefreshUtc > DateTimeOffset.UtcNow)
                return Project(entry.Value, true, today);

            try
            {
                var token = configuration["Bccr:Token"];
                if (string.IsNullOrWhiteSpace(token))
                {
                    logger.LogWarning("BCCR SDDE token is not configured.");
                    return Fallback(entry, today);
                }

                var start = today.AddDays(-7);
                var purchases = await QueryAsync(317, start, today, token, ct);
                var sales = await QueryAsync(318, start, today, token, ct);
                var common = purchases.Keys.Intersect(sales.Keys).OrderByDescending(x => x).FirstOrDefault();
                if (common == default || purchases[common] <= 7m || sales[common] <= 7m || purchases[common] > sales[common])
                    throw new InvalidDataException("BCCR purchase and sale rates are missing or inconsistent.");

                var value = new ExchangeRateResponse(purchases[common], sales[common], common, false, false);
                cache.Set(CacheKey, new CacheEntry(value, DateTimeOffset.UtcNow.Add(RefreshInterval)), Retention);
                return Project(value, false, today);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or FormatException or OverflowException or InvalidOperationException or TaskCanceledException)
            {
                if (ex is OperationCanceledException && ct.IsCancellationRequested) throw;
                logger.LogWarning("BCCR SDDE exchange rate request failed: {ErrorType}", ex.GetType().Name);
                return Fallback(entry, today);
            }
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private ExchangeRateResponse? Fallback(CacheEntry? entry, DateOnly today)
    {
        if (entry is null) return null;
        cache.Set(CacheKey, new CacheEntry(entry.Value, DateTimeOffset.UtcNow.AddMinutes(10)), Retention);
        return Project(entry.Value, true, today);
    }

    private static ExchangeRateResponse Project(ExchangeRateResponse value, bool cached, DateOnly today) =>
        value with { IsCached = cached, IsStale = value.EffectiveDate < today };

    private async Task<Dictionary<DateOnly, decimal>> QueryAsync(
        int code, DateOnly start, DateOnly end, string token, CancellationToken ct)
    {
        // The SDDE API documents the yyyy/MM/dd format for these query parameters.
        var from = Uri.EscapeDataString(start.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture));
        var to = Uri.EscapeDataString(end.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture));
        var path = $"indicadoresEconomicos/{code}/series?fechaInicio={from}&fechaFin={to}&idioma=ES";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > 512_000)
            throw new InvalidDataException("BCCR response size invalid.");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(bytes, ct)) > 0)
        {
            if (buffer.Length + read > 512_000)
                throw new InvalidDataException("BCCR response size invalid.");
            buffer.Write(bytes, 0, read);
        }
        if (buffer.Length == 0) throw new InvalidDataException("Empty BCCR response.");
        buffer.Position = 0;
        using var json = await JsonDocument.ParseAsync(buffer, cancellationToken: ct);
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("estado", out var status) || status.ValueKind != JsonValueKind.True ||
            !root.TryGetProperty("datos", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Invalid BCCR SDDE response envelope.");

        var values = new Dictionary<DateOnly, decimal>();
        foreach (var indicator in data.EnumerateArray())
        {
            if (indicator.ValueKind != JsonValueKind.Object ||
                !indicator.TryGetProperty("codigoIndicador", out var indicatorCode) ||
                !int.TryParse(indicatorCode.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var actualCode) ||
                actualCode != code ||
                !indicator.TryGetProperty("series", out var series) || series.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var observation in series.EnumerateArray())
            {
                if (observation.ValueKind != JsonValueKind.Object ||
                    !observation.TryGetProperty("fecha", out var dateElement) || dateElement.ValueKind != JsonValueKind.String ||
                    !DateOnly.TryParseExact(dateElement.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
                    date < start || date > end ||
                    !observation.TryGetProperty("valorDatoPorPeriodo", out var amountElement))
                    continue;

                var validAmount = amountElement.ValueKind switch
                {
                    JsonValueKind.Number => amountElement.TryGetDecimal(out _),
                    JsonValueKind.String => decimal.TryParse(amountElement.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out _),
                    _ => false
                };
                if (!validAmount) continue;
                var amount = amountElement.ValueKind == JsonValueKind.Number
                    ? amountElement.GetDecimal()
                    : decimal.Parse(amountElement.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture);
                if (amount is <= 0 or > 10_000) continue;
                values[date] = amount;
            }
        }
        if (values.Count == 0) throw new InvalidDataException("No valid BCCR SDDE observations.");
        return values;
    }

    private sealed record CacheEntry(ExchangeRateResponse Value, DateTimeOffset NextRefreshUtc);
}
