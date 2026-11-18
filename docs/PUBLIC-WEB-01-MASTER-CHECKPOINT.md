# REVESTIK — Public Website Master Checkpoint

- **Checkpoint date:** 2026-11-18
- **Active workstream:** `PUBLIC-AUTH-01` — blocks 01A, 01B and 01C implemented and locally verified; 01D partly verified; 01E production review not started
- **Next bounded block:** `PUBLIC-AUTH-01D` — close remaining local security/UX verification and reconcile the exact Git diff; **do not implement new features without an approved plan**
- **Repository:** https://github.com/cristoferlee/revestik
- **Branch:** `main`
- **Owner-reported local `HEAD`:** `b85183a8b1b39e16a1f8ee1028a0e4fcaa8be28e`
- **Workspace:** `C:\Projects\Revestik`
- **Owner-facing conversation:** Spanish or English; technical documentation in English.

> **Critical status boundary:** PUBLIC-AUTH-01A/01B/01C source exists **only in the owner's uncommitted local working tree**, with local build, full regression and real Google browser smoke reported as passed. These changes are **not GitHub-synchronized, deployed, production-verified or publicly enabled**. The public root's historical production `401` and functioning administrative production login are earlier observations, not fresh production evidence. No commit, push, migration, deployment, live OAuth change, public release or indexing has been authorized.

---

## 1. Sources of truth and working rules

**Read `AGENTS.md` first in every new chat.** Then read this complete checkpoint and relevant sections of `docs/ARCHITECTURE.md`, `docs/BUSINESS_RULES.md`, `docs/SECURITY.md`, `docs/DEVELOPMENT.md`, `docs/OPERATIONS.md`, `docs/ROADMAP.md` and accepted `docs/adr/` decisions, especially `002-authentication.md`.

Source/evidence precedence: **actual current local source and measured tests > explicit owner decisions and accepted ADRs > this checkpoint > older historical checkpoints > recollection**. GitHub shows only committed data and cannot establish local source contents. Inspect `git status -sb`, `git rev-parse HEAD`, relevant local diffs, and remote refs before source changes or staging.

Use the evidence distinctions **approved design**, **implemented locally**, **locally tested**, **GitHub-synchronized**, **production-verified** and **publicly enabled**. Neither a ZIP nor a test pass is proof of deployment or release readiness.

### Mandatory engineering protocol

**INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP**.

- **Investigate:** inspect current source, dependencies, migrations, endpoint/role matrix, tests, affected runtime paths and Git working tree; do not edit.
- **Plan:** identify one bounded block, specific files, business/security effects, dependencies, exclusions and measurable acceptance criteria.
- **Approve:** obtain owner approval for that individual block, including separately approved changes if scope expands.
- **Implement:** minimal, maintainable source; reuse existing code; no unrelated cleanup, architecture expansion or redesign.
- **Verify:** release build, appropriate HTTP/CSRF/SQL/concurrency tests, full regression where justified, browser checks, diff, staging scope and secret review. Do not treat an empty filtered test run as a pass.
- **Stop:** report verified/unverified items and next approval gate. **Never independently commit, push, deploy, run production migrations, change live Google credentials or enable the public site.**

### Source delivery and local preservation

The owner uses Windows PowerShell and prefers reviewable full source files with exact repository-relative paths, rather than snippets or installer/apply scripts. When a ZIP is explicitly requested and approved, it contains **only complete changed/new approved source files**, no scripts, binaries, secrets, `bin/`, `obj/` or backups. Validate archive inventory and original file hashes; if local contents differ, **STOP rather than overwrite**. Documentation-only changes should be delivered as a complete `.md`, not a ZIP. Inline non-destructive PowerShell instructions are acceptable. Never use `git reset --hard`, `git clean -fd`, blanket staging or broad overwrites. Stage only explicitly approved files after inspection; stage/commit/push have not yet been approved.

Preserve unrelated local work: contact/WhatsApp components, locally edited `appsettings` files, and unknown malformed untracked files. Do not include them implicitly in an authentication change, delete them, or expose local credentials. Google OAuth development credentials are in .NET User Secrets; never print or commit client secrets.

---

## 2. Architecture and protected boundaries

REVESTIK is a **.NET 10 modular monolith** with one intended deployment/origin. The `Revestik.Api` host serves the internal administrative Blazor WebAssembly application under `/administracion` and the public Razor Components/SSR website on the main domain. The primary local development workflow runs **`Revestik.Api` only**.

| Project | Responsibility |
| --- | --- |
| `src/Revestik.Api` | ASP.NET Core host, Minimal APIs, Identity/Google OAuth, authorization, EF Core/SQL Server, business logic, file storage, public SSR and admin assets |
| `src/Revestik.Client` | Administrative Blazor WASM, under `/administracion` |
| `src/Revestik.Public` | Public ES/EN/DE SSR website, published catalog/projects, authorized inline editor |
| `src/Revestik.Shared` | Shared transport contracts/DTOs, not a second domain/persistence layer |
| `tests/Revestik.Api.Tests` | xUnit, HTTP security and SQL/Testcontainers tests; not a fifth application project |

- Intended domain: `https://revestikcr.com`. Earlier admin subdomain and iframe/`postMessage` bridge code are historical technical debt; removal is a separate verified block, **not** part of authentication.
- Historical public root `401` before launch is an environment observation, not proof every public route is globally blocked. Verify actual ingress and deployment before enabling public traffic.
- Internal business modules include customers, quotations, sales, partial payments, FIFO inventory, purchasing and accounts payable, suppliers, expenses, Hacienda documents, projects and financial reports. Their authorization, transaction, tax and historical invariants remain unchanged by public identity.
- Public editorial products and projects are **independent** of inventory and internal/customer projects. Shared classifications reuse the existing category/subcategory/unit catalogs; publishing does not change FIFO, fiscal prices, internal quotes or balances.
- Internal fiscal/commercial `Customer` is **not** created automatically on public Google signup. Public accounts reuse `ApplicationUser` and Identity.
- Server-side authorization is the source of trust. Frontend route visibility, button state or edit preference do not grant privileges. Published-only read rules, CSRF, role checks, audit and storage boundaries remain applicable.

---

## 3. Previous public-site work and preserved local changes

### 3.1 Public catalog, Home and editorial editor

The public website includes localized Home and navigation, Acabados/Finishes, public catalog images, shared taxonomy, publishing/visibility rules, responsive galleries and authorized inline editing. The owner considered these earlier workstreams functionally complete subject to public release gates. Reopen only reproduced defects or independently approved improvements. Anonymous published catalog/project reads remain available by design; privileged editor mutations require server authorization and antiforgery.

The legacy iframe/editor bridge may still exist. Do not remove it incidentally during authentication or contact changes.

### 3.2 Public Projects

The historical Projects baseline includes migration `20261111221243_AddPublicProjectsFoundation`, persisted project translations/spaces/images/material tags, administrator-only management, anonymous eligible published content, stable slugs, real HTTP 404 for missing projects, ES/EN/DE detail and listing, up to five images and up to five material hotspots per image, and safe published-catalog material links. These are public portfolio records, never internal project or customer records.

**Historical owner-reported verification 2026-11-17:** 1,244/1,244 full regression; broader projects/hosting, HTTP, tagging and browser persistence checks; focused Gitleaks scans without reported leaks. This is local historical evidence, not current cloud verification. The master checkpoint preceding auth was included in a prior local/GitHub documentation update; always check the current `HEAD` rather than assume that older hash is latest.

### 3.3 PUBLIC-CONTACT-01 / 01B — local changes, separate scope

The owner reported working Home and navbar WhatsApp contact actions and a single Home bottom contact CTA, with successful local build and browser smoke. These changes are **not confirmed committed or deployed** and remain distinct from PUBLIC-AUTH. The current `PublicLayout.razor` diff contains both the new account controls and an existing `WhatsAppContactLink` replacement for the header CTA. Stage/review that file carefully when Git operations are eventually authorized. Do not add actual business contact numbers or private addresses to this checkpoint.

### 3.4 Exact owner-reported Git working tree after auth verification

Owner-reported local branch: `## main...origin/main`, local `HEAD` `b85183a8b1b39e16a1f8ee1028a0e4fcaa8be28e`.

Authentication files modified (tracked):

- `src/Revestik.Api/Authorization/PolicyNames.cs`
- `src/Revestik.Api/Authorization/RoleNames.cs`
- `src/Revestik.Api/Data/IdentitySeeder.cs`
- `src/Revestik.Api/Endpoints/AuthenticationEndpoints.cs`
- `src/Revestik.Api/Extensions/AuthenticationExtensions.cs`
- `src/Revestik.Api/Services/Users/UserAdministrationService.cs`
- `src/Revestik.Public/Components/App.razor`
- `src/Revestik.Public/Components/Layout/PublicLayout.razor` (**also contains unrelated Contact/WhatsApp modification**)
- `tests/Revestik.Api.Tests/Authentication/CsrfEndpointTests.cs`
- `tests/Revestik.Api.Tests/Authentication/UserAdministrationEndpointSecurityTests.cs`
- `tests/Revestik.Api.Tests/Authentication/UserAdministrationSqlTests.cs`
- `tests/Revestik.Api.Tests/PublicCatalog/PublicCatalogEndpointAuthorizationTests.cs`

Authentication files newly created, untracked:

- `src/Revestik.Api/Services/Authentication/PublicGoogleSignInService.cs`
- `src/Revestik.Public/Components/Layout/PublicAccountControls.razor`
- `src/Revestik.Public/wwwroot/css/public-auth.css`
- `src/Revestik.Public/wwwroot/js/public-auth.js`
- `tests/Revestik.Api.Tests/Authentication/InternalEndpointAuthorizationBoundaryTests.cs`
- `tests/Revestik.Api.Tests/Authentication/PublicGoogleLoginBoundaryTests.cs`
- `tests/Revestik.Api.Tests/Authentication/PublicGoogleSignInSqlTests.cs`

Other existing local changes **out of auth scope**: `src/Revestik.Api/appsettings.Development.json`, `src/Revestik.Api/appsettings.json`, `src/Revestik.Public/Components/Pages/Home.razor`, `src/Revestik.Public/Components/Layout/WhatsAppContactLink.razor`, and two malformed incidental untracked filenames shown as `"t \357\200\242..."` and `"taged = @(git diff --cached --name-only)"`. Do not stage or delete these accidentally. Refresh this inventory before future edits; the owner has not confirmed Git synchronization for auth.

---

## 4. PUBLIC-AUTH-01 — approved business rules

**Owner approval recorded 2026-11-18.** Google OAuth is the only approved registration method for public users. Login is **optional** for browsing published catalog/Projects and future quotation-by-WhatsApp; public signup must not create an internal fiscal `Customer` or automatically confer staff access.

| Rule | Accepted behavior |
| --- | --- |
| PUB-AUTH-001 | Public visitor can register via verified Google identity **only where registration is explicitly enabled**; no local public passwords |
| PUB-AUTH-002 | Reuse existing `ApplicationUser`, ASP.NET Core Identity and secure cookies, with distinct `PublicCustomer` role; no second auth database |
| PUB-AUTH-003 | Public authentication never grants employee/admin/editor, inventory, finance, internal project or customer permissions |
| PUB-AUTH-004 | Minimal initial data: user ID, display name and verified email; province and other details only later when needed |
| PUB-AUTH-005 | Published public reads and future basic WhatsApp quotation requests may work anonymously |
| PUB-AUTH-006 | Future persisted cart belongs to one authenticated account, with server-side ownership enforced |
| PUB-AUTH-007 | Future saved requests/history are private to their Identity owner; no user A/B data disclosure |
| PUB-AUTH-008 | Public signup never creates, links, merges with, modifies or promotes employee accounts |

PUB-AUTH-006 and 007 are future design decisions, **not** evidence that cart/requests or associated ownership tests exist today.

---

## 5. PUBLIC-AUTH-01A — local implementation COMPLETE, verified

**Objective:** deny internal endpoints to authenticated public-role users **before** allowing public registrations.

- Added `PolicyNames.AuthenticatedUser` for shared identity/session operations (`/api/auth/me`, `/csrf`, `/logout`).
- Default and Fallback authorization policies now require authenticated **employee** role from `RoleNames.All` (Administrator, Accountant, Sales, Warehouse); `PublicCustomer` is excluded.
- Existing narrower named employee/admin policies remain in effect; the internal administration API is never unlocked by generic Google authentication.
- Employee user listing and lookup exclude public-only Identity accounts; public-only identities cannot be promoted via staff user management flows.
- New/expanded HTTP and SQL tests cover anonymous 401, public 403 for bare-auth/private endpoints, employee-authorized routes, admin/editor restrictions, CSRF, and published-only public catalog reads.

**Evidence:** owner-reported focused 01A checks passed, followed by the full post-01C regression of **1,306/1,306**; source/diff reviewed. No production test or deployment.

---

## 6. PUBLIC-AUTH-01B — local implementation COMPLETE, verified

**Objective:** distinct Google OAuth entry and callback for public customers, with no email-only staff account linking.

- `RoleNames.PublicCustomer` is a separately seeded Identity role, never appended to employee `RoleNames.All`.
- Public challenge `/api/auth/public/login/google` and public app callback `/api/auth/public/external-callback` are separate from admin `/api/auth/login/google` and `/api/auth/external-callback`. Both use the existing Google middleware `/signin-google` and protected OAuth state to bind the correct intent. Public return destination is normalized to `/es`, `/en` or `/de` only; no arbitrary external/internal return URL.
- Public registration requires Google `email_verified`, provider + stable subject; existing identity is looked up by provider key. Same-email/different-subject and employee account collisions **fail closed**, rather than link by email. Staff callback continues to allow **pre-authorized employees** to link their first Google identity, but checks employee role and rejects an already-linked mismatched Google subject before signing in.
- New public account is `ApplicationUser` with only `PublicCustomer` role. SQL `SERIALIZABLE` transaction binds Identity user, public role, provider login and audit; SQL duplicate-key (2601/2627) and deadlock (1205), including nested EF exceptions, are recognized and rejected as conflicts. Disabled or unverified identities are rejected. No new customer table, migration, password or JWT flow.
- Shared identity cookie operations are allowed for valid Identity users while internal business APIs still demand employee permissions.
- Public registration is **off by default and hard-disabled for production hosts**, even if `Authentication:PublicGoogleRegistrationEnabled=true`. This means **both new public registration and returning public login are blocked in production** with the current design. A future production release requires separate explicit design and approval to decouple existing-account login from registration.
- Successful public-login audit persistence occurs **before** issuing the app authentication cookie.

**Evidence:** focused Identity/SQL tests, 3/3 additional runs of the simultaneous-first-signup test (historical owner report), full final **1,306/1,306** regression and real local Google signup smoke. No live production auth change.

---

## 7. PUBLIC-AUTH-01C — local implementation COMPLETE, verified

**Objective:** minimal accessible public navbar login, identity and CSRF-protected logout, preserving existing design.

- Added `PublicAccountControls.razor` to the existing `PublicLayout.razor` header; no separate portal, cart, user database, or duplicated authentication stack.
- Login is visible when public login is enabled; a signed-in visitor sees their account identity and a sign-out button. ES/EN/DE labels and localized return paths are implemented. Public CSS/JS are registered once in `App.razor`.
- Logout obtains an antiforgery token via `GET /api/auth/csrf`, then posts to `/api/auth/logout` with `X-CSRF-TOKEN`, and refreshes the current page. The browser receives no Google access token.
- SSR document responses with an authenticated principal are marked `Cache-Control: private, no-store` to protect identity markup from shared caching.
- Public session issues a nonpersistent Identity cookie with `ExpiresUtc = now + 4 hours` and `AllowRefresh = false`; there is no sliding extension for that public ticket. The **preexisting employee/admin** Identity cookie configuration remains 8 hours with sliding renewal. `SaveTokens = false` in Google middleware; no access-token storage or new custom token/session system.
- Public session properties have a dedicated unit test. It checks the issued properties, **not** four hours of elapsed browser time, browser closure or operational production expiry.
- `PublicLayout.razor` also contains unrelated local WhatsApp changes; review this shared file carefully when staging.

**Evidence:** owner reported successful Release build and smoke on `https://localhost:7126/es` with a new public Google account, persisted public identity/navigation and logout steps, denied `/administracion` for that public account, and successful existing pre-authorized administrative Google login. No full recorded desktop/mobile/three-language device matrix yet. Google local credentials are saved in User Secrets; `Authentication:PublicGoogleRegistrationEnabled=true` was set locally only.

---

## 8. PUBLIC-AUTH-01D — integrated local security acceptance PARTIAL

**Completed/verified by owner:**

- Release solution/test build after 01C changes.
- Post-01C full xUnit suite: **1,306 total / 1,306 passed / 0 failed / 0 skipped**. Evidence path reported: `Downloads\REVESTIK-AUTH-FINAL-a97eb3cd\FinalRegression.trx`. This is an **owner-reported local** TRX; the assistant reviewed the command output and final source-diff export, not the actual TRX file.
- Test `PublicIdentitySession_ExpiresAfterFourHours_WithoutPersistenceOrSliding` confirmed as executed and passing by the guarded test script.
- The previous full run had 1,305/1,306 with `DisabledPublicRoutes_DoNotStartOrCompleteGoogleLogin` returning 401 instead of 404 because it inherited development User Secrets enabling public signup. The test was corrected to explicitly disable registration inside its isolated test host. Subsequent focused 1/1 and full 1,306/1,306 passed. This was **a test isolation issue**, not evidence that production auth was enabled.
- `git diff --check` passed; exact source diff exported and reviewed. Real local public and pre-authorized employee Google login, and public-to-admin denial, passed manual smoke.

**Still pending / do not claim completed:**

- A documented full **anonymous / customer A / customer B / employee / Administrator** matrix across all relevant endpoints and editor experiences (subject to features actually implemented).
- Full manual responsive desktop/mobile and ES/EN/DE UX smoke, focus/navigation and error states.
- A live end-to-end expiry/renewal and disable/revocation test, or equivalent time-controlled integration tests; the four-hour unit test alone is not equivalent.
- Focused correlation/hostile redirect, rate-limit and failure-state verification beyond the tests already reviewed.
- Source/staged-change scope review before Git: preserve Contact/WhatsApp, unrelated `appsettings`, and unknown files. Any eventual commit needs a new explicit approval.

**Working rule:** avoid re-running a 13-minute regression just to collect an existing report. Run focused tests for any actual correction; a new full run is needed only after source changes that invalidate the previous evidence or when an approved release gate requires it.

---

## 9. PUBLIC-AUTH-01E — production readiness REVIEW ONLY, NOT STARTED

The current development design is intentionally **not** production-enabled. Before any production release:

- Independently verify live Azure revision and image digest, HTTPS/forwarded-header trust, cookies and Data Protection key storage, SQL migrations/backups, runtime secrets and actual public-route ingress behavior.
- Review Google Cloud consent/test-user eligibility, provider callback URI and credentials in the intended environment. Development Google OAuth uses a separate **Web application** client with HTTPS origin `https://localhost:7126` and middleware redirect `https://localhost:7126/signin-google`; do not add Revestik's own post-provider `/api/auth/.../external-callback` routes to Google Cloud's OAuth redirect list. Do not reuse Gmail integration credentials or paste secrets into Git.
- Review privacy/consent/legal text, public signup eligibility, abuse controls, cache/SEO, rollout and rollback plan; explicitly design how returning customers can sign in when signup is closed.
- Obtain independent approvals for any Git operation, live OAuth configuration, production migration, Azure deployment, enabling public traffic, or indexing. **No authorization has been given for those actions.**

---

## 10. Deferred public features — OUT OF SCOPE of AUTH

### PUBLIC-QUOTE-01

A future approval may allow selecting one or more **published** catalog products and quantities using existing selling units, then composing readable itemized WhatsApp `wa.me` text and collecting minimal name/province where necessary. Login is optional. Do not create an internal fiscal `COT`, reserve stock, promise a tax/price guarantee, integrate the WhatsApp API or generate/attach PDFs by default.

### PUBLIC-CART-01

Only if separately approved: persisted per-account carts using `ApplicationUser.Id`, FK/index/concurrency safeguards and SQL scoping of reads/writes. Do not trust browser-supplied owner IDs; test A/B isolation before claiming secure ownership. No cart tables or request histories exist merely because public Identity now does.

### PUBLIC-WEB-RELEASE

Public content/image rights, responsive/accessibility/performance, legal/privacy, SEO/noindex/canonical, verified media and storage, source-to-image traceability, full production smoke and independent public release approval.

### ARCHITECTURE-REFACTOR

After completing core features, perform an evidence-driven comprehensive review to remove **verified** duplication. Require independent approval, architectural impact analysis and full regression. Do not merge this into the authentication closeout.

### DEPENDENCY-MAINTENANCE

Review supported .NET 10 and package patch versions independently. Avoid opportunistic dependency updates alongside security-critical auth behavior.

---

## 11. Local verification and repository discipline

Reference commands (select only what a block needs; do not interpret them as authorization to mutate Git):

```powershell
git status -sb
git rev-parse HEAD
git diff --check
dotnet build Revestik.sln --configuration Release
dotnet test tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj --configuration Release
```

The owner ran full Release regression after source changes and a focused remediation of one test configuration issue. Distinguish **test execution** from **real browser validation** and **production evidence**. Do not re-use 1,306 as an unconditional invariant if future valid source/test changes alter the number.

### Git/staging safety

- No auth commits or pushes yet. `main...origin/main` is the owner-reported branch status.
- The auth diff includes shared `PublicLayout.razor` changes from Contact/WhatsApp. Avoid staging unrelated hunks by accident; use file/hunk-specific staging only when explicitly authorized and after inspecting exact final diff.
- Do not stage `src/Revestik.Api/appsettings.json`, `appsettings.Development.json`, unrelated `Home.razor`, unknown untracked files or secrets with auth work. Do not clean/delete them without owner approval.
- The checkpoint itself is a **documentation-only artifact** until separately applied to `docs/PUBLIC-WEB-01-MASTER-CHECKPOINT.md` in the owner's Windows workspace. Downloading this document does **not** modify the repository or synchronize Git.

---

## 12. Next-chat handoff

1. **Read `AGENTS.md` first**, then this full checkpoint, the relevant architecture/security docs and current code. Inspect the owner's current `git status -sb`, `git rev-parse HEAD`, selected local diffs and remote state. Treat this file as evidence/handoff, not as proof of current Git contents.
2. Do **not** reimplement 01A/01B/01C. They are locally implemented, 1,306/1,306 post-01C regression passed, and public+employee Google local browser smoke passed. Preserve the current Identity and domain architecture.
3. Next proposed work is **PUBLIC-AUTH-01D remaining local security/UX checks** and scoped diff review; separate and seek approval for a concrete plan before implementing any new feature. A minimal documentation-only checkpoint replacement may be applied with owner approval and verified without staging other work.
4. Keep PUBLIC-AUTH-01E **review only**. Registration remains hard-disabled in production; no public production login can currently complete through the 01B public route. Do not silently remove that gate.
5. Preserve all uncommitted Contact/WhatsApp work, local `appsettings`, unknown malformed files, and User Secrets. Do not include real OAuth credentials, private identities or phone numbers in handoff reports.
6. Later workstreams (PUBLIC-QUOTE-01 and any PUBLIC-CART-01) require their own INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP cycle. No code or production operation has implicit approval.
7. **No commit, push, deploy, Azure migration, Google Cloud production change, DNS/SEO/indexing or public enablement without separate explicit owner permission.**

**End state recorded:** auth blocks 01A/01B/01C implemented and owner-verified locally; 01D partial; 01E and production/public launch pending. Public-project features remain historically verified; Contact/WhatsApp changes remain a separate local workstream. Next action: checkpoint synchronization and remaining 01D acceptance plan, not a new authentication implementation.
