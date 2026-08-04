# Revestik

Revestik is a full-stack business management application designed to help a small family business organize customers, inventory, invoices, purchases, quotations, and daily financial operations.

The project began as a client-side JavaScript beta and is being rebuilt with the Microsoft .NET ecosystem to provide server-side validation, relational persistence, authentication, authorization, and a maintainable architecture.

## Project Status

Revestik is currently in active development and is not yet deployed for production use.

The current version includes a functional .NET architecture, customer management, SQL Server persistence, Costa Rican taxpayer lookup, structured customer locations, and Google authentication with role-based authorization.

The original HTML, CSS, and JavaScript application remains available under `legacy/vanilla-js/` and through the `v0.1.0-legacy` Git tag.

## Architecture

The solution is divided into three projects:

- **Revestik.Client** — Blazor WebAssembly user interface and client-side services.
- **Revestik.Api** — ASP.NET Core API, business services, integrations, authentication, and data access.
- **Revestik.Shared** — Request and response contracts shared between the client and API.

The client communicates with the API through HTTP. The API validates requests, enforces authorization policies, executes business operations, and uses Entity Framework Core to persist data in SQL Server.

## Technology Stack

- .NET 10
- C#
- ASP.NET Core Minimal APIs
- Blazor WebAssembly
- ASP.NET Core Identity
- Google OAuth 2.0
- Entity Framework Core
- SQL Server
- LINQ
- RESTful APIs
- Data Annotations
- Dependency Injection
- OpenAPI
- HTML5
- CSS3
- Git and GitHub

## Implemented Features

### Application interface

- Responsive application layout
- Dashboard
- Desktop and mobile navigation
- Module routing
- Loading, validation, success, and error states
- Spanish user-facing interface

### Customer management

- Create and update customers
- Search customers by name or identification
- Activate and deactivate customer records
- Server-side and client-side validation
- Support for Costa Rican physical identification, legal identification, and DIMEX
- Structured province, canton, and district selection
- Additional address details
- SQL Server persistence through Entity Framework Core

### External integrations

- Costa Rican Ministry of Finance taxpayer lookup
- Automatic taxpayer name retrieval
- Province, canton, and district catalog integration
- In-memory caching for location catalog responses
- Integration error handling

### Authentication and authorization

- Google OAuth authentication
- ASP.NET Core Identity user persistence
- Secure HttpOnly authentication cookies
- Unique and confirmed external email accounts
- Default authenticated-user requirement
- Server-enforced role authorization
- Unauthorized and access-denied API responses
- Local secret storage with .NET User Secrets

Current application roles:

- `Administrator`
- `Accountant`
- `Sales`
- `Warehouse`

Authorization policies control access to customer, inventory, invoice, and audit operations.

## Database

Revestik uses SQL Server with Entity Framework Core Code First.

Database changes are tracked through EF Core migrations, including:

- Initial customer storage
- Structured customer locations
- ASP.NET Core Identity tables

Apply the migrations with:

```powershell
dotnet tool restore

dotnet tool run dotnet-ef database update `
    --project src/Revestik.Api/Revestik.Api.csproj `
    --startup-project src/Revestik.Api/Revestik.Api.csproj
```

## Local Development

### Prerequisites

- .NET 10 SDK
- SQL Server or SQL Server Express
- Visual Studio Code or Visual Studio
- A Google OAuth client configured for local development

### Restore and build

```powershell
dotnet restore Revestik.sln
dotnet build Revestik.sln
```

### Development configuration

The development SQL Server connection string is configured in:

```text
src/Revestik.Api/appsettings.Development.json
```

Google credentials and the initial administrator email must be stored with .NET User Secrets and must not be committed to source control:

```powershell
dotnet user-secrets set `
    "Authentication:Google:ClientId" `
    "YOUR_GOOGLE_CLIENT_ID" `
    --project src/Revestik.Api/Revestik.Api.csproj

dotnet user-secrets set `
    "Authentication:Google:ClientSecret" `
    "YOUR_GOOGLE_CLIENT_SECRET" `
    --project src/Revestik.Api/Revestik.Api.csproj

dotnet user-secrets set `
    "Authentication:BootstrapAdministratorEmail" `
    "YOUR_AUTHORIZED_EMAIL" `
    --project src/Revestik.Api/Revestik.Api.csproj
```

For the default HTTPS development profile, configure this authorized redirect URI in Google Cloud:

```text
https://localhost:7126/signin-google
```

### Run the API

```powershell
dotnet run `
    --project src/Revestik.Api/Revestik.Api.csproj `
    --launch-profile https
```

Default API address:

```text
https://localhost:7126
```

### Run the Blazor client

Open a second terminal and execute:

```powershell
dotnet run `
    --project src/Revestik.Client/Revestik.Client.csproj `
    --launch-profile https
```

Default client address:

```text
https://localhost:7081
```

## Project Structure

```text
Revestik/
  legacy/
    vanilla-js/
  src/
    Revestik.Api/
      Authorization/
      Data/
      Endpoints/
      Extensions/
      Integrations/
      Models/
      Services/
    Revestik.Client/
      Layout/
      Pages/
      Services/
      wwwroot/
    Revestik.Shared/
      Authentication/
      Customers/
      Locations/
      Taxpayers/
  Revestik.sln
```

## Legacy Application

The original version was built with HTML, CSS, and JavaScript and used browser-based local storage.

It includes early implementations of:

- Quotation creation
- Quotation PDF generation
- Inventory management
- Purchase registration
- Customer and business workflows

Preserving this version documents the evolution from a browser-only prototype into a full-stack .NET application.

## Roadmap

The following functionality is planned or under development:

- Inventory persistence and management
- Purchases
- Quotations
- Electronic invoicing
- XML document generation and processing
- Costa Rican Ministry of Finance electronic-document integration
- Audit history for business operations
- Automated tests
- Production deployment
- Continuous integration and delivery

## Development Principles

- Business rules are enforced by the API.
- The client does not receive database credentials or authentication secrets.
- Authorization is enforced on the server, not only in the interface.
- External input is validated before persistence.
- Database access is handled through Entity Framework Core.
- Asynchronous operations are used for HTTP and database access.
- Components and services have focused responsibilities.
- Code, folders, routes, comments, and technical documentation use English.
- User-facing application content uses Spanish.