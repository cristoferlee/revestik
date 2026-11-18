# REVESTIK — Public Website Master Checkpoint

**Checkpoint date:** 2026-11-18
**Active workstream:** `PUBLIC-AUTH-01` — approved business design; implementation NOT started
**Next implementation block:** `PUBLIC-AUTH-01A` — internal authorization hardening, pending its own detailed approval
**Repository:** https://github.com/cristoferlee/revestik
**Branch:** `main`
**Verified GitHub source baseline (before this document update):** `75bf93d5cf26e059f0d065348ee536858df182e6`
**Workspace:** `C:\Projects\Revestik`
**Owner-facing conversation:** Spanish or English, as the owner prefers. **Technical documentation:** English.

> **State boundary:** The Public Projects closeout is committed in the verified GitHub baseline. The owner reports the Contact/WhatsApp work is implemented and works **locally**, but it is not confirmed synchronized to GitHub. Public customer authentication, cart ownership and quotation selection are **approved/planned**, not implemented. No production/public launch has been authorized through this checkpoint.

---

## 1. Mandatory reading order and sources of truth

**Read `AGENTS.md` FIRST in every new chat, before suggesting code, files, Git operations or deployments.** Then read this checkpoint and the relevant sections of:

1. `docs/ARCHITECTURE.md`, `docs/BUSINESS_RULES.md`, `docs/SECURITY.md`, `docs/DEVELOPMENT.md`, `docs/OPERATIONS.md`, and `docs/ROADMAP.md`.
2. Accepted ADRs in `docs/adr/`, especially `002-authentication.md` and database architecture decisions.
3. Current repository code, authorization policies, data model, migrations, tests, CI and the user's **actual local Git state**.
4. External official framework/provider documentation only when current behavior, security requirements or package versions need verification.

**Precedence:** actual source and measured execution evidence > accepted owner decisions and ADRs > current checkpoint > older checkpoint claims > AI memory or assumptions. Never invent missing endpoints, database tables, test passes, Azure state or source synchronization. Keep this document a maintainable handoff, **not a transcript**.

### Evidence labels

Always distinguish **owner-approved design**, **implemented in source**, **owner-reported locally tested**, **GitHub-synchronized**, **production-verified**, and **publicly enabled**. One status never implies another. An uploaded ZIP, successful build, Git commit or HTTP 401 alone is not proof of a secure deployed feature.

## 2. Architecture and existing application boundaries

REVESTIK is a **.NET 10 modular monolith** with one intended deployment/origin, not a distributed application:

| Project | Responsibility |
| --- | --- |
| `src/Revestik.Api` | Single ASP.NET Core host; Minimal APIs, Identity/Google OAuth, role policies, EF Core/SQL Server, business services, integrations, public SSR hosting and admin assets |
| `src/Revestik.Client` | Authenticated administration with Blazor WebAssembly under `/administracion` |
| `src/Revestik.Public` | Public multilingual website built on Razor Components/SSR with carefully scoped authorized editing |
| `src/Revestik.Shared` | Shared HTTP contracts/DTOs, not a competing domain/persistence layer |
| `tests/Revestik.Api.Tests` | Test project, not a fifth app; xUnit, HTTP/API and SQL/Testcontainers coverage |

- Intended domain: `https://revestikcr.com`. The earlier separate admin subdomain and iframe editing bridge are **historical** and must not be reintroduced as architecture.
- Current local standard: run **`Revestik.Api` only**, which serves both public SSR and admin WASM. Check `Properties/launchSettings.json` for the actual local port; the previously used development base is `https://localhost:7126`.
- The owner previously reported `/administracion` working in Azure and the public root returning 401 before launch. Those are **historical environment observations**; independently verify real ingress and deployment before any release claim.
- Legacy iframe/`postMessage` editor code may still be present. Removing it requires its **own approved and tested cleanup block**; never mix removal with authentication.
- Public marketing products are independent of internal inventory products. Shared classification uses existing product categories, subcategories and units, but public publishing must not affect FIFO stock, pricing, sales, internal quotation identity, payments or Hacienda documents.
- The internal `Customer` entity represents business/fiscal customers and carries different validation/identification requirements. **Do not automatically create it for public signups**.

## 3. Verified workstream baseline and pending local reconciliation

### 3.1 Internal business application (existing)

Includes authorized users/roles, customers, quotations, sales, partial payments, FIFO inventory, purchasing/accounts payable, suppliers, expenses, tax document handling, projects, dashboard/reports and external integrations. These remain protected business modules; PUBLIC-AUTH must not change their financial, inventory or domain rules.

### 3.2 Public site, catalog and projects (implemented in source)

Existing public website includes Home, localized ES/EN/DE routes, navigation, public Acabados/Finishes, shared taxonomy, image galleries, editorial content and restricted inline editing. The owner has considered earlier public-site workstreams complete subject to the remaining release gates. Do not reopen functioning features without a reproduced defect or approved scope.

**Public Projects** are separate editorial portfolio records, not internal customer projects. GitHub `main` commit `75bf93d5cf26e059f0d065348ee536858df182e6` contains 19 changed files including the prior checkpoint revision and source/test changes. Implemented design:

- Public-project foundation migration `20261111221243_AddPublicProjectsFoundation` and persisted project, translation, space, image and material-tag models.
- Administrator-only create/edit/publish/unpublish/delete, version validation, media upload and publication checks; anonymous API and SSR read only **published/eligible** content.
- Project listing/details in ES/EN/DE with stable slugs, localization, responsive gallery, proper missing-project 404 and existing image viewer.
- Up to five photographs per project; material hotspots up to five per photograph; links only to eligible public Acabados products, retaining safe name snapshots when products become unavailable.
- Material-tag changes use individual concurrency safeguards and do not require matching photograph image metadata versions. Keyboard-accessible coordinate editing was included in the local verification scope.
- Public media never discloses unpublished/private records or internal projects, customer contact details, balances, payments or project documents.

**Owner-reported verification for 2026-11-17:** full regression 1,244 passed / 0 failed / 0 skipped (duration 883.2 s); 44 broader Projects/hosting tests; 17 earlier focused tagging tests (subsets, not extra distinct tests); 7/7 HTTP routes; 6/6 material-tag smoke checks; persistence after restart; staged integrity checks and targeted Gitleaks scans without detected leaks. These are historical **local** reports, not current CI/production attestations.

### 3.3 PUBLIC-CONTACT-01 / 01B (owner-confirmed LOCAL ONLY)

The owner confirmed that the Home and navbar WhatsApp contact buttons work locally, that the Home's bottom section now has one contact CTA, and that the updated solution built successfully (39.1 s) with a manual browser smoke test. **Do not repeat or store phone numbers/email addresses in checkpoints.** The local changes were not part of the verified GitHub source baseline. Do not overwrite them or claim they are deployed/committed without checking local and remote Git.

At the last owner-reported Git status, unrelated `src/Revestik.Api/appsettings.Development.json` changes and two malformed incidental untracked files existed. **Never stage, delete, modify, clean or overwrite these incidentally.** The owner must supply fresh Git status/diff before any coding or archive extraction.

### 3.4 Remaining public release gates

Production media durability; content ownership/consent; privacy/legal text; accessibility/responsive/performance; Google/cloud config and sign-in; complete public route and API permissions; SEO/canonical/noindex; verified production revision/image, migrations and backups; public enablement only by **separate owner authorization**. A source commit does not deploy itself.

## 4. Rules for writing and reviewing code

1. **INVESTIGATE BEFORE WRITING:** Trace actual endpoints, role policies, services, EF entities/configurations/migrations, DTO contracts, UI, test fixtures, runtime paths, Git state and existing reusable components.
2. **KISS / DRY / YAGNI:** Small, maintainable vertical changes; reuse before adding abstractions. Remove only *proven* duplication in separately approved refactors; avoid speculative infrastructure.
3. **One modular monolith:** No invented microservices, CQRS, second app host, generic repositories, brokers, custom authentication/session engines, parallel catalogs or extra databases unless a real requirement and separate approval justify them.
4. **Use official mature .NET libraries:** ASP.NET Core Identity/Google external authentication/cookies, authorization, EF Core/SQL Server, existing xUnit/Testcontainers, QuestPDF only when a PDF feature is approved. Prefer maintained .NET 10 packages; coordinate upgrades as an independent, tested change.
5. **Server is authoritative:** Browser hiding, route names, claims supplied by clients or editor preferences never grant permissions. Enforce roles, resource ownership, publication eligibility, business validation, CSRF, rate limits, and file access in trusted API/host code.
6. **Separate domains:** Public visitors, public editorial content, private business customers, official quotations, inventory cost/stock, sales and internal projects must never be conflated. Preserve all FIFO, fiscal, transaction and historical invariants.
7. **EF and database correctness:** Explicit relationships/constraints/indexes when needed; deterministic pagination; safe concurrency/transactions; no N+1 query proliferation. Review every migration and real data impact before execution. Shared taxonomy migration `20261104230000_ConsolidateSharedProductTaxonomy` forbids automatic rollback assumptions.
8. **Secure by default:** Least privilege, authenticated-user vs employee-role distinction, Google provider ID verification, secure same-origin Identity cookies, antiforgery on state changes, safe redirects, session revocation, minimal OAuth scopes, sanitized logs and no secrets in source.
9. **Clean production-quality C#:** Descriptive names, clear methods, nullable correctness, typed contracts, cancellation tokens for I/O, explicit validation, helpful deterministic errors, appropriate tests, no unnecessary framework wrappers, no dead/duplicate implementations.
10. **Stable UX:** Preserve ES/EN/DE localization, SSR navigation, accessibility (keyboard, focus, labels), responsive layouts, SEO and existing editor controls. No unrelated redesign in a security block.
11. **Minimal diffs:** Change only preapproved files; do not include unrelated refactors, formatting-only sweeps, generated artifacts, backups or configuration edits. Never clobber changed local files.
12. **No unverified claims:** A passing compilation is not a passing test suite, a static source review is not a penetration test, and Git synchronization is not production verification. Report limitations explicitly.

## 5. Mandatory workflow — EVERY block, no exceptions

**INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP**

| Stage | Required output and boundary |
| --- | --- |
| **INVESTIGATE** | Read `AGENTS.md`, current sources, docs, migrations, tests and Git. Identify existing functionality, evidence and uncertainty. **No code or packaging**. |
| **PLAN** | Name a small block ID, its goal, exact proposed files, security/data effects, exclusions, dependencies and acceptance tests. No speculative functionality. |
| **APPROVE** | Owner explicitly approves **that implementation block**. Approval of overall product rules does not authorize every block or its successors. |
| **IMPLEMENT** | Only the approved change, with complete reviewed files when requested. Preserve uncommitted local work and secrets. No scripts/installers. |
| **VERIFY** | User/local build and focused + appropriate SQL/HTTP/security tests, full regression where justified, sequential browser smoke, `git diff --check`, exact diff and source inventory. |
| **STOP** | Report implemented vs tested vs pending; halt for owner acceptance. Never automatically start next block, stage/commit/push, migrate production, change OAuth Cloud settings, deploy or enable indexing. |

**Critical:** Git operations that alter source control, production changes, data migrations and deployment each require their own specific authorization. The owner may authorize a **documentation-only** GitHub update without approving application code changes.

## 6. Source deliveries, ZIP protocol, PowerShell and Git discipline

- The owner does **not** want installers or apply scripts. **Never** send `.ps1`, installers, generated binaries or scripts that automate applying a patch. This includes scripts embedded in archives.
- When an implementation block has been **specifically approved and the owner requests a ZIP**, deliver **one incremental ZIP for that block** containing the **complete contents only of the approved changed/new files**. Retain exact paths relative to `C:\Projects\Revestik` (e.g. `src/Revestik.Api/...`); no extra top-level parent folder, snippets masquerading as complete files, `bin/`, `obj/`, `.git/`, secrets, private data, backups or unrelated files.
- Before delivering a ZIP, examine archive inventory, compare reviewed base commit to the user's actual local file changes, and identify conflicts. **Do not recommend unconditional overwrite if the file differs**; a preflight error means STOP and reconcile, not continue to extract. The assistant cannot directly edit the owner's Windows filesystem.
- If the owner requests plain PowerShell instructions, send short, directly copy/pasteable, **non-destructive** commands, no placeholders requiring manual substitutions. Avoid variables that collide case-insensitively with read-only PowerShell automatic variables (e.g. `$home` is `$HOME`). Do not use guessed hashes or keep executing after a guard fails.
- For **documentation-only** changes, use a complete standalone `.md` file (or an explicitly approved direct GitHub file edit), **not** a ZIP. Document Git write failures honestly.
- Begin code work with `git status -sb`, `git rev-parse HEAD`, GitHub HEAD and affected file diffs. GitHub does not show local uncommitted changes. Never use `git reset --hard`, `git clean -fd`, broad extraction overwrites, blanket `git add .`/`git add -A`, or staging unrelated files. Stage only explicitly approved paths after diff/security review.
- Do not publish secrets or personal/customer/staff data in public GitHub, logs, issue descriptions, example code, tests or screenshots. Public business contact details are *not automatically credentials*, but their inclusion in Git/docs requires an explicit owner publication decision. Reference configuration keys rather than embedding real numbers/email addresses. Store Google client secrets and other credentials in environment secrets/secure cloud configuration.
- Preserve `appsettings.Development.json` and any unrelated unknown/untracked local files. Never delete them automatically. A green local test does not authorize a commit; a Git commit does not authorize a deployment.

## 7. PUBLIC-AUTH-01 — owner-approved business rules

**Approval recorded 2026-11-18:** Login is **optional** for anonymous catalog viewing and the future quotation-request action; **Google OAuth only** for public registration. Rules PUB-AUTH-001 through PUB-AUTH-008 accepted as the design baseline. Any ambiguity or widened scope returns to PLAN/APPROVE.

- **PUB-AUTH-001 — Registration:** A public visitor may register through Google with a verified email *once public registration is separately enabled*; no local public passwords.
- **PUB-AUTH-002 — Identity:** Reuse existing `ApplicationUser`, Identity and secure cookies; public accounts have a distinct `PublicCustomer` role, without another auth database/system.
- **PUB-AUTH-003 — Least privilege:** Public accounts never gain employee, admin, editor, finance, inventory, internal project or internal customer permissions merely from authentication.
- **PUB-AUTH-004 — Minimal data:** Initial registration needs only user ID, display name and verified email. Province is for future quotation requests; phone is not required for WhatsApp text requests.
- **PUB-AUTH-005 — Optional authentication:** All eligible published catalog, projects and Home reads remain anonymous; future quotation-by-WhatsApp requests may be sent without signing in.
- **PUB-AUTH-006 — Cart isolation (future):** A persisted cart, if later approved, belongs to exactly one authenticated public account. Every server operation checks ownership.
- **PUB-AUTH-007 — Request isolation (future):** Saved requests/history are private to their Identity owner; a visitor cannot read or modify another customer's requests.
- **PUB-AUTH-008 — Staff separation:** Public signup never creates, links into, modifies, merges with or promotes staff accounts.

### Design-time security invariants for block implementation

These are **required engineering checks**, not already-implemented or separately tested facts:

- Resolve Google identities using provider + stable provider key, rather than automatically linking to a privileged staff identity solely by verified email. Handle collisions safely.
- Public registration can assign only `PublicCustomer`. Existing `RoleNames.All` refers to **internal** Administrator, Accountant, Sales and Warehouse roles; never append `PublicCustomer` to that array or reuse staff policies.
- Require explicit employee-only authorization for private endpoints. Audit ASP.NET Core **DefaultPolicy vs FallbackPolicy** and every unqualified `.RequireAuthorization()`/group authorization; ordinary authentication is insufficient after public users exist.
- Session validation, inactivity/disabled-user rejection, safe OAuth state and redirects, CSRF on cookie-authenticated mutations, rate limits, no publicly cached private identity markup, no broad OAuth scopes or token disclosure.
- For future cart/request models, derive user ID **from the authenticated server principal**, never trust a browser-supplied owner ID; enforce SQL query scoping and mutation authorization; test accounts A/B.
- Do not use existing fiscal/commercial internal `Customer` as an anonymous/public registration profile.

### Confirmed source review (not a security penetration test)

- `src/Revestik.Api/Endpoints/AuthenticationEndpoints.cs`: Google challenge/callback, staff authorized user or bootstrap admin; provider-key lookup followed by fallback email lookup; redirect restriction currently oriented to `/administracion`.
- `src/Revestik.Api/Extensions/AuthenticationExtensions.cs`: existing Identity, secure cookie, Google middleware and authorization policies; default/fallback semantics need an employee/public audit.
- Several internal dashboards and related read endpoints have bare `.RequireAuthorization()`; they must not become accessible to an authenticated public customer.
- `src/Revestik.Api/Services/Users/UserAdministrationService.cs`: user listing and role assumptions are staff-oriented; new public identities must not silently enter staff management.
- `src/Revestik.Public/Hosting/PublicSiteHostingExtensions.cs`: public Razor component mapping is anonymous. Protect private user-specific routes/operations explicitly, never assume a UI attribute overrides globally anonymous mapping.
- Microsoft identity packages already exist. Coordinated supported .NET 10 updates belong in a **separate tested dependency-maintenance block**, not an opportunistic auth refactor.

## 8. PUBLIC-AUTH-01 — bounded implementation checklist

**Every checklist block is independently INVESTIGATED, PLANNED, APPROVED, IMPLEMENTED, VERIFIED, then STOPPED.** A checkbox stays empty until supported by actual source/test evidence. Changes outside each named block return to explicit approval. File paths below are *investigation candidates*, not advance authorization to edit all of them.

### PUBLIC-AUTH-01A — employee-only API boundary (FIRST)

**Goal:** Prevent an authenticated `PublicCustomer`-like principal from accessing internal services **before** public registration exists.

- [ ] Inventory every API endpoint/group and relevant middleware; classify routes as anonymous, shared-authenticated, employee-only or admin-only; inspect explicit and fallback/default policies.
- [ ] Find bare `.RequireAuthorization()` and accidental generic authentication checks; document the effective authorization matrix.
- [ ] Apply minimal employee-only policies to private routes while retaining more restrictive existing named policies, correct shared `/api/auth` semantics and public read access.
- [ ] Review `RoleNames.All` and staff administration assumptions; ensure they cannot incorporate public roles. Do **not** provision public accounts yet.
- [ ] Write focused HTTP tests: anonymous private = 401; public-role principal private = 403; authorized employee succeeds; anonymous public catalog works; editor still requires Administrator.
- [ ] Build/test approved files, inspect diff, report residual unknowns and STOP for acceptance before 01B.

**Review candidates:** `Extensions/AuthenticationExtensions.cs`, `Authorization/PolicyNames.cs`, selected API endpoints, `Services/Users/UserAdministrationService.cs` if proven necessary, focused authentication/endpoint tests.
**Explicit exclusions:** public registration, new database tables/migrations, public UI, carts, PDFs, WhatsApp, quotation workflow, dependency upgrades, Azure/Google Cloud changes.
**Acceptance:** complete reviewed route inventory and evidence that private routes deny public principals without regression of employee/admin/anonymous reads.

### PUBLIC-AUTH-01B — public Google Identity backend (AFTER 01A)

**Goal:** Reuse existing Google middleware and Identity, with distinct safe public signup and no implicit staff account linking.

- [ ] Trace current challenge/callback, provider-key and email-verified claims, bootstrap admin, rate limiting, correlation state, cookies, return paths and environment configuration.
- [ ] Implement clearly differentiated public vs internal login intent; reuse existing middleware; validate safe return destinations and OAuth state.
- [ ] Resolve identities by Google provider/stable subject. On email/provider conflicts with existing accounts, fail closed; never auto-attach a new Google identity to staff by email alone.
- [ ] Provision only `PublicCustomer` with safe transactions/constraints for duplicate or concurrent signups and failed role linking; check inactive accounts, audit and session revocation.
- [ ] Keep public signup **disabled by default** in all environments pending explicit release approval; test disabled and enabled modes.
- [ ] Add focused Identity/SQL/HTTP/Google test coverage, verify existing staff Google login and CSRF/logout behaviors, then STOP.

**Review candidates:** `Endpoints/AuthenticationEndpoints.cs`, `Extensions/AuthenticationExtensions.cs`, `Data/IdentitySeeder.cs`, role/policy declarations, focused tests and minimal configuration. Reuse `ApplicationUser` unchanged if adequate.
**Explicit exclusions:** password/JWT auth, second user system/table, fiscal `Customer` data, cart, quote/PDF, UI redesign, production OAuth settings/deployment.
**Acceptance:** public signup cannot impersonate/link to employees; only public role is assigned; staff flow survives; production signup remains disabled.

### PUBLIC-AUTH-01C — public navbar login/session UX (AFTER 01B)

**Goal:** Minimal public sign-in, signed-in user indicator and sign-out; no public portal/cart.

- [ ] Add accessible ES/EN/DE Google login entry; anonymous browsing remains unchanged.
- [ ] Add name/account indication and CSRF-protected logout with safe session-expired behavior; keep identity markup out of shared public caches.
- [ ] Preserve Home, navbar WhatsApp, Finishes, Projects, restricted inline editor and `/administracion` behavior.
- [ ] Run focused UI/security tests and desktop/mobile three-locale smoke; review exact diff and STOP.

**Review candidates:** `src/Revestik.Public/Components/Layout/`, narrowly required public SSR components/styles, small auth endpoints/contracts only if justified, tests.
**Explicit exclusions:** saved carts/quotes, profile table, province form, password pages, customer dashboard, PDF, general redesign.
**Acceptance:** anonymous browsing works; verified Google account sees only its own identity and can log out; internal app remains protected.

### PUBLIC-AUTH-01D — integrated local security and regression acceptance (AFTER 01C)

**Goal:** Independently verify the completed auth feature and report release blockers.

- [ ] Release build, relevant Identity/role tests, HTTP authorization tests, SQL/Testcontainers tests and complete regression on the exact source revision.
- [ ] Verify anonymous, public customer A/B, staff and Administrator matrix: private dashboard/customer/inventory/finance/user endpoints, editor denial, published public reads, 401/403 semantics.
- [ ] Test Google verified/unverified identity, email/provider collision, concurrent signup, callback/correlation, hostile redirects, CSRF/logout, cookie settings, expired/disabled sessions and rate limits.
- [ ] Execute local real Google login smoke, mobile/desktop ES/EN/DE, inspect `git diff --check`, file inventory, secrets and selective Git scope.
- [ ] Document actual passed/failed/unrun tests, owner smoke approval and STOP. Fix only verified in-scope defects; request approval for scope changes.

**Explicit exclusions:** invented fake cart/request tables simply to pass ownership tests, production migration/deployment and unrelated refactors.
**Acceptance:** owner-reviewed evidence-based local security matrix and appropriate regression. Actual cart/request A-vs-B tests are mandatory later **when those resources exist**, not now.

### PUBLIC-AUTH-01E — production readiness REVIEW ONLY (independent gate)

**Goal:** Recommend or block release; **not** enable registration or deploy.

- [ ] Verify real Azure revision/digest, HTTPS/proxy trust, secure cookies, persistent private Data Protection keys, database migration/backup state and environment separation.
- [ ] Verify Google Cloud external consent/eligibility, redirect URIs, secrets storage, account disable/rollout mechanism, rate limits, privacy/legal notices and production behavior.
- [ ] Produce rollout/rollback and production-smoke plan. Obtain distinct approval for any cloud setting change, registration enablement, migration, Git operation or deployment.
- [ ] Record verified evidence and STOP; no public indexing or removal of prelaunch access restrictions without separate authorization.

**Explicit exclusions:** live OAuth changes, feature enabling, Azure deployment, SQL migration, DNS/indexing changes under a design/checkpoint approval.
**Acceptance:** explicit owner-approved release decision, target revision and evidence plan. Local success never implies production security.

## 9. Deferred workstreams — NOT included in PUBLIC-AUTH-01

- **PUBLIC-QUOTE-01:** Later choose one or more **published public catalog** products and quantities in existing selling units, collect name/province as appropriate, prepare readable itemized **WhatsApp `wa.me` text**. Login remains optional. No WhatsApp API, automatic PDF attachment, internal COT, price/tax guarantee or inventory reservation by default.
- **PUBLIC-CART-01:** Only if persistent customer carts are approved separately: model `ApplicationUser.Id` ownership, SQL FK/unique/indexes/concurrency, row-scoped reads/writes, A/B isolation tests and data cleanup. No shared carts or client-supplied owner IDs.
- **PUBLIC-WEB-RELEASE:** Full public-site smoke, real imagery/content, legal/privacy, accessibility/performance/SEO and independent Azure production/public-indexing approval.
- **ARCHITECTURE-REFACTOR:** Comprehensive evidence-driven codebase review and removal of verified duplication **after** core features are complete, in separately approved changes with full regression.
- **DEPENDENCY-MAINTENANCE:** Confirm current supported .NET 10 patch versions and review coordinated upgrade separately; do not blend updates into PUBLIC-AUTH security behavior changes.

## 10. Verification commands and evidence policy

From the repository root, use **only** commands appropriate to the approved block; commands below are reference commands, **not a script to apply changes**:

```powershell
git status -sb
git rev-parse HEAD
git diff --check
dotnet restore Revestik.sln
dotnet build Revestik.sln --configuration Release
dotnet test tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj --configuration Release --no-restore
```

Run focused test filters and SQL/Testcontainers scenarios where relevant, then full regression before release. Validate auth HTTP codes, anti-CSRF, concurrency, publication filters and owner-specific resource permissions. Report exact environment, revision, test names and actual counts; distinguish historical results from fresh execution. Never stage unrelated files or publish secrets.

## 11. Exact next-chat handoff

1. **READ `AGENTS.md` FIRST**, then this complete checkpoint and relevant docs/ADRs. Inspect actual GitHub `main` and the user's local Git status/diffs; do not infer the user's workspace from GitHub.
2. Preserve owner-verified local WhatsApp changes, `appsettings.Development.json` and unknown/untracked files. If the checkpoint is not yet synchronized, reconcile the **documentation file only** before auth code work.
3. Treat PUB-AUTH-001 through 008 as **business decisions already approved**. Treat 01A–01E as the ordered **plan only**; each implementation block still requires specific owner approval.
4. **Next:** investigate/plan `PUBLIC-AUTH-01A` completely, especially all bare authenticated internal endpoints, DefaultPolicy vs FallbackPolicy, roles and tests. Present exact changed files, tests and no-schema boundary. **WAIT FOR APPROVAL BEFORE CODE.**
5. For each approved block: implement minimal source, reuse official .NET facilities, deliver source files as requested (ZIP only when requested and with exact full relative files), verify local build/tests/smoke/diff, **STOP**.
6. Never commit/push application changes, change Azure/Google Cloud, run production migrations, deploy, alter DNS or enable public access/indexing without **separate express authorization**.

**End state recorded:** Projects source Git-synchronized at the verified baseline; Contact improvements owner-reported locally verified but not confirmed Git-synced; public login/carts/quotation request are NOT implemented; current actionable step is the independently approved `PUBLIC-AUTH-01A` planning/authorization gate.