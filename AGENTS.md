# Revestik — agent instructions and current checkpoint

## Sources of truth

Inspect the current repository, tests, migrations, Git working tree, and deployment evidence before changes. This document is a continuity aid, not a substitute for source code. Consult `docs/ARCHITECTURE.md`, `docs/BUSINESS_RULES.md`, `docs/SECURITY.md`, `docs/OPERATIONS.md`, and accepted ADRs in `docs/adr/`.

## Mandatory workflow

**INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP.**

- **INVESTIGATE:** Trace actual endpoints, services, contracts, roles, persistence, dependencies, and tests. Separate evidence from hypotheses. Do not edit during investigation.
- **PLAN:** Identify bounded scope, affected files, domain/security impact, migrations, risks, and acceptance tests.
- **APPROVE:** Obtain explicit approval of the proposed scope before implementation; ask again for new scope.
- **IMPLEMENT:** Make minimal changes, preserve the working tree, and provide complete repository-relative files in source ZIPs when requested.
- **VERIFY:** Build, run focused and appropriate regression tests, check SQL/HTTP/CSRF/concurrency as applicable, perform sequential smoke tests, and inspect Git diff. Never report unexecuted tests as passed.
- **STOP:** Summarize evidence and outstanding work. Do not commit, push, deploy, or delete files without separate authorization.

## Engineering guardrails

- .NET 10, ASP.NET Core Minimal APIs, Blazor WebAssembly, EF Core/SQL Server, ASP.NET Core Identity, Google OAuth/cookies, QuestPDF, xUnit/Testcontainers.
- Modular monolith. Do not invent microservices, distributed queues, CQRS, or generic repositories.
- Backend enforces permissions, state transitions, currency rules, project boundaries, CSRF, financial invariants, and private-file access.
- Preserve FIFO stock provenance, transaction boundaries, quotation/sale identity, fiscal reconciliation distinctions, and closed-project restrictions.
- Do not change migrations, public contracts, security behavior, frameworks, or `.gitignore` without explicit approval.
- Work from `C:\Projects\Revestik` using Windows PowerShell; avoid destructive Git commands and blanket staging (`git add .`, `git add -A`).
- Never include private files, backups, credentials, customer data, tokens, or local-only configuration in patches, diagnostics, or commits.
- Communication with the project owner may be in Spanish; repository-facing documentation is **English** for technical recruiters, hiring managers, and collaborators. Preserve existing application UI language and established naming conventions.
- Prioritize responsive UX, accessibility, clear validation, and maintainability.

## Checkpoint — 2026-11-02

- Repository: `cristoferlee/revestik`; local branch `main`; reported local HEAD and remote tracking baseline `1ea2fd55cee9f9c755f6ec407396a477049d0953`. Re-verify before work.
- Project phase: internal production application on Azure; ongoing stabilization and documentation. Next planned phase: public customer-facing business website, with scope to be defined separately.
- Locally modified and **not yet committed** at this checkpoint: `src/Revestik.Api/Endpoints/AuthenticationEndpoints.cs`, `src/Revestik.Client/Pages/Login.razor`, and `src/Revestik.Client/Pages/Login.razor.css` (AUTH-PROD-01A).
- Untracked local backup: `src/Revestik.Api/Services/GmailIntegration/GmailIntegrationService.cs.gmail-quota01.bak`. Do not stage, publish, overwrite, or delete it without approval.
- AUTH-PROD-01A: friendly login recovery for a transient database-unavailable scenario during Google OAuth callback; reported local build, 67 focused authentication tests, UI smoke, and **1,073/1,073 full regression tests** passed. This does not establish deployment to production.
- Production architecture includes Azure Container Apps, Azure SQL Database, persistent private storage, and certificate-protected Data Protection keys. Verify live settings independently; repository deployment YAML may be a historical/preproduction snapshot.
- Gmail historical sync is resumable and uses a process-local background worker. Multi-replica synchronization requires distributed coordination before scaling.
- DOCS-PROD-01: documentation modernization for professional English-language repository presentation, architecture, security, operations, development, roadmap, and current state. No functional code changes are included in the documentation patch.

## Updating this checkpoint

Update verified SHA, test evidence, deployment status, pending work, and material decisions at each approved release. Keep it concise and current; Git history records implementation chronology. Do not mark a locally tested feature as deployed without a verified production revision.
