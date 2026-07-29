using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Revestik.Api.Authorization;
using Revestik.Api.Data;
using Revestik.Api.Models.Identity;

namespace Revestik.Api.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddRevestikAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan =
                    TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<RevestikDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "__Host-Revestik.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;

            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;

                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;

                return Task.CompletedTask;
            };
        });

        var googleClientId =
            configuration["Authentication:Google:ClientId"];
        var googleClientSecret =
            configuration["Authentication:Google:ClientSecret"];

        if (!string.IsNullOrWhiteSpace(googleClientId) &&
            !string.IsNullOrWhiteSpace(googleClientSecret))
        {
            services
                .AddAuthentication()
                .AddGoogle(
                    GoogleDefaults.AuthenticationScheme,
                    options =>
                    {
                        options.ClientId = googleClientId;
                        options.ClientSecret = googleClientSecret;
                        options.CallbackPath = "/signin-google";
                        options.SaveTokens = false;
                    });
        }

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(
                new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build())
            .AddPolicy(
                PolicyNames.AdministratorOnly,
                policy => policy.RequireRole(
                    RoleNames.Administrator))
            .AddPolicy(
                PolicyNames.ManageCustomers,
                policy => policy.RequireRole(
                    RoleNames.Administrator,
                    RoleNames.Accountant,
                    RoleNames.Sales))
            .AddPolicy(
                PolicyNames.ManageInventory,
                policy => policy.RequireRole(
                    RoleNames.Administrator,
                    RoleNames.Accountant,
                    RoleNames.Sales,
                    RoleNames.Warehouse))
            .AddPolicy(
                PolicyNames.ManageInvoices,
                policy => policy.RequireRole(
                    RoleNames.Administrator,
                    RoleNames.Accountant,
                    RoleNames.Sales))
            .AddPolicy(
                PolicyNames.ViewAuditLog,
                policy => policy.RequireRole(
                    RoleNames.Administrator));

        return services;
    }
}