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
10. Keep domain ownership explicit so neighboring modules integrate without collapsing responsibilities into one another.

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
* Product-linked inventory consumption.
* FIFO-based stock consumption.
* Exact inventory restoration when an issued sale is voided.
* Authorization and antiforgery protection.
* Automated lifecycle, query, payment, PDF, validation, persistence, concurrency, inventory-consumption, and reversal tests.

### Inventory Domain

**Status: Implemented**

The Inventory core is complete for the current product stage.

Implemented capabilities include:
* Configurable product categories.
* Product catalog management.
* Search, filtering, pagination, and deterministic ordering.
* CABYS support for physical products.
* Physical inventory units.
* Commercial units.
* Inventory/commercial unit conversion.
* Whole-unit quantity enforcement where required.
* Current stock tracking.
* Initial stock registration.
* Immutable inventory movements.
* Manual stock increases and decreases.
* FIFO inventory consumption.
* FIFO cost layers.
* Current product cost reference.
* Explicit unknown historical cost tracking.
* Unknown-cost resolution without overwriting the original cost source.
* Sale-related stock consumption.
* Prevention of negative inventory.
* Exact FIFO-layer restoration when an issued sale is voided.
* Provenance between original sale movements and reversal movements.
* Physical inventory counts.
* Traceable physical-count adjustments.
* Product deactivation and reactivation.
* Logical archival for permanent removal from normal product workflows.
* Preservation of historical product references after archival.
* Concurrency and data-integrity safeguards.
* Dedicated Inventory UI.
* Dedicated Physical Count UI.
* Dedicated Unknown Inventory Cost UI.
* Authorization and antiforgery protection.
* Automated inventory service, endpoint, integrity, cost-resolution, physical-count, quantity-rule, and reversal coverage.

Inventory owns physical stock behavior.

Purchases, supplier management, email ingestion, and XML 4.4 processing intentionally remain outside the Inventory domain.

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

**542 passed, 0 failed.**

## 4. Current Implemented Platform

The implemented platform now includes:

```text
Engineering foundation
        +
Customers
        +
Quotations
        +
Sales
        +
Inventory Core
        +
Suppliers / Purchases / Accounts Payable
        +
Purchase-to-Inventory receipt
        +
Electronic Documents / Hacienda XML 4.4 / CAByS
        +
Accounting Classification
        +
Manual Gmail ingestion
        +
Received Documents refinement
        =
Current implemented platform
```

Inventory is considered closed as a core domain for the current stage.

Further Inventory changes should be driven by real defects, new requirements, or explicit integration needs rather than continued expansion without a business requirement.

## 5. Recently Completed — Purchases / Suppliers / Accounts Payable

**Status: Implemented**

Purchases / Suppliers are implemented as dedicated operational workflows separate from Inventory.

Implemented areas include:

* Supplier records.
* Supplier search and lifecycle rules.
* Manual purchase registration.
* Product-linked purchase lines.
* Authorized product creation during Purchase entry.
* Purchase quantities, costs, discounts, taxes, and totals.
* CRC and USD with exchange-rate support.
* Cash and credit Purchases.
* Credit terms and due dates.
* Purchase payments.
* Accounts Payable summaries.
* Upcoming and overdue alerts.
* Supplier Purchase history.
* Explicit Purchase-to-Inventory receipt.
* FIFO cost-layer creation from valid Purchase costs.
* Traceability between Purchases and resulting Inventory movements.
* Prevention of duplicate Inventory application.
* Transactional consistency for critical operations.

Inventory remains responsible only for physical-stock behavior.

## 6. Recently Completed — Received Electronic Documents

### Costa Rican Hacienda XML 4.4

**Status: Implemented**

Received fiscal-document processing now exists as a dedicated Electronic Documents workflow rather than being embedded inside Purchases.

Implemented areas include:

* Manual fiscal XML import.
* Secure staging/quarantine before canonical acceptance.
* Original XML preservation.
* Document-family recognition.
* Local Hacienda XSD validation.
* Secure XML parsing.
* Detection of duplicate/rejected documents.
* Issuer, receiver, currency, totals, economic activity, lines, taxes, and discounts.
* Hacienda response association.
* Accounting classification.
* Operational destination.
* Payment-condition classification.
* Document-level and line-level classification.
* Learned classification rules.
* Confidence-based suggestions.
* Manual review/acceptance.

Electronic Documents do not automatically create Purchases or Inventory movements.

`FechaEmision` is the economic date of a received fiscal document.

### CAByS 2025

**Status: Implemented**

The local versioned CAByS catalog supports product and received-document workflows, including code/description lookup and hierarchical classification information.

### Manual Gmail Ingestion

**Status: Implemented**

The Google Workspace Gmail integration supports:

* Server-side OAuth 2.0.
* `gmail.readonly`.
* Connect/disconnect/status.
* Message search.
* MIME traversal.
* XML attachment retrieval.
* Per-attachment processing.
* Duplicate/rejected tracking.
* Controlled retry.
* Manual synchronization.
* Single-flight synchronization protection.
* Encrypted persisted integration state.

Gmail is transport only and does not directly create business transactions.

### Received Documents Refinement

**Status: Implemented**

The current refinement checkpoint includes:

* Current-calendar-month History by default.
* Explicit historical date-range filtering.
* `FechaEmision`-based period selection.
* Period accounting/financial summaries.
* Invalid date-range validation.
* Global Pending queue independent of the selected History period.
* Filter UI improvements.
* Accounting-category management UI improvements.
* Received-document review UX refinement.

### Manual Gmail Synchronization

**Status: Implemented / current operating model**

Both received-document Gmail ingestion and bank-voucher Gmail ingestion are manual
user-triggered workflows.

The bank-voucher mailbox integration additionally includes sender/subject filtering,
Gmail-rate-limit protection, duplicate safety by Gmail message id, and a retry-safe
synchronization cursor.

Periodic background synchronization is intentionally deferred rather than being the
next product step.


## 7. Near-Term Domains

### Expenses

**Status: Implemented**

The current module supports:

* Manual operating-expense capture.
* Banco Nacional card-voucher ingestion through a separate Gmail mailbox.
* Voucher review states: NeedsReview, Accepted, Matched, and Ignored.
* Conservative voucher-to-ElectronicDocument matching.
* Consolidated expense totals without voucher/XML double counting.
* Currency-separated totals with no invented conversion.

Pending UX polish includes paginating the voucher review queue and removing reviewed
items from the active review view immediately after an action.

### Accounts Receivable Expansion

**Status: Planned / conditional**

Sales already implement payment tracking and outstanding balances.

A broader receivables domain should be introduced only if requirements expand into customer-level aging, collection alerts, collection workflows, or cross-sale views.

### Reports

**Status: Planned**

Reports should aggregate data already owned by operational domains.

Expected areas include:
* Purchase totals.
* Sales totals.
* Inventory value.
* Purchase-versus-sales comparisons.
* Period-based summaries.
* Commercial totals.
* Other operational totals required by the business.

Reports should not become the source of transactional truth.

### Dashboard and Analytics

**Status: Planned**

The Dashboard/Main area should surface important current-period operational information.

Expected indicators may include:
* Current-month sales.
* Current-month purchases.
* Inventory value.
* Outstanding balances.
* Quotation activity.
* Commercial summaries.
* Other operational indicators as requirements become concrete.

Dashboard logic should consume information from source domains rather than own transactional rules.

### Application Configuration

**Status: Planned**

Expected areas may include:
* Company information.
* Business preferences.
* User administration.
* Role configuration.
* Other administrative settings justified by implemented workflows.

### Public RevestikCR.com Website

**Status: Planned**

A separate public-facing website is planned for final customers.

Its expected purpose includes:
* Presentation of completed projects.
* Company information.
* Customer-facing commercial content.
* Public business presence.

The public site should remain separate from the internal operational application and must not expose internal business-management functionality.

## 8. Deferred Capabilities

### Direct Electronic Invoicing with Ministerio de Hacienda

**Status: Deferred**

Internal `VEN` sales are not fiscal electronic invoices.

The business may continue using its external invoicing provider until direct Hacienda integration is intentionally reopened.

### Automated Outbound Email Distribution

**Status: Deferred**

Outbound document distribution remains deferred because authenticated document download supports the current workflow.

### WhatsApp Distribution

**Status: Deferred**

### AI-Assisted Expense Ingestion

**Status: Deferred enhancement**

AI-assisted extraction and classification may be considered later for documents where structured deterministic data is unavailable or insufficient.

Structured XML should use deterministic parsing where available.

### Test-Suite Performance Optimization

**Status: Deferred**

The current test suite prioritizes correctness and regression protection.

Performance optimization can be revisited after the main internal application and the public RevestikCR.com experience are further completed, or when deployment creates a practical need for faster feedback.

## 9. Production Infrastructure

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

## 10. Authorization Expansion

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

Manage physical counts

Resolve inventory costs

Register purchases

Manage suppliers

Register payments

View reports

Manage users

Manage configuration

```

Authorization should continue to be introduced according to real responsibilities rather than through speculative role complexity.

## 11. Auditability

Sales and Inventory already preserve important operational history.

Current examples include:
* Voided payments remain historical.
* Voided sales remain historical.
* Replacement sales preserve the original issued sale.
* Inventory movements preserve stock-changing history.
* Sale inventory reversals preserve provenance to original movements.
* Physical-count adjustments remain traceable.
* Product archival preserves historical references.

Future financial and operational domains may require broader audit information such as before/after values or centralized event history.

Audit infrastructure should be introduced when domain requirements justify it.

## 12. Testing Expansion

Inventory, Purchases, Accounts Payable, Electronic Documents, Hacienda XML 4.4, Gmail ingestion, and received-document period-summary coverage are now part of the implemented baseline.

Upcoming testing priorities include:

1. Periodic Gmail background-synchronization behavior when implemented.
2. Background execution concurrency and single-flight protection.
3. Retry/recovery behavior across repeated scheduled runs.
4. Expanded receivables behavior if introduced.
5. Expense classification/storage.
6. Reports and aggregation behavior.
7. Expanded authorization coverage.
8. Broader database migration verification.
9. Critical browser automation where justified.

Test-suite runtime optimization is intentionally not a current functional-development priority.

## 13. Performance and Background Processing

Performance work should be measurement-driven.

Background infrastructure should only be added for concrete asynchronous requirements.

Manual Gmail ingestion is already implemented. Periodic Gmail background synchronization is now the concrete next asynchronous requirement.

The background implementation should reuse the existing Gmail ingestion pipeline rather than introduce a second parallel ingestion model.

## 14. Definition of Done for New Domains

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
* Integration boundaries with neighboring domains are explicit.

## 15. Current Product Direction

```text
Engineering foundation
        ↓
Customers
        ↓
Quotations
        ↓
Sales
        ↓
Inventory Core
        ↓
Purchases / Suppliers / Accounts Payable
        ↓
Electronic Documents / Hacienda XML 4.4 / CAByS
        ↓
Manual Gmail ingestion
        ↓
Received Documents refinement
        ↓
Periodic Gmail background synchronization
        ↓
Expenses
        ↓
Reports
        ↓
Dashboard / Analytics
        ↓
RevestikCR.com
        ↓
Production hardening
```

Direct electronic invoicing, automated outbound email distribution, WhatsApp distribution, AI-assisted document ingestion, and test-suite performance optimization remain future capabilities rather than current MVP blockers.

## 16. Roadmap Principle

> Build the smallest architecture that safely supports the next real business requirement, then evolve it when evidence justifies the change.
