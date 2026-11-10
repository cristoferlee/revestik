# Revestik

**Production business operations platform and in-development public website | .NET 10 · ASP.NET Core · Blazor · SQL Server · Azure**

Revestik is a real-world software solution for a Costa Rican business specializing in construction finishes and renovation materials. It supports daily business operations—customers, quotations, sales, inventory, purchasing, fiscal documents, projects, financial visibility, and administration—while a customer-facing website is developed within the same solution.

Revestik is a **modular monolith**, not a microservices platform. The ASP.NET Core server is authoritative for authentication, authorization, business rules, transactions, and persistence. The administrative interface uses Blazor WebAssembly; the public website uses ASP.NET Core Razor Components with server-side rendering (SSR) and interactive server components where needed.

## Current status

- **Internal application:** Deployed on Azure and accessed at [`revestikcr.com/administracion`](https://revestikcr.com/administracion). Authorized users sign in through Google OAuth and ASP.NET Core Identity.
- **Public website:** Implemented in the repository but **not released to visitors**. The production root `https://revestikcr.com` currently returns HTTP 401 while the public beta is being completed and validated.
- **Public work completed so far:** The homepage, shared navigation, and finishes/product section have been implemented. Additional content, quality checks, and release approval remain before public launch.
- **Release distinction:** Code merged into `main`, passing CI, or tested locally is not necessarily deployed to Azure. Live revision, schema state, and smoke-test evidence must be checked separately.

The internal application originally ran at `app.revestikcr.com`. The public site was later developed for `revestikcr.com`; the resulting cross-origin editing and `iframe` bridge added unnecessary complexity. The architecture was consolidated onto one host and origin, with the administrative application under `/administracion`. The legacy `iframe` editor/bridge remains in the codebase pending controlled removal; **it is not the target architecture**.

See the [roadmap](docs/ROADMAP.md) for implemented work, public-beta scope, and remaining tasks.

## What the system does

| Area | Capabilities |
| --- | --- |
| Commercial operations | Customers, quotations, sales, partial payments, and PDF documents |
| Inventory | Internal products, stock movements, FIFO cost layers, receipts, and physical counts |
| Procurement | Suppliers, purchases, accounts payable, and explicit inventory receiving |
| Fiscal documents | Costa Rican electronic XML intake, validation, CABYS classification, and purchase reconciliation |
| Financial operations | Expenses, bank vouchers, gross margin, and operating-result summaries |
| Projects | Customer-linked projects, tasks, milestones, payments, private documents, and alerts |
| Administration | Google sign-in, Identity-based user management, roles, permissions, settings, and security audit records |
| Integrations | Gmail synchronization, Costa Rican tax-related services, and BCCR exchange-rate data |
| Public website (beta in development) | Multilingual company pages, homepage content, finishes catalog, product details, images, and administrator-only content editing |

The **public catalog is independent of internal inventory products**. Categories, subcategories, and active units of measure are reused where appropriate, without exposing private stock, costs, FIFO layers, customer data, or financial records. A received tax XML is not automatically an inventory receipt; similarly, a project payment is not automatically a sale payment or bank movement.

## Architecture and technology

The application contains **four primary projects**:

| Project | Responsibility |
| --- | --- |
| `Revestik.Api` | Single ASP.NET Core host; Minimal APIs, Identity/Google OAuth, authorization, domain services, EF Core/SQL Server, integrations, and private storage |
| `Revestik.Client` | Authenticated administrative interface built with Blazor WebAssembly, hosted at `/administracion` |
| `Revestik.Public` | Customer-facing SSR Razor Components site, multilingual pages, product presentation, and authenticated inline editing |
| `Revestik.Shared` | Shared request/response contracts and common transport types; not an alternative persistence layer |

```text
Browser
  |
  +-- /administracion  --> Revestik.Client (Blazor WebAssembly)
  |
  +-- /es, /en, /de    --> Revestik.Public (SSR / interactive server components)
  |
  +-- /api/*           --> Revestik.Api (trusted API)
                              |
                              +-- Identity / Google OAuth / authorization / CSRF
                              +-- Domain services / EF Core / SQL Server
                              +-- Private file storage / background integrations

All are hosted by Revestik.Api in the unified application.
```

```text
src/
  Revestik.Api/         Unified host, APIs, business rules, persistence, integrations
  Revestik.Client/      Administrative Blazor WebAssembly application
  Revestik.Public/      Public SSR site and inline editing components
  Revestik.Shared/      Shared contracts
tests/
  Revestik.Api.Tests/   Automated domain, API, hosting, security, and SQL integration tests
docs/                     Architecture, business rules, development, operations, security, roadmap
```

Other engineering foundations include .NET 10, Entity Framework Core, SQL Server, QuestPDF, cookie-based sessions, antiforgery protection, rate limiting, audit records, Azure Container Apps, Azure SQL Database, xUnit, and Testcontainers.

## Engineering principles

1. **Server-authoritative rules:** Client-side controls are not security boundaries. Permissions, financial invariants, domain states, and persistence constraints are enforced by the API.
2. **One deployment architecture:** API, administrative client, and public site are hosted together. The ordinary local workflow starts only `Revestik.Api`, rather than requiring three independent servers.
3. **Independent public catalog:** Public content is managed separately from inventory products, while reusing shared classification data and sales units.
4. **Traceable inventory and financial flows:** FIFO cost provenance, transactional operations, and distinct fiscal/operational records prevent unintended cross-domain effects.
5. **Resumable integrations:** Gmail ingestion uses persisted progress and duplicate handling; scaling the worker across replicas requires additional coordination.
6. **Controlled public launch:** The public website is not considered released merely because it has routes, assets, or working editor components. SEO, access, content, and security checks are release gates.

Accepted architectural decisions are preserved in [`docs/adr/`](docs/adr/) as historical records. Consult [the current architecture document](docs/ARCHITECTURE.md) for the four-project topology and unified hosting design.

## Run locally

The primary local development workflow starts the unified API host:

```powershell
cd C:\Projects\Revestik
dotnet restore Revestik.sln
dotnet run --project src/Revestik.Api/Revestik.Api.csproj --launch-profile https
```

The tracked API HTTPS launch profile uses `https://localhost:7126`; local administrative and public routes are served from that host. Standalone project launch profiles may exist for targeted debugging, but they are not required for normal integrated development. Configure SQL Server, Google OAuth, storage, and other secrets through approved local settings outside source control. See [DEVELOPMENT.md](docs/DEVELOPMENT.md) for verification and troubleshooting.

Tests can be run separately (some SQL integration tests require Docker/Testcontainers):

```powershell
dotnet test tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj
```

## Documentation

- [Architecture and project boundaries](docs/ARCHITECTURE.md)
- [Business rules and invariants](docs/BUSINESS_RULES.md)
- [Security model](docs/SECURITY.md)
- [Local development and testing](docs/DEVELOPMENT.md)
- [Deployment and operations](docs/OPERATIONS.md)
- [Roadmap and delivery status](docs/ROADMAP.md)
- [Public website project checkpoint (historical decisions and progress)](docs/PUBLIC-WEB-01-MASTER-CHECKPOINT.md)
- [Agent collaboration and approval workflow](AGENTS.md)

## Scope and transparency

Revestik is an internal business system with an in-progress public website, **not** a public multi-tenant SaaS or a checkout marketplace. Public-site development must not expose internal business information. This repository does not claim independent security certification, guaranteed uptime, fully audited disaster recovery, or production deployment of every commit. Test results, release artifacts, migrations, and Azure configuration must be verified against their intended environment.