# Local development and verification

## Prerequisites

- .NET 10 SDK and Git.
- SQL Server suitable for EF Core development and integration testing.
- Docker Desktop/WSL2, or an equivalent Docker environment, for Testcontainers-backed tests.
- Windows PowerShell is the established local workflow.
- Local Google OAuth credentials and any other integration credentials needed for the features being exercised. Keep secrets in user secrets or another approved secure configuration mechanism; never commit them.

## Solution layout

Revestik is one .NET 10 modular-monolith solution with four application projects:

| Project | Local responsibility |
| --- | --- |
| `src/Revestik.Api` | Main ASP.NET Core host, Minimal APIs, Identity, domain services, persistence, integrations, and unified site hosting |
| `src/Revestik.Client` | Authenticated administration built with Blazor WebAssembly, served under `/administracion` by the API host |
| `src/Revestik.Public` | Customer-facing Razor Components site using server-side rendering and selected interactive server components |
| `src/Revestik.Shared` | Shared request/response contracts and application DTOs; not EF Core entities |

`tests/Revestik.Api.Tests` is the separate automated test project. `Revestik.Public` also contains a standalone development entry point, but **running Client, Public, and API as three simultaneous processes is no longer the standard workflow**.

## Build

From the repository root:

```powershell
cd C:\Projects\Revestik
dotnet tool restore
dotnet restore Revestik.sln
dotnet build Revestik.sln
```

## Run locally: one API process

In the normal development workflow, start **only** the API host:

```powershell
cd C:\Projects\Revestik
dotnet run --project src/Revestik.Api/Revestik.Api.csproj --launch-profile https
```

The checked-in `src/Revestik.Api/Properties/launchSettings.json` declares:

| Local address | Purpose |
| --- | --- |
| `https://localhost:7126/` | Unified ASP.NET Core host and public-site routes |
| `https://localhost:7126/administracion/` | Administrative Blazor WebAssembly application |
| `https://localhost:7126/administracion/login` | Administrative Google OAuth login screen |
| `https://localhost:7126/api/health` | API/database health endpoint |
| `https://localhost:7126/es` | Spanish public home during local development |
| `https://localhost:7126/es/acabados` | Spanish public finishes/products page |
| `http://localhost:5029` | Additional HTTP listener from the API `https` launch profile; HTTPS is preferred |

The API hosts the administrative WASM assets at `/administracion` and the public Razor Components routes in the same application. Public pages may be exercised in local development, but **the production public root is not launched for visitors**: the owner reports that `https://revestikcr.com` currently returns HTTP 401. In production, `https://revestikcr.com/administracion` is the administrative entry point with Google OAuth.

The older standalone launch profiles for `Revestik.Client` (`https://localhost:7081`, `http://localhost:5181`) and the optional standalone `Revestik.Public` entry point remain relevant only to explicitly scoped diagnostics or integration testing. They must not be presented as mandatory processes in the normal local setup. The public SSR reader uses the configured `PublicCatalog:ApiBaseUrl`, defaulting to `https://localhost:7126/` in Development and a loopback API origin in the unified production host. Check effective local configuration and registered OAuth redirect URIs if authentication or catalog requests fail; do not change CORS, cookies, callback URLs, or production secrets simply to work around a local mismatch.

## Tests

Run the automated suite from the repository root:

```powershell
dotnet test Revestik.sln --configuration Release
```

Or target the API test project:

```powershell
dotnet test tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj
```

Example focused authentication regression:

```powershell
dotnet test tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj --filter "FullyQualifiedName~Authentication"
```

CI restores and builds the solution, runs `dotnet test Revestik.sln --configuration Release --no-build`, publishes `Revestik.Api`, and verifies that the `/administracion` WebAssembly assets are included. The GitHub CI run for commit `581aef9c99ddb7bbc1fffcbe49ec924699c980a2` completed successfully. The owner additionally reported **1,199/1,199 passing local regression tests** and **17/17 manual security checks** for the most recent public editor work. These are historical results for that revision, not evidence that tests have just been rerun or that the latest changes are deployed to Azure. SQL/Testcontainers readiness can affect integration tests.

## Database and migrations

EF Core migrations must be reviewed for schema and data impact, database target, deployment order, and recoverability. In particular, `20261104230000_ConsolidateSharedProductTaxonomy` consolidates previously separate taxonomy data and deliberately does **not** support an automatic down migration; applying it to a populated production database requires a separately approved data review and a verified backup/recovery plan. `20261109194500_AllowFinishesHeroSlots` extends page-hero storage to include the Acabados slots. Do not infer that either migration is applied to Azure merely because its file exists in `main` or tests pass. Never point local migration commands at production without explicit authorization.

## Public site and visual editor status

The public homepage, navigation, and product/finishes section are implemented in the repository; the complete public website is still under development and is **not approved for a public production beta launch**. Administrative editing requires an authenticated `Administrator`; a browser edit-mode preference does not grant authorization. Legacy `iframe` preview and bridge files remain temporarily in the tree but are scheduled for removal after verification of the integrated editor. Do not add new dependencies on that legacy bridge.

## Contribution and change control

Read [AGENTS.md](../AGENTS.md) and follow **INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP**. Check `git status -sb` before editing. Preserve unrelated local changes. Use scoped staging rather than `git add .` or `git add -A`; never commit private storage, backups, tokens, generated artifacts, or secrets. Commits, pushes, database migrations, and deployments require separate approval.

## Troubleshooting

Capture the exact URL/path, HTTP status, sanitized logs, Git SHA, test command, and reproduction steps. Distinguish a code defect from SQL Serverless cold start, unavailable external integration, expired Identity session, stale browser assets, or misconfigured development settings. Remember that anonymous public routes and authenticated administrative APIs have different trust boundaries; static admin assets may be served without making private API data anonymous. Do not claim that a locally verified change is deployed until the actual production image/revision and smoke checks are confirmed.