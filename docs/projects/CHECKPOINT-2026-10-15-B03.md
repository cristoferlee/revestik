# MASTER CHECKPOINT — Revestik Projects through B03

**Date:** 2026-10-15. **Stage:** B03 A+B and C+D implemented locally; B03-E targeted/full automated verification completed; documentation and scoped Git publication pending. **Do not claim the B03 commit/push has occurred until verified.**

## Start here in the next conversation

Read `docs/projects/AGENTS.md`, this checkpoint, `docs/projects/checklist.md`, `docs/projects/business-rules.md`, `docs/projects/b01-b02-decisions.md`, and the current source. The earlier `CHECKPOINT-2026-10-13-B02.md` is historical, not the current stage. Local uncommitted B03 files can be newer than GitHub. Do not repeat the original 97-question discovery.

## Working contract

INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP. User runs PowerShell in `C:\Projects\Revestik`. Explicit authorization before new block implementation, migrations, destructive actions, or publishing. Patch ZIPs contain only repository-relative source files, no README/scripts/temporary artifacts. Preserve existing code; .NET 10 / Minimal APIs / Blazor WASM / EF Core SQL Server / Identity / CSRF / xUnit SQL Server Testcontainers. No manual Projects UI smoke tests until full UI B07/B08. Performance investigation deferred.

## What B03 delivers

Optional per-Project weighted milestones and optional reusable templates. A Project without an active plan has no percentage. A configured plan can be activated only when its included milestone weights sum to exactly 100%. Completing an included milestone contributes its weight; reverting removes it from progress and requires a reason. Completing all milestones does **not** automatically complete/close the Project. The service centralizes percentage computation; APIs must not duplicate it. Plan and milestone versions protect against stale writes. Audit revisions are persisted for milestone changes. Templates supply independent milestone copies, not references that mutate other Projects.

Business rules, security and scope remain: one Customer per Project; responsible user/Admin can edit, authorized other roles read; administrator controls templates; CSRF on mutating endpoints; no automatic accounting, inventory, Sales or Quotation side effects.

## Implemented source (B03)

Modified tracked files:
- `src/Revestik.Api/Program.cs`
- `src/Revestik.Api/Data/Migrations/RevestikDbContextModelSnapshot.cs`

New configuration files:
- `src/Revestik.Api/Data/Configurations/ProjectMilestoneConfiguration.cs`
- `src/Revestik.Api/Data/Configurations/ProjectMilestonePlanConfiguration.cs`
- `src/Revestik.Api/Data/Configurations/ProjectMilestoneRevisionConfiguration.cs`
- `src/Revestik.Api/Data/Configurations/ProjectMilestoneTemplateConfiguration.cs`
- `src/Revestik.Api/Data/Configurations/ProjectMilestoneTemplateItemConfiguration.cs`

Migration:
- `src/Revestik.Api/Data/Migrations/20261015183929_AddProjectMilestones.cs`
- `src/Revestik.Api/Data/Migrations/20261015183929_AddProjectMilestones.Designer.cs`

Models:
- `src/Revestik.Api/Models/ProjectMilestone.cs`
- `src/Revestik.Api/Models/ProjectMilestonePlan.cs`
- `src/Revestik.Api/Models/ProjectMilestoneRevision.cs`
- `src/Revestik.Api/Models/ProjectMilestoneTemplate.cs`
- `src/Revestik.Api/Models/ProjectMilestoneTemplateItem.cs`

API/services/contracts:
- `src/Revestik.Api/Services/Projects/ProjectMilestoneService.cs`
- `src/Revestik.Api/Services/Projects/ProjectMilestoneTemplateService.cs`
- `src/Revestik.Api/Endpoints/ProjectMilestoneEndpoints.cs`
- `src/Revestik.Shared/Projects/ProjectMilestoneApiContracts.cs`

Tests:
- `tests/Revestik.Api.Tests/Projects/ProjectMilestoneSqlTests.cs` (6)
- `tests/Revestik.Api.Tests/Projects/ProjectMilestoneAdvancedSqlTests.cs` (4)
- `tests/Revestik.Api.Tests/Projects/ProjectMilestoneHttpTests.cs` (7)
- `tests/Revestik.Api.Tests/Projects/ProjectMilestoneAdditionalHttpTests.cs` (5)

**Total:** 2 modified tracked production files + 20 new B03 files, including 4 test files; verify actual staging rather than relying on this count.

## Verified results (owner-reported, 2026-10-15)

- Builds succeeded after A+B and C+D changes, including warning-free corrections to initial SQL tests.
- SQL tests: 6 initial + 4 advanced = **10/10**.
- HTTP tests: 7 initial + 5 additional = **12/12**, including role/ownership, valid/missing CSRF, optimistic conflicts, template application/deactivation and independent Project copies.
- **All 22 B03 tests succeeded. Full API test suite: 857/857 succeeded**, up from B02 835/835.
- `dotnet ef migrations list` ended with `20261015183929_AddProjectMilestones` (owner previously confirmed it applied; the latest list alone does not independently prove application).
- `dotnet ef migrations has-pending-model-changes` reported `No changes have been made to the model since the last migration.`
- `git diff --check` reported only the Windows LF→CRLF informational notice for `Program.cs`, no whitespace errors.

**Not yet proven:** strict simultaneous race test (two overlapping operations), final staged diff inspection, actual commit SHA and successful push. Do not silently mark these verified.

## Database and safety notes

Migration `AddProjectMilestones` creates five milestone/template-related tables and filtered active-milestone normalized-name uniqueness; SQL CHECK constraints/relations were reviewed when generated. Do not regenerate the already applied migration merely for documentation. Before publication, inspect `git diff --cached --check` and staged file list; include migration `.cs`, `.Designer.cs`, and snapshot together. `git diff --check` does not inspect untracked files.

## Immediate next actions

1. Extract the documentation-only closeout ZIP to repo root. Preserve `business-rules.md`, `b01-b02-decisions.md` and the historical B02 checkpoint.
2. Stage exactly the B03 files above and the three closeout docs (`AGENTS.md`, `checklist.md`, this checkpoint); inspect `git diff --cached --check`, `git diff --cached --stat`, and `git diff --cached --name-only`.
3. Do not commit or push until owner approves the staged contents. Once approved, commit with an appropriate Projects B03 message and push; capture real SHA and remote result. Then STOP.
4. Next approved block is B04 private files/documents/evidence. Conduct targeted technical investigation of storage, MIME limits, private access, versioning and deletion semantics before implementation. Do not invent unapproved upload quotas. Keep B03 strict-race coverage on the engineering risk list.

## Remaining roadmap

B01 foundation and B02 timeline/agreements completed earlier. B03 weighted milestones/templates verified locally; Git closeout pending. B04 private documents and images; B05 manual project payments (no fiscal/accounting effects); B06 tasks/due dates/lifecycle/dashboard alerts; B07 full coherent responsive Projects UI; B08 report PDF/final regression and manual UI smoke test after the UI is ready.
