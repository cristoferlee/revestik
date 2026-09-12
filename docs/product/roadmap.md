# Product Roadmap

## 1. Purpose

This document describes the current implementation status and planned evolution of Revestik.

The roadmap distinguishes between:

* Implemented capabilities.
* Capabilities currently being developed or hardened.
* Planned product functionality.
* Longer-term possibilities.

A feature appearing in this document does not imply that it is already available.

## 2. Roadmap Principles

Revestik development follows several principles:

1. Stabilize foundations before rapidly expanding functionality.
2. Protect important business rules on the server.
3. Add automated tests alongside critical behavior.
4. Keep documentation synchronized with meaningful architectural changes.
5. Avoid unnecessary architectural complexity.
6. Prioritize workflows that solve real operational needs.
7. Treat security and data integrity as product requirements rather than final-stage additions.

## 3. Current Foundation

The current application foundation includes:

### Application Architecture

* ASP.NET Core backend.
* Blazor WebAssembly frontend.
* Shared request and response contracts.
* Entity Framework Core.
* SQL Server.
* Hosted same-origin production architecture.

### Authentication and Security

* ASP.NET Core Identity.
* Cookie-based application authentication.
* Google external authentication.
* Server-side authorization.
* Role-based access foundations.
* Antiforgery protection for applicable mutable authenticated requests.
* Server-side secret boundary.
* Security-focused automated tests.

### Customer Domain

The customer domain currently provides the strongest implemented business foundation.

Current capabilities include:

* Customer creation.
* Customer editing.
* Customer retrieval.
* Customer listing.
* Search.
* Filtering.
* Ordering.
* Pagination.
* Customer deactivation/removal behavior according to current business rules.
* Identification validation.
* Identification uniqueness enforcement.
* Physical person identification support.
* Legal entity identification support.
* DIMEX identification support.
* Geographic/address information.
* Email and phone information.
* Server-side validation.
* Database integrity constraints.
* Automated tests.

### Engineering Foundation

The project currently includes:

* Automated test project.
* Unit/focused behavior tests.
* Integration tests.
* Hosting tests.
* CI verification.
* Release build verification.
* Publish verification.
* Static asset verification.
* Health endpoint.
* Version-controlled EF Core migrations.
* Architecture documentation.
* Security documentation.
* Business-rule documentation.
* Architecture Decision Records.
* Local development documentation.
* Testing documentation.
* Deployment documentation.

## 4. Current Development Phase

Revestik is currently in a **foundation and domain-hardening phase**.

The priority is not to maximize the number of visible modules.

The priority is to ensure that new modules are built on a stable foundation containing:

```text
Architecture
     +
Authentication
     +
Authorization
     +
Security
     +
Persistence
     +
Testing
     +
Documentation
     =
Reliable foundation
```

The customer domain serves as the initial reference implementation for how future business domains should be structured.

## 5. Near-Term Priorities

The next development stage should focus on completing and validating the engineering foundation before expanding into larger transactional modules.

### Documentation Completion

Complete the first documentation baseline:

* Product overview.
* Business rules.
* Architecture overview.
* Security overview.
* Architecture Decision Records.
* Local development guide.
* Testing strategy.
* Deployment guide.
* Product roadmap.
* Repository README.

### Customer Domain Review

Before treating Customers as a reference domain, review:

* Endpoint behavior.
* Validation consistency.
* Authorization requirements.
* Persistence constraints.
* Error responses.
* Pagination.
* Search.
* Ordering.
* UI behavior.
* Test coverage.

### Production Infrastructure Decision

When Revestik approaches its first real deployment, select and document:

* Hosting platform.
* Production database.
* Secret management.
* Observability.
* Backup strategy.
* Deployment workflow.
* Migration workflow.
* Rollback expectations.

The hosting decision should be recorded through an ADR rather than assumed.

## 6. Planned Business Domains

The following domains are part of the intended Revestik product direction.

Their exact implementation order may change according to business value and technical dependencies.

### Suppliers

Planned capabilities include:

* Supplier registration.
* Supplier contact information.
* Supplier identification.
* Supplier status.
* Supplier search and filtering.
* Relationships with purchases and products.

### Inventory

Planned capabilities include:

* Product/material catalog.
* Stock quantities.
* Inventory movements.
* Stock adjustments.
* Purchase-related stock increases.
* Sale/invoice-related stock decreases.
* Search and filtering.
* Inventory status visibility.

Inventory rules must be defined before implementation because incorrect stock transitions can corrupt operational data.

### Quotations

Planned capabilities include:

* Quotation creation.
* Customer association.
* Line items.
* Quantities.
* Prices.
* Totals.
* Consecutive numbering.
* Quotation status.
* PDF generation.
* Conversion into later commercial workflows where applicable.

Financial calculations should have dedicated automated tests.

### Purchases

Planned capabilities include:

* Supplier association.
* Purchase registration.
* Purchased items.
* Costs.
* Purchase totals.
* Inventory integration.
* Purchase history.

### Accounts Receivable

Planned capabilities include:

* Outstanding balances.
* Payment registration.
* Partial payments.
* Paid/unpaid status.
* Due dates.
* Aging visibility.
* Payment history.
* Alerts for relevant collection periods.

Financial state transitions must be explicitly documented and tested.

### Invoicing

Planned capabilities include:

* Invoice creation.
* Customer association.
* Invoice lines.
* Tax calculations.
* Totals.
* Consecutive numbering.
* Payment status.
* Integration with accounts receivable.
* Inventory impact where applicable.

Electronic invoicing introduces additional regulatory and integration requirements and should not be treated as ordinary CRUD functionality.

### CABYS

Planned capabilities include support for Costa Rican CABYS classification where required by invoicing and product workflows.

Implementation should consider:

* Data source.
* Update strategy.
* Search.
* Product association.
* Regulatory requirements.

### Dashboard and Analytics

Planned dashboard capabilities include operational indicators such as:

* Sales information.
* Receivables.
* Inventory indicators.
* Customer information.
* Quotation activity.
* Business summaries.

Dashboard queries should be designed according to actual reporting requirements rather than loading transactional datasets into the browser.

### Application Configuration

Planned administrative configuration may include:

* Company information.
* Commercial settings.
* Invoicing configuration.
* User administration.
* Role configuration.
* Application preferences.

Sensitive configuration must remain server-side where appropriate.

## 7. Electronic Invoicing

Electronic invoicing is a significant future domain for Revestik.

It requires separate analysis because implementation depends on Costa Rican regulatory requirements and external government services.

Before implementation, Revestik should define:

* Current Ministerio de Hacienda technical requirements.
* Authentication requirements.
* Electronic document format.
* Signing requirements.
* Consecutive numbering rules.
* Tax rules.
* CABYS requirements.
* Submission workflow.
* Response processing.
* Rejection handling.
* Retry behavior.
* Contingency behavior.
* Storage requirements.
* Audit requirements.

External-service availability must not be assumed.

The integration should be designed so temporary government-service failures do not corrupt internal financial state.

## 8. Authorization Expansion

As new business modules are implemented, authorization should evolve from foundational role support into a documented permission model.

Potential business capabilities include:

```text
View customers
Manage customers
View inventory
Manage inventory
Create quotations
Approve quotations
Register payments
Manage invoices
Manage users
Manage configuration
```

Roles and permissions should reflect real organizational responsibilities rather than being created arbitrarily.

## 9. Auditability

Financial and administrative operations may eventually require stronger audit capabilities.

Potential audit information includes:

* Who performed an action.
* What entity changed.
* When it changed.
* Relevant previous state.
* Relevant resulting state.

Audit requirements should be determined according to business and regulatory needs.

A generic audit framework should not be introduced before those requirements are understood.

## 10. Data Integrity Expansion

Future domains should continue the layered integrity model already established in Customers:

```text
Client guidance
      ↓
Server validation
      ↓
Business rules
      ↓
Persistence configuration
      ↓
Database constraints
```

Important invariants should not depend exclusively on the user interface.

## 11. Testing Expansion

Each new critical domain should introduce tests alongside implementation.

Priority testing areas include:

* Financial calculations.
* Inventory movements.
* Payment state transitions.
* Quotation totals.
* Invoice totals.
* Identification and tax rules.
* Authorization.
* External integration boundaries.
* Database constraints.

Critical workflows may later receive browser-based end-to-end tests.

## 12. Performance

Performance work should be driven by measurements.

Potential future areas include:

* Dashboard queries.
* Large customer datasets.
* Inventory searches.
* Invoice history.
* Reporting.
* Concurrent users.
* External service latency.

Possible optimization techniques may include:

* Query projection.
* Indexing.
* Pagination.
* Caching.
* Background processing.

These should be introduced when a measured requirement justifies them.

## 13. Background Processing

Some future workflows may benefit from background processing.

Potential examples include:

* Electronic document submission.
* Retry processing.
* Notification delivery.
* Report generation.
* Scheduled reminders.

Background infrastructure should be introduced when asynchronous work becomes a concrete requirement.

## 14. Notifications

Future accounts-receivable workflows may require notifications or alerts.

Potential scenarios include:

* Upcoming due dates.
* Overdue balances.
* Failed external submissions.
* Important inventory conditions.

The notification channel and scheduling mechanism have not yet been selected.

## 15. Observability

Before production use becomes business-critical, Revestik should establish production observability.

Potential capabilities include:

* Centralized logs.
* Exception monitoring.
* Request latency.
* Health monitoring.
* Database connectivity monitoring.
* Deployment visibility.

The specific technology should be selected together with the production infrastructure.

## 16. Backup and Recovery

Before Revestik becomes the authoritative source for important operational data, the production database must have documented backup and recovery procedures.

Recovery capability is part of product reliability.

It should not be postponed until after a data-loss event.

## 17. Deployment Automation

Current CI verifies build, tests, publish, and important hosted assets.

Future production work may add:

* Versioned deployment artifacts.
* Controlled production deployment.
* Environment configuration.
* Database migration stage.
* Smoke tests.
* Rollback mechanism.

Continuous deployment should follow production infrastructure design rather than precede it.

## 18. Potential Future Architecture Changes

The current Client/API/Shared architecture remains appropriate for the present application.

Future complexity may justify changes such as:

* Additional application/domain layers.
* Background worker processes.
* Separate reporting infrastructure.
* External integration abstractions.
* Additional data stores.
* Independently deployable components.

These changes should occur only when a concrete requirement justifies them.

Microservices are not a roadmap objective by themselves.

## 19. Explicitly Not Current Goals

The following are not current objectives unless future requirements change:

* Microservices for every business domain.
* Kubernetes solely for architectural complexity.
* Multiple databases without workload justification.
* Replacing EF Core without measured need.
* Replacing cookie authentication solely to introduce JWT.
* Creating abstraction layers only to imitate a reference architecture.
* Introducing technologies primarily to increase the number of technologies listed in the repository.

Revestik should favor understandable, maintainable solutions that solve real product requirements.

## 20. Definition of Done for New Domains

A substantial business domain should not be considered complete solely because its user interface works.

Depending on the feature, completion should consider:

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

Not every small change requires every item, but critical business functionality should be evaluated against this standard.

## 21. Roadmap Status Model

Future roadmap updates should use clear status terminology.

### Implemented

The capability exists in the application and is supported by the current codebase.

### In Progress

Implementation has started but should not yet be considered complete.

### Planned

The capability is intentionally expected but implementation has not started or is not sufficiently complete.

### Under Evaluation

The capability or technology is being considered but has not been selected.

### Deferred

The capability is intentionally postponed.

This terminology prevents planned functionality from being mistaken for current functionality.

## 22. Roadmap Maintenance

This roadmap should be updated when:

* A significant business domain is completed.
* Priorities materially change.
* A planned capability is removed.
* A major architecture decision changes product direction.
* Production infrastructure is selected.
* Regulatory requirements materially change implementation plans.

The roadmap should not be updated for every small commit.

## 23. Current Product Direction

The current direction can be summarized as:

```text
Engineering foundation
        ↓
Customer domain hardening
        ↓
Core operational domains
        ↓
Financial workflows
        ↓
Electronic invoicing integration
        ↓
Production hardening
        ↓
Operational expansion
```

The exact sequence may evolve.

The guiding objective is to build Revestik as a reliable business application rather than a collection of disconnected features.

## 24. Roadmap Principle

The roadmap principle for Revestik is:

> Build the smallest architecture that safely supports the next real business requirement, then evolve it when evidence justifies the change.

Revestik should grow through deliberate product and engineering decisions rather than through unnecessary technical complexity.
