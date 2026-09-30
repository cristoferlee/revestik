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

Users who manage customers, quotations, sales, payments, purchases, suppliers, inventory, and daily operational records.

### Management

Users who require visibility into commercial activity, balances, expenses, receivables, inventory, purchases, and operational performance.

## 4. Product Goals

* Centralize important business information.
* Reduce dependence on disconnected spreadsheets and manual records.
* Enforce consistent business rules.
* Provide reliable customer and operational data.
* Improve visibility into business activity.
* Support secure multi-user access.
* Automate repetitive administrative processes where practical.
* Preserve traceability of commercial and inventory operations.
* Build a reliable production application before introducing high-risk integrations.
* Provide a technical foundation that can grow with additional business modules.

## 5. Current Product Status

The application foundation, Customer domain, Quotations domain, Sales domain, and Inventory core are implemented and verified.

The next major domain is **Purchases / Suppliers**.

Inventory is considered functionally complete for the current product stage and should only receive changes when a real defect, requirement, or integration need is identified.

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
* Product-linked inventory consumption when an issued sale uses inventory products.
* FIFO-based stock consumption.
* Exact restoration of consumed inventory layers when an issued sale is voided.

### Inventory

The Inventory core is implemented and verified.

Current capabilities include:

* Configurable product categories.
* Product catalog with search, filtering, pagination, and deterministic ordering.
* CABYS support for physical products.
* Physical inventory units and commercial units.
* Conversion between inventory and commercial quantities.
* Whole-unit quantity rules where required.
* Current stock visibility.
* Initial stock registration.
* Immutable inventory movements.
* Manual stock increases and decreases.
* FIFO inventory consumption.
* FIFO cost layers.
* Current product cost reference.
* Tracking of inventory quantities with unknown historical cost.
* Explicit resolution of unknown costs without rewriting the original source cost.
* Product-linked sale consumption.
* Prevention of negative inventory.
* Exact FIFO restoration when a sale is voided.
* Traceability between original sale movements and reversal movements.
* Physical inventory counts.
* Physical-count adjustments with traceability.
* Product deactivation and reactivation.
* Logical archival for products permanently removed from normal product workflows.
* Preservation of historical database records and references after product archival.
* Concurrency and data-integrity protections for critical inventory operations.
* Dedicated Inventory, Physical Count, and Unknown Inventory Cost user interfaces.

Inventory represents physical materials only.

Services, transportation, installation, and other commercial charges do not create inventory stock.

Purchases are intentionally kept outside the Inventory domain. The future Purchases domain will create valid stock-entry operations when accepted purchases affect inventory.

### External Integration Foundations

* Costa Rican taxpayer lookup.
* Structured Costa Rican location data.
* Server-side external-integration boundaries.
* Appropriate caching of reference data.

### Automated Quality Checks

Current verified baseline:

**439 passing tests, 0 failed.**

Coverage includes customers, quotations, sales, products, inventory movements, FIFO behavior, inventory costs, physical counts, sale inventory consumption and reversal, security, SQL Server persistence/concurrency, PDFs, and hosting.

## 7. Current Development Focus

### Purchases / Suppliers

The next major domain will manage supplier and purchase workflows independently from Inventory while integrating with it where physical stock is affected.

Planned capabilities include:

* Supplier records.
* Manual purchase registration.
* Purchase lines linked to products.
* Purchase quantities and costs.
* Purchase totals.
* Purchase history.
* Inventory increases generated by accepted purchases.
* Creation of inventory cost layers from valid purchase costs.
* Traceability between a purchase and its resulting inventory movements.

### Purchase Invoices

Purchase invoices will live inside the Purchases domain rather than as a separate top-level module.

A dedicated section is expected to manage Costa Rican XML 4.4 documents received by the business.

Planned behavior includes:

* Reception of XML 4.4 documents from the business email workflow.
* XML deserialization.
* Identification of document type.
* Classification of purchase invoices.
* Classification of credit notes.
* Detection and handling of invalid or unexpected documents.
* Interpretation of applicable tax rates, including 13%, 4%, 1%, exempt, and other supported cases.
* Review and acceptance before a received document affects operational records.
* Creation or adjustment of purchase information from accepted documents.
* Inventory impact only after the corresponding purchase operation is considered valid.

The email integration acts as an input source for Purchases. Inventory itself should not be responsible for reading email or parsing fiscal XML documents.

## 8. Planned Product Areas

### Expenses

Planned capabilities include structured capture of operating expenses and small card expenses.

### Accounts Receivable Expansion

Sales already track payments and outstanding balances.

A broader accounts-receivable domain may be added later if requirements expand into aging, collection workflows, alerts, or cross-sale customer balances.

### Reports

Reports will aggregate information already owned by operational domains rather than becoming the source of transactional data.

Expected reporting areas include:

* Purchase totals.
* Sales totals.
* Inventory value.
* Purchase-versus-sales comparisons.
* General commercial and operational totals.
* Period-based summaries.

Purchase reporting is expected to use accepted purchase information and received purchase documents where applicable.

### Dashboard and Analytics

The Dashboard/Main area will be a separate application area focused on presenting the most important operational information for the current period.

Expected indicators may include:

* Current-month sales.
* Current-month purchases.
* Inventory value.
* Outstanding balances.
* Quotation activity.
* Commercial summaries.
* Other operational indicators as requirements become concrete.

The Dashboard should consume information from source domains and should not own transactional business logic.

### Application Configuration

Planned administrative configuration may include company information, business preferences, user administration, and role configuration.

### Public RevestikCR.com Website

A separate public-facing website is planned for final customers.

Its purpose is expected to include presentation of completed projects, company information, commercial presence, and other customer-facing content without exposing the internal operational application.

## 9. Deferred Capabilities

### Direct Electronic Invoicing

Direct integration with Costa Rica's Ministerio de Hacienda remains deferred.

Revestik Sales are internal commercial documents and should not be confused with fiscal electronic invoices.

Fiscal invoices can continue to be issued through the existing external invoicing provider until direct integration is intentionally reopened.

### Automated Outbound Email and WhatsApp Distribution

Automated outbound distribution remains deferred because authenticated PDF download already supports the current manual workflow.

### AI-Assisted Expense Ingestion

AI-assisted extraction and classification of purchase invoices, bank vouchers, PDFs, and images is a future capability.

Where structured XML exists, deterministic parsing should be preferred.

### Test-Suite Performance Optimization

The current automated suite prioritizes regression protection and correctness.

Performance optimization of the test suite can be revisited after the main Revestik application and the public RevestikCR.com experience are further completed or after deployment creates a practical need for faster execution.

## 10. Product Principle

Revestik favors completing smaller, reliable business workflows before introducing integrations with higher operational, regulatory, or security risk.

Each domain should own its business responsibility while integrating with neighboring domains through explicit operations.

Inventory owns physical stock behavior.

Purchases will own purchase and supplier workflows.

Reports and Dashboard will consume operational information without becoming the source of transactional truth.

The product should become useful in production incrementally rather than waiting for every possible automation to exist.