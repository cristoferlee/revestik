# Architecture

## System overview

Revestik is a **.NET 10 modular monolith**. A Blazor WebAssembly client communicates with a trusted ASP.NET Core Minimal API through shared request/response contracts. Entity Framework Core persists business state in SQL Server. In production, the API also serves the published client as a same-origin application.

```text
Browser (Blazor WebAssembly)
          |
          | HTTPS / cookie-authenticated API calls
          v
ASP.NET Core API (Identity, authorization, CSRF, rate limits)
          |
          +-- Domain services / transactional workflows --> EF Core --> SQL Server
          +-- Private document and state storage
          +-- PDF generation (QuestPDF)
          +-- Gmail / Hacienda / BCCR / location services
```

The architecture is not microservices, CQRS, or an event-driven distributed system. The deployment model favors operational simplicity for a small internal team.

## Solution boundaries

| Project | Responsibility |
| --- | --- |
| `Revestik.Client` | UI, navigation, client interaction, responsive presentation, non-authoritative validation |
| `Revestik.Api` | Endpoints, authentication, authorization, domain rules, transactions, persistence, integrations, document handling |
| `Revestik.Shared` | Transport DTOs and shared contracts; not database entities |
| `Revestik.Api.Tests` | Automated domain, API, security, and database integration verification |

Business-critical invariants are enforced on the backend and, where appropriate, reinforced by database constraints and transactions.

## Business modules

- **Customers and commercial workflows:** customer records, quotations, sales, discounts, payments, PDF outputs.
- **Products and inventory:** catalog, stock movements, physical counts, FIFO cost layers and cost resolution.
- **Suppliers and procurement:** purchases, accounts payable, explicit receiving into inventory.
- **Electronic documents and fiscal workflows:** Hacienda XML v4.4 intake, secure parsing, CABYS, classification, reconciliation, period summaries.
- **Expenses and bank vouchers:** independent operational records and review workflows.
- **Projects:** customer-linked records, milestones, tasks, deadlines, project payments, documents, activity, and reports.
- **Dashboard and reporting:** activity trends, recent events, expense summaries, gross margin, operating results, document alerts, exchange rates.
- **Administration:** company preferences, users and permissions, authentication, audit records.

## Critical domain distinctions

1. **Quotations do not reserve or deduct inventory.** Inventory changes occur through explicit sale and receipt workflows.
2. **Sales inventory movements use FIFO cost provenance.** Voiding a sale must preserve the correct restoration semantics.
3. **Purchases do not silently receive stock.** The receiving workflow is explicit.
4. **Received XML documents are not interchangeable with manual purchases, bank vouchers, or expenses.** Reconciliation prevents unintended duplication.
5. **Project payments are operational project records**, not `SalePayments`, bank movements, or tax documents. Closed project states enforce write restrictions.

The normative rule identifiers and qualifications are in [BUSINESS_RULES.md](BUSINESS_RULES.md). This summary does not replace them.

## Identity and trust boundaries

Google OAuth establishes external identity; ASP.NET Core Identity and server-side authorization determine application access. Application sessions use cookies. Antiforgery validation protects relevant authenticated mutations. The server validates role and resource scope, rather than trusting UI visibility.

Incoming Gmail messages, XML attachments, and user uploads are untrusted. Electronic documents pass through controlled validation and intake flows. Project files are private, not served from the public web root.

## Gmail ingestion and background processing

Historical Gmail synchronization runs in an API-hosted `BackgroundService`, with persisted checkpoints, duplicate handling, bounded page processing, and resume/retry paths. The current worker permits one active job **per API process**. Scaling to multiple replicas would require distributed coordination; it must not be assumed safe without a new design.

## Production hosting and state

The production architecture uses Azure Container Apps and Azure SQL Database. The API requires a configured durable, private storage root for received-document inbox data, Gmail state, project documents, and persistent Data Protection keys. Data Protection keys are protected with a certificate. The runtime fails startup if mandatory production storage or certificate configuration is absent.

Azure deployment manifests in `deploy/azure/` may be historical or environment-specific. They are not proof of current live settings. See [OPERATIONS.md](OPERATIONS.md) for verification responsibilities.

## Architectural decision records

Accepted ADRs in [`docs/adr/`](adr/) are the source of truth for previously approved consequential decisions. Add a new ADR when changing a difficult-to-reverse architectural choice; do not silently rewrite accepted rationale.
