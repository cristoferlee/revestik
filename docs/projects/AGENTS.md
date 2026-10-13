# Projects — Continuity instructions (read this first)

**Updated:** 2026-10-13. **Phase:** B02 implemented locally, SQL migrations reportedly applied, automated suite 828/828 passing; B02 commit/push and one owner-level HTTP security scenario still pending. **B03 not started.**

## Mandatory reading order for a new ChatGPT conversation

1. `docs/projects/CHECKPOINT-2026-10-13-B02.md` — detailed current state, exact migration IDs, files, tests, open risks and handoff.
2. `docs/projects/checklist.md` — progress and immediate next actions.
3. `docs/projects/business-rules.md` — approved original 97-answer functional baseline (preserve this document).
4. `docs/projects/b01-b02-decisions.md` — subsequent decisions, B01/B02 technical implementation and explicit gaps.
5. Source files listed in the checkpoint, directly from current `main`. Confirm the local working tree/HEAD before any patch; uncommitted changes can be newer than GitHub.

**Do not repeat the 97-question functional investigation** or reopen settled business decisions. Perform targeted, short technical investigation only for a new block, changed code or an unresolved design detail; do not claim that reading GitHub replaces inspecting the current local code before modifying it.

## Operating contract

**INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP**. Investigation means inspect actual business rules and *relevant current files*, not invent them. Present PLAN with boundaries, entities, migrations, authorization, file list and test matrix; wait for explicit approval before implementing an unapproved block. The owner explicitly authorized grouped B02 subblocks A+B and C+D and E separately.

- Discuss in Spanish; C# identifiers and technical repository docs in English; UI copy in Spanish.
- Clean, SOLID where useful, maintainable, scalable pragmatic modular monolith. .NET 10 / ASP.NET Core Minimal APIs / Blazor WASM / EF Core SQL Server / Identity cookie auth / CSRF / xUnit + SQL Server Testcontainers. Reuse existing conventions; no speculative microservices, CQRS, generic repository, event bus, overengineering.
- Deliver ZIPs with **only actual source files and repository-relative paths**. **No README, patch scripts, temporary files, or ZIPs inside repository**. No fragile mass replacements; preserve unrelated and local user changes. Existing production files should be diffed/verified against local state before replacement.
- Server enforces authorization and invariants, not only client. CSRF for mutating Minimal APIs. SQL integrity, concurrency, migrations, and regression must be verified on real outputs.
- No commit/push, database update, destructive operation or unintended change without user authorization. User executes PowerShell commands in `C:\Projects\Revestik` and reports outputs. Do not invent test results. Scope staging explicitly, check `git diff --cached --check`, `git diff --cached --stat`, `git status`.
- **User explicitly postponed ALL manual UI smoke tests until the full Projects functionality and coherent responsive Blazor UI are complete** (B07/B08). Continue automated tests per block; do not ask for partial smoke tests.
- Investigating the suite's performance regression is deferred until Projects is finished. Preserve tests and SQL fixture isolation.
- Root `AGENTS.md` is a local ignored Codex-era file; rely on this tracked document, not the root file.

## Boundaries

Exactly one Customer per Project, many contacts; no automatic Sale/Quotation linkage, no accounting or inventory side effects from manual project payments, private files only. Responsible user edits, administrators intervene, other ManageProjects roles may read. Permanent project deletion deletes only project-owned data, never Customer or unrelated business records. B02 event voids are logical and histories remain until permanent project deletion.

## Next action

**Finish B02-E:** add/verify HTTP tests with a valid CSRF token for authorized *non-owner* mutating another owner's event or agreement (expect 403); run affected and full regression as warranted; verify applied migrations and clean git status. Then stage/publish B02 and this documentation only with owner's permission. Record real B02 commit SHA only after it exists. **STOP** until instructed to start B03 progress/milestones. Do not claim B02 fully closed before security test and push are confirmed.
