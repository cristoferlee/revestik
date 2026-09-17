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

The application foundation and Customer domain are implemented and hardened.

The Quotations domain is currently **In Progress**. Its backend persistence, monetary calculations, validation, authorization, API operations, and automated verification are implemented. The Blazor user interface and document-generation workflow remain to be completed before Quotations is considered an end-to-end implemented product module.

## 6. Implemented Capabilities

### Authentication and Access Control

The application currently includes an authentication and authorization foundation using ASP.NET Core Identity.

Implemented capabilities include:

* Authenticated application access.
* Cookie-based authentication.
* Role and policy-based authorization.
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

```text
docs/product/business-rules.md
```

### Quotation Backend

The Quotations backend currently provides the server-side foundation for commercial quotations.

Implemented capabilities include:

* Server-side quotation persistence.
* Association with an existing active customer.
* Quotation lines.
* Optional association of quotation lines with registered products.
* Manual quotation lines.
* Required CABYS codes.
* Decimal commercial quantities.
* CRC and USD currency support.
* IVA-inclusive pricing calculations.
* 0% and 13% IVA handling.
* Percentage discounts.
* Fixed-amount discounts.
* Additional quotation charges.
* Server-calculated quotation totals.
* Backend-generated quotation consecutive numbers.
* SQL Server sequence-backed quotation numbering.
* Preservation of quotation identity and consecutive number during updates.
* Quotation creation through the API.
* Quotation retrieval by identifier through the API.
* Quotation modification through the API.
* Role and policy-based quotation authorization.
* Antiforgery protection for mutable quotation endpoints.
* Server-side validation of quotation requests, lines, and charges.
* Automated unit, service, authorization, HTTP contract, and SQL Server integration tests.

Creating or modifying a quotation does not reserve, deduct, or otherwise modify inventory.

Quotation PDF generation and the complete Blazor quotation workflow are not yet implemented and therefore the Quotations domain remains **In Progress**.

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
* Customer pagination behavior.
* Customer service behavior.
* Quotation monetary calculations.
* Quotation request and nested request validation.
* Quotation service behavior.
* Quotation authorization.
* Quotation API contracts.
* Quotation consecutive-number generation.
* Concurrent quotation-number generation against SQL Server.
* CSRF protection.
* Mutable endpoint protection.
* Development hosting behavior.
* Production hosting behavior.

At the current documented baseline, the complete automated test suite contains **161 passing tests with no failures**.

The CI pipeline also verifies that the hosted Blazor application is correctly produced during publishing.

## 7. In-Progress Product Areas

### Quotations

The Quotations domain is the current active business module.

Its server-side foundation is implemented and verified.

Current remaining work includes:

* Blazor quotation listing.
* Quotation creation user interface.
* Customer selection and integration with the existing Customer domain.
* Quotation line editing.
* Product-assisted quotation lines when Inventory becomes available.
* Additional-charge editing.
* Immediate client-side total feedback.
* Quotation editing workflow.
* PDF/document generation.
* Final end-to-end workflow verification.

The server remains authoritative for quotation validation and monetary calculations even when equivalent client-side calculations are introduced for immediate user feedback.

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

### Inventory

Inventory management capabilities intended to track:

* Products.
* Available quantities.
* Inventory movements.
* Product information.
* Commercial availability.

Quotation creation does not currently depend on Inventory because quotation lines may be entered manually.

When Inventory is implemented, registered products may provide initial quotation information without making quotation creation an inventory movement.

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

Quotation lines already require a CABYS code.

A broader CABYS/product-classification domain remains planned and may later provide catalog, search, product association, and regulatory-support capabilities.

### Sales and Invoicing

Future commercial workflows will build on rules already established by Quotations where appropriate, including:

* Customer association.
* Commercial lines.
* CABYS information.
* Monetary calculations.
* Discounts.
* Additional charges.
* Commercial document generation.

The implementation should reuse appropriate quotation foundations rather than recreating equivalent business logic independently.

Sales and invoicing introduce additional behavior that does not belong to quotations, including confirmed sale state and inventory impact where applicable.

### Electronic Invoicing

Electronic invoicing is a future integration area.

The planned interim workflow may allow an electronic invoice to be requested from Revestik while the actual electronic document is generated through the existing external invoicing process.

Requesting an electronic invoice must not by itself be treated as confirmation that the sale has been electronically invoiced.

Direct electronic invoicing integration must eventually be designed according to the applicable Ministerio de Hacienda technical and legal requirements at the time it is implemented.

Electronic invoicing is not considered an implemented Revestik capability until the required workflow has been developed and verified.

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

For financial calculations, the client may provide immediate feedback, but authoritative quotation values are calculated by the server.

### Incremental delivery

Modules should be implemented and validated incrementally rather than attempting to build the complete business platform at once.

### Real requirements over artificial complexity

Architecture and features should respond to actual business requirements. Revestik should not introduce technical complexity solely to demonstrate technologies or design patterns.

### Reuse proven business foundations

When later commercial workflows share concepts already implemented by Quotations, common behavior should be reused or extracted when the concrete requirements justify it.

Premature abstraction should still be avoided.

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

The current development direction is to complete the Quotations vertical workflow before expanding into additional transactional domains.

The immediate sequence is:

```text
Customer domain
    ↓
Quotation backend
    ↓
Quotation Blazor workflow
    ↓
Quotation document generation
    ↓
Sales / invoicing workflows
    ↓
Inventory and financial integration
```

The exact sequence may evolve as real business dependencies become clearer.

Development should prioritize complete vertical workflows over a large number of partially implemented modules.

The objective is not simply to add features, but to create reliable workflows where the UI, API, business rules, persistence, security, and automated verification work together.
