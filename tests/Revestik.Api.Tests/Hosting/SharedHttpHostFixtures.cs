namespace Revestik.Api.Tests.Hosting;

/// <summary>
/// Shares the expensive ASP.NET Core host only within a single xUnit test class.
/// Individual tests still create and dispose their own HttpClient and cookies.
/// </summary>
public sealed class DevelopmentHttpHostFixture : IAsyncLifetime
{
    public RevestikWebApplicationFactory Factory { get; } = new("Development");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await Factory.DisposeAsync();
}

public sealed class ProductionHttpHostFixture : IAsyncLifetime
{
    public RevestikWebApplicationFactory Factory { get; } = new("Production");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await Factory.DisposeAsync();
}
