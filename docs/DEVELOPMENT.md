# Local development and verification

## Prerequisites

- .NET 10 SDK, Git, SQL Server and suitable local credentials.
- Windows PowerShell is the project's established local shell. Keep production secrets out of source control.
- The repository uses local .NET tools where configured.

## Build and test

From `C:\Projects\Revestik`:

```powershell
dotnet tool restore
dotnet restore Revestik.sln
dotnet build Revestik.sln
dotnet test tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj --no-build
```

Run focused tests before a relevant module's regression; run the full suite when justified by cross-module changes or release. Some SQL integration tests depend on Docker/Testcontainers and compatible SQL Server setup. Do not interpret incomplete runs as passing.

## Data and migrations

EF Core migrations must be reviewed for schema and data impact before application. Confirm database target, backup and deployment conditions. Never invent or apply destructive migrations automatically.

## Working agreements

Review `AGENTS.md` before changes. Submit a bounded INVESTIGATE/PLAN for approval. Use repository-relative files and avoid overwriting newer local code with earlier GitHub versions. Run smoke tests sequentially and record actual results. Never commit private storage, generated binaries or secrets.

## Troubleshooting

When a workflow fails, capture the route, HTTP status, relevant sanitized logs, current SHA and reproduction. Keep credentials, customer records and uploaded documents out of diagnostics.
