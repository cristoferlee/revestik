using Revestik.Public.Hosting;

namespace Revestik.Public;

// Keep this executable entry point distinct from the API host Program type,
// which is used by WebApplicationFactory in integration tests.
internal static class PublicSiteEntryPoint
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddRevestikPublicSite(builder.Configuration, builder.Environment);

        var app = builder.Build();
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/error", createScopeForErrors: true);
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseAntiforgery();

        app.UseRevestikPublicProjectStatus();
        app.MapRevestikPublicSite();
        app.Run();
    }
}
