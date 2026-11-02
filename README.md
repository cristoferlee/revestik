# Revestik

**Production-deployed business operations platform | .NET 10 · ASP.NET Core · Blazor WebAssembly · SQL Server · Azure**

Revestik is a real-world business management application built to support the day-to-day operations of a Costa Rican construction finishes business. It consolidates commercial workflows, inventory, purchasing, received electronic tax documents, financial visibility, customer projects, and administrative controls into a single internal system.

The application is a **modular monolith**, not a collection of microservices. Its architecture deliberately keeps business-critical rules and authorization on the server, while a Blazor WebAssembly client provides a responsive user interface.

## Project status

- **Current phase:** Internal production deployment on Azure, with ongoing stabilization, maintenance, and documentation.
- **Production audience:** Authorized internal business users; this is not currently a public self-service SaaS product.
- **Next planned phase:** A customer-facing public website for the business, distinct from the authenticated operations application.
- **Important distinction:** A change tested locally is not considered deployed until its production release is verified.

See the [roadmap](docs/ROADMAP.md) for completed capabilities, current work, and planned development.

## What the system does

| Area | Capabilities |
| --- | --- |
| Commercial operations | Customer records, quotations, sales, partial payments, and PDF documents |
| Inventory | Product catalog, stock movements, FIFO cost layers, receipts, and physical counts |
| Procurement | Suppliers, purchases, accounts payable, and explicit inventory receipt workflows |
| Fiscal documents | Costa Rican electronic XML intake, validation, CABYS classification, and purchase reconciliation |
| Financial operations | Expenses, bank vouchers, gross margin and operating-result summaries |
| Projects | Customer-linked projects, tasks, milestones, payments, private documents, and alerts |
| Administration | Google sign-in, Identity-based user management, roles, permissions, and security audit records |
| Integrations | Gmail synchronization, Costa Rican tax-related services, and BCCR exchange-rate data |

These are separate business workflows. For example, a received tax XML is not automatically an inventory receipt, and a project payment is not automatically a sales payment or bank transaction.

## Technology and engineering

- **Backend:** C# / .NET 10, ASP.NET Core Minimal APIs, ASP.NET Core Identity.
- **Frontend:** Blazor WebAssembly with responsive layouts.
- **Data:** Entity Framework Core, SQL Server, migrations, constraints, and transactional domain operations.
- **Documents:** QuestPDF; private file storage for sensitive attachments.
- **Authentication and security:** Google OAuth, application cookies, server-side authorization, antiforgery protection, rate limiting, Data Protection, and audit logging.
- **Infrastructure:** Azure Container Apps and Azure SQL Database; production configuration includes durable storage requirements.
- **Quality:** xUnit and integration tests, including SQL Server scenarios using Testcontainers.

### Solution layout

```text
src/Revestik.Api/          API, domain services, persistence, integrations, PDF generation
src/Revestik.Client/       Blazor WebAssembly UI
src/Revestik.Shared/       Shared request/response contracts
tests/Revestik.Api.Tests/  Automated tests
docs/                     Architecture, business rules, operations, security, roadmap
```

## Engineering decisions worth exploring

1. **Modular monolith:** Keeps deployment and operations manageable while preserving domain boundaries.
2. **Server-authoritative business logic:** Prevents browser state or hidden controls from bypassing financial and authorization rules.
3. **FIFO inventory provenance:** Cost layers and movement history support traceable stock consumption and restoration.
4. **Separate fiscal and operational records:** Avoids double counting and accidental cross-domain effects.
5. **Resumable Gmail ingestion:** Uses persisted checkpoints, duplicate detection, bounded background processing, and retry paths.
6. **Production-aware security:** Persistent, certificate-protected Data Protection keys; private document storage; explicit reverse-proxy trust configuration.

For implementation details and trade-offs, read [Architecture](docs/ARCHITECTURE.md) and the accepted decisions in [`docs/adr/`](docs/adr/).

## Documentation

- [Architecture and boundaries](docs/ARCHITECTURE.md)
- [Business rules and invariants](docs/BUSINESS_RULES.md)
- [Security model](docs/SECURITY.md)
- [Local development and testing](docs/DEVELOPMENT.md)
- [Deployment and operations](docs/OPERATIONS.md)
- [Roadmap and current phase](docs/ROADMAP.md)
- [Agent collaboration rules](AGENTS.md)

## Running locally

Use the [.NET 10 development guide](docs/DEVELOPMENT.md). Local SQL Server, Google OAuth credentials, and other integration secrets must be configured outside source control. Some integration tests require Docker.

## Scope and transparency

This repository documents a working internal business application and its engineering evolution. It does **not** claim independent security certification, guaranteed uptime, complete disaster-recovery readiness, or deployment of changes that exist only in a developer's working tree. Production configuration, test evidence, and known limitations should be verified against the target environment before release.
