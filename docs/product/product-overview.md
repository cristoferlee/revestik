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
* Taxpayer information.
* Spreadsheets and manually generated documents.

This creates several problems:

* Duplicate or inconsistent data.
* Limited visibility into business performance.
* Manual reconciliation between systems.
* Difficulty tracking outstanding payments.
* Increased possibility of human error.
* Limited control over who can modify business information.
* Difficulty maintaining historical and operational records.

Revestik is intended to provide a centralized application for these workflows.

## 3. Target Users

Revestik is primarily designed for small and medium-sized businesses with operational and commercial workflows involving customers, products, quotations, payments, and inventory.

Potential users include:

### Administrators

Users responsible for system configuration, access control, and sensitive business operations.

### Commercial and administrative staff

Users who manage customers, quotations, sales-related information, and daily operational records.

### Management

Users who require visibility into business activity, financial indicators, accounts receivable, and operational performance.

The exact permissions available to each category will evolve as additional modules are implemented.

## 4. Product Goals

The primary goals of Revestik are:

* Centralize important business information.
* Reduce dependence on disconnected spreadsheets and manual records.
* Enforce consistent business rules.
* Provide reliable customer and operational data.
* Improve visibility into business activity.
* Support secure multi-user access.
* Automate repetitive administrative processes where practical.
* Provide a technical foundation that can grow with additional business modules.

## 5. Current Product Status

Revestik is under active development.

Functionality is classified using three states:

* **Implemented** — present in the current application.
* **In Progress** — actively being developed or integrated.
* **Planned** — part of the intended product scope but not yet considered implemented.

This distinction prevents roadmap functionality from being presented as existing functionality.

## 6. Implemented Capabilities

### Authentication and Access Control

The application currently includes an authentication and authorization foundation using ASP.NET Core Identity.

Implemented capabilities include:

* Authenticated application access.
* Cookie-based authentication.
* Role and policy-based authorization foundation.
* External authentication integration.
* Protected server endpoints.
* Antiforgery protection for authenticated mutable operations.

### Customer Management

Customer management is currently one of the most developed business modules.

Implemented capabilities include:

* Customer creation.
* Customer retrieval.
* Customer editing.
* Customer deactivation.
* Customer listing.
* Pagination.
* Filtering by identification type.
* Name-based search.
* Customer active/inactive state.
* Validation of Costa Rican identification formats.
* Unique customer identification enforcement.
* Customer contact information.
* Costa Rican location information.
* Creation and modification timestamps.

Customer records currently support:

* Physical person identification.
* Legal entity identification.
* DIMEX identification.

Detailed rules are maintained in:

```text id="k3r6x2"
docs/product/business-rules.md
```

### Location Data

Customer information supports structured Costa Rican location data including:

* Province.
* Canton.
* District.
* Additional address information.

### Taxpayer Integration Foundation

The solution contains contracts and server-side integration components related to taxpayer information.

External integrations are kept behind the server boundary rather than being called directly from the Blazor WebAssembly client.

### Hosted Web Application

The production hosting model supports serving the Blazor WebAssembly client from the ASP.NET Core application.

This provides a unified web application deployment model while maintaining the logical separation between client and server code.

### Automated Quality Checks

The project includes automated tests and continuous integration.

Current automated coverage includes areas such as:

* Customer validation.
* Pagination behavior.
* Customer service behavior.
* CSRF protection.
* Mutable endpoint protection.
* Development hosting behavior.
* Production hosting behavior.

The CI pipeline also verifies that the hosted Blazor application is correctly produced during publishing.

## 7. In-Progress Product Areas

Revestik continues to evolve beyond its current customer-management and platform foundation.

Areas under development may include the expansion and integration of business workflows that depend on the core customer, authentication, and persistence infrastructure.

An area should only be moved to **Implemented** after the relevant end-to-end behavior is available and verified.

## 8. Planned Product Areas

The intended product direction includes business capabilities such as:

### Dashboard and Analytics

Centralized visibility into relevant business and financial indicators.

Potential information includes:

* Sales activity.
* Accounts receivable.
* Inventory indicators.
* Commercial performance.
* Operational summaries.

### Quotations

Management of commercial quotations, including:

* Sequential quotation numbers.
* Customer association.
* Products and quantities.
* Pricing.
* Totals.
* Quotation status.
* Document/PDF generation.

### Inventory

Inventory management capabilities intended to track:

* Products.
* Available quantities.
* Inventory movements.
* Product information.
* Commercial availability.

### Accounts Receivable

Tracking of customer balances and payment status.

Intended capabilities include:

* Amount owed.
* Amount paid.
* Partial payments.
* Outstanding balances.
* Payment status.
* Due-date awareness and alerts.

### Suppliers

Centralized supplier information and future integration with purchasing and inventory workflows.

### Product and Tax Classification

Support for product information and Costa Rican tax-related classifications such as CABYS where required by the applicable business workflow.

### Electronic Invoicing

Electronic invoicing is a potential future integration area.

Any implementation involving Costa Rican electronic invoicing must be designed according to the applicable Ministerio de Hacienda technical and legal requirements at the time it is implemented.

It is not considered an implemented Revestik capability unless the complete required workflow has been developed and verified.

### Application Configuration

Centralized business and application settings required by the operational modules.

## 9. Product Principles

### Business rules belong in the system

Important operational rules should be explicitly represented and validated instead of relying on users to remember them.

### Data consistency over convenience

Where business-critical information must be unique, required, or correctly formatted, Revestik should enforce that requirement.

### Security by default

Business information should not become publicly accessible simply because a developer forgot to protect an individual endpoint.

### Clear distinction between UI and authority

The frontend guides the user, but the server and persistence layers remain responsible for protecting the integrity of business data.

### Incremental delivery

Modules should be implemented and validated incrementally rather than attempting to build the complete business platform at once.

### Real requirements over artificial complexity

Architecture and features should respond to actual business requirements. Revestik should not introduce technical complexity solely to demonstrate technologies or design patterns.

## 10. Scope Management

New functionality should be classified before implementation.

A proposed feature should answer:

1. What business problem does it solve?
2. Who needs it?
3. What business rules apply?
4. What data does it require?
5. Does it introduce security or authorization requirements?
6. Does it depend on another module?
7. How will its expected behavior be verified?

Features that do not yet have clear answers to these questions should remain in the roadmap rather than being treated as committed functionality.

## 11. Product Direction

Revestik is intended to evolve from its current application foundation into an integrated business management platform.

Development should prioritize complete vertical workflows over a large number of partially implemented modules.

The objective is not simply to add features, but to create reliable workflows where the UI, API, business rules, persistence, security, and automated verification work together.
