using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Revestik.Public.Services;

namespace Revestik.Api.Tests.Hosting;

[Collection("Isolated production hosting tests")]
public sealed class PublicSiteHostingTests(ProductionHttpHostFixture fixture)
    : IClassFixture<ProductionHttpHostFixture>
{
    [Theory]
    [InlineData("/es/proyectos")]
    [InlineData("/en/projects")]
    [InlineData("/de/projekte")]
    public async Task PublicSsrRoutes_RenderHtmlWithoutAuthentication(string path)
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("REVESTIK", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PublicRoot_RedirectsToLocalizedSsrPage()
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/en", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task UnknownExampleProject_RetainsNotFoundStatus()
    {
        // The production app uses loopback HTTP; TestServer has no listener on
        // that port. Stub only the public project reader's HTTP transport.
        var calls = 0;
        using var factory = CreateProjectHost(simulateOutage: false, onRequest: () => Interlocked.Increment(ref calls));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/es/proyectos/inexistente-prueba-404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task UnknownExampleProject_PublicApiUnavailable_ReturnsServiceUnavailable()
    {
        var calls = 0;
        using var factory = CreateProjectHost(simulateOutage: true, onRequest: () => Interlocked.Increment(ref calls));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/es/proyectos/inexistente-prueba-404");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("es", "/es/proyectos/baseline-publicado")]
    [InlineData("en", "/en/projects/baseline-publicado")]
    [InlineData("de", "/de/projekte/baseline-publicado")]
    public async Task PublishedProject_DetailRequestCount(string language, string path)
    {
        var calls = 0;

        using var factory = CreateProjectHost(
            simulateOutage: false,
            onRequest: () => Interlocked.Increment(ref calls),
            published: true, language: language);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Baseline Published Project", html);
        Assert.Contains("Baseline project description", html);

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task PublishedProject_IndependentRequestsReadFreshContent()
    {
        var calls = 0;
        var name = "Original Project Name";

        using var factory = CreateProjectHost(
            simulateOutage: false,
            onRequest: () => Interlocked.Increment(ref calls),
            published: true,
            projectName: () => name);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        const string path = "/es/proyectos/baseline-publicado";

        using var first = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains("Original Project Name",
            await first.Content.ReadAsStringAsync());
        Assert.Equal(1, calls);

        name = "Updated Project Name";

        using var second = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var html = await second.Content.ReadAsStringAsync();

        Assert.Contains("Updated Project Name", html);
        Assert.DoesNotContain("Original Project Name", html);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task InvalidProjectSlug_DoesNotRequestDetail()
    {
        var calls = 0;

        using var factory = CreateProjectHost(
            simulateOutage: false,
            onRequest: () => Interlocked.Increment(ref calls));

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync(
            "/es/proyectos/-invalid-slug");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("/es/acabados", false, 3)]
    [InlineData("/es/acabados?categoryId=101", true, 3)]
    public async Task Finishes_CatalogRequestCount(
        string path, bool selected, int expectedCalls)
    {
        var requests = new System.Collections.Concurrent.ConcurrentQueue<string>();

        using var factory = new RevestikWebApplicationFactory(
            "Production",
            customizeTestServices: services =>
            {
                services.AddHttpClient<PublicCatalogReader>()
                    .ConfigurePrimaryHttpMessageHandler(() =>
                        new FinishesCatalogHttpHandler(requests));

                services.AddHttpClient<FinishesHeroReader>()
                    .ConfigurePrimaryHttpMessageHandler(() =>
                        new FinishesCatalogHttpHandler(requests));
            });

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Porcelanato Baseline", html);
        Assert.Contains("Producto Multiple Baseline", html);

        if (selected)
        {
            Assert.Contains("Exteriores Baseline", html);
            Assert.Contains("Antideslizantes Baseline", html);
            Assert.Contains("Destonificado Baseline", html);

            Assert.Contains(
                "/api/public/catalog/categories?includeSubcategoriesFor=101",
                requests);
        }
        else
        {
            Assert.DoesNotContain("Exteriores Baseline", html);
        }

        if (!selected)
            Assert.Contains("/api/public/catalog/categories", requests);

        Assert.DoesNotContain(
            "/api/public/catalog/categories/porcelanato-baseline",
            requests);
        Assert.Contains("/api/public/site/finishes-hero", requests);

        Assert.Contains(requests, uri =>
            uri.StartsWith("/api/public/catalog/products?"));

        Assert.Equal(expectedCalls, requests.Count);

        Console.WriteLine(
            $"FINISHES_HTTP_OPTIMIZED: {path}, calls={requests.Count}");

        foreach (var uri in requests)
            Console.WriteLine($"FINISHES_HTTP_REQUEST: {uri}");
    }

    private sealed class FinishesCatalogHttpHandler(
        System.Collections.Concurrent.ConcurrentQueue<string> requests)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);

            var uri = request.RequestUri!;
            requests.Enqueue(uri.PathAndQuery);

            var category = new CategoryDto
            {
                Id = 101,
                Slug = "porcelanato-baseline",
                NameEs = "Porcelanato Baseline"
            };

            if (uri.Query == "?includeSubcategoriesFor=101")
            {
                category.Subcategories =
                [
                    new()
                    {
                        Id = 201,
                        CategoryId = 101,
                        Slug = "exteriores",
                        NameEs = "Exteriores Baseline"
                    },
                    new()
                    {
                        Id = 202,
                        CategoryId = 101,
                        Slug = "antideslizantes",
                        NameEs = "Antideslizantes Baseline"
                    },
                    new()
                    {
                        Id = 203,
                        CategoryId = 101,
                        Slug = "destonificado",
                        NameEs = "Destonificado Baseline"
                    }
                ];
            }

            object payload = uri.AbsolutePath switch
            {
                "/api/public/catalog/categories" =>
                    new[] { category },

                "/api/public/catalog/products" =>
                    new ProductPageDto
                    {
                        Page = 1,
                        PageSize = 12,
                        Total = 1,
                        Items =
                        [
                            new()
                            {
                                Id = 301,
                                Slug = "producto-multiple-baseline",
                                Translations =
                                [
                                    new()
                                    {
                                        LanguageCode = "es",
                                        Name = "Producto Multiple Baseline"
                                    }
                                ]
                            }
                        ]
                    },

                "/api/public/site/finishes-hero" =>
                    Array.Empty<HomeHeroSlideDto>(),

                _ => throw new InvalidOperationException(
                    $"Unexpected catalog request: {uri}")
            };

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = System.Net.Http.Json.JsonContent.Create(payload)
                });
        }
    }

    private static RevestikWebApplicationFactory CreateProjectHost(bool simulateOutage, Action? onRequest = null, bool published = false, string language = "es", Func<string>? projectName = null) =>
        new("Production", customizeTestServices: services =>
            services.AddHttpClient<PublicProjectsReader>()
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new ProjectDetailHttpHandler(simulateOutage, onRequest, published, language, projectName)));

    private sealed class ProjectDetailHttpHandler(bool simulateOutage, Action? onRequest, bool published, string language, Func<string>? projectName) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            onRequest?.Invoke();
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(published ? "/api/public/projects/baseline-publicado" : "/api/public/projects/inexistente-prueba-404", request.RequestUri?.AbsolutePath);
            Assert.Equal($"?language={language}", request.RequestUri?.Query);

            if (simulateOutage)
                throw new HttpRequestException("Simulated public projects API outage.");

            if (published)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = System.Net.Http.Json.JsonContent.Create(new ProjectDetailDto
                    {
                        Id = 1,
                        Slug = "baseline-publicado",
                        TypeCode = "residential",
                        Name = projectName?.Invoke() ?? "Baseline Published Project",
                        Description = "Baseline project description",
                        SeoTitle = "Baseline Published Project | REVESTIK",
                        SeoDescription = "Baseline SEO description"
                    })
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    [Fact]
    public async Task PrivateApplicationRoute_IsNotShadowedByPublicSsr()
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/administracion/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public sealed class PublicSiteStaticAssetsTests(DevelopmentHttpHostFixture fixture)
    : IClassFixture<DevelopmentHttpHostFixture>
{
    [Fact]
    public async Task PublicStylesheet_UsesExistingRootRelativePath()
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/css/public.css");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
    }
}
