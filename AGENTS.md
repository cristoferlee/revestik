# Revestik — development instructions and current checkpoint

## Sources of truth

Inspect current repository source, migrations, tests and the local working tree before modifying anything. This file is a continuity aid, not a substitute for current evidence. Read `docs/ARCHITECTURE.md`, `docs/BUSINESS_RULES.md`, and `docs/SECURITY.md` when relevant. Approved architectural decisions remain in `docs/adr/`.

## Mandatory workflow

**INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP.**

- INVESTIGATE actual endpoints, services, contracts, authorization, migrations, UI dependencies and tests. Distinguish confirmed defects from hypotheses. Do not modify code.
- PLAN bounded behavior, files, domain/security impacts, migrations, regression risks and acceptance tests.
- APPROVE requires explicit user approval of the specific plan; obtain approval for new scope.
- IMPLEMENT minimally, preserving recent local changes. Source ZIPs should contain complete files with repository-relative paths.
- VERIFY compile, focused tests, relevant SQL/HTTP/CSRF/concurrency cases, module regression, smoke test one scenario at a time and Git diff. Never report unexecuted tests as passed.
- STOP summarize evidence and pending work; never commit or push without separate authorization.

## Guardrails

- .NET 10, Blazor WASM, ASP.NET Core Minimal APIs, EF Core, SQL Server, ASP.NET Core Identity, cookies/Google sign-in, antiforgery, xUnit/Testcontainers and QuestPDF. Modular monolith; no invented microservices, event bus, CQRS or generic repositories.
- Backend enforces authorization, business states, currency rules, project ownership, CSRF, data integrity and private-file boundaries. Hiding buttons is not security.
- Do not change migrations, public contracts, cross-domain effects, frameworks or `.gitignore` without approval.
- Work with Windows PowerShell in `C:\Projects\Revestik`. Avoid destructive Git commands and blanket staging (`git add .`, `git add -A`).
- `src/RevestikPrivateDocuments/` is private, untracked storage and must not be staged, printed or deleted. Do not include any private contents in diagnostics or ZIPs.
- Spanish interaction and user-facing UI; preserve existing code naming conventions. Prioritize accessibility, clear validations and stable UX.

## Current verified checkpoint — 2026-10-20

- Repository: `cristoferlee/revestik`, branch `main`.
- Reported local HEAD and last verified remote baseline: `b105a52e3ebd48a3ab4415389a93c9d1b651b76d`. Recheck before work.
- Projects B01–B08, related UX, closed-project financial write-lock and GH-01 were published at this baseline.
- Latest *Projects-only* regression reported: 193 passed, 0 failed, 0 skipped; 26/26 focused financial tests passed; 7/7 PDF and 4/4 linked-evidence smoke checks reported. These are not full-suite results.
- Latest verified pre-GH-02 baseline: `main...origin/main` with only private untracked storage. During GH-02, `README.md` is modified and new documentation is untracked; inspect actual local status.
- Projects' manually entered payments are **not** SalePayments, bank movements, tax documents or accounting entries. Completed, Cancelled and Archived block financial/evidence mutations even for Administrator. Project documents stay private and project-scoped.
- Current work: GH-02 documentation consolidation. New documentation is staged locally; historical files have been removed locally but GH-02 is not yet published. Verify local Git status before editing.

## Updating this checkpoint

At each approved release, replace only the current state, verified SHA, test scope/results and genuine pending work. Do not append a running diary. Git history preserves completed implementation chronology. When beginning a new conversation, reread this file and verify its checkpoint against the actual source.
