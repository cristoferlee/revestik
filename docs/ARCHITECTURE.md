# Architecture

Revestik is a single .NET 10 solution organized as a modular monolith. It has a Blazor WebAssembly client, a trusted ASP.NET Core API and shared transport contracts. EF Core and SQL Server provide relational persistence. The server hosts the published client in a same-origin deployment.

## Boundaries

- **Client:** navigation, interaction and early validation; never trusted for permission or financial enforcement.
- **API:** endpoints, services, authentication, authorization, antiforgery, transactions, document generation, uploads and external integrations.
- **Shared:** transport DTOs and contracts; persistence entities belong on the server.
- **Database:** relational constraints, indexes, migrations and concurrency safeguards.

## Domain separation

Customers are master data for related projects and commercial records. Quotations do not reserve inventory. Issued sales can consume stock via FIFO; voiding restores original stock provenance. Purchases feed inventory only via explicit receipt. Received fiscal XML, vouchers, expenses and reconciliation follow distinct validation and business flows. Project abonos are operational records, not Sales payments or bank transactions.

## External trust boundaries

Google OAuth establishes an external identity; Revestik controls application access. Gmail is an input channel, not proof that a fiscal document is accepted. XML is validated securely before canonical processing. Uploaded project files are stored privately, outside publicly served web assets.

## Design decisions

Accepted ADRs in `docs/adr/` describe the Client/API/Shared separation, cookie sessions and EF Core/SQL Server choice. Do not alter accepted rationale when updating the operational summary. New ADRs are for consequential, difficult-to-reverse decisions, not routine features.
