# ADR-003: Use Entity Framework Core with SQL Server

**Status:** Accepted
**Date:** 2026-09-12

## Context

Revestik manages structured business data including customers, authentication information, locations, and future transactional domains such as quotations, inventory, accounts receivable, suppliers, and invoicing.

The persistence solution must support:

* Relational data.
* Referential integrity.
* Transactions.
* Unique constraints.
* Schema evolution.
* Querying and filtering.
* Integration with ASP.NET Core Identity.
* Reliable enforcement of important data invariants.

The application also requires a persistence approach that integrates naturally with the .NET ecosystem.

## Decision

Revestik will use:

* **Entity Framework Core** as the primary application data-access technology.
* **Microsoft SQL Server** as the relational database.

Database schema evolution will be managed through EF Core migrations.

Entity-specific persistence rules should be configured through EF Core configuration classes where practical rather than placing all configuration directly inside the `DbContext`.

The general persistence flow is:

```mermaid
flowchart LR
    Endpoint[API Endpoint]
    Service[Application Service]
    EF[Entity Framework Core]
    DB[(SQL Server)]

    Endpoint --> Service
    Service --> EF
    EF --> DB
```

## Rationale

Revestik's data is primarily relational.

Business domains such as customers, quotations, products, payments, suppliers, and inventory naturally require relationships, constraints, filtering, sorting, and transactional consistency.

SQL Server provides the relational capabilities required by these workflows.

Entity Framework Core provides a strongly integrated .NET persistence layer with support for:

* LINQ queries.
* Change tracking.
* Migrations.
* Relationships.
* Constraints and indexes.
* Async database operations.
* ASP.NET Core Identity integration.
* Parameterized database access.

This combination provides sufficient capability for the current application without requiring a custom persistence framework.

## Data Integrity Strategy

Application validation alone is not considered sufficient to protect important persisted data.

Where practical, critical invariants should also be represented in the persistence model.

The intended protection model is:

```text
Client validation
        ↓
Server request validation
        ↓
Application/business rules
        ↓
EF Core model configuration
        ↓
SQL Server constraints and indexes
```

Each layer has a different responsibility.

The database remains the final authority over persisted relational integrity.

Examples already used by the customer domain include:

* Required fields.
* Maximum lengths.
* Supported identification types.
* Unique identification numbers.

## Entity Framework Core Usage

Application code should prefer normal EF Core query APIs and LINQ for database operations.

Read-only queries should use `AsNoTracking()` when change tracking is unnecessary and doing so improves clarity or efficiency.

Asynchronous database APIs should be preferred for request-processing paths.

Persistence entities should remain internal server-side implementation details and should not become the public HTTP contract.

## SQL Parameterization

Normal EF Core LINQ queries generate parameterized database commands.

Application code must not construct SQL statements by concatenating untrusted user input.

If raw SQL becomes necessary in the future, values must be parameterized and the implementation should receive additional review.

Using EF Core does not make arbitrary raw SQL automatically safe.

## Migrations

Database schema changes are managed using EF Core migrations.

A migration should represent an intentional schema change associated with an application requirement.

Migration files should be version controlled.

Changes involving existing production data must consider:

* Existing rows.
* New required fields.
* Default values.
* Constraint compatibility.
* Index creation.
* Data transformation.
* Rollback/recovery strategy where appropriate.

A migration compiling successfully does not by itself guarantee that it is safe for production data.

## ASP.NET Core Identity

The Revestik database context also supports ASP.NET Core Identity persistence.

Using the same relational database infrastructure simplifies the current application architecture and allows Identity to use its established EF Core persistence model.

Identity implementation details must remain server-side.

## Alternatives Considered

### Direct ADO.NET

Direct ADO.NET would provide lower-level control over database access.

It was not selected as the primary persistence mechanism because Revestik benefits from EF Core's:

* Object mapping.
* LINQ support.
* Migrations.
* Change tracking.
* Identity integration.
* Model configuration.

ADO.NET may still be appropriate for a specific future requirement if profiling demonstrates a concrete need.

### Dapper

Dapper could provide lightweight object mapping with explicit SQL.

It was not selected as the primary persistence technology because the current application benefits more from EF Core's complete relational model and migration support than from reducing ORM abstraction.

Dapper could be introduced selectively in the future if a measured performance problem justifies it.

### SQLite

SQLite offers a simple deployment model and is useful for smaller or embedded applications.

It was not selected as the primary Revestik database because Revestik is intended to support a multi-user business application and already targets SQL Server infrastructure.

### NoSQL Database

A document-oriented or other NoSQL database was not selected as the primary persistence model.

The current business data has strong relational characteristics and benefits from relational constraints and transactional behavior.

Introducing a NoSQL database without a domain-specific requirement would add complexity without a demonstrated advantage.

## Consequences

### Positive

* Strong integration with .NET.
* Relational integrity.
* Database migrations are version controlled.
* Mature tooling.
* LINQ-based querying.
* ASP.NET Core Identity integration.
* Support for indexes and constraints.
* Parameterized queries through normal EF Core usage.
* Suitable foundation for future transactional business modules.

### Negative

* EF Core introduces ORM behavior that developers must understand.
* Inefficient LINQ can still generate inefficient SQL.
* Change tracking can introduce unnecessary overhead when used incorrectly.
* Database migrations require care once production data exists.
* The application remains dependent on SQL Server-specific infrastructure and behavior in some areas.

These trade-offs are currently acceptable.

## Performance Considerations

EF Core should not be assumed to make database access automatically efficient.

Database-related performance should be evaluated using evidence.

Potential areas include:

* Generated SQL.
* Query count.
* Index usage.
* Pagination.
* Projection.
* Change tracking.
* Large result sets.
* Relationship loading.
* Database execution plans.

Optimization should be driven by measured problems rather than replacing EF Core preemptively.

## Future Review Triggers

This decision should be reviewed if:

* Measured query performance cannot reasonably be addressed with EF Core.
* A business domain requires a substantially different persistence model.
* Reporting workloads require specialized storage.
* The application introduces large analytical datasets.
* SQL Server operational requirements become inappropriate for the deployment environment.
* Independent services require separate data ownership.

A future architecture may use more than one persistence technology.

That should happen because a specific workload requires it, not because multiple database technologies are available.

## Decision Principle

Revestik uses EF Core and SQL Server because they fit the application's current relational business requirements and .NET architecture.

Persistence technology should evolve only when application requirements or measured operational evidence justify the change.
