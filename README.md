# Revestik

Revestik is a modular-monolith business management application built with .NET 10, ASP.NET Core Minimal APIs, Blazor WebAssembly, Entity Framework Core and SQL Server.

It supports customers, quotations, sales, inventory, suppliers, purchases, accounts payable, received Costa Rican electronic documents, expense workflows, bank vouchers, fiscal reconciliation and customer-linked projects. Business-critical validation and authorization run server-side.

## Solution

- `src/Revestik.Api` — API, Identity, persistence, integrations and server-side PDF generation.
- `src/Revestik.Client` — Blazor WebAssembly user interface.
- `src/Revestik.Shared` — API request/response contracts.
- `tests/Revestik.Api.Tests` — automated tests, including SQL Server integration tests.

## Getting started

Install the .NET 10 SDK and a compatible SQL Server environment. Follow [development instructions](docs/DEVELOPMENT.md) for safe local setup. Do not commit connection strings containing secrets, tokens, private documents or production data.

## Engineering reference

- [Architecture](docs/ARCHITECTURE.md)
- [Business rules](docs/BUSINESS_RULES.md)
- [Security](docs/SECURITY.md)
- [Development and testing](docs/DEVELOPMENT.md)
- [Deployment and operations](docs/OPERATIONS.md)
- [Agent instructions and current checkpoint](AGENTS.md)

Accepted architecture decisions are recorded in [`docs/adr/`](docs/adr/).

Revestik is under active development. Successful compilation or module tests do not independently establish production readiness.
