# Architecture Overview

## 1. Purpose

This document describes the current high-level architecture of Revestik.

Revestik is a full-stack business management web application built with .NET and Blazor. The application separates the browser client, server-side API, shared contracts, persistence, authentication, and external integrations while remaining within a single solution.

This document reflects the implemented architecture. Planned functionality is documented separately and should not be represented here as already implemented.

## 2. Solution Structure

The solution is organized into the following primary projects:

```text
Revestik
├── src
│   ├── Revestik.Client
│   ├── Revestik.Api
│   └── Revestik.Shared
├── tests
│   └── Revestik.Api.Tests
└── docs
```

### Revestik.Client

`Revestik.Client` is the Blazor WebAssembly frontend.

Its responsibilities include:

* Rendering the user interface.
* Managing client-side navigation and UI state.
* Performing client-side form validation.
* Communicating with the API through HTTP services.
* Managing the client-side authentication state.
* Obtaining and sending antiforgery tokens for protected mutable requests.

The client does not access SQL Server directly.

### Revestik.Api

`Revestik.Api` is the ASP.NET Core server application and represents the trusted server-side boundary of Revestik.

Its responsibilities include:

* Exposing HTTP API endpoints.
* Authentication and authorization.
* Server-side request validation.
* Antiforgery protection.
* Business operations and application services.
* Database access through Entity Framework Core.
* External service integrations.
* Serving the published Blazor WebAssembly application in the hosted production model.
* Health checks and application hosting configuration.

The API is internally organized by responsibility, including areas such as:

```text
Endpoints
Services
Data
Models
Authorization
Integrations
```

### Revestik.Shared

`Revestik.Shared` contains contracts shared between the client and API.

These include request and response models for domains such as:

* Authentication.
* Customers.
* Locations.
* Taxpayer information.
* Common response structures such as pagination.

This project allows the client and API to share their HTTP contract without giving the client access to server implementation or persistence details.

### Revestik.Api.Tests

`Revestik.Api.Tests` contains automated tests for server-side behavior.

Current test areas include:

* Customer validation.
* Customer pagination.
* Customer service behavior.
* CSRF protection.
* Mutable endpoint protection.
* Development hosting.
* Production hosting.

## 3. High-Level Architecture

```mermaid
flowchart TB
    User[User / Browser]
    Client[Revestik.Client<br/>Blazor WebAssembly]
    Api[Revestik.Api<br/>ASP.NET Core]
    Shared[Revestik.Shared<br/>HTTP Contracts]
    EF[Entity Framework Core]
    DB[(SQL Server)]
    External[External Services]

    User --> Client
    Client -->|HTTPS / JSON| Api
    Client -.-> Shared
    Api -.-> Shared
    Api --> EF
    EF --> DB
    Api --> External


## 4. Request Flow

```mermaid
sequenceDiagram
    actor User
    participant Client as Blazor Client
    participant API as ASP.NET Core API
    participant Service as CustomerService
    participant EF as Entity Framework Core
    participant DB as SQL Server

    User->>Client: Submit customer form
    Client->>Client: Client-side validation

    Client->>API: HTTP request + auth cookie + CSRF token

    API->>API: Authenticate user
    API->>API: Authorize operation
    API->>API: Validate CSRF token
    API->>API: Validate request

    API->>Service: Execute business operation
    Service->>EF: Read / persist data
    EF->>DB: Parameterized database operation

    DB-->>EF: Database result
    EF-->>Service: Entity / result
    Service-->>API: Response DTO
    API-->>Client: JSON response

    Client-->>User: Update interface

## 5. Persistence

Revestik uses Entity Framework Core with SQL Server.

`RevestikDbContext` is the application's EF Core database context and also integrates ASP.NET Core Identity persistence.

Entity configuration is separated from the `DbContext` using `IEntityTypeConfiguration<T>` implementations and applied from the API assembly.

Database integrity is enforced at multiple levels where appropriate:

1. Request validation.
2. Server-side application logic.
3. EF Core model configuration.
4. SQL Server constraints and indexes.

This layered approach prevents the correctness of persisted data from depending exclusively on the frontend.

## 6. Authentication and Authorization

Revestik uses ASP.NET Core Identity with cookie-based authentication.

The application follows a protected-by-default model: authenticated access is required unless an endpoint is explicitly configured for anonymous access.

Authorization policies and roles are used for operations requiring elevated permissions.

Mutable authenticated requests are additionally protected with antiforgery validation.

Detailed security behavior is documented in:

```text
docs/security/security-overview.md
```

## 7. Hosted Application Model

During development, the Blazor client and API can run with development-specific configuration.

For the published application, the ASP.NET Core API also serves the compiled Blazor WebAssembly application.

Static assets are served using ASP.NET Core static asset infrastructure.

Blazor routes fall back to the client application's `index.html`, while unknown `/api/*` routes are kept separate from the SPA fallback so an invalid API request cannot accidentally return the Blazor HTML application.

## 8. External Integrations

External systems are isolated from UI components behind server-side integrations and services.

This keeps external communication inside the trusted server boundary and avoids exposing implementation details or credentials to the Blazor WebAssembly client.

Integrations should be accessed through abstractions where practical so their implementation can evolve independently from consuming application code.

## 9. Testing and Continuous Integration

Automated tests are maintained in `Revestik.Api.Tests`.

The GitHub Actions CI pipeline currently validates the application by:

1. Restoring dependencies.
2. Building the solution in Release configuration.
3. Running automated tests.
4. Publishing the hosted application.
5. Verifying required Blazor WebAssembly assets.
6. Verifying runtime assets and compressed static assets.

CI runs for changes targeting `main` and helps prevent invalid builds from being merged unnoticed.

## 10. Architectural Principles

The current architecture follows these principles:

### Separation of concerns

Frontend presentation, HTTP contracts, server-side behavior, persistence, and tests have distinct responsibilities.

### Server as the trust boundary

The client is never trusted to enforce business or security rules by itself.

### Defense in depth

Important rules can be enforced across validation, application logic, persistence configuration, and database constraints.

### Explicit contracts

Client/server communication uses shared request and response contracts instead of exposing persistence entities directly.

### Incremental complexity

Revestik favors an architecture appropriate for its current size rather than introducing additional architectural layers without a demonstrated requirement.

The architecture may evolve as the application grows, but additional complexity should be introduced to solve concrete problems rather than to follow a pattern for its own sake.

## 11. Current Architectural Classification

Revestik is best described as a modular full-stack .NET application with:

* A Blazor WebAssembly client.
* An ASP.NET Core API and application host.
* Shared HTTP contracts.
* Internal server-side separation by responsibility.
* Entity Framework Core and SQL Server persistence.
* ASP.NET Core Identity authentication.
* Automated server and hosting tests.

It is not currently implemented as Clean Architecture or as independently deployable microservices.

That distinction is intentional: the current structure provides clear separation while keeping deployment and development complexity appropriate for the application's present scope.
