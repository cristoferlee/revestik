# Architecture

## System overview

Revestik is a **.NET 10 modular monolith** composed of four application projects: `Revestik.Api`, `Revestik.Client`, `Revestik.Public`, and `Revestik.Shared`. The solution retains one trusted ASP.NET Core backend, Entity Framework Core with SQL Server, and shared request/response contracts. It is **not** a collection of microservices, CQRS, or an event-driven distributed system.

The API is the **unified application host**. It serves the authenticated business administration built with Blazor WebAssembly under `/administracion/` and the customer-facing website built with Razor Components (server-side rendering, with interactive server components for editing) at public language routes. It also owns authentication, authorization, application APIs, domain operations, persistence, and integrations.

```text
Browser
  |
  +-- /administracion/...   Blazor WebAssembly administration (Revestik.Client)
  |        |                Identity session cookie; protected API operations
  |        v
  |     /api/...             ASP.NET Core endpoints (Revestik.Api)
  |
  +-- /es, /en, /de ...     Public Razor Components SSR (Revestik.Public)
           |                Anonymous public reads; authorized editor only
           v
        Public read endpoints / protected admin content endpoints
                      |
                      v
           Revestik.Api unified host
             +-- ASP.NET Core Identity / Google OAuth / policies / CSRF
             +-- Domain services / transactions --> EF Core --> SQL Server
             +-- Private documents, public-catalog media, durable state
             +-- QuestPDF, Gmail, Hacienda, BCCR and other integrations

Revestik.Shared: common transport contracts across application projects
```

A single deployed host does **not** make internal business APIs or private files public. Security is enforced at the server-side endpoint and resource boundaries, not by route naming, frontend navigation, or hidden controls.

## Solution boundaries

| Project | Responsibility |
| --- | --- |
| `Revestik.Api` | Unified ASP.NET Core host; Minimal API endpoints, Identity/Google OAuth, authorization, domain rules, transactions, EF Core, storage, integrations, and document handling |
| `Revestik.Client` | Blazor WebAssembly administrative UI, navigation, client interaction, responsive presentation, and non-authoritative validation |
| `Revestik.Public` | Multilingual customer-facing Razor Components site, SSR pages, public presentation assets, public data readers, and administrator-only in-page editing components |
| `Revestik.Shared` | Shared transport DTOs, requests, responses, and contracts; not database entities or trusted authorization logic |

The separate `tests/Revestik.Api.Tests` project verifies application behavior; it is **not** a fifth application component or an independent deployed service. Business-critical invariants are enforced by the API and, where appropriate, by database constraints and transactions.

The earlier accepted [ADR-001](adr/001-architecture.md) recorded the original three-project design. It remains an historical decision record; the subsequent addition of `Revestik.Public` and the unified host are the current implementation.

## Hosting, routes, and local development

- **Production domain:** `revestikcr.com` is the unified host. The authenticated administration is reached at `https://revestikcr.com/administracion` and directs unauthenticated business users to the Google OAuth sign-in experience.
- **Public release boundary:** On 2026-11-26 the owner verified that the public root redirects to `/es` and returns HTTP 200. Anonymous reachability is not formal commercial launch approval; indexing and public authentication changes require separate approval.
- **Historical domain:** `app.revestikcr.com` was retired from Azure ingress and DNS; administration is under `/administracion`.
- **Main local workflow:** Run **`Revestik.Api` only**. It hosts the administrative client and public SSR site together, including the Google OAuth integration. There is no longer a requirement to run Client, Public, and API as three simultaneous processes for normal development. The separate `Revestik.Public` program remains available for specialized development or testing, not as the principal hosting design.
- **Administrative routing:** The Blazor WebAssembly files and SPA fallback are scoped to `/administracion`. The application's public SSR root is not an unrestricted fallback to the private administration. The HTML entry page and static assets are not authorization boundaries; sensitive endpoints remain protected.
- **Public routing:** `Revestik.Public` provides multilingual pages under `/es`, `/en`, and `/de` (for example, `/es/acabados`), with SSR, public media routes, and interactive server components where authorized editing is requested.

See [DEVELOPMENT.md](DEVELOPMENT.md) for the current launch profile, URLs, and test commands, and [OPERATIONS.md](OPERATIONS.md) for production release checks. Do not infer a live Azure image's Git SHA from the current `main` branch without image/revision evidence.

## Public website and editorial architecture

The public website is a corporate presentation and lead-generation site, not a checkout marketplace or multi-tenant SaaS. Implemented public scope includes catalog, projects, Solutions, editorial pages, cart and preliminary quotation PDF. Formal acceptance remains outstanding.

### Catalog and data ownership

- Public catalog products are stored separately from private inventory products. Publication does not expose internal stock, FIFO costing, supplier, margin, or accounting information.
- Public products and internal inventory reuse the existing **shared category and subcategory taxonomy** and active units catalog. Public visibility depends on public slugs, product publication, archival state, and public-category relationships; the internal `IsActive` flag is not itself the public publication switch.
- The API owns public read endpoints, privileged catalog mutations, validation, publication rules, image lifecycle, concurrency safeguards, and the database model. The public SSR layer consumes public/read-only APIs; it does not use administrator credentials for normal content retrieval.
- Hero media and editable homepage content are persisted independently of the private inventory domain. The home and finishes heroes have distinct logical slots within the current shared hero storage model.
- Public catalog media are kept in an application-controlled `public-catalog` directory under the configured persistent storage root in production, with pending/active lifecycle management. Media access must respect public visibility or privileged access rules; private project and fiscal documents remain separate.

### Visual editing and legacy cleanup

The target editing experience is **in-page editing on the real public site**, using `Revestik.Public` interactive server components. The server checks the active Identity session and the `Administrator` role on each editor render; edit-mode query/cookie state is a **preference**, not authentication. Administrative mutations are separately protected by authorization, CSRF checks, and validation.

The legacy administrative iframe editor and message bridge were retired in commit `9cbd7d4`; the independent PDF preview iframe is not part of that cleanup.

## Business modules

- **Customers and commercial workflows:** customer records, quotations, sales, discounts, payments, PDF outputs.
- **Products and inventory:** internal product catalog, shared classification taxonomy, stock movements, physical counts, FIFO cost layers and cost resolution.
- **Suppliers and procurement:** purchases, accounts payable, explicit receiving into inventory.
- **Electronic documents and fiscal workflows:** Hacienda XML v4.4 intake, secure parsing, CABYS, classification, reconciliation, period summaries.
- **Expenses and bank vouchers:** independent operational records and review workflows.
- **Projects:** customer-linked records, milestones, tasks, deadlines, project payments, documents, activity, and reports.
- **Dashboard and reporting:** activity trends, recent events, expense summaries, gross margin, operating results, document alerts, exchange rates.
- **Administration:** company preferences, users and permissions, authentication, audit records.
- **Public website:** multilingual content pages, public product discovery, media, and administrator-only editorial tools.

## Critical domain distinctions

1. **Quotations do not reserve or deduct inventory.** Inventory changes occur through explicit sale and receipt workflows.
2. **Sales inventory movements use FIFO cost provenance.** Voiding a sale must preserve the correct restoration semantics.
3. **Purchases do not silently receive stock.** The receiving workflow is explicit.
4. **Received XML documents are not interchangeable with manual purchases, bank vouchers, or expenses.** Reconciliation prevents unintended duplication.
5. **Project payments are operational project records**, not `SalePayments`, bank movements, or tax documents. Closed project states enforce write restrictions.
6. **Public catalog products are not private inventory products.** Shared category and unit definitions do not grant public access to internal inventory or financial data.

The normative rule identifiers and qualifications are in [BUSINESS_RULES.md](BUSINESS_RULES.md). This summary does not replace them.

## Identity and trust boundaries

Google OAuth establishes external identity; ASP.NET Core Identity and server-side authorization determine application access. Application sessions use cookies. Antiforgery validation protects relevant authenticated mutations. The server validates role and resource scope rather than trusting UI visibility.

Anonymous public content requests and authenticated administrative writes have different trust requirements even when they share an origin. Editor cookies, preview state, client-side flags, or UI buttons never authorize an API operation by themselves. Public API responses must enforce publication visibility; privileged API routes require the appropriate server-side policy.

Incoming Gmail messages, XML attachments, and user uploads are untrusted. Electronic documents pass through controlled validation and intake flows. Project files are private, not served from the public web root. Review cross-origin permissions and cookie/OAuth redirects before changing hosting or domain configuration. See [SECURITY.md](SECURITY.md).

## Gmail ingestion and background processing

Historical Gmail synchronization runs in an API-hosted `BackgroundService`, with persisted checkpoints, duplicate handling, bounded page processing, and resume/retry paths. The current worker permits one active job **per API process**. Scaling to multiple replicas would require distributed coordination; it must not be assumed safe without a new design.

## Production hosting and state

Production uses Azure Container Apps (0.5 vCPU, 1 GiB, one replica) and Azure SQL Standard S0 (10 DTU). The API requires durable private storage for inbox data, Gmail state, project documents, public media and certificate-protected Data Protection keys. Startup requires mandatory storage and certificate configuration.

The public catalog and shared taxonomy have EF Core migrations. Migration presence in `main`, successful automated tests, or a healthy container do **not** independently establish that the corresponding changes are applied to production SQL. Review existing data, backup/restore readiness, and target database before authorizing schema changes. In particular, the shared-taxonomy consolidation has data-preservation checks and does not support automatic reversal.

Azure deployment manifests in `deploy/azure/` may be historical or environment-specific. They are not proof of current live settings. Record the actual image digest, revision, migration state, and smoke-test evidence per release. See [OPERATIONS.md](OPERATIONS.md).

## Architectural decision records

Accepted ADRs in [`docs/adr/`](adr/) preserve the rationale for decisions at the time they were adopted. Current architecture changes are documented above; add a new ADR when changing a difficult-to-reverse architectural choice, rather than silently rewriting an accepted historical record.

## Approved architecture decisions — 2026-11-26

SQL remains authoritative for public content and pricing; existing cache and visibility controls remain. No persistent publication layer or additional CDN is planned. See [ADR-004](adr/004-publication-architecture.md), [ADR-005](adr/005-hosting-and-cdn.md) and [ADR-006](adr/006-container-resource-allocation.md). See [the final closure plan](REVESTIK-FINAL-CLOSURE-PLAN.md) and [Azure checkpoint](AZURE-COST-OPTIMIZATION-CHECKPOINT-20261126.md).
