# Product Roadmap

## 1. Purpose

This document describes the current implementation status and planned evolution of Revestik.

Status terminology:

* **Implemented** — available and verified in the current codebase.
* **In Progress** — actively being developed.
* **Planned** — intentionally expected but not yet implemented.
* **Under Evaluation** — being considered but not selected.
* **Deferred** — intentionally postponed.

A roadmap item should not be presented as implemented merely because it is planned.

## 2. Roadmap Principles

Revestik development follows these principles:

1. Complete business workflows vertically.
2. Protect important rules on the server.
3. Add automated verification alongside critical behavior.
4. Keep documentation synchronized with the codebase.
5. Avoid unnecessary architectural complexity.
6. Prioritize workflows that solve real operational needs.
7. Treat security and data integrity as product requirements.
8. Do not let high-risk integrations block a smaller useful production release.

## 3. Implemented Foundation

### Application Architecture

* ASP.NET Core backend.
* Blazor WebAssembly frontend.
* Shared request/response contracts.
* EF Core and SQL Server.
* Hosted same-origin production architecture.

### Authentication and Security

* ASP.NET Core Identity.
* Cookie-based authentication.
* Google external authentication.
* Role/policy authorization foundation.
* Antiforgery protection.
* Server-side secret boundary.
* Security regression tests.

### Customer Domain

**Status: Implemented**

Customer management includes validation, persistence, active/inactive state, pagination, filtering, deterministic ordering, and data-integrity protections.

### Quotations Domain

**Status: Implemented**

The complete Quotations vertical workflow includes:

* Create/edit workflow in Blazor.
* Active-customer association.
* `Draft` and `Issued` states.
* Customer snapshot on issue/reissue.
* Manual and product-assisted lines.
* Optional product association.
* Optional CABYS for commercial quotations.
* Unit of measure and decimal quantities.
* CRC and USD.
* IVA-inclusive 0% / 13% calculations.
* Percentage and fixed discounts.
* Additional charges.
* Client calculation feedback.
* Server-authoritative totals.
* SQL Server sequence-backed `COT-xxxxxx`.
* Preservation of identity on edit/reissue.
* Backend PDF generation with QuestPDF.
* Authenticated PDF download.
* Authorization and antiforgery protection.
* Automated unit, API, service, persistence, and concurrency verification.

Quotations do not modify inventory.

### Product Support for Quotations

**Status: Implemented as supporting capability**

A focused Product model and paginated lookup support quotation entry.

This is not yet the complete Inventory domain.

### Engineering Foundation

* Automated test project.
* Unit/focused behavior tests.
* Integration and hosting tests.
* SQL Server Testcontainers coverage.
* CI verification.
* Release build verification.
* Publish/static-asset verification.
* Health endpoint.
* EF Core migrations.
* Architecture, security, product, testing, and deployment documentation.

Current automated test baseline:

**194 passed, 0 failed, 0 skipped.**

## 4. Current Development Phase

Revestik has completed its first full transactional commercial workflow.

```text
Engineering foundation
        +
Customers
        +
Quotations
        =
Current implemented platform
```

The next step is to investigate and implement **Sales** without prematurely redesigning Quotations into a generic transaction model.

## 5. Current Focus — Sales

**Status: Planned / next investigation block**

Sales must be treated as a separate business document from Quotations.

The investigation should define:

* Conversion from quotation to sale.
* Internal `VEN-xxxxxx` numbering.
* Sale states.
* Cancellation/void behavior.
* Payment information.
* Manual versus registered-product lines.
* Inventory movement timing.
* Prevention of invalid negative stock.
* Optional external fiscal-document reference.

No Sales implementation should begin until these rules are planned and approved.

## 6. Near-Term Domains

### Inventory

**Status: Planned**

Expected areas:

* Product/material catalog evolution.
* Stock quantities.
* Inventory movements.
* Stock adjustments.
* Purchase-related stock increases.
* Sale-related stock decreases.
* Search/filtering.
* Stock visibility.

Incorrect stock transitions can corrupt operational data, so movement rules must be explicit and tested.

### Purchases

**Status: Planned**

Expected areas:

* Supplier association.
* Purchase registration.
* Purchased items.
* Costs and totals.
* Purchase history.
* Inventory integration.

### Suppliers

**Status: Planned**

Expected areas:

* Supplier identity.
* Contact information.
* Status.
* Search/filtering.
* Relationships with purchases and products.

### Expenses

**Status: Planned**

Expected areas:

* Small operating expenses.
* Card expenses.
* Categories.
* Merchant/vendor information.
* Date, amount, currency, and payment source.
* Review and correction workflow.

A later enhancement may extract data from bank vouchers received as PDFs or images.

### Accounts Receivable

**Status: Planned**

Expected areas:

* Outstanding balances.
* Payments.
* Partial payments.
* Paid/unpaid state.
* Due dates.
* Aging.
* Payment history.
* Collection alerts.

### Dashboard and Analytics

**Status: Planned**

Reporting should be built from structured server-side data and real management requirements.

Potential indicators include sales, purchases, expenses, receivables, inventory, quotation activity, and business summaries.

### Application Configuration

**Status: Planned**

Potential areas include company information, business preferences, user administration, and roles.

## 7. Deferred Capabilities

### Direct Electronic Invoicing with Ministerio de Hacienda

**Status: Deferred**

Direct Costa Rican electronic invoicing is intentionally postponed.

The current business can continue generating fiscal electronic invoices through its existing external provider while Revestik manages internal operational workflows.

Revestik should revisit direct Hacienda integration only after the core application is production-proven and the regulatory/integration scope is intentionally reopened.

### Automated Email Distribution

**Status: Deferred**

Manual PDF download currently satisfies the quotation-distribution need.

### WhatsApp Distribution

**Status: Deferred**

Direct WhatsApp integration is not an MVP requirement.

### AI-Assisted Expense Ingestion

**Status: Deferred enhancement**

Future expense workflows may use document AI/LLM-assisted extraction and classification for unstructured PDFs or images.

Structured XML should use deterministic parsing when available.

Early automation should include human confirmation before persisted expense creation.

## 8. Production Infrastructure

Before Revestik becomes authoritative for important production operations, select and document:

* Hosting platform.
* Production SQL Server environment.
* Secret management.
* Domain/DNS/TLS.
* Observability.
* Backup and recovery.
* Deployment workflow.
* Migration workflow.
* Rollback expectations.

The production hosting decision should be recorded through an ADR when selected.

## 9. Authorization Expansion

Authorization should evolve with real organizational responsibilities.

Potential capabilities include:

```text
View customers
Manage customers
View quotations
Manage quotations
View sales
Manage sales
View inventory
Manage inventory
Register purchases
Register payments
Manage users
Manage configuration
```

Permissions should reflect real roles rather than arbitrary technical groupings.

## 10. Auditability

Financial and operational domains may require stronger audit information, including:

* Who performed an action.
* What changed.
* When it changed.
* Relevant previous state.
* Relevant resulting state.

Audit infrastructure should be introduced when domain requirements justify it.

## 11. Testing Expansion

Each critical domain should introduce tests with implementation.

Upcoming priorities include:

1. Sales state and conversion rules.
2. Inventory movement and stock invariants.
3. Purchase-to-stock behavior.
4. Accounts receivable/payment transitions.
5. Expanded authorization coverage.
6. Database migration verification.
7. Critical browser workflows where automation is justified.

## 12. Performance and Background Processing

Performance work should be driven by measurements.

Background infrastructure should only be added when a concrete asynchronous requirement exists.

Potential future cases include large report generation, scheduled alerts, or retryable external integrations.

## 13. Definition of Done for New Domains

A substantial domain should be evaluated against:

* Business rules documented.
* Request validation implemented.
* Server-side rules enforced.
* Authorization defined.
* Persistence model defined.
* Database integrity considered.
* Error behavior defined.
* Relevant automated tests added.
* UI workflow functional.
* Documentation updated.
* Build succeeds.
* Test suite succeeds.
* CI succeeds.

## 14. Current Product Direction

```text
Engineering foundation
        ↓
Customers
        ↓
Quotations
        ↓
Sales
        ↓
Inventory
        ↓
Purchases / Suppliers
        ↓
Expenses
        ↓
Dashboard / Analytics
        ↓
Production hardening
```

Electronic invoicing, automated email, WhatsApp, and AI-assisted document ingestion are future capabilities and are not current MVP blockers.

## 15. Roadmap Principle

> Build the smallest architecture that safely supports the next real business requirement, then evolve it when evidence justifies the change.