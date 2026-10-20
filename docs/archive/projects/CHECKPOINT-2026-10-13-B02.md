# MASTER CHECKPOINT — Revestik Projects through B02

**Date:** 2026-10-13. **Owner workflow:** Windows PowerShell, `C:\Projects\Revestik`, ChatGPT delivers repository-relative ZIP patches; owner runs tests/migrations/git commands. **Actual status:** B01 implemented, migrated and published; B02 A+B/C+D implemented and migrated locally, E tests and full regression passed, B02 not yet confirmed committed/pushed. **Next planned block:** B03 milestone-based progress (not approved for implementation yet). **Do not repeat functional INVESTIGATE.**

## 0 — New chat startup checklist

1. Read this file, `docs/projects/AGENTS.md`, `docs/projects/business-rules.md`, `docs/projects/b01-b02-decisions.md`, `docs/projects/checklist.md` from `main`.
2. Get latest `main`/commit from GitHub; check whether this B02 checkpoint and code were actually pushed. The presence of docs alone is not proof that B02 code was staged.
3. Ask for **only** the current `git status --short` and relevant local source files if needed to avoid overwriting uncommitted changes. Repo can be behind working tree; inspect actual files before authoring patches. Do not request another 97-question discovery.
4. Close any open B02-E security check and commit before B03. Once closed: INVESTIGATE **targeted B03 dependencies only** → PLAN → explicit APPROVE → IMPLEMENT → VERIFY → STOP.

## 1 — What the user is building

Revestik is a Costa Rican building-materials/works business application using .NET 10, Blazor WASM, ASP.NET Core Minimal APIs, EF Core/SQL Server, Identity cookies/Google OAuth, CSRF, SQL Server Testcontainers, xUnit and QuestPDF. Projects is an independent *customer-linked operational dossier* for houses, renovations, kitchen furniture, supply/installation/other works, mainly recording coordination and evidence. A project requires one existing Customer; many Projects per Customer, project-specific contacts; quotations/sales remain separate authoritative modules, PDF references attached manually in future B04. No automatic accounting, payment, inventory or quotation sync. No public portal. Existing customers may be deactivated without losing Projects. The future manual payments tracking module does not pay Sales.

Full functional decisions are recorded in `business-rules.md` (97 answers plus later clarifications). **Those answers are approved; do not re-interview**. Follow-up B01 decisions are in `b01-b02-decisions.md`. Remaining *genuine* design choices: B03 exact milestone catalog/weights/reversal, B04 upload limits/storage/delete-multi-reference, B05 currency/overpayments/voids, B06 lifecycle/closure, B08 PDF scope.

## 2 — How we work (non-negotiable)

**INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP.** Investigate the actual code relevant to changes; no speculative changes. Plan includes business behavior, domain, dependencies, authorization/CSRF, SQL migration impact, API and tests. Implement approved scope only in small verifiable patches, **ZIPs with complete C# files and relative paths only; absolutely no README, temporary scripts or unrelated files**. Preserve local work. Never run git commit/push or apply migrations without explicit owner approval. User executes PowerShell and reports actual results. Prefer existing minimal APIs/services/DTOs, clean maintainable pragmatic monolith, no microservices/CQRS/generic repository/event bus. Code identifiers/docs English; user UI Spanish. Tests: compile, targeted SQL, auth/CSRF, concurrency, regression; do not suppress warnings with null-forgiving when avoidable. `git diff --check` excludes untracked files, so stage/review new files carefully.

**Manual UI smoke tests are postponed until Projects is completely functional and the Blazor UI is coherent and responsive** (B07/B08). Do not ask to run them per block. Test performance work is postponed until Projects completes; do not degrade SQL isolation or delete tests in the interim.

## 3 — B01 timeline / verified achievements

- Agreed initial `Planning`, creator responsible, Administrator-only reassignment, ManageProjects roles Administrator/Accountant/Sales can read, owner/admin writes, budget optional, active Customer required for creation, inactive Customer Projects still readable.
- A: Project/ProjectContact domain entities + configurations; B: IProjectService/ProjectService and shared contracts; C: endpoints/DI/policy/CSRF; D: `20261013205336_AddProjectsFoundation` (applied to development DB); E: six SQL tests passed, later eight; F: SQL duplicate-index distinction, optimistic `Version` with 409 conflict, authorization/CSRF tests. The original 20 fiscal files were committed/pushed separately *before* Projects implementation; preserve fiscal work.
- B01 SQL: Projects, ProjectContacts; restrict FKs to Customers/AspNetUsers, cascade contacts on Project removal, unique `(CustomerId, NormalizedName)`, agreed amount check, currency CRC/USD, Version `bigint` initialized to 1. Project owner-based write authorization enforced server-side; CSRF mutating endpoints.
- At B01 conclusion: **787/787 tests passed** (422.1s test duration on that run); Projects foundation migration owner-reported applied; B01 code later visibly present in GitHub `main`. Commit SHA **not verified here**; don't fabricate.
- B01 temporary README and integration scripts were intentionally deleted; never reintroduce them.

## 4 — B02 delivery schedule and what was built

Owner explicitly grouped **B02 A+B**, then **B02 C+D**, then **B02 E alone**.

### B02 A+B — categories + events

New `ProjectEventCategory`, `ProjectEvent`, `ProjectEventRevision`, EF configurations, `ProjectActivityService`, `ProjectActivityEndpoints`, `ProjectActivityContracts`. Admin-managed categories (create/list/deactivate); project timeline using actual occurrence date, sorted descending, filters; events create/read/edit/logical void, server-side ownership, revision snapshots prior to update/void and optimistic event version. `Program.cs` adds `AddScoped<ProjectActivityService>()` and `MapProjectActivityEndpoints()`. Initial ZIP did **not** include Program.cs; a subsequent program-only ZIP fixed integration. A premature build got CS0246/CS1061 because A+B files hadn't yet been extracted; applying code-only ZIP resolved errors. Final A+B build succeeded.

Migration `20261013215919_AddProjectActivityFoundation`: categories/events/revisions, normalized category name unique, version and foreign keys. Applied per owner; **three targeted SQL tests passed (~100s)**.

### B02 C+D — event comments, agreements + audit/security

New `ProjectEventComment`, `ProjectEventAgreement`, `ProjectEventAgreementRevision`, EF configurations, `ProjectEventDetailService`, `ProjectEventDetailEndpoints`, `ProjectEventDetailContracts`. Comments append-only/author-attributed; agreement attached to event with status Pending/Fulfilled/Modified/Unfulfilled and manually entered who/when/medium/notes. Agreement update snapshots prior version, rejects stale version; event revision history available for administrative review; writer must be project responsible or Administrator, admin-only audit reads, CSRF on writes. Program.cs adds `AddScoped<ProjectEventDetailService>()` and `MapProjectEventDetailEndpoints()`. Build succeeded in **34.9s**.

Migration `20261013221354_AddProjectEventDetails`: comments/agreements/agreement revisions; check constraint on agreement status, unique revision history `(ProjectEventId, PreviousVersion)`, cascade only for physical parent deletion, no unrelated changes. Applied per owner; last migration list reportedly had **no Pending**. Event author IDs do not have FK to Identity in B02 SQL, intentionally need server-authenticated assignment validation.

### B02 E — automated checks and regression

- `ProjectActivitySqlTests`: 3 SQL integration tests pass.
- `ProjectEventDetailSqlTests`: 5/5 SQL integration tests pass (58.4s test run, 84.7s including build). Two `CS8604`/`CS8602` nullable warnings in this file: user was instructed to insert `Assert.NotNull(comments)` and replace indexed history access with `var previous = Assert.Single(history)` after NotNull. Subsequent latest test run passed; *confirm warnings are gone rather than assume*.
- `ProjectActivityAuthorizationTests`: filtered HTTP run **33/33 passed**, 0 failed/skipped, 57.9s test duration, 89.4s build+tests. Covers anonymous 401, Warehouse 403, nonadmin audit/category access 403, missing CSRF 400; **does not prove HTTP 403 for an authenticated permitted-role non-owner with a valid token**. That scenario is the remaining B02-E security coverage gap.
- **Full suite after B02: 828 tests, 828 succeeded, 0 failed/skipped, 318.9s test duration, 319.5s total build+run**. `git diff --check` no errors; only harmless LF→CRLF notices.
- This is stronger regression coverage than previous B01 787. Do NOT interpret faster execution on one run as definitive performance optimization; previous suites 422.1s and filtered tests can vary.

## 5 — B02 migrations and models

B02 migration SQL was reviewed before application:

1. `20261013215919_AddProjectActivityFoundation` creates `ProjectEventCategories`, `ProjectEvents`, `ProjectEventRevisions`. Event-to-Project cascade physically, Category non-cascading, revisions cascade physically, unique category normalized name and revision `(ProjectEventId, PreviousVersion)`.
2. `20261013221354_AddProjectEventDetails` creates `ProjectEventComments`, `ProjectEventAgreements`, `ProjectEventAgreementRevisions`. Agreements PK=`ProjectEventId`; status SQL CHECK; history version unique. Append-only comments; revision rows removed only if parent event intentionally deleted with project; user-facing event void is logical.

The EF migration snapshot is changed (`src/Revestik.Api/Data/Migrations/RevestikDbContextModelSnapshot.cs`) and migrations are currently **untracked on local working tree** until staged. `Program.cs` is modified. Do not regenerate existing applied migrations. `RevestikDbContext` uses `ApplyConfigurationsFromAssembly`, so new configurations are auto-discovered; DbSet declarations are not mandatory.

## 6 — Exact B02 source files expected (latest local status)

Modified tracked:

- `src/Revestik.Api/Program.cs`
- `src/Revestik.Api/Data/Migrations/RevestikDbContextModelSnapshot.cs`

New untracked source:

- `src/Revestik.Api/Data/Configurations/ProjectEventCategoryConfiguration.cs`
- `src/Revestik.Api/Data/Configurations/ProjectEventConfiguration.cs`
- `src/Revestik.Api/Data/Configurations/ProjectEventRevisionConfiguration.cs`
- `src/Revestik.Api/Data/Configurations/ProjectEventCommentConfiguration.cs`
- `src/Revestik.Api/Data/Configurations/ProjectEventAgreementConfiguration.cs`
- `src/Revestik.Api/Data/Configurations/ProjectEventAgreementRevisionConfiguration.cs`
- `src/Revestik.Api/Data/Migrations/20261013215919_AddProjectActivityFoundation.cs`
- `src/Revestik.Api/Data/Migrations/20261013215919_AddProjectActivityFoundation.Designer.cs`
- `src/Revestik.Api/Data/Migrations/20261013221354_AddProjectEventDetails.cs`
- `src/Revestik.Api/Data/Migrations/20261013221354_AddProjectEventDetails.Designer.cs`
- `src/Revestik.Api/Models/ProjectEventCategory.cs`
- `src/Revestik.Api/Models/ProjectEvent.cs`
- `src/Revestik.Api/Models/ProjectEventRevision.cs`
- `src/Revestik.Api/Models/ProjectEventComment.cs`
- `src/Revestik.Api/Models/ProjectEventAgreement.cs`
- `src/Revestik.Api/Models/ProjectEventAgreementRevision.cs`
- `src/Revestik.Api/Services/Projects/ProjectActivityService.cs`
- `src/Revestik.Api/Services/Projects/ProjectEventDetailService.cs`
- `src/Revestik.Api/Endpoints/ProjectActivityEndpoints.cs`
- `src/Revestik.Api/Endpoints/ProjectEventDetailEndpoints.cs`
- `src/Revestik.Shared/Projects/ProjectActivityContracts.cs`
- `src/Revestik.Shared/Projects/ProjectEventDetailContracts.cs`
- `tests/Revestik.Api.Tests/Projects/ProjectActivitySqlTests.cs`
- `tests/Revestik.Api.Tests/Projects/ProjectEventDetailSqlTests.cs`
- `tests/Revestik.Api.Tests/Projects/ProjectActivityAuthorizationTests.cs`

**Total B02 code:** 2 modified tracked + 25 new = 27 code files. Validate with `git diff --cached --stat`, not memory, after staging. Keep the four docs from this checkpoint too.

## 7 — Important external module dependencies; where the next chat should look

- `src/Revestik.Api/Models/Project.cs` / `ProjectContact.cs`, `Data/Configurations/ProjectConfiguration.cs`, `Services/Projects/{IProjectService,ProjectService}.cs`, `Endpoints/ProjectEndpoints.cs`, `src/Revestik.Shared/Projects/{ProjectContracts,ProjectStatus}.cs`.
- `src/Revestik.Api/Authorization/{PolicyNames,RoleNames}.cs`, `Extensions/AuthenticationExtensions.cs`, `Endpoints/AntiforgeryValidationFilter.cs` or its actual location; check exact path, don't assume.
- `src/Revestik.Api/Program.cs` and `Data/RevestikDbContext.cs` (`ApplyConfigurationsFromAssembly`).
- `tests/Revestik.Api.Tests/Hosting/{SqlServerIntegrationTestFixture,RevestikWebApplicationFactory}.cs` (confirm exact filenames/paths); SQL static container + unique DB per class; existing HTTP test conventions.
- Client later: existing CustomerSelector.razor, Home.razor Dashboard, CookieHandler/CsrfHandler and nav style; use the current code, not old ZIP snapshots.
- `docs/product/business-rules.md`, `docs/architecture/architecture-overview.md`, `docs/security/security-overview.md`, `docs/development/testing.md` for conventions, not as replacements for Projects rules.

## 8 — Known limitations / risks

- **HTTP non-owner valid-CSRF scenario pending.** Add test validating authorized-but-nonresponsible cannot write event and agreement despite correct CSRF, expecting 403. Do not mistake role policy for resource permission.
- B02 actor fields are IDs and lack DB user FKs; endpoints/services must assign from trusted authenticated identity.
- `ProjectEventRevisions` and agreement revisions preserve modifications within their scope; wider general project administrative audit of create/reassign/download may still require work.
- B04 files/attachment-based timeline filter and event attachments intentionally absent; no premature file service in B02.
- B03 progress weights and milestone set not approved. Do not conflate event categories with milestone completion or count photos as progress.
- Suites are slower than earlier historic 680 in ~202s; one prior run 787/787 in 422.1s, later 828/828 in 318.9s. Performance investigation explicitly postponed until full Projects delivery.
- GitHub docs currently stale until this ZIP is committed. The old checklist incorrectly said implementation not started; this checkpoint corrects it.

## 9 — Exact next steps (before moving to B03)

1. Add and run B02 security HTTP test(s) with actual valid CSRF, a logged-in nonowner with permitted role, and an event/agreement owned by someone else; assert 403. Prefer modifying only project tests, not production unless a genuine bug is found. If test infrastructure can't issue valid CSRF, inspect current test helper and use its established methods rather than faking a response.
2. Run targeted tests and full suite when warranted; report real totals/timing, no fabricated claims.
3. Confirm `dotnet ef migrations list` includes both B02 migrations without `(Pending)`, and `git diff --check`.
4. Extract/update this documentation ZIP into repository root (will populate `docs/projects/`). The ZIP contains docs only, no README or temp scripts.
5. Stage ONLY the two modified B02 tracked source files, the 25 B02 new files and four `docs/projects/*` files. Review `git diff --cached --check`, `git diff --cached --stat`, `git status --short` and changed files. Be conscious that already-applied migrations must have their .cs and .Designer.cs and snapshot committed together.
6. Upon owner approval, `git commit -m "feat(projects): implement activity timeline agreements and audit"` and `git push origin main`; record actual SHA after push. No claim of completed publication without output.
7. **STOP.** Next conversation can design B03 without new broad investigation: determine optional unique milestones, weights summing to 100, transitions, void/rollback and reopening, UI-bound DTO/API plans, migration/test matrix. Obtain explicit APPROVE before implementing B03.

## 10 — Future roadmap and smoke-test timing

- B01 Foundation — implemented and published.
- B02 Events/agreements/audit — this checkpoint, closeout in progress.
- B03 Automatic weighted progress — pending.
- B04 Private documents/images/versioning — pending.
- B05 Manual project payments and balance — pending.
- B06 Tasks, due dates, lifecycle, Dashboard alerts — pending.
- B07 Entire cohesive responsive Blazor UI: Projects → Customer → Project → Summary, Activity, Documents, Agreements, Payment Receipts, Plans — pending.
- B08 Lightweight PDF, integration, regression and **final manual UI smoke test only after the interface is complete** — pending.

## 11 — Handoff compact message for a fresh chat

> Continue Revestik Projects from GitHub, not a new functional INVESTIGATE. Read `docs/projects/AGENTS.md`, `CHECKPOINT-2026-10-13-B02.md`, `business-rules.md`, `b01-b02-decisions.md`, and `checklist.md` on current `main`. B01 is implemented/published. B02 events/categories/comments/agreements/revisions are implemented locally and SQL migrated; 828/828 regression tests passed. Verify the new B02 commit/push and the outstanding valid-CSRF non-owner HTTP test. Keep ZIPs code-only, strict INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP, no manual UI smoke until B07/B08, no performance investigation yet. After closing B02, start B03 PLAN only (weights not approved). Preserve unrelated work. Do not claim GitHub up to date until verified.
