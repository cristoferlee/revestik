# Business Rules

## 1. Purpose

This document defines the business rules currently enforced by Revestik.

Business rules describe required system behavior independently from the user interface or a specific implementation detail.

Each rule has a stable identifier that can be referenced from source code, automated tests, pull requests, issues, and future documentation.

This document distinguishes implemented rules from planned behavior. A rule must not be marked as implemented unless the current application enforces it.

---

## 2. Rule Status

Rules may use the following statuses:

* **Implemented** — currently enforced by the application.
* **In Progress** — implementation has started but the complete behavior is not yet available.
* **Planned** — intended behavior that has not yet been implemented.

---

# 3. Customer Management

## CUS-001 — Customer name is required

**Status:** Implemented

Every customer must have a name.

The name must not exceed 150 characters.

---

## CUS-002 — Customer identification type is required

**Status:** Implemented

Every customer must have one supported identification type.

Currently supported values are:

* `PhysicalPerson`
* `LegalEntity`
* `Dimex`

The database also restricts persisted identification types to the supported values.

---

## CUS-003 — Customer identification number is required

**Status:** Implemented

Every customer must have an identification number.

The identification number:

* Must contain only digits.
* Must not contain spaces.
* Must not contain hyphens.
* Must not exceed 12 digits.

Additional format requirements depend on the identification type.

---

## CUS-004 — Physical person identification format

**Status:** Implemented

A physical person identification number must:

* Contain exactly 9 digits.
* Not begin with `0`.

---

## CUS-005 — Legal entity identification format

**Status:** Implemented

A legal entity identification number must contain exactly 10 digits.

---

## CUS-006 — DIMEX identification format

**Status:** Implemented

A DIMEX identification number must:

* Contain 11 or 12 digits.
* Not begin with `0`.

---

## CUS-007 — Customer identification must be unique

**Status:** Implemented

Two customer records cannot share the same identification number.

Uniqueness is enforced at the database level through a unique index.

The application must translate a database uniqueness violation into an appropriate business/API conflict instead of exposing the underlying database exception directly.

---

## CUS-008 — Customer email is required

**Status:** Implemented

Every customer must have an email address.

The email:

* Must have a valid email format.
* Must not exceed 254 characters.

---

## CUS-009 — Customer phone number is required

**Status:** Implemented

Every customer must have a phone number.

The phone number must contain between 8 and 20 characters.

---

## CUS-010 — Customer structured location is required

**Status:** Implemented

Every customer must contain structured Costa Rican location information consisting of:

* Province.
* Canton.
* District.

Province codes must correspond to the supported one-digit province code format.

Canton and district codes must each contain exactly two digits.

---

## CUS-011 — Additional address information is required

**Status:** Implemented

Every customer must provide additional address information (`OtherSigns`).

The value must contain between 5 and 160 characters.

---

## CUS-012 — Customers have an active state

**Status:** Implemented

Every customer has an `IsActive` state.

New customers are active by default unless explicitly specified otherwise by an authorized operation.

The active state allows customer availability to be controlled without requiring physical deletion of the database record.

---

## CUS-013 — Customer deletion is currently a deactivation

**Status:** Implemented

The current customer deletion operation performs a soft-delete style operation.

The customer record remains persisted and is marked inactive instead of being physically removed from the database.

As a consequence, historical customer information remains available in persistence and the identification number remains associated with that record.

---

## CUS-014 — Customer creation timestamp

**Status:** Implemented

Customer records maintain a UTC creation timestamp.

Creation timestamps represent when the record was created by the application and should not be treated as client-controlled business data.

---

## CUS-015 — Customer modification timestamp

**Status:** Implemented

Customer records maintain a UTC modification timestamp.

The modification timestamp is updated when persisted customer information changes.

---

## CUS-016 — Customer lists are paginated

**Status:** Implemented

Customer list operations use pagination rather than requiring the complete customer dataset to be returned in every request.

Pagination contracts must provide sufficient information for the client to navigate the result set.

---

## CUS-017 — Customer page parameters are bounded

**Status:** Implemented

Customer pagination parameters must be validated and normalized so invalid or unreasonable page requests do not result in uncontrolled database queries.

Exact technical bounds belong to the API contract and implementation and may evolve without changing the underlying business requirement for bounded pagination.

---

## CUS-018 — Customers can be filtered by identification type

**Status:** Implemented

Customer listings may be filtered according to the supported identification types:

* Physical person.
* Legal entity.
* DIMEX.

---

## CUS-019 — Customer search currently uses the customer name

**Status:** Implemented

The currently implemented customer search filters customer records by name.

Searching by identification number should not be described as implemented until that behavior exists in the application.

---

## CUS-020 — Customer listing has deterministic ordering

**Status:** Implemented

Customer listings apply deterministic ordering.

The current implementation includes special ordering behavior for applicable legal-entity records followed by alphabetical ordering.

Any future change to ordering behavior should preserve deterministic results for pagination.

---

# 4. Customer Data Integrity

## DAT-001 — Frontend validation is not authoritative

**Status:** Implemented

Client-side validation exists to improve user experience and reduce invalid requests.

It must not be treated as the authoritative enforcement mechanism for business-critical data.

Requests reaching the server must still satisfy server-side rules.

---

## DAT-002 — Database constraints protect critical invariants

**Status:** Implemented

Business-critical invariants should be protected at the persistence level when practical.

Current customer persistence protections include:

* Required columns.
* Maximum lengths.
* Supported identification types.
* Unique customer identification.

This prevents invalid data from being accepted solely because one application validation path was bypassed.

---

## DAT-003 — Persistence entities are not the public HTTP contract

**Status:** Implemented

Database entities should not be exposed directly as the client/server API contract.

Shared request and response models define communication between the Blazor client and ASP.NET Core API.

This allows persistence implementation and external contracts to evolve independently.

---

# 5. Authentication and Access

## AUTH-001 — Application access is protected by default

**Status:** Implemented

Server endpoints require authenticated access unless anonymous access is explicitly allowed.

Public access is therefore an explicit decision rather than the default behavior.

---

## AUTH-002 — Authorization can restrict privileged operations

**Status:** Implemented

Operations requiring elevated permissions may be restricted using roles and authorization policies.

Authentication establishes who the user is.

Authorization determines whether that authenticated user may perform a particular operation.

---

## AUTH-003 — Mutable authenticated requests require antiforgery protection

**Status:** Implemented

Authenticated operations that modify server state must pass antiforgery validation where applicable.

An invalid or missing required antiforgery token must prevent the protected operation from executing.

---

# 6. Planned Business Domains

The following domains are part of the intended Revestik product direction but their business rules are not yet considered implemented.

They will receive their own rule sections when development begins.

## Quotations

### QUO-001 — A quotation belongs to an existing customer

**Status:** Planned

Every quotation must belong to an existing customer through `CustomerId`.

Customer identification remains unique and is used by the user interface to locate the customer. When an identification is entered, the existing customer information should be loaded.

A quotation cannot be completed when required customer information is missing.

Hacienda lookup is part of the intended customer-identification workflow. Its integration details are not yet defined.

---

### QUO-002 — Quotation lines may use registered or manual products

**Status:** Planned

A quotation may contain products already registered in Revestik or products entered manually for that quotation.

A product does not need to exist in Inventory to be quoted.

---

### QUO-003 — Existing products may provide initial quotation values

**Status:** Planned

Selecting an existing product may populate available information such as its name or description, CABYS code, and price.

An automatically populated price is only a starting value. The user may freely change the quotation price without additional role-based approval.

---

### QUO-004 — Quotation quantities represent commercial quantities

**Status:** Planned

Quotation quantities may be decimal and represent the commercial quantity, commonly square meters.

Quotations must not apply a universal box-to-square-meter conversion.

---

### QUO-005 — Quoting does not modify inventory

**Status:** Planned

Quoting a product must not reserve, deduct, or otherwise modify inventory.

---

### QUO-006 — CABYS is required for quotation lines

**Status:** Planned

Every quotation line must have a CABYS code.

---

### QUO-007 — Quotation prices include IVA when enabled

**Status:** Planned

When 13% IVA is enabled, the unit price entered by the user represents the public or final unit price with IVA included.

The system must separate the IVA-inclusive amount into its taxable base and IVA instead of adding 13% to the entered price.

Conceptually:

```text
base = IVA-inclusive amount / 1.13
IVA = IVA-inclusive amount - base
```

When IVA is removed for an exempt transaction, the IVA portion is removed and the taxable or base amount becomes the resulting total.

---

### QUO-008 — Monetary calculations use decimal arithmetic

**Status:** Planned

All quotation monetary calculations must use decimal arithmetic.

The exact monetary rounding precision and rules are not yet defined.

---

### QUO-009 — Quotation lines support percentage or fixed discounts

**Status:** Planned

A quotation line may receive either a percentage discount or a fixed monetary discount.

The user chooses the discount type according to the commercial negotiation.

A discount must not produce a negative line amount.

No maximum discount percentage or additional approval rule is currently defined.

---

### QUO-010 — Quotations support additional charges

**Status:** Planned

A quotation may contain additional charges with one of the following types:

* `Service`
* `Transport`
* `Installation`
* `Other`

Each additional charge must have a description or detail and a positive monetary amount.

Additional charges are added to the quotation total.

The tax treatment of additional charges is not yet defined.

---

### QUO-011 — Quotation numbers are generated by the backend

**Status:** Planned

Every quotation must have a unique consecutive number generated by the backend.

The legacy browser-local `COT-0001` counter does not define the persistence rule for the current application.

The exact quotation consecutive format is not yet defined.

---

### QUO-012 — Saved quotations retain their identity when modified

**Status:** Planned

A saved quotation can be reopened and modified while retaining the same quotation identity and consecutive number.

No Draft, Sent, Approved, or Expired lifecycle is currently defined and such a lifecycle must not be introduced yet.

---

### QUO-013 — Quotations are persisted server-side

**Status:** Planned

Quotations must be persisted server-side.

Browser-local storage is not an authoritative quotation store.

---

### QUO-014 — PDF generation is separate from persistence

**Status:** Planned

Quotations should support PDF generation.

Generating a PDF and persisting a quotation are separate operations.

---

### QUO-015 — Projects are outside the Quotations domain

**Status:** Planned

Projects are a future separate feature and must not be introduced into Quotations.

---

### QUO-016 — Quotations support CRC and USD

**Status:** Planned

Quotations must support CRC and USD currencies.

---

### QUO-017 — Quotation totals recalculate immediately in the user interface

**Status:** Planned

The user interface should immediately recalculate line totals and quotation totals when relevant values change.

---

### QUO-018 — Quotation workflows remain clearly separated

**Status:** Planned

The user interface should preserve clear separation between quotation lines, additional charges, totals, and document actions.

---

### Unresolved Quotations Decisions

The following rules require a future business decision:

* Exact quotation consecutive format.
* Monetary rounding precision and rules.
* Tax treatment of additional charges.
* Hacienda integration details.

## Inventory

Reserved prefix:

```text
INV-###
```

Future rules may cover:

* Products.
* Available quantities.
* Inventory movements.
* Stock adjustments.
* Product availability.

## Accounts Receivable

Reserved prefix:

```text
AR-###
```

Future rules may cover:

* Customer balances.
* Payments.
* Partial payments.
* Outstanding amounts.
* Payment status.
* Due dates.
* Alerts.

## Suppliers

Reserved prefix:

```text
SUP-###
```

Future rules may cover supplier identity, contact information, purchasing relationships, and supplier status.

## Electronic Invoicing

Reserved prefix:

```text
EINV-###
```

Electronic invoicing rules must not be defined from assumptions.

Before implementation, requirements must be verified against the applicable Costa Rican Ministerio de Hacienda specifications and legal requirements.

---

# 7. Rule Change Process

A business rule should be reviewed whenever application behavior changes.

A change may require updates to:

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

The affected layers depend on the rule being changed.

---

# 8. Traceability

Where practical, automated tests should make the business behavior they protect easy to identify.

For example:

```text
CUS-004
Physical person identification must contain exactly 9 digits
        ↓
CustomerUpsertRequestValidationTests
```

The purpose of traceability is not to duplicate documentation inside every test name.

Its purpose is to make it possible to answer:

> Which automated verification protects this business rule?

As Revestik grows, rule identifiers may be referenced from test documentation, pull requests, or development issues when doing so improves clarity.

---

# 9. Source of Truth

This document describes expected business behavior.

The executable application and database ultimately determine the behavior currently enforced in production.

If this document and the implementation disagree, the discrepancy must be investigated rather than assuming either side is automatically correct.

The resolution may require either:

* Correcting the implementation because it violates the intended business rule, or
* Updating this document because the documented rule is obsolete.

Business rules should therefore evolve together with the codebase.
