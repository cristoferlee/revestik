using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Revestik.Api.Configuration;

/// <summary>
/// Reverse proxy configuration. Dynamic mode accepts X-Forwarded-Proto from any
/// direct peer and MUST only be enabled when the Azure Container Apps ingress
/// is the exclusive network path to Kestrel and overwrites that header.
/// </summary>
public static class RevestikForwardedHeaders
{
    public static void Configure(
        ForwardedHeadersOptions options,
        bool trustDynamicIngress,
        IEnumerable<IPAddress> trustedProxies)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        if (!trustDynamicIngress)
        {
            foreach (var address in trustedProxies)
                options.KnownProxies.Add(address);
        }
        // With both trust collections empty, ASP.NET Core trusts all immediate
        // peers for this one header. Do not enable dynamic mode based only on an
        // ingress IP restriction; separately audit possible internal routes.
    }
}
