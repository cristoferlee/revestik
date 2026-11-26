using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Revestik.Api.Authorization;
using Revestik.Shared.Authentication;
using Revestik.Api.Services.Authentication;
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

        // Refresh identity claims and reject revoked security stamps within one minute.
        services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.FromMinutes(1));

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "__Host-Revestik.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;

            // Preserve Identity's SecurityStamp validator. Public login
            // shutdown adds a configuration-only check, not a SQL query.
            var validateIdentityPrincipal = options.Events.OnValidatePrincipal;

            options.Events.OnValidatePrincipal = async context =>
            {
                static bool MustReject(
                    System.Security.Claims.ClaimsPrincipal? principal,
                    bool loginEnabled)
                {
                    if (PublicAuthenticationRules.IsEmployee(principal))
                        return false;

                    if (PublicAuthenticationRules.IsPublicCustomer(principal))
                        return !loginEnabled;

                    // Unknown, roleless and mixed-role accounts
                    // cannot establish a valid application identity.
                    return true;
                }

                var currentConfiguration = context.HttpContext.RequestServices
                    .GetRequiredService<IConfiguration>();

                var isEmployee =
                    PublicAuthenticationRules.IsEmployee(context.Principal);

                // Public configuration must not affect employee sessions.
                var loginEnabled = !isEmployee &&
                    bool.TryParse(
                        currentConfiguration[
                            PublicAuthenticationRules.LoginEnabledKey],
                        out var enabled) && enabled;

                var currentVersion = isEmployee
                    ? 0
                    : GoogleLoginFlow.PublicSessionVersion(currentConfiguration);

                if (MustReject(context.Principal, loginEnabled) ||
                    PublicAuthenticationRules.IsPublicCustomer(context.Principal) &&
                    !GoogleLoginFlow.HasCurrentPublicSessionVersion(
                        context.Properties, currentVersion))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(
                        IdentityConstants.ApplicationScheme);
                    return;
                }

                var wasPublic =
                    PublicAuthenticationRules.IsPublicCustomer(context.Principal);

                // A public ticket cannot become an employee ticket,
                // and an employee ticket cannot become a public ticket.

                var originalExpiry = context.Properties.ExpiresUtc;

                await validateIdentityPrincipal(context);

                // Recheck after Identity refreshes the principal. Never
                // let a public cookie become an employee session.
                if (context.Principal is not null &&
                    (MustReject(context.Principal, loginEnabled) ||
                     wasPublic != PublicAuthenticationRules.IsPublicCustomer(
                         context.Principal)))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(
                        IdentityConstants.ApplicationScheme);
                    return;
                }

                if (wasPublic && context.Principal is not null)
                {
                    if (originalExpiry is null ||
                        originalExpiry <= DateTimeOffset.UtcNow)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(
                            IdentityConstants.ApplicationScheme);
                        return;
                    }

                    // Identity requests renewal after refreshing claims.
                    // CookieAuthenticationHandler normally recalculates the
                    // expiry from the ticket lifetime, extending the session.
                    // Explicitly issue the refreshed principal with the
                    // original absolute deadline instead.
                    context.Properties.ExpiresUtc = originalExpiry;
                    context.Properties.IsPersistent = false;
                    context.Properties.AllowRefresh = false;

                    if (context.ShouldRenew)
                    {
                        context.ShouldRenew = false;
                        context.Properties.IssuedUtc = DateTimeOffset.UtcNow;

                        await context.HttpContext.SignInAsync(
                            IdentityConstants.ApplicationScheme,
                            context.Principal,
                            context.Properties);
                    }
                }
            };

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
                        options.ClaimActions.MapJsonKey(
                            "urn:google:email_verified", "email_verified");
                    });
        }

        // A valid session is not sufficient to access Revestik's internal APIs.
        // PublicCustomer is deliberately NOT included in RoleNames.All.
        var internalUserPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireRole(RoleNames.All)
            .RequireAssertion(context =>
                PublicAuthenticationRules.IsEmployee(context.User))
            .Build();

        services.AddScoped<Revestik.Api.Services.Authentication.PublicGoogleSignInService>();

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(internalUserPolicy)
            .SetFallbackPolicy(internalUserPolicy)
            .AddPolicy(
                PolicyNames.AuthenticatedUser,
                policy => policy.RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        PublicAuthenticationRules.IsEmployee(context.User) ||
                        PublicAuthenticationRules.IsPublicCustomer(context.User)))
            .AddPolicy(
                PolicyNames.PublicCustomerOnly,
                policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                    PublicAuthenticationRules.IsPublicCustomer(context.User)))
            .AddPolicy(
                PolicyNames.AdministratorOnly,
                policy => policy.RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        PublicAuthenticationRules.IsEmployee(context.User))
                    .RequireRole(
                    RoleNames.Administrator))
            .AddPolicy(
                PolicyNames.ManageCustomers,
                policy => policy.RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        PublicAuthenticationRules.IsEmployee(context.User))
                    .RequireRole(
                    RoleNames.Administrator,
                    RoleNames.Accountant,
                    RoleNames.Sales))
            .AddPolicy(
                PolicyNames.ManageProjects,
                policy => policy.RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        PublicAuthenticationRules.IsEmployee(context.User))
                    .RequireRole(
                    RoleNames.Administrator,
                    RoleNames.Accountant,
                    RoleNames.Sales))
            .AddPolicy(
                PolicyNames.ManageInventory,
                policy => policy.RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        PublicAuthenticationRules.IsEmployee(context.User))
                    .RequireRole(
                    RoleNames.Administrator,
                    RoleNames.Accountant,
                    RoleNames.Sales,
                    RoleNames.Warehouse))
            .AddPolicy(
                PolicyNames.ManageProductCatalog,
                policy => policy.RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        PublicAuthenticationRules.IsEmployee(context.User))
                    .RequireRole(
                    RoleNames.Administrator,
                    RoleNames.Accountant,
                    RoleNames.Sales))
            .AddPolicy(
                PolicyNames.ManageSales,
                policy => policy.RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        PublicAuthenticationRules.IsEmployee(context.User))
                    .RequireRole(
                    RoleNames.Administrator,
                    RoleNames.Accountant,
                    RoleNames.Sales))
            .AddPolicy(
                PolicyNames.ManageQuotations,
                policy => policy.RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        PublicAuthenticationRules.IsEmployee(context.User))
                    .RequireRole(
                    RoleNames.Administrator,
                    RoleNames.Accountant,
                    RoleNames.Sales))
            .AddPolicy(
                PolicyNames.ManagePurchases,
                policy => policy.RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        PublicAuthenticationRules.IsEmployee(context.User))
                    .RequireRole(RoleNames.All))
            .AddPolicy(
                PolicyNames.ManagePurchasePayments,
                policy => policy.RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        PublicAuthenticationRules.IsEmployee(context.User))
                    .RequireRole(
                    RoleNames.Administrator,
                    RoleNames.Accountant))
            .AddPolicy(
                PolicyNames.ManageExpenses,
                policy => policy.RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        PublicAuthenticationRules.IsEmployee(context.User))
                    .RequireRole(
                    RoleNames.Administrator,
                    RoleNames.Accountant))
            .AddPolicy(
                PolicyNames.ManageAccountingClassification,
                policy => policy.RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        PublicAuthenticationRules.IsEmployee(context.User))
                    .RequireRole(
                    RoleNames.Administrator,
                    RoleNames.Accountant))
            .AddPolicy(
                PolicyNames.ViewAuditLog,
                policy => policy.RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        PublicAuthenticationRules.IsEmployee(context.User))
                    .RequireRole(
                    RoleNames.Administrator));

        return services;
    }
}
