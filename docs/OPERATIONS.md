# Deployment and operations

## Operational status and release boundaries

Revestik is a **.NET 10 modular monolith** hosted on Azure Container Apps with Azure SQL Database. The solution contains four application projects: `Revestik.Api`, `Revestik.Client`, `Revestik.Public`, and `Revestik.Shared`; its automated tests live in `tests/Revestik.Api.Tests`.

The original authenticated business application was deployed at `app.revestikcr.com`. During public-website development, the separate domain and cross-origin `iframe` editor bridge proved difficult to maintain. The approved target architecture consolidates the business application at **`https://revestikcr.com/administracion`** and hosts the public site in the same `Revestik.Api` application. The old `iframe` bridge is legacy code pending separately approved removal, not the intended long-term editing architecture.

**Production status reported by the owner on 2026-11-10:**

- `https://revestikcr.com/administracion` serves the administrative experience, whose entry point is Google OAuth sign-in; business access remains restricted to authorized users.
- `https://revestikcr.com/` currently returns **HTTP 401**. The customer-facing public website is **not approved for production launch**.
- Public Home, shared navigation, and the Finishes/products section are implemented in the repository but remain part of the unfinished public-site beta. Their presence in `main` does not prove that the corresponding functionality or database migrations have been deployed.
- A prior Azure revision was reported healthy, but the exact Git SHA and database migration state of the **currently active** production image must be verified before any new release. Locally built images do not necessarily retain commit metadata.

Distinguish **implemented in source**, **tested**, **deployed**, and **publicly released**. Do not infer one state from another. Repository-visible configuration, tests, and example deployment manifests are not an independent audit of live Azure settings.

## Build and release checks

From the repository root, on the exact candidate commit:

```powershell
cd C:\Projects\Revestik
git status -sb
git rev-parse HEAD
dotnet restore Revestik.sln
dotnet build Revestik.sln --configuration Release --no-restore
dotnet test Revestik.sln --configuration Release --no-build --verbosity normal
dotnet publish src/Revestik.Api/Revestik.Api.csproj --configuration Release --no-build --output artifacts/publish
```

The suite includes xUnit and SQL Server integration tests; Docker/Testcontainers may be required. CI additionally verifies that the published administrative Blazor assets exist under `artifacts/publish/wwwroot/administracion/`. A prior owner-reported **1,199/1,199** local regression and passing GitHub Actions CI are historical evidence, **not** proof of the tests or deployed behavior for a future revision.

Before any approved deployment, record the source Git SHA, image digest, Azure revision, selected environment, test results, migration plan, and smoke-test outcomes. Compile and test success alone does not authorize deployment. Do not commit, push, run production migrations, alter DNS, or deploy without separate approval.

## Unified hosting and routes

`Revestik.Api` is the primary application host, serving the following concerns from one ASP.NET Core runtime:

| Surface | Implementation | Access and operational notes |
| --- | --- | --- |
| `/administracion` | `Revestik.Client` (Blazor WebAssembly) | Administrative SPA and Google OAuth entry flow. Server-side API authorization protects business data and mutations; serving HTML/static files is not itself proof of authentication. |
| `/es`, `/en`, `/de` and their site routes | `Revestik.Public` (Razor Components SSR) | Customer-facing site under active development. Public release remains blocked until the beta and release checks are approved. |
| `/api/*` | `Revestik.Api` (Minimal APIs) | Business, identity, public read, and restricted admin endpoints. Unknown `/api/*` routes must not fall back to the administrative SPA. |
| `/api/health` | API health endpoint | Used for HTTP and SQL connectivity verification; it is not a comprehensive application smoke test. |

`Revestik.Shared` contains shared contracts, not a standalone process. In the established local development flow, run **only `Revestik.Api`**; it hosts the public pages and the compiled administrative client. The checked-in API HTTPS launch profile uses `https://localhost:7126` (HTTP `http://localhost:5029`). The standalone `Client` and `Public` entry points remain available for specialized development and tests but are not required as three concurrently running apps. See [DEVELOPMENT.md](DEVELOPMENT.md).

The public SSR readers call anonymous, read-only catalog endpoints through an internal API origin: by default `https://localhost:7126/` in development and loopback `http://127.0.0.1:8080/` in the unified non-development host. `PublicCatalog:ApiBaseUrl` can override the origin in supported environments; HTTP is restricted to loopback. These internal requests must not carry administrative credentials.

The production host uses HTTPS redirection, HSTS, secure response controls, explicit reverse-proxy forwarding rules, and targeted static-asset caching. Reverse-proxy trust must be validated against the actual Azure ingress/network configuration; do not copy preproduction proxy settings into production. Recheck OAuth redirect origins and callback routes whenever the hosting domain or path changes. `app.revestikcr.com` is the superseded administrative address and must not be reintroduced as a separate required service.

## Mandatory durable storage and secrets

In production, `RevestikStorage:RootPath` must reference an **existing, absolute, mounted, durable and private** directory. Relevant paths include:

- `received-documents-inbox/` — received tax-document staging.
- `gmail/` — Gmail and bank-voucher synchronization state.
- `project-documents/` — private customer project attachments.
- `dataprotection-keys/` — persisted application cookie/Data Protection keys.
- `public-catalog/pending/` — temporarily staged public catalog media.
- `public-catalog/active/` — committed public catalog images and related media.

In development, public catalog media is stored under the API's `App_Data/public-catalog/`; do not treat local paths as production storage. Public catalog image uploads are validated and limited to 5 MiB per file. The hosted cleanup worker handles stale pending files and orphaned active media according to its implementation. Backups, storage mounts, permissions and restore procedures must cover both SQL metadata and the **corresponding image files**. A SQL backup alone cannot restore lost image bytes.

Production Data Protection keys require certificate protection with a private key. Store SQL credentials, Google OAuth settings, integration tokens, Data Protection certificates/passwords, and other secrets through approved protected configuration—not in Git, published web assets, diagnostic output, ZIPs, or Markdown documentation. Verify volume access and certificate continuity across restarts and revisions.

## Database and migrations

EF Core migrations require a target-specific review, backup/restore readiness and explicit approval **before** modifying production SQL. Confirm the active production revision's expected schema and compare it with the database migration history; never infer migration application from the presence of files in Git.

The public catalog adds particular migration risks:

- `20261103201149_AddPublicCatalogFoundation` and `20261103204137_AddPublicCatalogImages` introduce the original public catalog data and media models.
- `20261104120000_AddPublicHomeHeroSlides` and `20261104180000_AddPublicHomeSections` add editable site content.
- `20261104201623_AddSharedProductTaxonomy` introduces inventory/public shared subcategories and transitional bindings.
- `20261104230000_ConsolidateSharedProductTaxonomy` transforms existing classifications and removes duplicated legacy taxonomy tables. Its migration explicitly rejects ambiguous or unsafe data and **cannot be automatically rolled back without risk of data loss**. Review real production data and arrange verified backup restoration before approving its execution.
- `20261109194500_AllowFinishesHeroSlots` extends Home hero slots `1–3` to include independent Finishes slots `4–6`. Its down migration rejects rollback while Finishes slots are populated.

Existing category/subcategory IDs, slugs, image associations and visibility are business-sensitive data. Do not use destructive schema resets or automatic migration-on-deploy as a substitute for reviewing them. A database rollback also does not restore external integrations or private files automatically. SQL Serverless cold starts may affect availability; confirm current Azure SQL settings before attributing failures to them.

## Release and smoke verification

For an **administrative-only** release, verify and record, at minimum:

1. The intended Azure Container Apps revision is ready, receives the expected traffic, and corresponds to the approved image digest and source revision.
2. `GET /api/health` responds as expected, including SQL connectivity.
3. `/administracion` loads the Google OAuth sign-in entry; authorized login, redirect, session continuation, logout and expired/unauthorized behavior work on the current domain.
4. Business API endpoints enforce role/policy checks and relevant CSRF protection. Validate core quotations/sales, FIFO inventory, purchases, project restrictions and private documents with non-sensitive test data.
5. Persistent storage, PDF generation, Gmail import/checkpoint behavior and relevant logs remain healthy without exposing credentials or customer data.
6. The **current public-release gate remains intact**: the owner-reported root `/` 401 should not be inadvertently replaced by an openly accessible beta. Inspect localized public routes separately instead of assuming that root behavior proves every route is restricted.

The **public beta launch requires separate approval** after content completion, mobile/accessibility and image checks, navigation/search/product detail validation, administrator-only editing checks, public/private data separation, SEO and `noindex` policy review, and a production-specific migration/backup and smoke-test plan. Do not describe Home/navbar/products being implemented as a completed public website launch.

The inline public editor is restricted by the authenticated `Administrator` role and server-side CSRF/authorization checks. The browser edit-mode preference is **not** an authorization mechanism. Legacy `iframe` preview scripts and bridge files are pending cleanup **after** replacement verification, under a separately approved code-change scope.

## Recovery and outstanding operational risks

Confirm alert owners, Azure SQL backup retention, persistent-volume backups, tested restores, RPO/RTO, certificate and Data Protection recovery, OAuth configuration, monitoring and rollback procedures for the actual target environment. These are required verifications, **not** claims of independently audited disaster-recovery readiness.

The Gmail worker coordinates one active synchronization job **per API process**. Running multiple active replicas requires distributed coordination or a separately reviewed design; do not scale replicas assuming the existing process-local lock is sufficient.

Known areas for future, separately approved investigation include legacy `iframe` removal, public-image persistence after hosting changes, public SEO/indexing release gates, and validating how unpublished featured products affect subsequent Home edits. Keep these separate from the present documentation update and from any production-release authorization.