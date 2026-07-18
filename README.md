# Revestik

Revestik is a financial and inventory management application designed to help a small business organize its daily operations.

The project is being migrated from a client-side JavaScript beta to a full-stack application built with the Microsoft .NET ecosystem.

## Project Status

Revestik is currently in active development.

The original JavaScript beta is preserved in the `main` branch and under the `v0.1.0-legacy` tag.

The .NET migration is being developed in the `migration/blazor-dotnet` branch.

## Current Architecture

The solution currently contains three projects:

```text
src/
├── Revestik.Client/
├── Revestik.Api/
└── Revestik.Shared/

Revestik.Client

A standalone Blazor WebAssembly application responsible for the user interface and client-side interaction.

Revestik.Api

An ASP.NET Core Web API responsible for endpoints, validation, business operations, and data access coordination.

Revestik.Shared

A shared class library for contracts and data transfer objects used by the client and API.

Current Technologies
.NET 10
C#
Blazor WebAssembly
ASP.NET Core Web API
HTML5
CSS3
OpenAPI
Git
GitHub
Current Features
Responsive application layout
Business dashboard
Mobile navigation
Module routing
API health endpoint
Shared project references
Original JavaScript beta preserved for comparison
Planned Technologies
Entity Framework Core
SQL Server
Dependency Injection
Asynchronous programming
RESTful API integration
Authentication and authorization
Automated testing
Deployment pipelines
Planned Modules
Dashboard
Customers
Quotes
Invoices
Inventory
Purchases
Settings
Legacy Application

The original HTML, CSS, and JavaScript beta is available in: legacy/vanilla-js/

The legacy version includes:

Quotation creation
Quotation PDF generation
Product inventory management
Purchase registration
Browser-based persistence with local storage

Its functionality will be migrated incrementally to Blazor, ASP.NET Core, Entity Framework Core, and SQL Server.

Build

Restore and build the complete solution: dotnet restore Revestik.sln
dotnet build Revestik.sln

Run the Blazor client:

dotnet run --project src/Revestik.Client/Revestik.Client.csproj

Run the ASP.NET Core API:

dotnet run --project src/Revestik.Api/Revestik.Api.csproj

Development Approach

The migration follows these principles:

Business logic belongs in C# services.
The client communicates with the server through HTTP APIs.
The API controls access to application data.
SQL Server is responsible for relational persistence and data integrity.
Components and services should have focused responsibilities.
Code, folders, routes, and technical documentation use English.
User-facing application content uses Spanish.