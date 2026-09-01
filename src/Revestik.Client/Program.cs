using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Revestik.Client;
using Revestik.Client.Services.Authentication;
using Revestik.Client.Services.Customers;
using Revestik.Client.Services.Locations;
using Revestik.Client.Services.Taxpayers;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseAddress = builder.HostEnvironment.IsDevelopment()
    ? new Uri(
        builder.Configuration["ApiBaseUrl"]
            ?? throw new InvalidOperationException(
                "The development API base URL was not configured."))
    : new Uri(builder.HostEnvironment.BaseAddress);

builder.Services.AddAuthorizationCore();

builder.Services.AddTransient<CookieHandler>();

builder.Services.AddKeyedScoped<HttpClient>(
    CsrfTokenService.HttpClientKey,
    (serviceProvider, _) =>
    {
        var cookieHandler =
            serviceProvider.GetRequiredService<CookieHandler>();

        cookieHandler.InnerHandler = new HttpClientHandler();

        return new HttpClient(cookieHandler)
        {
            BaseAddress = apiBaseAddress
        };
    });

builder.Services.AddScoped<CsrfTokenService>();

builder.Services.AddScoped(serviceProvider =>
{
    var csrfTokenService =
        serviceProvider.GetRequiredService<CsrfTokenService>();

    var csrfHandler = new CsrfHandler(
        csrfTokenService,
        apiBaseAddress);

    var cookieHandler =
        serviceProvider.GetRequiredService<CookieHandler>();

    cookieHandler.InnerHandler = new HttpClientHandler();
    csrfHandler.InnerHandler = cookieHandler;

    return new HttpClient(csrfHandler)
    {
        BaseAddress = apiBaseAddress
    };
});

builder.Services.AddScoped<
    CookieAuthenticationStateProvider>();

builder.Services.AddScoped<AuthenticationStateProvider>(
    serviceProvider =>
        serviceProvider.GetRequiredService<
            CookieAuthenticationStateProvider>());

builder.Services.AddScoped<
    IAuthenticationService,
    AuthenticationService>();

builder.Services.AddScoped<
    ICustomerApiService,
    CustomerApiService>();

builder.Services.AddScoped<
    ITaxpayerApiService,
    TaxpayerApiService>();

builder.Services.AddScoped<
    ILocationApiService,
    LocationApiService>();

await builder.Build().RunAsync();
