using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Revestik.Client;
using Revestik.Client.Services.Locations;
using Revestik.Client.Services.Customers;
using Revestik.Client.Services.Taxpayers;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException(
        "The API base URL was not configured.");

builder.Services.AddScoped(_ =>
{
    return new HttpClient
    {
        BaseAddress = new Uri(apiBaseUrl)
    };
});

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