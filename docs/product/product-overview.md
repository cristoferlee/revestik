# Product Overview

## 1. Product Summary

Revestik is a business management web application designed for small and medium-sized businesses that need to centralize commercial, financial, customer, inventory, and operational information.

The project originates from real operational workflows commonly handled through disconnected spreadsheets, documents, messaging, and manual processes.

Revestik aims to bring these workflows into a single system while maintaining clear business rules, reliable data, and an architecture capable of evolving as the business grows.

## 2. Problem

Small businesses often manage daily operations across multiple disconnected tools.

Typical information may be distributed between:

* Customer records.
* Quotations.
* Inventory records.
* Accounts receivable.
* Supplier information.
* Sales and financial reports.
* Purchase invoices and small card expenses.
* Taxpayer information.
* Spreadsheets and manually generated documents.

This creates problems such as inconsistent data, limited visibility, manual reconciliation, difficulty tracking balances and stock, and increased possibility of human error.

Revestik is intended to provide a centralized application for these workflows.

## 3. Target Users

Revestik is primarily designed for small and medium-sized businesses with operational and commercial workflows involving customers, products, quotations, sales, purchases, expenses, payments, and inventory.

Potential users include:

### Administrators

Users responsible for system configuration, access control, and sensitive business operations.

### Commercial and administrative staff

Users who manage customers, quotations, sales-related information, purchases, suppliers, and daily operational records.

### Management

Users who require visibility into commercial activity, financial indicators, expenses, receivables, inventory, and operational performance.

## 4. Product Goals

The primary goals of Revestik are:

* Centralize important business information.
* Reduce dependence on disconnected spreadsheets and manual records.
* Enforce consistent business rules.
* Provide reliable customer and operational data.
* Improve visibility into business activity.
* Support secure multi-user access.
* Automate repetitive administrative processes where practical.
* Build a reliable production application before introducing high-risk integrations.
* Provide a technical foundation that can grow with additional business modules.

## 5. Current Product Status

Revestik is under active development.

Functionality uses four practical states:

* **Implemented** — present and verified in the current application.
* **In Progress** — actively being developed or integrated.
* **Planned** — intended product functionality that has not started.
* **Deferred** — intentionally postponed so it does not block the current product milestone.

The application foundation, Customer domain, and Quotations domain are implemented and verified.

The next active domain is **Sales**.

## 6. Implemented Capabilities

### Authentication and Access Control

Implemented capabilities include:

* ASP.NET Core Identity.
* Cookie-based authentication.
* Google external authentication.
* Role and policy-based authorization.
* Protected server endpoints.
* Antiforgery protection for authenticated mutable operations.
* Server-side secret boundary.

### Customer Management

Implemented capabilities include:

* Customer creation, retrieval, editing, deactivation, and reactivation.
* Pagination, filtering, and name-based search.
* Deterministic ordering.
* Customer active/inactive state.
* Validation of Costa Rican identification formats.
* Unique customer identification enforcement.
* Contact and Costa Rican location information.
* Creation and modification timestamps.
* Layered server and database integrity protections.

### Quotations

The Quotations domain is implemented end to end.

Implemented capabilities include:

* Blazor create/edit workflow.
* Active-customer selection.
* `Draft` and `Issued` quotation states.
* Customer snapshot on issue/reissue.
* Manual quotation lines.
* Optional registered-product association.
* Product-assisted line entry.
* Optional CABYS for commercial quotation lines.
* Unit of measure.
* Decimal commercial quantities.
* CRC and USD currency support.
* IVA-inclusive price calculations.
* 0% and 13% IVA handling.
* Percentage and fixed discounts.
* Additional charges.
* Immediate client-side total feedback.
* Server-authoritative calculations.
* Backend-generated `COT-000001`-style consecutive numbers.
* SQL Server sequence-backed numbering.
* Preservation of quotation identity and COT number during updates and reissue.
* Confirmed-line workflow before save or issue.
* Backend PDF generation with QuestPDF.
* Authenticated PDF download from the Blazor client.
* Server-side validation and authorization.
* SQL Server persistence and concurrency verification.

Quotation operations do not reserve or deduct inventory.

### Product Support for Quotations

A focused product lookup capability supports quotation entry.

It includes:

* Product persistence.
* Search and pagination.
* Product-assisted quotation line entry.
* Authorization and pagination verification.

This capability supports Quotations but does not yet represent the complete Inventory domain.

### Location and Taxpayer Integration Foundations

The application includes:

* Structured Costa Rican location data.
* Costa Rican taxpayer lookup integration.
* Server-side external-integration boundaries.
* Caching for appropriate reference data.

### Automated Quality Checks

The project includes automated tests and continuous integration covering customers, quotations, products, security, SQL Server behavior, and hosting.

Current baseline:

**194 passing tests, 0 failed, 0 skipped.**

## 7. Current Development Focus

### Sales

Sales is the next domain to investigate and implement.

The Sales domain will be separate from Quotations.

Expected design questions include:

* How a quotation becomes a sale.
* Internal `VEN-xxxxxx` numbering.
* Sale states and cancellation behavior.
* Inventory movement timing.
* Payment information.
* Handling manual lines versus registered products.
* Optional reference to externally issued fiscal documents.

The exact rules will be defined during the Sales investigation before implementation.

## 8. Planned Product Areas

### Inventory

Planned capabilities include product/material catalog behavior, stock quantities, movements, adjustments, purchase-related increases, and sale-related decreases.

### Purchases and Suppliers

Planned capabilities include supplier records, purchase registration, purchased items, costs, totals, stock impact, and purchase history.

### Expenses

Planned capabilities include structured capture of operating expenses and small card expenses.

A later phase may use AI-assisted document understanding for vouchers received as PDF or image, but human confirmation should remain part of early automation.

### Accounts Receivable

Planned capabilities include balances, payments, partial payments, due dates, aging, history, and collection alerts.

### Dashboard and Analytics

Planned dashboards will focus on actual operational reporting needs, including sales, purchases, expenses, receivables, inventory, quotation activity, and business summaries.

### Application Configuration

Planned administrative configuration may include company information, business preferences, user administration, and role configuration.

## 9. Deferred Capabilities

### Direct Electronic Invoicing

Direct integration with Costa Rica's Ministerio de Hacienda is intentionally deferred.

Revestik currently treats quotations and internal commercial operations separately from fiscal electronic invoicing. Fiscal invoices can continue to be issued through the existing external invoicing provider.

Direct Hacienda integration may be revisited after the core application is production-proven.

### Automated Email and WhatsApp Distribution

Automated distribution of quotations or documents through email and WhatsApp is deferred because manual PDF download already satisfies the current workflow.

### AI-Assisted Expense Ingestion

AI-assisted extraction and classification of purchase invoices, bank vouchers, PDFs, and images is a future capability.

Where structured XML exists, deterministic parsing should be preferred. AI should be reserved for unstructured documents and classification tasks.

## 10. Product Principle

Revestik favors completing smaller, reliable business workflows before introducing integrations with higher operational, regulatory, or security risk.

The product should become useful in production incrementally rather than waiting for every possible automation to exist.