# Local Development Setup

## 1. Purpose

This document describes how to configure and run Revestik in a local development environment.

The instructions reflect the current repository configuration.

Local development runs the Blazor WebAssembly client and ASP.NET Core API as separate development processes.

## 2. Prerequisites

### .NET SDK

Revestik targets .NET 10.

The current development environment uses:

```text
.NET SDK 10.0.103
```

Verify the installed SDK:

```powershell
dotnet --version
```

A compatible .NET 10 SDK is required.

### SQL Server

Local development currently uses SQL Server Express.

The configured development instance is:

```text
localhost\SQLEXPRESS
```

The development database is:

```text
RevestikDb
```

SQL Server must be running before applying migrations or starting workflows that require database access.

### Git

Git is required to clone the repository and participate in the normal branch and pull-request workflow.

### Development Environment

Visual Studio Code or Visual Studio may be used.

Revestik does not require an IDE-specific project format beyond the normal .NET solution and project files.

## 3. Solution Projects

The solution currently contains:

```text
src/Revestik.Api/Revestik.Api.csproj
src/Revestik.Client/Revestik.Client.csproj
src/Revestik.Shared/Revestik.Shared.csproj
tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj
```

Verify the solution:

```powershell
dotnet sln Revestik.sln list
```

## 4. Clone and Enter the Repository

Clone the repository:

```powershell
git clone https://github.com/cristoferlee/revestik.git
```

Enter the repository:

```powershell
cd revestik
```

## 5. Restore Local .NET Tools

Revestik versions the Entity Framework Core command-line tool through `dotnet-tools.json`.

Restore repository tools:

```powershell
dotnet tool restore
```

Verify the EF Core tool:

```powershell
dotnet tool run dotnet-ef --version
```

The currently versioned EF Core CLI version is:

```text
10.0.10
```

Using the repository-local tool is preferred over relying on an unrelated globally installed `dotnet-ef` version.

## 6. Restore Dependencies

Restore NuGet dependencies:

```powershell
dotnet restore Revestik.sln
```

## 7. Development Database Configuration

The development connection string is currently configured in:

```text
src/Revestik.Api/appsettings.Development.json
```

The default local configuration targets:

```text
Server=localhost\SQLEXPRESS
Database=RevestikDb
Trusted_Connection=True
TrustServerCertificate=True
```

This configuration assumes Windows-integrated authentication to the local SQL Server instance.

Developers using a different local SQL Server configuration should provide an appropriate development-only connection string without committing credentials.

## 8. Apply Database Migrations

Restore tools first if necessary:

```powershell
dotnet tool restore
```

Apply the current EF Core migrations:

```powershell
dotnet tool run dotnet-ef database update `
    --project src/Revestik.Api/Revestik.Api.csproj `
    --startup-project src/Revestik.Api/Revestik.Api.csproj
```

This creates or updates the local `RevestikDb` schema according to the migrations committed to the repository.

Database migrations should not be generated merely to repair an incorrectly configured local environment.

## 9. Development Secrets

The API project uses .NET User Secrets.

Secrets must not be committed to source control.

The following configuration values are currently required for the Google authentication flow:

```text
Authentication:Google:ClientId
Authentication:Google:ClientSecret
Authentication:BootstrapAdministratorEmail
```

Configure them using:

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

User Secrets are intended for local development.

Production secrets require an environment-appropriate secret-management mechanism.

## 10. Google OAuth Development Configuration

The API HTTPS development profile runs at:

```text
https://localhost:7126
```

The Google external authentication callback is:

```text
https://localhost:7126/signin-google
```

That URI must be configured as an authorized redirect URI in the corresponding Google OAuth client.

The Blazor client HTTPS development address is:

```text
https://localhost:7081
```

The API development configuration uses that address as the authentication client base URL.

## 11. Development URLs

### API

HTTPS:

```text
https://localhost:7126
```

HTTP:

```text
http://localhost:5029
```

### Blazor Client

HTTPS:

```text
https://localhost:7081
```

HTTP:

```text
http://localhost:5181
```

The recommended development workflow uses the HTTPS profiles.

## 12. Development CORS

During development, the client and API run on different local origins.

The API currently allows the development client origins:

```text
https://localhost:7081
http://localhost:5181
```

CORS is required in this development topology because the browser considers these origins different from the API origin.

This does not imply that production should allow unrestricted cross-origin access.

## 13. Client API Configuration

The Blazor development configuration is stored in:

```text
src/Revestik.Client/wwwroot/appsettings.Development.json
```

The current API base URL is:

```text
https://localhost:7126
```

The client uses this address when running in Development.

In the hosted production model, the client uses the application's own base address instead.

## 14. Run the API

Open a terminal in the repository root.

Run:

```powershell
dotnet run `
    --project src/Revestik.Api/Revestik.Api.csproj `
    --launch-profile https
```

The API should start at:

```text
https://localhost:7126
```

Keep this terminal running.

## 15. Run the Blazor Client

Open a second terminal in the repository root.

Run:

```powershell
dotnet run `
    --project src/Revestik.Client/Revestik.Client.csproj `
    --launch-profile https
```

The client should be available at:

```text
https://localhost:7081
```

The browser may open automatically according to the client launch profile.

## 16. Verify the Application

A basic local verification should include:

1. API starts without configuration exceptions.
2. Client starts successfully.
3. Client can communicate with the API.
4. SQL Server is reachable.
5. Database migrations are current.
6. Authentication provider configuration is available.
7. The login flow can complete.
8. Authenticated API calls succeed.
9. A basic customer read operation works.

The API also exposes a health endpoint:

```text
/api/health
```

The health endpoint checks database connectivity as part of its current implementation.

## 17. Build the Solution

From the repository root:

```powershell
dotnet build Revestik.sln
```

For a Release build:

```powershell
dotnet build Revestik.sln `
    --configuration Release
```

The solution should build without errors before changes are considered ready for review.

## 18. Run Automated Tests

Run the complete solution test suite:

```powershell
dotnet test Revestik.sln
```

Release configuration:

```powershell
dotnet test Revestik.sln `
    --configuration Release
```

Additional testing guidance is maintained in:

```text
docs/development/testing.md
```

## 19. Common Local Development Flow

A normal development session is:

```text
Start SQL Server
      ↓
Open repository
      ↓
Restore tools/dependencies if required
      ↓
Confirm migrations
      ↓
Run API
      ↓
Run Blazor Client
      ↓
Develop
      ↓
Build
      ↓
Run tests
```

## 20. Troubleshooting

### API reports that the database connection failed

Check:

* SQL Server Express is running.
* The configured SQL Server instance exists.
* The local Windows account can connect.
* `RevestikDb` exists or migrations can create it.
* The connection string matches the local environment.

### Client cannot reach the API

Check:

* API is running.
* Client `ApiBaseUrl` matches the API HTTPS address.
* The correct development launch profiles are being used.
* The client origin is present in the Development CORS configuration.
* The local HTTPS development certificate is trusted.

### Google authentication is unavailable

Check:

* Google Client ID exists in User Secrets.
* Google Client Secret exists in User Secrets.
* The redirect URI matches the API HTTPS address.
* The Google OAuth client configuration contains the correct redirect URI.

### Authentication succeeds externally but access is denied

External identity and Revestik authorization are separate.

Check:

* The email corresponds to an authorized Revestik account.
* The application user is active.
* The expected role is assigned.
* Bootstrap administrator configuration is correct for first-time initialization.

### EF Core command is unavailable

Run:

```powershell
dotnet tool restore
```

Then use:

```powershell
dotnet tool run dotnet-ef
```

rather than depending on a global EF installation.

## 21. Local Development Principle

A developer should be able to understand and reproduce the local development environment from version-controlled configuration and this document without requiring undocumented machine-specific knowledge.

Secrets are the exception: their required names are documented, but their values must remain outside source control.
