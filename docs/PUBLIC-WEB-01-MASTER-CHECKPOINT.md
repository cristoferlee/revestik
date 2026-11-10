# REVESTIK — Public Website Master Checkpoint

**Active workstream:** `PUBLIC-PROJECTS-01`
**Next step:** `INVESTIGATE` — no implementation authorized
**Checkpoint date:** 2026-11-10
**Repository:** https://github.com/cristoferlee/revestik
**Branch:** `main`
**GitHub HEAD verified for this checkpoint:** `581aef9c99ddb7bbc1fffcbe49ec924699c980a2`
**Local workspace:** `C:\Projects\Revestik`
**Owner-confirmed baseline:** GitHub is synchronized with the local project at the handoff. This statement is the owner's confirmation, not an independent inspection of the local Windows worktree. Recheck `git status -sb` and `git rev-parse HEAD` before starting another work block.
**Owner communication:** Spanish. **Repository-facing documentation:** English.
**Status:** Earlier public-site workstreams are considered closed by the owner; the next development focus is the **public Projects section**. The public website is **not yet authorized for public launch**.

---

## 1. Purpose and maintenance policy

This is the concise, authoritative **working handoff** for the next AI-assisted development session. Its purpose is to preserve the current architecture, agreed rules, approved product intent, evidence boundaries, and exactly what to investigate next.

- **Do not grow this file into a daily transcript.** Keep one current snapshot, stable decisions, an active-workstream checklist, and the next handoff.
- Preserve historical rationale through Git commits, accepted ADRs, and previous checkpoints. Do not silently delete or rewrite accepted ADRs.
- Once this replacement is committed, the full previous checkpoint can be recovered from Git history, including GitHub revision `581aef9c99ddb7bbc1fffcbe49ec924699c980a2`.
- **Reality overrides memory:** inspect actual source, migrations, integration tests, runtime configuration, and current Git diff whenever a claim affects implementation.
- Clearly label each statement as **implemented in source**, **owner-reported verified**, **production-verified**, **approved design**, or **pending investigation**. Never equate these.
- Do not assume documentation edits in a local working tree have already been pushed to GitHub; inspect the commit first.

### Documents to consult, in order

1. `AGENTS.md` — mandatory collaboration and change-control rules.
2. This checkpoint — current phase and constraints.
3. `docs/ARCHITECTURE.md`, `docs/BUSINESS_RULES.md`, `docs/SECURITY.md`, `docs/DEVELOPMENT.md`, `docs/OPERATIONS.md`, and `docs/ROADMAP.md`.
4. `docs/adr/` — accepted, historical architecture decisions.
5. Current code, tests, migration history, Git status, and only then external reference sites when research is warranted.

Do not treat a stale statement in an older checkpoint as a reason to reintroduce an abandoned implementation.

## 2. Current solution and hosting

REVESTIK is a **.NET 10 modular monolith**, not a SaaS or microservice architecture. There are **four application projects** and a separate test project:

| Project | Current responsibility |
| --- | --- |
| `src/Revestik.Api` | Single ASP.NET Core host; Minimal APIs, Identity, authorization, EF Core/SQL Server, business services, media, integrations, SSR hosting, and administration static assets |
| `src/Revestik.Client` | Blazor WebAssembly **internal administration** under `/administracion`; authenticated business workflows |
| `src/Revestik.Public` | Public marketing site using Razor Components/SSR; limited interactive server components for authorized inline editing |
| `src/Revestik.Shared` | Shared transport contracts; **not** the database domain model |
| `tests/Revestik.Api.Tests` | xUnit/API/SQL integration and security tests, including Testcontainers scenarios |

### One origin, one host

- The intended unified origin is `https://revestikcr.com`.
- The internal application is accessed through `https://revestikcr.com/administracion`, including Google OAuth login.
- The former `app.revestikcr.com` origin was superseded by the unified-domain decision; **do not design new cross-domain bridges**.
- The owner reports that the root public website remains restricted with **HTTP 401**, while administration is operational. This is a reported environment state; recheck the live environment before production claims.
- `Revestik.Api` hosts the public SSR site and administrative Blazor WASM assets in one deployment. **Do not start three independent servers for normal local development.**
- The legacy iframe/preview bridge still appears in repository code. It is **technical debt**, not the target architecture. Remove it only after replacement behavior is demonstrably safe, as an explicitly approved block.
- A repository commit is **not** proof that the corresponding image/revision is deployed to Azure.

### Local development baseline

From `C:\Projects\Revestik`, with configured SQL Server, development secrets, and .NET 10 SDK:

```powershell
dotnet run --project src/Revestik.Api/Revestik.Api.csproj --launch-profile https
```

- API origin: `https://localhost:7126` (verify `src/Revestik.Api/Properties/launchSettings.json` if changed).
- Admin: `https://localhost:7126/administracion`.
- Public site: same origin, with locale-based routes.
- `https://localhost:7081` and `http://localhost:5181` belong to the former separately launched client profile; they are **not** the recommended unified-host workflow.
- Some SQL integration tests require Docker Desktop/WSL2 and Testcontainers.

## 3. Completed baseline and evidence boundaries

### Internal application — existing business platform

The internal application already includes customers, quotations, sales, partial payments, FIFO inventory, suppliers, purchasing and accounts payable, expenses, received electronic tax documents (Hacienda XML), projects, reports/dashboard, users/roles, and external integrations. Its business rules are **out of scope** for public Projects unless a reviewed shared component can be reused without compromising separation.

### Public website — prior workstreams closed by owner

- Public site shell, Home, navigation, common visual design, responsive presentation, and localized routes.
- Public catalog/Finishes, product image presentation, category/subcategory sharing, and admin editing experience.
- Unified hosting and inline public-site editor with an Administrator security boundary.
- The owner states that the previously active flows have been completed. **Do not reopen Home, Finishes, catalog, or prior infrastructure work without a concrete defect or explicit new scope.**
- Main branch snapshot inspected for this handoff: `581aef9c99ddb7bbc1fffcbe49ec924699c980a2`, whose commit message concerns the Finishes editor and separate hero.
- Historic owner-reported test evidence includes `1,199/1,199` local regressions and `17/17` manual security scenarios. These are **historical reports**, not a fresh test run against the next Projects implementation or an Azure production attestation.

### Public Projects — existing scaffolding, not a finished content system

Current source contains:

- `src/Revestik.Public/Components/Pages/Projects.razor`
- `src/Revestik.Public/Components/Pages/ProjectDetail.razor`
- `src/Revestik.Public/Components/Pages/ProjectGallery.razor`
- `src/Revestik.Public/PublicProjectCatalog.cs`
- `src/Revestik.Public/PublicSeo.cs`
- `src/Revestik.Public/SiteLinks.cs`

The current gallery and detail pages use **illustrative, hard-coded records**, example slugs, and photograph placeholders. `PublicProjectCatalog.cs` explicitly says not to read the internal customer/project database and anticipates a reviewed published-only read model. This is the starting point: **reuse the existing views and routing; investigate persistence and administration before changing them**.

## 4. Non-negotiable engineering rules

1. **Reuse first:** inspect and reuse existing services, components, DTO patterns, EF configurations, validators, media storage, localization, SEO, error handling, tests, and styles. No duplicated catalog, taxonomy, image pipeline, or editor infrastructure without evidence of necessity.
2. **Simple architecture:** retain the modular monolith. Do not introduce a new app host, microservice, CQRS framework, message broker, generic repository layer, additional identity system, or new dependency just to build the public Projects page.
3. **Server authority:** frontend visibility is not security. Enforce role, resource scope, validation, publication state, upload checks, and mutations in the API/host.
4. **Domain boundaries:** internal `Projects` are private commercial/customer records; public portfolio projects are reviewed editorial content. **Never expose the internal Projects table, its documents, contact details, balances, payments, or customer records through anonymous site routes.**
5. **Explicit public read model:** anonymous APIs and media routes must return **published and approved public records only**. No business DTOs or internal entity serialization by accident.
6. **No silent financial/inventory effects:** publishing a public project or listing its materials must not create sales, move inventory, modify FIFO costs, register payments, or alter purchasing/fiscal workflows.
7. **Data integrity:** use EF Core/SQL Server conventions, suitable keys/FKs/constraints/indexes, optimistic concurrency where needed, transaction boundaries for multi-record writes, and reviewed migrations. **No destructive migration or automatic rollback assumption.**
8. **Security by design:** Identity/Google OAuth, secure cookies, Administrator-only editing, CSRF validation, upload validation, audit of privileged changes, rate limits where applicable, and private persistent media storage.
9. **KISS/DRY/YAGNI:** avoid abstractions or options with no demonstrated V1 use; prefer the simplest maintainable extension of current code.
10. **Production-aware quality:** clean code, descriptive naming, nullable correctness, cancellation tokens, bounded queries/pagination, no N+1 media reads, accessibility, responsive layouts, and predictable error states.
11. **Respect existing behavior:** avoid unrelated refactors, redesigns, formatting-only edits, reorganization, dependency upgrades, and configuration changes in a Projects block.
12. **Evidence-driven:** no invented routes, tests, tables, deployed revisions, successful results, or customer/project claims. Investigate first and state uncertainty.

## 5. Mandatory collaboration workflow

**INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP** is required for every coherent implementation block.

### INVESTIGATE — read-only

- Read latest GitHub `main`, relevant checkpoint/docs/ADRs, real source and test fixtures. Record the **actual commit SHA**.
- Review the user's local `git status -sb`/diff when supplied and identify unrelated modified/untracked files.
- Trace data model, routes, API authorization, editor components, image storage, translations, SEO, and existing tests before proposing code.
- Research public-project portfolio UX only to resolve an open design question. Distinguish inspiration from requirements.
- Present findings, conflicts, reuse opportunities, risks, and **specific decisions the owner must make**.
- **Do not write code, generate ZIPs, migrate databases, edit GitHub, or deploy.**

### PLAN — bounded proposal, no implementation

- Define a small coherent scope with stable task ID (`PUBLIC-PROJECTS-01A`, `01B`, etc.).
- List exact files expected to change, database/schema impact, security and API effects, dependencies, alternatives, and acceptance criteria.
- Explicitly flag anything requiring a migration or a new table/index. Avoid implementation details being presented as already approved.
- Prefer a vertical slice that can be independently built, tested, and smoke-tested.

### APPROVE — explicit owner decision

- Obtain the owner's clear approval of the proposed block **before** implementation.
- Approval for one block does not authorize adjacent features, migrations on real environments, commits, pushes, deletions, DNS, or Azure deployment.
- If investigation uncovers changed scope, return to PLAN and obtain approval again.

### IMPLEMENT — minimal approved changes

- Change only approved paths. Preserve local edits, backups, secrets, unknown files, and current public-site behavior.
- No speculative schema, duplicated UI, temporary bridges, new packages, or security shortcuts.
- Do not silently overwrite a file that differs from the reviewed repository version.

### VERIFY — evidence before claims

- User applies the reviewed changes locally and runs build, focused tests, relevant integration/security tests, sequential smoke tests, and full regression when appropriate.
- Compare exact results with the expected behavior, review `git diff --check`, and inspect the file list.
- State what ran, what passed, what failed, what was not run, and which commit/environment was tested. Never declare `passed`, `deployed`, or `secure` without supporting evidence.

### STOP — controlled closure

- Summarize delivered files, verifications, known defects, remaining tasks, and the next block.
- **Do not commit, push, create branches, merge, run production migrations, delete files, change DNS, or deploy** unless the owner separately authorizes that action.
- Do not continue to another block automatically.

## 6. File deliveries, ZIPs, Git, and safety

### Approved source-code changes

- Deliver **one complete, incremental ZIP for each explicitly approved coherent code block**, when a ZIP is the requested delivery form.
- Package **complete contents of only changed/new source files**, preserving exact paths relative to `C:\Projects\Revestik` (for example `src/Revestik.Public/...`).
- No extra parent directory; no partial file snippets presented as replacements.
- No installer, `.ps1`, automated apply script, generated binary, `bin/`, `obj/`, `.git/`, private data, backups, secrets, or unrelated files.
- Before delivery, inspect the archive's file inventory and verify it contains only approved paths. Identify any local conflict before telling the user to extract.
- Let the owner extract into the repository, inspect the diff, and run the verification commands. **Do not imply the assistant has edited the owner's Windows filesystem.**
- For documentation-only updates, provide a **complete individual `.md` file**, not a ZIP or executable, unless requested otherwise.

### Git discipline

- Start with `git status -sb` and `git rev-parse HEAD`; compare the reviewed GitHub SHA.
- Never use `git reset --hard`, `git clean -fd`, broad overwrites, or blanket staging (`git add .` / `git add -A`).
- Stage only explicitly approved paths. Review secrets, diff, and test evidence before any authorized commit or push.
- A committed source change is not necessarily deployed. Log the image digest/container revision and smoke-test outcome for release claims.
- User-controlled local configuration (including `src/Revestik.Api/appsettings.Development.json`) and backup files must not be altered incidentally.

## 7. PUBLIC-PROJECTS-01 — product goal and accepted boundaries

### Goal

Turn the existing public Projects section into a **small, elegant, responsive, maintainable portfolio of real, owner-approved work**. Visitors should see what REVESTIK has done, view photographs and relevant materials, and contact the business. **This is not a marketplace, customer portal, or a mirror of the internal project-management module.**

### Approved or previously established design intent

- **Existing public routes must be reused**:
  - `/es/proyectos` and `/es/proyectos/{slug}`
  - `/en/projects` and `/en/projects/{slug}`
  - `/de/projekte` and `/de/projekte/{slug}`
- Preserve the established site branding, responsive layout, typography, navbar/footer, and **Glassy image viewer**. Avoid an unrelated redesign.
- Use real project cards and a simple detail page; the administrator's inline mode should provide contextual **Edit** controls and a final neutral **“+ Agregar nuevo proyecto”** card, consistent with the existing visual editor.
- **Option A is the V1 direction:** a project detail may contain **up to five images** and a list of materials/finishes. The principal image is visually prominent. A visitor can inspect the gallery with the existing image viewer.
- **Option B (image hotspots)** was discussed but is **deferred**. Do not implement it without a new design approval.
- A project may show a category/type, title, description, editorial details, materials, and a contact CTA. Display location, company/client names, and project-specific claims **only with verified permission and accuracy**.
- Spanish is the default content language. Existing source provides English and German routes; preserve those paths. Requirements for translations, missing-language fallback, and per-language SEO text must be verified during INVESTIGATE.
- The public content must be independently reviewed/published; do not fabricate customer projects, photographs, completion claims, prices, product availability, or business participation.
- Admin editing is **within the public page**, not in a second admin application or a cross-origin iframe. Editor actions must remain invisible and inaccessible to anonymous users and unauthorized roles.
- The existing public products system may be reused for material/product references **only if investigation confirms an appropriate relationship**. Never link directly to private inventory or internal cost/stock information.

### Current code versus what remains to be designed

**Already in source:** localized listing/detail routes, SSR components, an example-only `PublicProjectCatalog`, placeholder cards/gallery, SEO helper, language switch behavior for known example slugs, and shared image viewer.

**Not established by this checkpoint as implemented:** persistent public-project entities, real published-project API, project management editor, image upload/delete for projects, verified production project content, project-specific database migration, or validated new indexing strategy. These require investigation, an approved plan, and tests.

### Open design questions — resolve in INVESTIGATE, not by assumption

1. **Public persistence:** extend the existing public content model or introduce a minimal `PublicProject` read/write model? Which tables, translations, images, relations, slugs, uniqueness constraints, and indexes are justified?
2. **Categories:** are a small set of editorial labels sufficient, or is a reusable category entity needed? Do **not** duplicate inventory/product taxonomy merely because both are named categories.
3. **Materials:** descriptive list versus optional references to existing **published public catalog products**; what should happen if a product is unpublished or deleted?
4. **Publication lifecycle:** create-and-publish confirmation, unpublish, edit, archive/delete, concurrency, and visibility on public routes. Reuse existing patterns where applicable rather than inventing drafts.
5. **Images:** principal image, gallery (maximum five total), existing validated upload/storage/cleanup, ordering, localized alt text, and replacement behavior.
6. **Localized URLs:** immutable shared slug versus language-specific slug; existing `SiteLinks.LanguagePath` assumes recognized example slugs. Define behavior before changing URLs.
7. **Editorial data:** which real projects, images, participation descriptions, client permissions, and optional location details may legally and accurately be published?
8. **Presentation:** should Projects reuse the Home/Finishes hero or keep the current header? Decide only after reviewing the existing visual hierarchy and owner's preference.
9. **SEO:** metadata, canonical/hreflang, 404 for unknown slugs, performance, image sizes, accessible cards/gallery, and prelaunch `noindex,follow` handling.
10. **Security:** Administrator authorization, antiforgery, concurrency, media access, audit events, error leakage, safe input/output, and anonymous published-only reads.

## 8. Proposed work sequence (not authorization)

| Block | Target | Exit evidence |
| --- | --- | --- |
| `PUBLIC-PROJECTS-01-INVESTIGATE` | Inspect current repo, editor/catalog/media patterns, public routes, internal/public separation, and owner UX decisions | Written investigation + unresolved decisions; **no code** |
| `PUBLIC-PROJECTS-01A` | Minimal public-project domain/schema and approved migration, if needed | Reviewed schema/constraints/indexes, focused SQL tests; no production migration |
| `PUBLIC-PROJECTS-01B` | Secure admin mutations and published-only public reads, using established media pipeline | Auth/CSRF/validation/concurrency/publishing tests |
| `PUBLIC-PROJECTS-01C` | Inline administration within public pages: contextual edit and add card, images and materials | Local admin/non-admin/anonymous UI smoke tests |
| `PUBLIC-PROJECTS-01D` | SSR listing, detail, responsive gallery, locale navigation, SEO, link to Contact | ES/EN/DE, missing slug 404, published-only, mobile/desktop verification |
| `PUBLIC-PROJECTS-01E` | Accessibility, privacy/content approval, security and performance verification, full regression | Release-candidate evidence; deployment remains separately gated |

The block breakdown is **provisional**. After INVESTIGATE, collapse or reorder blocks if that reduces duplication and unnecessary complexity. Each implementation block still requires its own approved PLAN.

## 9. Verification and acceptance checklist

### Engineering verification (after an approved implementation)

From the repository root, run the commands appropriate to the approved block:

```powershell
git status -sb
git diff --check
dotnet restore Revestik.sln
dotnet build Revestik.sln --configuration Release
dotnet test tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj --configuration Release --no-restore
```

- Add focused tests by specific filter/project when appropriate; retain SQL Server integration tests for data constraints and API behavior.
- Verify optimistic concurrency, bad input, unauthorized/forbidden requests, CSRF, image limits, and cleanup/rollback of failed multi-record operations.
- Smoke-test **in sequence**, record result per step: anonymous view, published-only listing, invalid slug 404, language switching, gallery, mobile layout, admin edit, non-admin denial, CSRF denial, persistence after reload, unpublish, and relevant media.
- Verify that no internal customer/project/file endpoint or record becomes anonymously readable.
- Confirm no unrelated files, secrets, or environment configuration changed and that the delivered ZIP contains only approved files.
- Re-run full regression and public website smoke tests before any release request. Do not invent passing counts.

### Production and publication gate

- **No public launch and no production migration/deployment based merely on this checkpoint.**
- Confirm real content ownership/consent, privacy/cookie/legal text, accessibility, security, SEO, responsive presentation, performance, correct public indexing policy, and all public pages together.
- Require separate owner approval for each production migration and Azure rollout; verify deployed revision, health, admin login, and public behavior afterward.
- Remove prelaunch `noindex`/access restrictions only through the planned and approved site launch procedure; do not silently make unfinished pages publicly indexable.

## 10. Exact next-chat starting instruction

**Start at `PUBLIC-PROJECTS-01-INVESTIGATE` only.**

Read `AGENTS.md`, this checkpoint, the current `main` commit, relevant architecture/business/security/operations docs, and the existing `Projects.razor`, `ProjectDetail.razor`, `ProjectGallery.razor`, `PublicProjectCatalog.cs`, `SiteLinks.cs`, `PublicSeo.cs`, editor components, public catalog/media endpoints, EF configurations/migrations, and available tests.

Produce, **in Spanish**:

1. What can be **reused unchanged**, what must be extended, and what is demonstrably placeholder or duplicated.
2. The minimum feasible public-project data model, with alternatives and practical index/relationship considerations **without executing or creating a migration**.
3. Security, privacy, localization, SEO, mobile, media, and test implications.
4. The few consequential owner decisions needed before planning; present concrete UX alternatives where necessary.
5. A proposed short phased PLAN with affected files and acceptance criteria **for approval only**.

**Do not implement, generate source ZIPs, commit, push, migrate databases, deploy, or reopen completed Home/Finishes work during this investigation. STOP after the investigation and proposed plan.**

---

**Checkpoint closure:** previous public-site workstreams are considered complete by the owner; the only active feature-development target is the public Projects portfolio. The GitHub HEAD above is the verified read baseline; local synchronization is owner-confirmed. Maintain this document as a compact working snapshot, not an accumulating history log.