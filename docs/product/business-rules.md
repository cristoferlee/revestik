# Business Rules

## 1. Purpose

This document defines the business rules currently enforced by Revestik.

Business rules describe required system behavior independently from a specific UI implementation.

Rule statuses:

* **Implemented** — currently enforced.
* **In Progress** — implementation has started but is incomplete.
* **Planned** — intended behavior not yet implemented.
* **Deferred** — intentionally postponed.

---

# 2. Customer Management

## CUS-001 — Customer name is required

**Status:** Implemented

Every customer must have a name with the supported maximum length.

## CUS-002 — Customer identification type is required

**Status:** Implemented

Supported identification types are:

* `PhysicalPerson`
* `LegalEntity`
* `Dimex`

## CUS-003 — Customer identification number is required

**Status:** Implemented

Identification numbers are validated according to the selected identification type.

## CUS-004 — Physical person identification format

**Status:** Implemented

A physical person identification number must contain exactly 9 digits and must not begin with `0`.

## CUS-005 — Legal entity identification format

**Status:** Implemented

A legal entity identification number must contain exactly 10 digits.

## CUS-006 — DIMEX identification format

**Status:** Implemented

A DIMEX identification number must contain 11 or 12 digits and must not begin with `0`.

## CUS-007 — Customer identification must be unique

**Status:** Implemented

Two customer records cannot share the same identification number.

Uniqueness is protected by the database and translated into an appropriate application/API conflict.

## CUS-008 — Customer email is required

**Status:** Implemented

A valid customer email address is required.

## CUS-009 — Customer phone number is required

**Status:** Implemented

A customer phone number is required within the supported length constraints.

## CUS-010 — Customer structured location is required

**Status:** Implemented

Costa Rican customers require structured province, canton, and district information.

## CUS-011 — Additional address information is required

**Status:** Implemented

Customers require additional address information (`OtherSigns`) within the supported length constraints.

## CUS-012 — Customers have an active state

**Status:** Implemented

Customer availability is controlled through `IsActive`.

## CUS-013 — Customer deletion currently behaves as deactivation

**Status:** Implemented

The current delete operation deactivates the customer rather than physically removing the persisted record.

## CUS-014 — Customer creation timestamp

**Status:** Implemented

Customer records maintain a server-controlled UTC creation timestamp.

## CUS-015 — Customer modification timestamp

**Status:** Implemented

Customer records maintain a server-controlled UTC modification timestamp.

## CUS-016 — Customer lists are paginated

**Status:** Implemented

Customer list operations use bounded pagination.

## CUS-017 — Customer page parameters are bounded

**Status:** Implemented

Invalid or unreasonable pagination parameters are rejected or normalized according to the API contract.

## CUS-018 — Customers can be filtered by identification type

**Status:** Implemented

Customer listings support the implemented identification-type filters.

## CUS-019 — Customer search currently uses the customer name

**Status:** Implemented

Customer search currently filters by customer name.

## CUS-020 — Customer listing has deterministic ordering

**Status:** Implemented

Customer listings use deterministic ordering so pagination remains stable.

---

# 3. Data Integrity

## DAT-001 — Frontend validation is not authoritative

**Status:** Implemented

Business-critical validation remains enforced by the server.

## DAT-002 — Database constraints protect critical invariants

**Status:** Implemented

Important persisted invariants are protected by EF Core configuration and SQL Server constraints/indexes where appropriate.

## DAT-003 — Persistence entities are not the public HTTP contract

**Status:** Implemented

Shared request/response models define the public client/API contract.

---

# 4. Authentication and Access

## AUTH-001 — Application access is protected by default

**Status:** Implemented

Endpoints require authenticated access unless anonymous access is explicitly allowed.

## AUTH-002 — Authorization restricts privileged operations

**Status:** Implemented

Roles and authorization policies restrict operations that require elevated permissions.

## AUTH-003 — Mutable authenticated requests require antiforgery protection

**Status:** Implemented

Applicable authenticated state-changing requests require antiforgery validation.

---

# 5. Quotations

The Quotations domain is **Implemented** end to end.

## QUO-001 — A quotation belongs to an existing active customer

**Status:** Implemented

Every quotation belongs to an existing customer through `CustomerId`.

The customer must be active when the quotation is created or modified.

## QUO-002 — Quotation lines may use registered or manual products

**Status:** Implemented

A quotation line may optionally reference a registered product through `ProductId`.

Manual lines are valid when they satisfy the quotation-line rules.

## QUO-003 — Existing products may assist quotation entry

**Status:** Implemented

Selecting an existing product may populate available product information for the active line editor.

Product-provided values are starting values for the quotation and remain editable according to the quotation workflow.

## QUO-004 — Quotation quantities represent commercial quantities

**Status:** Implemented

Quotation quantities may be decimal.

Quotations do not assume a universal packaging or box-to-area conversion.

## QUO-005 — Quotations do not modify inventory

**Status:** Implemented

Creating, modifying, issuing, or reissuing a quotation does not reserve, deduct, or otherwise modify inventory.

A quotation is a commercial proposal rather than a confirmed stock movement.

## QUO-006 — CABYS is optional in commercial quotations

**Status:** Implemented

A quotation line may contain a CABYS code, but CABYS is not required to save or issue a commercial quotation.

Future fiscal documents may impose different requirements without changing this quotation rule.

## QUO-007 — Quotation prices include IVA when enabled

**Status:** Implemented

The entered unit price represents the commercial final unit price.

When the applicable tax rate is 13%, IVA is extracted from the IVA-inclusive amount rather than added again.

The currently supported quotation tax rates are 0% and 13%.

## QUO-008 — Monetary calculations use decimal arithmetic and defined rounding

**Status:** Implemented

Quotation monetary calculations use decimal arithmetic and the implemented rounding rules.

The server is authoritative for persisted and returned totals.

Client calculations exist for immediate feedback only.

## QUO-009 — Quotation lines support percentage or fixed discounts

**Status:** Implemented

A quotation line may receive a percentage or fixed monetary discount.

A discount must not produce an invalid negative line amount.

## QUO-010 — Quotations support additional charges

**Status:** Implemented

A quotation may contain supported additional-charge types such as transport, installation, service/other applicable values defined by the current contract.

Additional charges contribute to the quotation total.

## QUO-011 — Quotation numbers are generated by the backend

**Status:** Implemented

Each quotation receives a backend-generated consecutive number in the commercial format:

```text
COT-000001
```

The numeric portion is generated through a SQL Server sequence.

## QUO-012 — Saved quotations preserve their identity

**Status:** Implemented

Editing or reissuing a quotation preserves its quotation identifier and `COT` number.

Updates do not create a new commercial quotation identity.

## QUO-013 — Quotations are persisted server-side

**Status:** Implemented

Quotations, quotation lines, and charges are persisted through ASP.NET Core, EF Core, and SQL Server.

Browser-local state is not the authoritative store.

## QUO-014 — Quotation PDF generation is implemented in the backend

**Status:** Implemented

Commercial quotation PDFs are generated by the backend using QuestPDF.

PDF generation is separate from quotation persistence.

An authenticated API endpoint returns the PDF for an existing quotation.

## QUO-015 — Projects are outside the Quotations domain

**Status:** Implemented

Project-management concepts are not introduced into Quotations unless a future Projects domain creates a concrete requirement.

## QUO-016 — Quotations support CRC and USD

**Status:** Implemented

A quotation may use CRC or USD.

Currency conversion and exchange-rate management are outside the current Quotations domain.

## QUO-017 — Quotation totals recalculate in the user interface

**Status:** Implemented

The Blazor interface provides immediate calculation feedback while the server remains authoritative.

## QUO-018 — Quotation workflows remain clearly separated in the UI

**Status:** Implemented

The quotation UI separates line entry, confirmed lines, charges, totals, and document actions.

## QUO-019 — Only one quotation line editor is active at a time

**Status:** Implemented

The quotation UI uses one active line editor for new or edited lines.

Confirmed lines are represented separately in a compact summary.

## QUO-020 — Lines must be confirmed before save or issue

**Status:** Implemented

A line being actively edited must be confirmed before the quotation can be saved or issued.

This prevents partially edited UI state from being silently persisted.

## QUO-021 — Quotations support Draft and Issued states

**Status:** Implemented

The implemented quotation lifecycle contains:

* `Draft`
* `Issued`

No additional quotation lifecycle such as Sent, Approved, Rejected, or Expired should be assumed until explicitly implemented.

## QUO-022 — Issued quotations store a customer snapshot

**Status:** Implemented

When a quotation is issued or reissued, Revestik stores the customer data required by the current quotation document:

* Customer name.
* Identification number.
* Email.
* Phone number.

The snapshot prevents a later customer edit from changing the customer information represented by the issued quotation.

## QUO-023 — Reissuing preserves the quotation identity and COT number

**Status:** Implemented

Reissuing an existing quotation updates its issued representation while preserving the same quotation identifier and commercial `COT` number.

## QUO-024 — PDF download uses the authenticated application flow

**Status:** Implemented

The Blazor client requests the quotation PDF through the authenticated `HttpClient` flow and downloads it through JavaScript Blob handling.

The client does not generate the authoritative quotation document itself.

---

# 6. Product Support for Quotations

## PROD-001 — Products may support quotation entry without defining the full Inventory domain

**Status:** Implemented

A focused product model and lookup service may be used to assist quotation entry.

This support does not imply that stock quantities, inventory movements, or complete Inventory rules are implemented.

## PROD-002 — Product lookup is paginated and authorized

**Status:** Implemented

Product lookup behavior uses the current pagination contract and protected API access.

---

# 7. Planned Business Domains

## Sales

**Status:** Planned / next investigation block

Sales will be a separate business domain from Quotations.

Rules such as sale identity, `VEN-xxxxxx` numbering, inventory movement timing, payment state, cancellation, and quotation conversion must be defined before implementation.

## Inventory

**Status:** Planned

Future rules will cover stock, movements, adjustments, availability, and relationships with sales and purchases.

## Purchases and Suppliers

**Status:** Planned

Future rules will cover supplier data, purchases, costs, purchased items, and stock increases.

## Accounts Receivable

**Status:** Planned

Future rules will cover balances, payments, partial payments, due dates, aging, and collection alerts.

## Expenses

**Status:** Planned

Future rules will cover operating expenses and small card expenses.

AI-assisted extraction from unstructured vouchers is a later enhancement rather than a prerequisite for basic expense recording.

---

# 8. Deferred Integrations

## Electronic Invoicing

**Status:** Deferred

Direct integration with Costa Rica's Ministerio de Hacienda is intentionally postponed.

Fiscal electronic invoices may continue to be generated through the existing external invoicing provider.

Quotations and future internal sales must remain conceptually separate from fiscal electronic documents.

## Automated Email and WhatsApp Distribution

**Status:** Deferred

Automated document distribution is not required for the current MVP because users can download the quotation PDF and distribute it manually.

---

# 9. Rule Change Process

A business-rule change may require updates to:

1. This document.
2. Shared request or response contracts.
3. Server-side validation.
4. Application services.
5. EF Core configuration.
6. Database migrations.
7. Automated tests.
8. Client-side validation or UI behavior.
9. API documentation.

Not every rule requires changes at every layer.

---

# 10. Source of Truth

This document describes expected business behavior.

The executable application and database determine the behavior actually enforced by the current codebase.

If documentation and implementation disagree, the discrepancy must be investigated rather than assuming either side is automatically correct.