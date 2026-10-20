# ADR-001: Use Client, API, and Shared Projects

**Status:** Accepted
**Date:** 2026-09-11

## Context

Revestik requires a browser-based user interface, server-side business and security enforcement, persistent storage, and explicit contracts between the frontend and backend.

The application is currently developed as a single .NET solution and does not require independently deployable microservices.

A structure was needed that:

* Separates browser code from trusted server-side code.
* Prevents database and server implementation details from being exposed to the client.
* Allows request and response contracts to be shared between the client and API.
* Remains simple enough for the current application size.
* Supports automated testing and future growth.

## Decision

Revestik will use three primary application projects:

```text
Revestik.Client
Revestik.Api
Revestik.Shared
```

### Revestik.Client

Contains the Blazor WebAssembly frontend.

Responsibilities include:

* User interface.
* Navigation.
* Client-side validation.
* HTTP communication.
* Client authentication state.
* Client-side service abstractions.

The client does not access the database directly.

### Revestik.Api

Contains the trusted ASP.NET Core server application.

Responsibilities include:

* HTTP endpoints.
* Authentication.
* Authorization.
* Antiforgery protection.
* Server-side validation.
* Business operations.
* External integrations.
* Entity Framework Core.
* SQL Server access.
* Production application hosting.

### Revestik.Shared

Contains contracts that must be understood by both the client and API.

Examples include:

* Requests.
* Responses.
* Enumerations.
* Pagination contracts.
* Authentication contracts.

Persistence entities and server implementation details do not belong in `Revestik.Shared`.

## Rationale

This architecture provides a clear trust boundary between browser code and server-side behavior.

It also avoids duplicating HTTP request and response models between the frontend and backend.

For the current scope of Revestik, introducing additional projects for Domain, Application, Infrastructure, or other Clean Architecture layers would increase complexity without solving an existing project problem.

The current organization provides separation of concerns while maintaining a simple development and deployment model.

## Alternatives Considered

### Single ASP.NET Core project

The frontend, API, contracts, and persistence could exist in a single project.

This was not selected because it would make the separation between browser responsibilities and server responsibilities less explicit.

### Fully separated frontend and backend repositories

The client and API could be developed in independent repositories.

This was not selected because Revestik is currently one product developed and released together.

Separate repositories would introduce additional versioning, deployment, and contract coordination complexity without a current requirement.

### Clean Architecture with additional projects

A structure such as:

```text
Revestik.Domain
Revestik.Application
Revestik.Infrastructure
Revestik.Api
Revestik.Client
Revestik.Contracts
```

was considered as a possible future architecture.

It was not selected because the current application does not have sufficient complexity to justify these additional abstraction boundaries.

Architecture should evolve in response to concrete maintainability or scaling problems rather than being expanded preemptively.

### Microservices

Independently deployable services were not selected.

The current Revestik domains do not require independent scaling, deployment, ownership, or operational isolation.

A distributed architecture would introduce substantial additional complexity in areas such as:

* Networking.
* Authentication between services.
* Distributed transactions.
* Observability.
* Deployment.
* Data ownership.
* Failure handling.

without providing a current business benefit.

## Consequences

### Positive

* Clear client/server trust boundary.
* Shared API contracts without exposing persistence entities.
* Simple solution structure.
* Straightforward local development.
* Straightforward testing.
* Single application deployment remains possible.
* Architecture can evolve incrementally.

### Negative

* The API project contains several server-side responsibilities.
* As the number of business domains grows, the API project may become too large.
* Shared contracts create compile-time coupling between client and API.

These trade-offs are currently acceptable.

## Future Review Triggers

This decision should be reviewed if:

* The API becomes difficult to navigate or maintain.
* Business logic becomes significantly more complex.
* Multiple applications need to consume the same backend.
* Revestik requires independently deployable components.
* Separate teams become responsible for different domains.
* Infrastructure concerns begin to dominate application code.
* Shared contracts create undesirable coupling.

A review does not automatically require adopting Clean Architecture or microservices.

The architecture should change only when a specific problem justifies the additional complexity.
