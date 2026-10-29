using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Revestik.Api.Extensions;

public static class RevestikRateLimiting
{
    public const string Login = "login-google";
    public const string Health = "health";
    public const string GmailSync = "gmail-sync";
    public const string DocumentUpload = "document-upload";

    public static IServiceCollection AddRevestikRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers["Retry-After"] =
                        Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };
            // Broad global budget; specific high-cost operations have tighter named limits.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    UserOrAddress(context), _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 600,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            options.AddPolicy(Login, context => RateLimitPartition.GetFixedWindowLimiter(
                Address(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.AddPolicy(Health, context => RateLimitPartition.GetFixedWindowLimiter(
                Address(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.AddPolicy(GmailSync, context => RateLimitPartition.GetFixedWindowLimiter(
                UserOrAddress(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 3,
                    Window = TimeSpan.FromMinutes(5),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.AddPolicy(DocumentUpload, context => RateLimitPartition.GetFixedWindowLimiter(
                UserOrAddress(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });
        return services;
    }

    private static string UserOrAddress(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true &&
        !string.IsNullOrWhiteSpace(context.User.FindFirstValue(ClaimTypes.NameIdentifier))
            ? "user:" + context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            : "ip:" + Address(context);

    // Do not accept untrusted X-Forwarded-For headers. Configure trusted proxies in DEPLOY-01.
    private static string Address(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
