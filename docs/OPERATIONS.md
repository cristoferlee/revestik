# Deployment and operations

This is an operational guide, not a claim that a production environment is already configured. Confirm live hosting, retention and recovery procedures before release.

## Release pipeline

```powershell
dotnet restore Revestik.sln
dotnet build Revestik.sln --configuration Release --no-restore
dotnet test Revestik.sln --configuration Release --no-build
dotnet publish src/Revestik.Api/Revestik.Api.csproj --configuration Release --no-restore --output artifacts/publish
```

Published ASP.NET Core API serves the Blazor WebAssembly application. Ensure client routes use the SPA fallback while unknown `/api/*` requests remain API responses. Validate static asset and cache behavior after deployment.

## Environment and secrets

Configure production SQL Server, cookie/Data Protection persistence, Google OAuth/Gmail integration, HTTPS and any required document storage separately from development settings. Avoid putting secrets into client assets, source, build logs or repositories.

## Database and data preservation

Plan schema compatibility, controlled migrations, backup/restore and rollback before releases. Database rollback does not necessarily revert external side effects or privately stored documents. Test restoration procedures before relying on them.

## Release verification

Validate startup, database reachability, authentication, authorization, CSRF, critical financial write locks, health checks, private document access, logging, background integration health when deployed, and monitored failure signals. Record the exact artifact/commit and tests executed.

## Recovery ownership

Before production go-live, define hosting owner, alert routing, backup frequency/retention, recovery objectives, document-store recovery and incident response. Do not invent unapproved numeric service levels or claim unimplemented controls exist.
