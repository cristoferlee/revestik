using Revestik.Public;
using Revestik.Public.Components;
using Revestik.Public.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents();
builder.Services.AddHttpClient<PublicCatalogReader>(client =>
{
    // This points to the existing Revestik.Api; never authenticate as an administrator.
    var url = builder.Configuration["PublicCatalog:ApiBaseUrl"];
    if (string.IsNullOrWhiteSpace(url))
    {
        if (!builder.Environment.IsDevelopment())
            throw new InvalidOperationException("PublicCatalog:ApiBaseUrl is required outside development.");
        url = "https://localhost:7081/";
    }
    if (!Uri.TryCreate(url, UriKind.Absolute, out var address) ||
        address.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(address.Query) ||
        !string.IsNullOrEmpty(address.Fragment) || address.AbsolutePath != "/")
        throw new InvalidOperationException("PublicCatalog:ApiBaseUrl must be an HTTPS origin without a path or query.");
    client.BaseAddress = address;
    client.Timeout = TimeSpan.FromSeconds(10);
});
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
// Keep unknown project example routes as real HTTP 404s during SSR.
// The Razor page still renders the localized, accessible unavailable message.
app.Use(async (context, next) =>
{
    var segments = context.Request.Path.Value?.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (segments is { Length: 3 } &&
        ((segments[0] == "es" && segments[1] == "proyectos") ||
         (segments[0] == "en" && segments[1] == "projects") ||
         (segments[0] == "de" && segments[1] == "projekte")) &&
        !PublicProjectCatalog.IsKnownSlug(segments[2]))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
    }

    await next();
});
app.MapGet("/", (HttpContext context) =>
{
    var language = context.Request.Headers.AcceptLanguage.ToString().Split(',', ';')[0].Trim().ToLowerInvariant();
    var culture = language.StartsWith("de") ? "de" : language.StartsWith("en") ? "en" : "es";
    return Results.Redirect($"/{culture}", permanent: false);
});
// Same-origin public images: proxy fixed numeric IDs, not arbitrary URLs.
app.MapGet("/media/catalog/{id:int}", async (int id, PublicCatalogReader catalog, CancellationToken ct) =>
{
    if (id <= 0) return Results.NotFound();
    try
    {
        using var result = await catalog.Image(id, ct);
        if (result.StatusCode == System.Net.HttpStatusCode.NotFound) return Results.NotFound();
        if (!result.IsSuccessStatusCode) return Results.StatusCode(503);
        var type = result.Content.Headers.ContentType?.MediaType;
        if (type is not ("image/jpeg" or "image/png" or "image/webp" or "image/gif"))
            return Results.StatusCode(502);
        // Media upload contract caps originals at 5 MB. Buffer to avoid disposing
        // the upstream HTTP response before Kestrel completes its own response.
        var bytes = await result.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length is <= 0 or > 5242880) return Results.StatusCode(502);
        return Results.File(bytes, type);
    }
    catch (HttpRequestException) { return Results.StatusCode(503); }
    catch (TaskCanceledException) { return Results.StatusCode(503); }
}).AllowAnonymous();
app.MapRazorComponents<App>();
app.Run();
