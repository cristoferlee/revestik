using Revestik.Public;
using Revestik.Public.Components;
using Revestik.Public.Services;

namespace Revestik.Public.Hosting;

/// <summary>
/// Registers the same public SSR site in the standalone development host and
/// in the existing Revestik.Api host. Neither host uses administrator cookies
/// or privileged endpoints when reading public catalog content.
/// </summary>
public static class PublicSiteHostingExtensions
{
    public static IServiceCollection AddRevestikPublicSite(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddRazorComponents().AddInteractiveServerComponents();
        services.AddCascadingAuthenticationState();
        services.AddHttpClient<PublicCatalogReader>(client =>
        {
            client.BaseAddress = PublicApiAddress(configuration, environment);
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHttpClient<FinishesHeroReader>(client =>
        {
            client.BaseAddress = PublicApiAddress(configuration, environment);
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHttpClient<PublicProjectsReader>(client =>
        {
            client.BaseAddress = PublicApiAddress(configuration, environment);
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }

    private static Uri PublicApiAddress(IConfiguration configuration, IHostEnvironment environment)
    {
        // In the unified container, requests are internal loopback traffic to
        // the API's existing listener, not a second app or external domain.
        // The configured origin remains available for local integration tests.
        var value = configuration["PublicCatalog:ApiBaseUrl"]
            ?? (environment.IsDevelopment()
                ? "https://localhost:7126/"
                : "http://127.0.0.1:8080/");

        if (!Uri.TryCreate(value, UriKind.Absolute, out var origin) ||
            origin.Scheme is not ("https" or "http") ||
            !string.IsNullOrEmpty(origin.UserInfo) ||
            !string.IsNullOrEmpty(origin.Query) ||
            !string.IsNullOrEmpty(origin.Fragment) ||
            origin.AbsolutePath != "/")
            throw new InvalidOperationException(
                "PublicCatalog:ApiBaseUrl must be an HTTP(S) origin without credentials, path, query or fragment.");

        // HTTP is permitted only for same-process-loopback routing. The public
        // site and browser-facing API still enforce HTTPS at their ingress.
        if (origin.Scheme == Uri.UriSchemeHttp && !origin.IsLoopback)
            throw new InvalidOperationException("Public catalog HTTP traffic must use loopback.");

        return origin;
    }

    // An unknown or unpublished project must retain an actual HTTP 404.
    // Resolve using only the anonymous, published-only API; never use internal projects.
    public static IApplicationBuilder UseRevestikPublicProjectStatus(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            var segments = context.Request.Path.Value?.Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments is { Length: 3 } &&
                ((segments[0] == "es" && segments[1] == "proyectos") ||
                 (segments[0] == "en" && segments[1] == "projects") ||
                 (segments[0] == "de" && segments[1] == "projekte")))
            {
                if (!SiteLinks.IsProjectSlug(segments[2]))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                }
                else
                {
                    try
                    {
                        var reader = context.RequestServices.GetRequiredService<PublicProjectsReader>();
                        if (await reader.Detail(segments[2], segments[0], context.RequestAborted) is null)
                            context.Response.StatusCode = StatusCodes.Status404NotFound;
                    }
                    catch (HttpRequestException)
                    {
                        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                        return;
                    }
                    catch (TaskCanceledException) when (!context.RequestAborted.IsCancellationRequested)
                    {
                        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                        return;
                    }
                }
            }
            await next(context);
        });
        return app;
    }

    public static IEndpointRouteBuilder MapRevestikPublicSite(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/", (HttpContext context) =>
        {
            var language = context.Request.Headers.AcceptLanguage.ToString()
                .Split(',', ';')[0].Trim().ToLowerInvariant();
            var culture = language.StartsWith("de") ? "de" : language.StartsWith("en") ? "en" : "es";
            return Results.Redirect($"/{culture}", permanent: false);
        }).AllowAnonymous();

        endpoints.MapGet("/media/catalog/{id:int}", async (
            int id, PublicCatalogReader catalog, CancellationToken ct) =>
        {
            if (id <= 0) return Results.NotFound();
            try
            {
                using var response = await catalog.Image(id, ct);
                return await ImageResult(response, allowGif: true, ct);
            }
            catch (HttpRequestException) { return Results.StatusCode(503); }
            catch (TaskCanceledException) { return Results.StatusCode(503); }
        }).AllowAnonymous();

        endpoints.MapGet("/media/home-hero/{slot:int}", async (
            int slot, PublicCatalogReader catalog, CancellationToken ct) =>
        {
            if (slot is < 1 or > 3) return Results.NotFound();
            try
            {
                using var response = await catalog.HomeHeroImage(slot, ct);
                return await ImageResult(response, allowGif: false, ct);
            }
            catch (HttpRequestException) { return Results.StatusCode(503); }
            catch (TaskCanceledException) { return Results.StatusCode(503); }
        }).AllowAnonymous();

        endpoints.MapGet("/media/finishes-hero/{slot:int}", async (
            int slot, FinishesHeroReader hero, CancellationToken ct) =>
        {
            if (slot is < 1 or > 3) return Results.NotFound();
            try
            {
                using var response = await hero.Image(slot, ct);
                return await ImageResult(response, allowGif: false, ct);
            }
            catch (HttpRequestException) { return Results.StatusCode(503); }
            catch (TaskCanceledException) { return Results.StatusCode(503); }
        }).AllowAnonymous();

        endpoints.MapGet("/media/home-solutions", async (
            PublicCatalogReader catalog, CancellationToken ct) =>
        {
            try
            {
                using var response = await catalog.HomeSolutionsImage(ct);
                return await ImageResult(response, allowGif: false, ct);
            }
            catch (HttpRequestException) { return Results.StatusCode(503); }
            catch (TaskCanceledException) { return Results.StatusCode(503); }
        }).AllowAnonymous();

        // Explicitly include the assembly where the public @page components live.
        endpoints.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AllowAnonymous();

        return endpoints;
    }

    private static async Task<IResult> ImageResult(
        HttpResponseMessage response, bool allowGif, CancellationToken ct)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return Results.NotFound();
        if (!response.IsSuccessStatusCode)
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not ("image/jpeg" or "image/png" or "image/webp") &&
            !(allowGif && mediaType == "image/gif"))
            return Results.StatusCode(StatusCodes.Status502BadGateway);

        // Buffer before upstream disposal and keep the existing 5 MB limit.
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length is <= 0 or > 5 * 1024 * 1024)
            return Results.StatusCode(StatusCodes.Status502BadGateway);

        return Results.File(bytes, mediaType);
    }
}
