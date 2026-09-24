# Product Roadmap

## 1. Purpose

This document describes the current implementation status and planned evolution of Revestik.

Status terminology:

* **Implemented** — available and verified in the current codebase.
* **In Progress** — actively being developed.
* **Planned** — intentionally expected but not yet implemented.
* **Under Evaluation** — being considered but not selected.
* **Deferred** — intentionally postponed.

## 2. Roadmap Principles

1. Complete business workflows vertically.
2. Protect important rules on the server.
3. Add automated verification alongside critical behavior.
4. Keep documentation synchronized with the codebase.
5. Avoid unnecessary architectural complexity.
6. Prioritize workflows that solve real operational needs.
7. Treat security and data integrity as product requirements.
8. Preserve important business history instead of deleting it.
9. Do not let high-risk integrations block a smaller useful production release.

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

Includes creation/editing, Draft/Issued lifecycle, customer snapshot, commercial lines, discounts, charges, COT numbering, PDFs, history/search/filtering, and conversion into linked Sale Drafts.

Quotations remain historical after conversion and do not modify inventory.

### Sales Domain

**Status: Implemented**

Includes:

* Direct Sales.
* Sales from issued quotations.
* Draft/Issued/Voided lifecycle.
* SQL Server sequence-backed `VEN-xxxxxx`.
* Customer snapshots.
* Manual/product-assisted lines.
* Discounts and charges.
* IVA-inclusive calculations.
* Internal PDFs.
* Searchable/paginated history.
* Per-currency summaries.
* Partial/full payments.
* Payment history and payment voids.
* Outstanding-balance tracking.
* Sale voiding.
* Replacement/correction workflow.
* Quotation-to-sale and sale-to-replacement traceability.
* Authorization and antiforgery protection.
* Automated lifecycle, query, payment, PDF, validation, persistence, and concurrency tests.

### Product Support for Commercial Entry

**Status: Implemented as supporting capability**

A focused Product model and paginated lookup support quotations and sales.

This is not yet the complete Inventory domain.

### Engineering Foundation

* Automated test project.
* Focused behavior tests.
* Integration and hosting tests.
* SQL Server Testcontainers coverage.
* CI verification.
* Release build verification.
* Publish/static-asset verification.
* Health endpoint.
* EF Core migrations.
* Architecture, security, product, testing, and deployment documentation.

Current verified baseline:

**257 passed, 0 failed.**

## 4. Current Development Phase

The implemented platform now includes:

```text
Engineering foundation
        +
Customers
        +
Quotations
        +
Sales
        =
Current implemented platform
```

## 5. Current Focus — Inventory

**Status: Planned / next major domain**

Inventory should define:

* Product/material catalog evolution.
* Stock quantities.
* Inventory movements.
* Stock adjustments.
* Sale-related decreases.
* Purchase-related increases.
* Insufficient-stock behavior.
* Search/filtering.
* Stock visibility.
* Traceability of stock-changing operations.

Incorrect inventory transitions can corrupt operational data, so movement rules must be explicit and tested before implementation.

## 6. Near-Term Domains

### Purchases
**Status: Planned**

Expected areas:

* Supplier association.
* Purchase registration.
* Purchased items.
* Costs and totals.
* Purchase history.
* Inventory increases.

### Suppliers
**Status: Planned**

### Expenses
**Status: Planned**

### Accounts Receivable Expansion
**Status: Planned / conditional**

Sales already implement payment tracking and outstanding balances.

A broader receivables domain should be introduced only if requirements expand into customer-level aging, collection alerts, or cross-sale views.

### Dashboard and Analytics
**Status: Planned**

### Application Configuration
**Status: Planned**

## 7. Deferred Capabilities

### Direct Electronic Invoicing with Ministerio de Hacienda
**Status: Deferred**

Internal `VEN` sales are not fiscal electronic invoices.

The business may continue using its external invoicing provider until direct Hacienda integration is intentionally reopened.

### Automated Email Distribution
**Status: Deferred**

### WhatsApp Distribution
**Status: Deferred**

### AI-Assisted Expense Ingestion
**Status: Deferred enhancement**

Structured XML should use deterministic parsing where available.

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

## 10. Auditability

Sales already preserve important operational history through voiding and replacement relationships.

Future financial and operational domains may require broader audit information such as before/after values or centralized event history.

Audit infrastructure should be introduced when domain requirements justify it.

## 11. Testing Expansion

Upcoming priorities include:

1. Inventory movement and stock invariants.
2. Purchase-to-stock behavior.
3. Expanded receivables behavior if introduced.
4. Expense classification/storage.
5. Expanded authorization coverage.
6. Database migration verification.
7. Critical browser automation where justified.

## 12. Performance and Background Processing

Performance work should be measurement-driven.

Background infrastructure should only be added for concrete asynchronous requirements.

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