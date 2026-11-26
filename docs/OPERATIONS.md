# Deployment and operations

## Operational status and release boundaries

Revestik is a **.NET 10 modular monolith** hosted on Azure Container Apps with Azure SQL Database. The solution contains four application projects: `Revestik.Api`, `Revestik.Client`, `Revestik.Public`, and `Revestik.Shared`; its automated tests live in `tests/Revestik.Api.Tests`.

The unified host serves administration at `https://revestikcr.com/administracion`. The former administrative subdomain was retired from ingress and DNS. The legacy administrative iframe editor and message bridge were retired in commit `9cbd7d4`; the independent PDF preview iframe is not part of that cleanup.

**Production status reported by the owner on 2026-11-26:**

- `https://revestikcr.com/administracion` serves the administrative experience, whose entry point is Google OAuth sign-in; business access remains restricted to authorized users.
- On 2026-11-26 the owner verified that the public root redirects to `/es` and returns HTTP 200. Anonymous reachability is not formal commercial launch approval; indexing and public authentication changes require separate approval.
- Public website, cart and editorial features are deployed in the current artifact; complete functional acceptance is outstanding.
- Verified revision `ca-revestik-prod--hotfix-sql-68638b8`, source `68638b8`, 58/58 migrations, SQL Standard S0. See the Azure checkpoint for exact evidence.

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
| `/api/health` | SQL-independent liveness | Does not establish database connectivity; `/api/health/database` is Administrator-only. |

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

In development, public catalog media is stored under the API's `App_Data/public-catalog/`. New uploads are limited to 5 MiB, 8192 pixels per side and 24 million pixels. The API decodes JPEG, PNG, WebP and GIF, applies EXIF orientation and writes only a static WebP at quality 82. It processes one upload at a time and asks concurrent callers to retry. The maximum output side is 1920 pixels for product and project images and 2560 for heroes. Existing media retains its original format and storage key.

The worker removes expired pending files after 24 hours without querying SQL. Under **Configuración → Imágenes del sitio público**, administrators can explicitly scan for active files older than 24 hours and confirm deletion of up to 50 candidates per operation. The scan checks at most 5,000 files and shows up to 500 candidates. The recoverable total applies to the displayed candidates. Re-run discovery if selection tickets expire after 15 minutes. Deletion rechecks references and file attributes under the same in-process lock as publication. Review coordination before deploying any topology with overlapping API processes, including overlapping revisions.

Backups and restores must cover SQL metadata and the corresponding image files. To recover from unintended loss, restore the corresponding media from backup and verify the associated SQL metadata before exposing the restored file. A SQL backup alone cannot restore lost bytes.

The API publishes matching versions of `SkiaSharp` and `SkiaSharp.NativeAssets.Linux.NoDependencies`. The Linux image also contains QuestPDF's separate native renderer. Verify both libraries in the exact published image. A previous API revision that validates active keys as bare GUIDs cannot read new `GUID.webp` files. Once uploads have run on the new revision, retain a rollback build that can read both key formats, or restore coordinated SQL and media backups under a separately approved recovery plan.

Production Data Protection keys require certificate protection with a private key. Store SQL credentials, Google OAuth settings, integration tokens, Data Protection certificates/passwords, and other secrets through approved protected configuration—not in Git, published web assets, diagnostic output, ZIPs, or Markdown documentation. Verify volume access and certificate continuity across restarts and revisions.

## Database and migrations

EF Core migrations require a target-specific review, backup/restore readiness and explicit approval **before** modifying production SQL. Confirm the active production revision's expected schema and compare it with the database migration history; never infer migration application from the presence of files in Git.

The public catalog adds particular migration risks:

- `20261103201149_AddPublicCatalogFoundation` and `20261103204137_AddPublicCatalogImages` introduce the original public catalog data and media models.
- `20261104120000_AddPublicHomeHeroSlides` and `20261104180000_AddPublicHomeSections` add editable site content.
- `20261104201623_AddSharedProductTaxonomy` introduces inventory/public shared subcategories and transitional bindings.
- `20261104230000_ConsolidateSharedProductTaxonomy` transforms existing classifications and removes duplicated legacy taxonomy tables. Its migration explicitly rejects ambiguous or unsafe data and **cannot be automatically rolled back without risk of data loss**. Review real production data and arrange verified backup restoration before approving its execution.
- `20261109194500_AllowFinishesHeroSlots` extends Home hero slots `1–3` to include independent Finishes slots `4–6`. Its down migration rejects rollback while Finishes slots are populated.

Existing taxonomy IDs, slugs, image associations and visibility are business-sensitive. Do not use destructive resets or automatic migration-on-deploy. Database rollback does not restore external integrations or files. Production SQL is Standard S0 without Serverless auto-pause.

## Release and smoke verification

For an **administrative-only** release, verify and record, at minimum:

1. The intended Azure Container Apps revision is ready, receives the expected traffic, and corresponds to the approved image digest and source revision.
2. `GET /api/health` responds as expected, including SQL connectivity.
3. `/administracion` loads the Google OAuth sign-in entry; authorized login, redirect, session continuation, logout and expired/unauthorized behavior work on the current domain.
4. Business API endpoints enforce role/policy checks and relevant CSRF protection. Validate core quotations/sales, FIFO inventory, purchases, project restrictions and private documents with non-sensitive test data.
5. Persistent storage, PDF generation, Gmail import/checkpoint behavior and relevant logs remain healthy without exposing credentials or customer data.
6. The **current public-release gate remains intact**: the owner-reported root `/` 401 should not be inadvertently replaced by an openly accessible beta. Inspect localized public routes separately instead of assuming that root behavior proves every route is restricted.

The **public beta launch requires separate approval** after content completion, mobile/accessibility and image checks, navigation/search/product detail validation, administrator-only editing checks, public/private data separation, SEO and `noindex` policy review, and a production-specific migration/backup and smoke-test plan. Do not describe Home/navbar/products being implemented as a completed public website launch.

The inline editor requires Administrator and server-side CSRF/authorization checks. The legacy administrative iframe editor and message bridge were retired in commit `9cbd7d4`; the independent PDF preview iframe is not part of that cleanup.

## Recovery and outstanding operational risks

Confirm alert owners, Azure SQL backup retention, persistent-volume backups, tested restores, RPO/RTO, certificate and Data Protection recovery, OAuth configuration, monitoring and rollback procedures for the actual target environment. These are required verifications, **not** claims of independently audited disaster-recovery readiness.

The Gmail worker coordinates one active synchronization job **per API process**. Running multiple active replicas requires distributed coordination or a separately reviewed design; do not scale replicas assuming the existing process-local lock is sufficient.

Remaining operational work is tracked in [the final closure plan](REVESTIK-FINAL-CLOSURE-PLAN.md); no additional infrastructure change is approved.

## Current capacity and recovery evidence

SQL Standard S0 (10 DTU, 250 GiB configured maximum), ACA 0.5 vCPU / 1 GiB / min=max=1. SQL short-term retention 7 days, differential interval 12 hours, local backup redundancy. Independent BACPAC export and local restore verified; Azure PITR and Azure Files restore remain untested. Protect backups outside Git; no production secrets or data in reports. See [the final closure plan](REVESTIK-FINAL-CLOSURE-PLAN.md) and [Azure checkpoint](AZURE-COST-OPTIMIZATION-CHECKPOINT-20261126.md).

## Public authentication shutdown and reopening

The configuration keys are `Authentication:PublicGoogleLoginEnabled`, `Authentication:PublicGoogleRegistrationEnabled`, and `Authentication:PublicSessionVersion` (environment variables use double underscores). Both flags default to false. To close registration only, leave login enabled and set registration false. To revoke all public sessions, disable both flags and increase the positive integer session version in the same configuration rollout. Never reuse or decrease a version; reopening uses the increased version. Confirm all serving revisions use the same settings before reopening. Invalid versions block public sessions; internal sessions retain their independent configuration. Verify anonymous browsing/PDF, internal login, existing public login with registration closed, and rejection of old public cookies before declaring the change successful. No production changes are implied by this procedure.
