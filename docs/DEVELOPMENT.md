# Local development and verification

## Prerequisites

- .NET 10 SDK and Git.
- SQL Server suitable for EF Core development and integration testing.
- Docker Desktop/WSL2 or equivalent Docker environment for Testcontainers-backed tests.
- Windows PowerShell is the established local workflow.
- Local Google OAuth and other integration credentials where those features are exercised. Keep secrets in user secrets or an approved secure configuration mechanism.

## Build

```powershell
cd C:\Projects\Revestik
dotnet tool restore
dotnet restore Revestik.sln
dotnet build Revestik.sln
```

## Local application

Run the API and client in separate terminals:

```powershell
dotnet run --project src/Revestik.Api/Revestik.Api.csproj --launch-profile https
```

```powershell
dotnet run --project src/Revestik.Client/Revestik.Client.csproj --launch-profile https
```

The development setup has used API `https://localhost:7081` and client `https://localhost:5181`. Check current `launchSettings.json` and OAuth redirect configuration before relying on these addresses. Production serves the published client from the API origin.

## Tests

```powershell
dotnet test tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj --no-restore
```

Example focused authentication regression:

```powershell
dotnet test tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj --filter "FullyQualifiedName~Authentication" --no-restore
```

Test counts are historical evidence, not a permanent badge. At the documentation checkpoint, the developer reported **1,073 passing tests** for the local authentication-recovery change. Re-run tests on the exact code being released. Docker/Testcontainers and SQL readiness can affect integration test execution.

## Data and migrations

EF Core migrations require explicit review of schema impact, data preservation, target database, and deployment order. Do not automatically apply destructive migrations or point local tools at production. Avoid exposing customer data in fixtures and diagnostics.

## Contribution and change control

Read [AGENTS.md](../AGENTS.md) and follow **INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP**. Check `git status -sb` before editing. Preserve unrelated local changes. Use scoped staging rather than `git add .`; never commit private storage, backups, tokens, generated artifacts, or secrets.

## Troubleshooting

Capture the exact route, HTTP status, sanitized logs, Git SHA, test command, and reproduction steps. Distinguish a code defect from a SQL cold start, unavailable external integration, expired session, or misconfigured environment. Do not claim that a retry or workaround is deployed until the production revision is verified.
