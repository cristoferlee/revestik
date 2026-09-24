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
* Sales records.
* Inventory records.
* Accounts receivable.
* Supplier information.
* Sales and financial reports.
* Purchase invoices and small card expenses.
* Taxpayer information.
* Spreadsheets and manually generated documents.

This creates inconsistent data, limited visibility, manual reconciliation, difficulty tracking balances and stock, and increased possibility of human error.

## 3. Target Users

### Administrators

Users responsible for system configuration, access control, and sensitive business operations.

### Commercial and administrative staff

Users who manage customers, quotations, sales, payments, purchases, suppliers, and daily operational records.

### Management

Users who require visibility into commercial activity, balances, expenses, receivables, inventory, and operational performance.

## 4. Product Goals

* Centralize important business information.
* Reduce dependence on disconnected spreadsheets and manual records.
* Enforce consistent business rules.
* Provide reliable customer and operational data.
* Improve visibility into business activity.
* Support secure multi-user access.
* Automate repetitive administrative processes where practical.
* Preserve traceability of commercial operations.
* Build a reliable production application before introducing high-risk integrations.
* Provide a technical foundation that can grow with additional business modules.

## 5. Current Product Status

The application foundation, Customer domain, Quotations domain, and Sales domain are implemented and verified.

The next major domain is **Inventory**.

## 6. Implemented Capabilities

### Authentication and Access Control

* ASP.NET Core Identity.
* Cookie-based authentication.
* Google external authentication.
* Role and policy-based authorization.
* Protected server endpoints.
* Antiforgery protection for authenticated mutable operations.
* Server-side secret boundary.

### Customer Management

* Customer creation, retrieval, editing, deactivation, and reactivation.
* Pagination, filtering, and search.
* Deterministic ordering.
* Customer active/inactive state.
* Costa Rican identification validation.
* Unique identification enforcement.
* Contact and structured location information.
* Layered server and database integrity protections.

### Quotations

* Create/edit workflow.
* `Draft` and `Issued` states.
* Customer snapshot on issue/reissue.
* Manual and product-assisted lines.
* Optional CABYS.
* Unit of measure.
* Decimal commercial quantities.
* CRC and USD.
* IVA-inclusive calculations.
* 0% and 13% IVA.
* Percentage and fixed discounts.
* Additional charges.
* Server-authoritative calculations.
* `COT-xxxxxx` SQL Server sequence-backed numbering.
* Backend PDF generation.
* Authenticated PDF download.
* History, filters, pagination, and detail view.
* Conversion of issued quotations into Sale Drafts.
* Preservation of the original quotation as historical source.

Quotations do not reserve or deduct inventory.

### Sales

* Direct Sale Draft creation.
* Sale Draft creation from an issued quotation.
* `Draft`, `Issued`, and `Voided` states.
* `VEN-xxxxxx` backend-generated numbering on issue.
* Manual and product-assisted lines.
* Optional CABYS.
* CRC and USD.
* IVA-inclusive monetary calculations.
* Line and general discounts.
* Additional charges.
* Customer snapshot on issue.
* Sale history with filters, pagination, summary cards, and detail view.
* Internal sale PDF generation.
* Partial and full payment registration.
* Payment methods including Cash, Sinpe, Bank Transfer, International Transfer, Card, and Other.
* Paid and outstanding balance tracking.
* Payment history and payment voiding.
* Sale voiding with reason.
* Replacement/correction workflow with historical traceability.
* Quotation-to-sale traceability.
* Original-to-replacement sale traceability.

### Product Support

A focused Product model and paginated lookup support commercial line entry.

This is not yet the complete Inventory domain.

### External Integration Foundations

* Costa Rican taxpayer lookup.
* Structured Costa Rican location data.
* Server-side external-integration boundaries.
* Appropriate caching of reference data.

### Automated Quality Checks

Current verified baseline:

**257 passing tests, 0 failed.**

Coverage includes customers, quotations, sales, products, security, SQL Server persistence/concurrency, PDFs, and hosting.

## 7. Current Development Focus

### Inventory

The next major domain should define:

* Product/material catalog evolution.
* Stock quantities.
* Inventory movements.
* Stock adjustments.
* Sale-related stock decreases.
* Purchase-related stock increases.
* Rules for insufficient stock.
* Search/filtering and stock visibility.

Inventory should be implemented only after explicit movement rules are agreed because incorrect stock transitions can corrupt operational data.

## 8. Planned Product Areas

### Purchases and Suppliers

Planned capabilities include supplier records, purchase registration, purchased items, costs, totals, stock impact, and purchase history.

### Expenses

Planned capabilities include structured capture of operating expenses and small card expenses.

### Accounts Receivable Expansion

Sales already track payments and outstanding balances.

A broader accounts-receivable domain may be added later if requirements expand into aging, collection workflows, alerts, or cross-sale customer balances.

### Dashboard and Analytics

Planned dashboards will focus on actual operational reporting needs, including sales, purchases, expenses, receivables, inventory, quotation activity, and business summaries.

### Application Configuration

Planned administrative configuration may include company information, business preferences, user administration, and role configuration.

## 9. Deferred Capabilities

### Direct Electronic Invoicing

Direct integration with Costa Rica's Ministerio de Hacienda remains deferred.

Revestik Sales are internal commercial documents and should not be confused with fiscal electronic invoices.

Fiscal invoices can continue to be issued through the existing external invoicing provider until direct integration is intentionally reopened.

### Automated Email and WhatsApp Distribution

Automated distribution remains deferred because authenticated PDF download already supports the current manual workflow.

### AI-Assisted Expense Ingestion

AI-assisted extraction and classification of purchase invoices, bank vouchers, PDFs, and images is a future capability.

Where structured XML exists, deterministic parsing should be preferred.

## 10. Product Principle

Revestik favors completing smaller, reliable business workflows before introducing integrations with higher operational, regulatory, or security risk.

The product should become useful in production incrementally rather than waiting for every possible automation to exist.