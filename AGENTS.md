# Revestik — agent instructions and current checkpoint

## Sources of truth

Inspect the current repository, tests, migrations, Git working tree, and deployment evidence before making changes. Source code and verified execution results prevail over outdated checkpoints. Consult `docs/ARCHITECTURE.md`, `docs/BUSINESS_RULES.md`, `docs/SECURITY.md`, `docs/DEVELOPMENT.md`, `docs/OPERATIONS.md`, and accepted ADRs in `docs/adr/`. The historical public-site decisions are recorded in `docs/PUBLIC-WEB-01-MASTER-CHECKPOINT.md`; older status statements in that file may be superseded.

## Mandatory workflow

**INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP.**

- **INVESTIGATE:** Trace the real code, endpoints, services, contracts, roles, persistence, dependencies, tests, and affected runtime paths. Distinguish confirmed facts, owner decisions, assumptions, and outstanding evidence. Do not edit while investigating.
- **PLAN:** Define the smallest bounded scope, affected files, security/domain impact, migration and data risks, and acceptance criteria. Prefer simplification and reuse over adding another layer or duplicate workflow.
- **APPROVE:** Obtain explicit owner approval before implementing; request separate approval if the scope changes.
- **IMPLEMENT:** Make minimal, focused changes. Preserve unrelated changes and provide complete repository-relative files when requested. Do not create installers, scripts, ZIPs, or additional deliverables unless requested.
- **VERIFY:** Build, run focused and appropriate regression tests, validate SQL/HTTP/CSRF/concurrency where applicable, perform ordered smoke tests, and inspect the exact Git diff. Report only tests actually executed and distinguish local from production verification.
- **STOP:** Report what changed, what passed, what remains unknown, and the next approval boundary. Never commit, push, deploy, run production migrations, or delete files without separate authorization.

## Solution architecture

Revestik is a **.NET 10 modular monolith** with four application projects:

| Project | Responsibility |
| --- | --- |
| `Revestik.Api` | ASP.NET Core application host; Minimal APIs, Identity/Google OAuth, authorization, EF Core/SQL Server, business services, integrations, files, and published web applications. |
| `Revestik.Client` | Authenticated administrative interface built with Blazor WebAssembly, hosted under `/administracion`. |
| `Revestik.Public` | Multilingual public website built with ASP.NET Core Razor Components/SSR and interactive server-side editing components. |
| `Revestik.Shared` | Shared transport contracts and DTOs; not a second persistence/domain layer. |

`tests/Revestik.Api.Tests` is the automated test project, not a fifth application project.

The intended deployment is **one host and one main domain**, `revestikcr.com`. `Revestik.Api` hosts both web experiences and the API. The earlier administrative subdomain `app.revestikcr.com` and the cross-origin iframe editing bridge are historical integration choices, not the target architecture. The old iframe editor and associated bridge code still require a separately approved, verified cleanup; do not declare them removed.

The normal current local startup runs **`Revestik.Api` alone**; it serves the administrative WASM client and the public site. Separate development entry points may exist for targeted debugging, but do not instruct contributors to start three processes as the standard workflow. Verify the actual launch profile and Google OAuth callback configuration before providing port-specific instructions.

## Current deployment and product status — 2026-11-10

- **Internal administration:** Deployed on Azure Container Apps with Azure SQL Database. The owner confirms `https://revestikcr.com/administracion` works and presents the Google OAuth sign-in flow for unauthenticated visitors. Administrative access and protected operations remain server-authorized.
- **Public launch gate:** The owner confirms `https://revestikcr.com` currently returns **HTTP 401** in production. This is an intentional pre-launch restriction, not evidence that the customer-facing website is live. Do not enable public access or search indexing without separate approval.
- **Public website development:** Home, shared navigation/navbar, and the finishes/products section are implemented in the repository. The full public site is still being completed and validated as a **beta** before its public production launch. Other pages and editor capabilities must be verified individually; never mark the whole public site complete based on a few finished sections.
- **Public catalog boundaries:** Public product records are independent of internal inventory products. Categories, subcategories, and selling units are shared with existing administrative catalog settings. Anonymous catalog reads must obey publication and visibility rules; privileged mutations require Administrator authorization and relevant antiforgery checks.
- **Visual editing:** The target is to edit actual public pages on the unified origin, not a second, duplicated preview application. Legacy `iframe`/`postMessage` editor components remain technical debt pending validated removal.
- **Evidence baseline:** GitHub `main` was verified at `581aef9c99ddb7bbc1fffcbe49ec924699c980a2` (2026-11-10). GitHub CI for that SHA completed successfully. The owner previously reported a local regression of **1,199/1,199** and **17/17** manual security checks for the public-editor recovery. These reports do not establish that the same code, database migrations, or public pages have been deployed to Azure.
- **Deployment traceability:** The most recent previously documented Azure revision was `ca-revestik-prod--0000018` on 2026-11-06, built from local sources without reliable Git SHA metadata. Verify the live Azure revision, image digest, configuration, database migration history, and smoke results independently before making production claims.
- **Known follow-up items:** Remove obsolete iframe/bridge code only after replacement verification; check public-media availability after hosting changes; verify SEO `noindex` behavior and launch readiness; check featured-product selections when publication status changes. These are investigation items, not blanket permission to modify code.

## Engineering and security guardrails

- Keep trusted business logic and enforcement in the API. Browser UI visibility is never authorization. Preserve role/resource checks, CSRF, rate limits, private file access, input validation, and secure Google OAuth return paths.
- Preserve FIFO stock provenance, transaction boundaries, quotation/sale identities, fiscal-document reconciliation, project boundaries, and closed-project restrictions. Do not conflate public marketing data with private customers, inventory costs, financial records, or project documents.
- Use ASP.NET Core Identity, EF Core/SQL Server, QuestPDF, and the existing xUnit/Testcontainers verification patterns where applicable. Do not invent microservices, CQRS, generic repositories, queues, or duplicated abstractions without evidence and approval.
- Treat migrations involving public/shared taxonomy as **data-sensitive**. In particular, `20261104230000_ConsolidateSharedProductTaxonomy` explicitly forbids automatic rollback and requires a separate production data review. No production migrations are implicitly authorized by a documentation or UI task.
- Production storage, Google OAuth credentials, SQL connections, Data Protection certificates, and external integrations must be confirmed against the actual target environment. Never publish secrets or infer a safe production configuration from checked-in development settings.
- Work from `C:\Projects\Revestik` using Windows PowerShell. Preserve the working tree; avoid destructive Git commands and blanket staging (`git add .`, `git add -A`). Do not overwrite unrelated local modifications, including `src/Revestik.Api/appsettings.Development.json`.
- Conversation with the owner may be in Spanish; repository technical documentation is in **English**. Preserve the application's established UI language and names.
- Prioritize responsive UX, accessibility, clear error handling, SEO readiness, maintainability, and actual reuse. Prefer removing proven duplication over introducing a parallel implementation.

## Maintaining this checkpoint

Keep this file concise and current. Update it only with verified Git/test/deployment evidence and explicit owner decisions. Store detailed business rules, architecture, release procedures, and historical decisions in their dedicated documents rather than duplicating entire checkpoints here. Distinguish **implemented**, **tested**, **deployed**, **publicly enabled**, and **planned** in every future handoff.