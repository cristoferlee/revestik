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

The deployment is one host and one main domain, `revestikcr.com`, with administration at `/administracion`. The former subdomain was retired from ingress and DNS. The legacy administrative iframe editor and message bridge were retired in commit `9cbd7d4`; the independent PDF preview iframe is not part of that cleanup.

The normal current local startup runs **`Revestik.Api` alone**; it serves the administrative WASM client and the public site. Separate development entry points may exist for targeted debugging, but do not instruct contributors to start three processes as the standard workflow. Verify the actual launch profile and Google OAuth callback configuration before providing port-specific instructions.

## Current deployment and product status — 2026-11-26

- **Internal administration:** Deployed on Azure Container Apps with Azure SQL Database. The owner confirms `https://revestikcr.com/administracion` works and presents the Google OAuth sign-in flow for unauthenticated visitors. Administrative access and protected operations remain server-authorized.
- **Public launch gate:** On 2026-11-26 the owner verified that the public root redirects to `/es` and returns HTTP 200. Anonymous reachability is not formal commercial launch approval; indexing and public authentication changes require separate approval.
- **Public website:** Home, Finishes, products, projects, Solutions, About, Contact, cart and preliminary PDF are implemented in source; final acceptance remains required.
- **Public catalog boundaries:** Public product records are independent of internal inventory products. Categories, subcategories, and selling units are shared with existing administrative catalog settings. Anonymous catalog reads must obey publication and visibility rules; privileged mutations require Administrator authorization and relevant antiforgery checks.
- **Visual editing:** The integrated inline editor is the current architecture. The legacy administrative iframe editor and message bridge were retired in commit `9cbd7d4`; the independent PDF preview iframe is not part of that cleanup.
- **Evidence baseline:** `68638b8118c9583662a85599736031eea080b365`; owner-reported 1,409/1,409 regression and successful CI. See the Azure checkpoint for deployment evidence.
- **Deployment:** `ca-revestik-prod--hotfix-sql-68638b8`; SQL Standard S0, 58 migrations verified, ACA 0.5 vCPU / 1 GiB / one replica.
- **Active work:** Follow `docs/ROADMAP.md` and `docs/REVESTIK-FINAL-CLOSURE-PLAN.md`. No persistent publication layer, extra CDN or ACA reduction is planned.

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

## Closure discipline

Use `COMPLETED` (implemented and verified), `CLOSED — NO CHANGE` (investigated and retained), `CANCELLED` (explicitly discarded), `NOT APPLICABLE`, `SUPERSEDED` (replacement identified), and `TRANSFERRED` (destination identified). Do not equate transfer with implementation. Keep unresolved evidence explicit; do not expand release scope without approval.
