# Roadmap and delivery status

> **Status snapshot: 2026-11-10.** Repository baseline: `main` at `581aef9c99ddb7bbc1fffcbe49ec924699c980a2`. This is a planning and status document, not proof that every commit is deployed. Confirm the current source, production revision, database migrations, and smoke-test evidence before making release claims.

This roadmap distinguishes **implemented in source**, **verified in a particular environment**, **deployed**, and **pending**. Delivery dates are not committed. Material work follows **INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP**.

## Phase 1 — Core internal business platform (implemented)

REVESTIK is a .NET 10 modular monolith supporting customers, quotations, sales and partial payments, FIFO inventory, suppliers, purchases and accounts payable, expenses, electronic tax-document intake, reconciliation, projects, reporting, and administration. Business rules, security checks, and persistence remain server-authoritative.

The current application comprises four primary projects:

| Project | Responsibility |
| --- | --- |
| `Revestik.Api` | ASP.NET Core host, Minimal APIs, authentication, domain services, persistence, integrations, and document handling |
| `Revestik.Client` | Authenticated administrative user interface built with Blazor WebAssembly |
| `Revestik.Public` | Customer-facing public website built with Razor Components / server-side rendering, plus the newer inline editing experience |
| `Revestik.Shared` | Shared transport contracts and DTOs |

`Revestik.Api.Tests` is a separate automated test project, not a fifth primary application project.

## Phase 2 — Internal production and unified hosting (operational; stabilization continues)

The internal platform runs on Azure Container Apps with Azure SQL Database. Its production design requires secure configuration, durable private storage, certificate-protected Data Protection keys, and health and authentication checks.

**Hosting evolution:**

1. The administrative application initially operated at `app.revestikcr.com`, with the public website planned for `revestikcr.com`.
2. Cross-domain editing and the legacy iframe-based preview introduced unnecessary complexity.
3. The solution was consolidated under a single ASP.NET Core host and intended domain, `revestikcr.com`. Administrative navigation now uses `/administracion`, and Google OAuth was adapted to that arrangement.
4. Normal local development starts **`Revestik.Api` only**; it serves both the administrative Blazor WebAssembly assets and the public Razor Components site. Running the client and public projects independently is no longer the normal workflow.

**Owner-confirmed production behavior on 2026-11-10:**

- `https://revestikcr.com/administracion` is operational and presents the Google OAuth sign-in flow to users who need to authenticate.
- `https://revestikcr.com` currently returns HTTP **401**; the customer-facing website has **not** been launched to the public.
- `app.revestikcr.com` is the previous domain, not the destination of the unified architecture.

These observations do not establish the exact Git SHA or migration set running in Azure. A code change, passing CI run, or successful local test must not be labeled deployed without production revision and smoke-test evidence.

**Continuing operations:** authentication/session reliability, performance and cold-start assessment, Gmail synchronization, recovery/backup verification, release traceability, security validation, and maintenance. See [OPERATIONS.md](OPERATIONS.md).

## Phase 3 — Public business website and inline editor (in development; not publicly launched)

The public site is a focused corporate presentation for construction finishes, projects, solutions, and customer contact. It is **not** a large marketplace, public ERP, or multi-tenant SaaS offering. Internal customer, inventory, purchasing, financial, and private-document information must remain protected.

**Implemented in repository / owner-reported functional progress:**

- Public website hosted by `Revestik.Api`, with Razor Components and reusable navigation and presentation elements.
- Home page, navigation bar, and the product/finishes experience reported complete in the current local development work.
- Public products independent of operational inventory products, while reusing the shared category, subcategory, and unit catalogs.
- Product publication and visibility checks, image handling, SEO-friendly slugs, featured products, and administrative editing endpoints.
- An inline editor rendered on the actual public pages for authorized administrators, with server-side role checks and protected write operations.
- Expanded automated tests and successful GitHub CI runs. Re-run and record results for the exact release candidate; historical test counts are not evergreen certification.

**Transitional architecture:** The older `Revestik.Client` public-site editor/iframe and its JavaScript bridge still coexist with the new inline editor. The iframe approach is **legacy**, not the target design. Remove its unused UI, bridge scripts, and related code only after the replacement is fully verified and a separate removal scope is approved. Documentation must not imply that cleanup has already happened.

**Remaining before public beta approval:**

- Complete and review the remaining public pages and content, including projects and contact workflows, against the approved scope.
- Verify responsive layouts, accessibility, SEO and indexing policy, multilingual content, image performance, navigation, and error states.
- Review privacy notices, cookie/third-party disclosures, form data handling, and other legal/compliance requirements before making them public.
- Verify public anonymous read access, editor permissions, CSRF, file/media visibility, and separation from the internal application.
- Check EF Core migration and data compatibility, including shared-taxonomy consolidation and Home/Finishes hero changes, on the **intended** database. The consolidation migration has no automatic rollback; any production data migration requires separately approved review and backup readiness.
- Perform build, automated regression, local acceptance, and production/prelaunch smoke checks. Confirm the Azure revision and the intended release gate before changing public access from its current restricted behavior.

**Release gate:** The presence of public-site code in a production-capable host does **not** authorize exposing the public website. Keep the public root restricted until its beta has been completed, tested, reviewed, and explicitly approved for launch. Do not infer launch authorization from a Git commit, deployment, or migration alone.

## Future opportunities (not committed)

Possible later work includes dashboard caching/performance improvements, session-experience refinements, PWA/offline support, richer project presentation, and other capabilities justified by operational needs. No feature, date, or budget is committed by this section.

## How progress is verified

- **Implemented:** exists in current source; supported by appropriate code and test evidence.
- **Locally verified:** specific tests or smoke checks completed against an identified local revision.
- **Deployed:** a particular built artifact/revision and relevant behavior verified in the target environment.
- **Publicly launched:** public access deliberately enabled **after** the release gate is approved and verified.
- **Pending:** design, implementation, verification, cleanup, or release work remains.

Use Git history, the active codebase, test outputs, migrations, accepted ADRs, and deployment evidence as the sources of truth. Historical checkpoints describe how decisions evolved; they do not override the current implementation.