# Deployment and operations

## Operational status

Revestik has been deployed as an **internal production application** using Azure Container Apps and Azure SQL Database. This document describes repository-visible runtime requirements and operational checks; it does not attest that every live Azure setting, backup, alert, or recovery procedure has been independently audited.

## Build and release checks

From the repository root:

```powershell
dotnet restore Revestik.sln
dotnet build Revestik.sln --configuration Release --no-restore
dotnet test tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj --configuration Release --no-restore
dotnet publish src/Revestik.Api/Revestik.Api.csproj --configuration Release --no-restore --output artifacts/publish
```

The test suite may require Docker/Testcontainers. Do not deploy based on compilation alone. Record the exact Git SHA, image digest/revision, test results, and environment-specific release steps. Do not assume the repository's example Azure manifest is identical to the currently running configuration.

## Hosting behavior

The ASP.NET Core API serves the published Blazor WebAssembly client in non-development environments. SPA routes fall back to `index.html`; unknown `/api/*` routes return an API 404. The client entry point is configured to avoid stale HTML caching. HTTPS, HSTS, secure headers, and exception handling are configured for production.

Reverse-proxy forwarding is explicitly configured and opt-in. A dynamic Azure ingress trust mode is guarded by runtime checks and must not be enabled without verifying network isolation and trusted ingress behavior. Never copy preproduction proxy or IP settings into production without review.

## Mandatory durable storage and secrets

In production, `RevestikStorage:RootPath` must identify an existing absolute, mounted durable directory. The API uses this root for:

- `received-documents-inbox/` — private received-document staging.
- `gmail/` — Gmail and bank-voucher synchronization state.
- `project-documents/` — private project files.
- `dataprotection-keys/` — persisted application Data Protection key ring.

A certificate with a private key is required to protect the persistent key ring. The SQL connection string, OAuth credentials, Gmail credentials, certificate and password must be injected securely, not committed. Verify mounted volume permissions and backup coverage independently.

## Database and migrations

- Verify the intended SQL target before running migrations.
- Review schema/data impact and compatibility with the current application version.
- Ensure appropriate backup and restoration procedures exist before destructive changes.
- Treat Azure SQL Serverless auto-pause/cold starts as possible operational factors; verify live settings before making performance or availability claims.
- A database rollback does not necessarily undo external integrations or changes to private files.

## Health and smoke verification

The API exposes `GET /api/health`, which checks SQL connectivity and returns healthy status when the database is reachable; a failed connection may return HTTP 503. After deployment verify:

1. Container revision readiness and HTTP health response.
2. Google sign-in, session continuity, sign-out and authorized/unauthorized access.
3. CSRF behavior and server-side role restrictions.
4. Key commercial operations, FIFO inventory and closed-project write restrictions.
5. Private document access, PDF generation, Gmail progress/retry and durable state.
6. Application logs and relevant errors, without exposing sensitive values.

Perform smoke tests against non-sensitive records and record the results. A passing health check alone is not sufficient release evidence.

## Recovery and operational risks

Confirm and document actual owners, alert routes, Azure SQL backup retention, file-volume backup, restoration tests, RPO/RTO, certificate/key-ring recovery, and rollback steps. These items are **verification requirements**, not assertions that an audited disaster-recovery program exists.

The Gmail background worker coordinates one job per API process. Multi-replica deployment requires a distributed lease or equivalent coordination before enabling parallel replicas.

## Pending local work

As of the documentation baseline, an authentication error-recovery improvement (AUTH-PROD-01A) was reported validated locally, with a full regression of **1,073 passing tests**. It is not represented here as deployed; confirm the commit and production revision separately before changing that status.
