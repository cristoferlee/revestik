# REVESTIK — Public Website Master Checkpoint

**Active workstream:** PUBLIC-PROJECTS-01 — local verification complete; Git handoff pending
**Next step:** Verify the final accessibility change, confirm Git synchronization, then continue the separately gated public-site release review.
**Checkpoint date:** 2026-11-18
**Repository:** https://github.com/cristoferlee/revestik
**Branch:** `main`
**Historical GitHub baseline (2026-11-10):** `581aef9c99ddb7bbc1fffcbe49ec924699c980a2`
**Local workspace:** `C:\Projects\Revestik`
**Owner-confirmed baseline:** 18 project source/test files plus this checkpoint prepared for synchronization; development configuration and two incidental untracked files excluded. Confirm local and remote HEAD before new work.
**Owner communication:** Spanish. **Repository-facing documentation:** English.
**Status:** Public Projects implemented and verified locally. Confirm repository synchronization from Git; production deployment and public launch remain unauthorized.

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

### Public Projects — implemented and verified locally

The original illustrative Projects implementation has been replaced in the local working tree by persisted public portfolio content.

**Implemented locally:**

- Public-project foundation migration `20261111221243_AddPublicProjectsFoundation`, with project, translation, space, image and material-tag tables.
- Separate public editorial projects, independent of internal customer/project records.
- Administrator-only project management with creation, editing, publication, unpublication, deletion and validated photograph uploads.
- Published-only anonymous project APIs and media access.
- SSR listing and project detail components backed by `PublicProjectsReader`.
- Spanish, English and German routing, localized metadata and language navigation.
- Responsive gallery with up to five photographs per project.
- Material hotspots with up to five tags per photograph, linked to published Acabados products.
- Product-name snapshots retained when linked products become unavailable.
- Material-tag writes no longer require matching photograph `ImageVersion`; individual tag concurrency protection remains.
- Real HTTP 404 responses for unknown project routes.

**Evidence:**

The owner reports successful local persistence, public smoke tests, routing checks and full regression. See Section 9.

**Boundary:**

These are local implementation and verification claims, not production deployment or public-launch evidence. Confirm GitHub synchronization from the actual Git state.

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

## 7. PUBLIC-PROJECTS-01 — implemented architecture and accepted boundaries

### Product goal

Provide a small, elegant, responsive public portfolio of owner-approved work, with project photographs, relevant materials and a contact path.

This is not a marketplace, customer portal, or duplicate of the internal project-management module.

### Implemented architecture

- Reuse the existing .NET modular monolith, SQL Server, public SSR site and administrative security model.
- Keep public editorial projects independent of internal Projects, customers, inventory transactions and financial records.
- Use `PublicProjectReader` on the API for published-only data and `PublicProjectsReader` on the public SSR site.
- Reuse the existing public media storage and image validation pipeline.
- Retain the existing localized routes:
  - `/es/proyectos/{slug}`
  - `/en/projects/{slug}`
  - `/de/projekte/{slug}`
- Project slugs remain stable after creation. Language switching preserves project context.
- Use one project cover and up to five photographs in total, with localized alternative text.
- Reuse the existing gallery/image viewer and contextual Administrator editing.
- Use fixed project types and space classifications; do not create unnecessary category infrastructure.
- Use material hotspots positioned relative to photographs, with a maximum of five tags per image.
- Link hotspots to public Acabados products through `ProductId` and the current published slug.
- Do not display product thumbnails inside the material-tag selector.
- The backend currently requires a registered product-cover record for product selection and public product availability.
- Retain the last published product name when a referenced product is deleted or unpublished; never expose unpublished product details.
- Keep optimistic concurrency for individual material tags.
- Offer keyboard-accessible numeric coordinates as an alternative to mouse/touch dragging in the tag editor; verify this interaction manually before publication.
- Photograph metadata versions are not required for independent tag writes.
- Keep Administrator authorization, antiforgery validation, publication filtering, upload limits and auditing.

### Explicitly excluded

- Internal project/customer data exposure.
- A second product catalog or independent material inventory.
- A new tagging service, manually maintained product URLs or external translation API.
- Unrelated application refactoring.
- Production changes without separate approval.

## 8. Local completion and remaining release gates

**Completed locally:**

Public-project persistence, administrative editing, photographs, material hotspots, public reading, localization, responsive presentation and routing verification.

**Source-control status:**

At handoff preparation, 19 intended files were staged: 18 project source/test files and this checkpoint. Verify the actual committed state using Git.

**Repository closure checks (verify against Git):**

- Review the complete staged diff and updated checkpoint.
- Re-run staged integrity and secret checks after documentation is added.
- Commit and push only with owner approval.
- Verify GitHub synchronization after pushing.

**Still pending before production/public launch:**

- Separate approval for production database changes and deployment.
- Verification of production media persistence, authentication and health.
- Review of actual project content, permissions and privacy requirements.
- Final accessibility, performance and public SEO/indexing review.
- Explicit approval before changing prelaunch access or `noindex` rules.

The comprehensive codebase refactoring and duplicate-code removal remain a separate later workstream.

## 9. Verification and acceptance checklist

### Local verification evidence — 2026-11-17

**Owner-reported results:**

- Full regression: **1,244 passed, 0 failed, 0 skipped**.
- Full test run duration: 883.2 seconds; successful build reported in 896.9 seconds.
- Broader Projects/hosting tests: **44 passed**.
- Earlier focused tagging tests: **17 passed**. These are subsets, not additional independent tests.
- HTTP routing: **7/7 passed** for published ES/EN/DE pages and missing project/API routes.
- Public material-tag smoke tests: **6/6 passed**, including link behavior, editing, deletion, mobile usability and persistence.
- Material-tag persistence was also verified after restarting localhost.
- Selective Git staging: 18 project source/test files.
- `git diff --cached --check`: passed.
- Gitleaks 8.30.1 staged scan: no leaks reported; output indicated zero commits and approximately 39.81 KB inspected.
- Four independent Gitleaks source/file scans: no leaks reported in the inspected targets.

These results apply to the inspected local environment.

They do not establish production readiness, complete security assurance or a separate Release-configuration build.

### Engineering verification (for subsequent changes)

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

Resume from the **PUBLIC-PROJECTS-01 repository handoff**, not the original investigation phase.

1. Read `AGENTS.md`, this checkpoint, the current Git status and staged diff.
2. Preserve the verified implementation and existing architectural boundaries.
3. Confirm the 19 intended files are staged or committed and the development configuration remains excluded.
4. Review this checkpoint alongside the implementation.
5. Validate the final diff, tests and security checks before a commit.
6. Request explicit owner approval before committing and pushing.
7. Verify local and remote commit identifiers match before considering repository handoff complete.

Do not add new architecture, reopen completed Projects functionality, remove unrelated files, migrate production, deploy or enable public indexing as part of this handoff.

**Current state:**

Public Projects are functionally verified locally.

Repository synchronization must be verified from Git; production release remains separately gated.
