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

The Quotations domain is currently **In Progress**.

Its server-side business rules, persistence, monetary calculations, authorization, and API operations are implemented. Rules that specifically depend on the Blazor user interface or document generation remain planned until those workflows are implemented and verified.

### QUO-001 — A quotation belongs to an existing active customer

**Status:** Implemented

Every quotation must belong to an existing customer through `CustomerId`.

The selected customer must be active when the quotation is created or modified.

The server validates this requirement independently from the user interface.

Customer lookup and selection behavior in the Blazor interface remains part of the quotation UI workflow.

---

### QUO-002 — Quotation lines may use registered or manual products

**Status:** Implemented

A quotation line may optionally reference a registered product through `ProductId`.

A product does not need to exist in Inventory to be quoted.

Manual quotation lines are therefore valid as long as they satisfy the required quotation-line rules.

---

### QUO-003 — Existing products may provide initial quotation values

**Status:** In Progress

When product and quotation UI integration is implemented, selecting an existing product may populate available information such as its name or description, CABYS code, and price.

An automatically populated price is only a starting value.

The quotation price remains commercial quotation data and may be changed according to the quotation workflow.

The current backend supports optional product association but does not itself implement the product-selection user experience.

---

### QUO-004 — Quotation quantities represent commercial quantities

**Status:** Implemented

Quotation quantities may be decimal and represent the commercial quantity being quoted.

Quotations do not apply a universal box-to-square-meter conversion.

Any future unit or packaging conversion must be defined by the relevant product or inventory requirements rather than assumed by the Quotations domain.

---

### QUO-005 — Quoting does not modify inventory

**Status:** Implemented

Creating or modifying a quotation must not reserve, deduct, or otherwise modify inventory.

A quotation represents a commercial proposal rather than a confirmed inventory movement.

Future sales or invoicing workflows may affect inventory when their own business rules determine that a real inventory movement has occurred.

---

### QUO-006 — CABYS is required for quotation lines

**Status:** Implemented

Every quotation line must have a CABYS code.

CABYS is required even when the quotation line represents a manually entered product rather than a registered Inventory product.

---

### QUO-007 — Quotation prices include IVA when enabled

**Status:** Implemented

The unit price entered for a quotation line represents the public or final unit price.

When the applicable tax rate is 13%, that unit price already includes IVA.

The system separates the IVA-inclusive amount into its taxable base and IVA instead of adding another 13% to the entered price.

Conceptually:

```text
base = IVA-inclusive amount / 1.13
IVA = IVA-inclusive amount - base
```

When the applicable tax rate is 0%, the IVA portion is removed and the taxable or base amount becomes the resulting amount.

The currently supported quotation tax rates are 0% and 13%.

---

### QUO-008 — Monetary calculations use decimal arithmetic and defined rounding

**Status:** Implemented

Quotation monetary calculations use decimal arithmetic.

Calculated monetary values are rounded to two decimal places using midpoint rounding away from zero.

The server is authoritative for persisted and returned quotation calculations.

A future client implementation may reproduce calculations for immediate user feedback but must not replace server-side calculation authority.

---

### QUO-009 — Quotation lines support percentage or fixed discounts

**Status:** Implemented

A quotation line may receive either:

* A percentage discount.
* A fixed monetary discount.

The selected discount is applied as part of the quotation-line monetary calculation.

A discount must not produce a negative line amount.

The request contract validates the applicable discount rules before the quotation is persisted.

No additional role-based discount approval workflow is currently implemented.

---

### QUO-010 — Quotations support additional charges

**Status:** Implemented

A quotation may contain additional charges with one of the following types:

* `Service`
* `Transport`
* `Installation`
* `Other`

Each additional charge must contain the information required by the quotation request contract and a valid monetary amount.

Additional charges contribute to the quotation total.

The current quotation calculation adds charge amounts to the quotation total.

The final tax treatment required for additional charges in future invoicing or electronic invoicing workflows remains a separate business decision.

---

### QUO-011 — Quotation numbers are generated by the backend

**Status:** Implemented

Every quotation receives a consecutive number generated by the backend when the quotation is created.

The current format is:

```text
COT-000001
```

The numeric portion is generated using a SQL Server sequence.

Quotation-number generation is therefore not controlled by browser-local state.

The sequence-based implementation is verified against SQL Server, including concurrent number generation.

The current `COT-` format belongs to the commercial quotation workflow and must not be assumed to represent a future Ministerio de Hacienda fiscal consecutive.

---

### QUO-012 — Saved quotations retain their identity when modified

**Status:** Implemented

A saved quotation can be modified while retaining:

* Its quotation identifier.
* Its quotation consecutive number.
* Its original creation timestamp.

Updating a quotation replaces its current quotation lines and additional charges with the submitted state and records the modification time.

No `Draft`, `Sent`, `Approved`, or `Expired` lifecycle is currently implemented.

Such a lifecycle must not be treated as existing behavior until its requirements are explicitly defined and implemented.

---

### QUO-013 — Quotations are persisted server-side

**Status:** Implemented

Quotations are persisted through the ASP.NET Core backend using Entity Framework Core and SQL Server.

Quotation persistence includes the quotation record, quotation lines, and additional charges.

Browser-local storage is not an authoritative quotation store.

---

### QUO-014 — PDF generation is separate from persistence

**Status:** Planned

Quotations should support generation of a commercial quotation document or PDF.

Generating a document and persisting a quotation are separate operations.

A quotation must not depend on successful PDF generation in order to exist as persisted business data.

PDF generation is not currently considered implemented.

---

### QUO-015 — Projects are outside the Quotations domain

**Status:** Implemented

Projects are outside the current Quotations domain.

The Quotations implementation must not introduce project-management concepts solely because a quotation may eventually be associated with a larger commercial project.

If Projects becomes a concrete product requirement, it should be designed as its own domain and integrated deliberately.

---

### QUO-016 — Quotations support CRC and USD

**Status:** Implemented

Quotations support the following currencies:

* CRC.
* USD.

The selected currency applies to the quotation as a whole.

Currency conversion or exchange-rate management is not currently part of the Quotations domain.

---

### QUO-017 — Quotation totals recalculate immediately in the user interface

**Status:** Planned

The Blazor quotation interface should provide immediate recalculation feedback when values affecting line or quotation totals change.

Client-side calculations exist for user experience only.

The server remains authoritative for final quotation calculations.

---

### QUO-018 — Quotation workflows remain clearly separated

**Status:** Planned

The quotation user interface should preserve clear separation between:

* Quotation lines.
* Additional charges.
* Totals.
* Document actions.

The exact UI organization will be defined during implementation of the Blazor quotation workflow.

---

### Unresolved Quotations Decisions

The following areas still require future implementation or business decisions:

* Blazor quotation workflow and interaction design.
* Product-selection and product-prefill behavior.
* PDF/document layout and generation.
* Final tax treatment of additional charges for later invoicing workflows.
* Broader CABYS catalog integration.
* Future quotation lifecycle requirements, if required.
* Relationship between quotations and later sales/invoice workflows.
* Electronic invoicing integration details.

The current `COT-` consecutive is an internal commercial quotation identifier and does not define future fiscal numbering requirements.


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
