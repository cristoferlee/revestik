# PREPROD-FIX-HTTPS-02 — Review gate

## Source changes

- Preserves existing explicit-IP proxy mode and disabled-by-default behavior.
- Adds optional `ReverseProxy__TrustDynamicAzureIngress=true` for changing Azure Container Apps ingress peers.
- In either mode only `X-Forwarded-Proto` is handled, with `ForwardLimit=1`. Never trust `X-Forwarded-For` or `X-Forwarded-Host`.
- Dynamic mode checks for `CONTAINER_APP_NAME` and rejects simultaneous explicit-IP configuration; these are guardrails, **not** proof of network isolation.

## Critical security condition — NOT YET APPROVED FOR AZURE

Dynamic mode clears `KnownProxies` and `KnownIPNetworks`, so a caller that can reach Kestrel directly could forge the scheme. The currently configured ingress restriction (`177.93.21.181/32`) governs public ingress only and **does not prove internal traffic isolation**. Do not enable this flag or deploy the change for production traffic until all direct paths to container port 8080 have been assessed, including same-environment workloads and VNet connectivity. Follow Microsoft ASP.NET Core forwarded headers security guidance.

## Verification

1. Extract complete paths at the repository root, backing up locally first if necessary.
2. `dotnet build Revestik.sln`
3. `dotnet test tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj --filter "FullyQualifiedName~ProductionHostingTests"`
4. Full suite with controlled concurrency, and a security review.
5. Do not deploy to Azure or enable the opt-in flag until the trust-boundary review is approved. No Git commit/push in preproduction.

If isolation cannot be substantiated, prefer an ingress architecture that explicitly authenticates upstream requests or a proxy with stable trusted network ranges, instead of this dynamic mode.
