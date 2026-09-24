# Testing Strategy

## 1. Purpose

This document describes the current automated testing strategy for Revestik.

The objective is not to maximize test count or chase an arbitrary coverage percentage.

Tests should protect important business behavior, security boundaries, persistence assumptions, API contracts, and hosting behavior so regressions can be detected before changes reach `main`.

## 2. Test Project

Automated server-side tests are located in:

```text
tests/Revestik.Api.Tests
```

The project uses xUnit and is included in `Revestik.sln`.

Tests run locally and in GitHub Actions.

## 3. Current Test Baseline

Current complete-suite baseline:

**257 passed, 0 failed.**

This number is a snapshot, not a quality target.

## 4. Current Testing Areas

```text
Revestik.Api.Tests
├── Authentication
├── Customers
├── Products
├── Quotations
├── Sales
└── Hosting
```

Coverage includes focused behavior, HTTP/API integration, SQL Server integration, security, concurrency, document generation, and hosting verification.

## 5. Customer Tests

Current customer coverage includes:

* Identification-format validation.
* Required customer data.
* Pagination contracts.
* Service pagination behavior.
* Filtering and search.
* Deterministic ordering.
* Deactivation/reactivation behavior.
* Relevant persistence and error behavior.

## 6. Product Tests

Product support currently assists commercial entry.

Automated coverage includes authorization, pagination request contracts, and service pagination behavior.

## 7. Quotation Tests

Quotation coverage includes:

* Request and nested-request validation.
* Decimal quantities and line rules.
* Monetary calculations.
* 0% and 13% IVA.
* Percentage/fixed discounts.
* Additional charges.
* Service behavior.
* Draft/Issued behavior.
* Customer snapshots.
* Authorization.
* API contracts.
* SQL Server persistence.
* SQL Server sequence-based COT generation.
* Concurrent COT generation.
* Product-linked/manual quotation behavior.
* PDF behavior where covered by the suite.

Critical quotation UI workflows are also manually verified.

## 8. Sales Tests

Sales coverage includes:

* Sale request validation.
* Nested line/charge validation.
* Sale monetary calculations.
* General and line discounts.
* Draft/Issued/Voided lifecycle behavior.
* Direct sale creation.
* Quotation-to-sale conversion behavior.
* Customer snapshots.
* Query/list/history behavior.
* Summary behavior.
* Payment registration.
* Partial/full balance transitions.
* Payment void behavior.
* Sale void/replacement behavior.
* PDF service/endpoint behavior.
* SQL Server-backed VEN generation.
* Sequential VEN generation.
* Concurrent VEN uniqueness.
* Mutable-endpoint antiforgery behavior.

The current Sales hardening suite includes dedicated request-validation and number-generator concurrency coverage.

## 9. Authentication and CSRF Tests

Security tests verify observable behavior such as:

* Missing antiforgery tokens are rejected where required.
* Invalid antiforgery tokens are rejected.
* Protected mutable operations cannot execute without required security checks.
* Authorization protects applicable endpoints.
* Sales mutable endpoints participate in the same CSRF model.

## 10. Hosting Tests

Hosting verification covers:

* Blazor application hosting.
* Static asset handling.
* SPA fallback.
* Separation between client routes and `/api/*`.
* Production static assets.

Unknown `/api/*` routes must remain API responses and must not fall through to `index.html`.

## 11. Static Asset Verification

Published application checks include assets such as:

* `wwwroot/index.html`
* `_framework/blazor.webassembly.js`
* .NET runtime assets
* Brotli-compressed assets

## 12. Test Classification

### Focused / Unit Tests

Used for isolated behavior such as request validation and monetary calculations.

### Integration Tests

Used when behavior depends on routing, middleware, persistence, authorization, or API contracts.

### SQL Server Integration Tests

Used when behavior depends specifically on SQL Server, including sequences, constraints, migrations, persistence behavior, or concurrency.

### Hosting / Application Tests

Used for published-application and hosting assumptions.

### Manual UI Verification

Used for critical Blazor workflows not yet protected by browser automation.

## 13. Testing Pyramid

```text
             /\
            /  \
           / E2E\
          /------\
         /Integration\
        /------------\
       / Focused Tests \
      /________________\
```

A behavior should be tested at the lowest level capable of verifying it reliably.

## 14. Running Tests

From the repository root:

```powershell
dotnet test Revestik.sln
```

API test project only:

```powershell
dotnet test tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj
```

Release configuration:

```powershell
dotnet test Revestik.sln `
    --configuration Release
```

SQL Server/Testcontainers integration tests require Docker.

## 15. Test Naming

A useful pattern is:

```text
MethodOrScenario_Condition_ExpectedBehavior
```

## 16. Regression Workflow

```text
Reproduce
   ↓
Define expected behavior
   ↓
Add/update regression test
   ↓
Confirm failure
   ↓
Implement fix
   ↓
Confirm pass
   ↓
Run relevant suite
```

## 17. Database Testing

SQL Server-dependent behavior is verified against an isolated SQL Server instance through Testcontainers.

Appropriate scenarios include:

* Sequences.
* Constraints/indexes.
* EF Core migrations.
* Database-generated behavior.
* Persistence.
* Concurrency.

## 18. UI Testing

Revestik does not currently maintain a comprehensive automated browser/E2E suite.

Important UI changes receive manual smoke verification.

Current high-value manually verified workflows include:

* Authentication.
* Customer management.
* Quotations.
* Quotation history.
* Quotation-to-sale conversion.
* Sales creation/issue.
* Sales history.
* Payments.
* Sale void/replacement.

## 19. Performance Testing

The automated suite is not a substitute for load/performance testing.

Performance work should begin when measurable workload requirements exist.

## 20. Security Testing Limitations

Automated security regression tests do not constitute a penetration test or security certification.

The suite does not claim comprehensive coverage for infrastructure security, DAST, dependency assessment, or a complete authorization matrix.

## 21. Continuous Integration

GitHub Actions currently performs:

```text
Restore
   ↓
Build Release
   ↓
Run Tests
   ↓
Publish Application
   ↓
Verify Hosted Blazor Assets
```

## 22. Pre-Review Verification

```powershell
dotnet restore Revestik.sln

dotnet build Revestik.sln `
    --configuration Release `
    --no-restore

dotnet test Revestik.sln `
    --configuration Release `
    --no-build
```

## 23. What Should Be Tested

Prioritize:

* Business rules.
* Financial calculations.
* Data integrity.
* Authentication/authorization.
* Security boundaries.
* API contracts.
* State transitions.
* Persistence.
* Critical hosting assumptions.

## 24. Deterministic Tests

Tests should avoid uncontrolled dependencies on current time, random values, external network services, developer-machine state, execution order, or existing local database contents.

## 25. Future Testing Priorities

1. Inventory movement and stock invariants.
2. Purchase-to-stock behavior.
3. Expanded accounts-receivable rules if introduced.
4. Expense classification/storage behavior.
5. Expanded authorization coverage.
6. Broader migration verification.
7. Critical browser workflows where automation becomes justified.

Direct electronic invoicing tests remain out of scope while that integration is deferred.

## 26. Definition of a Verified Change

A critical change should normally satisfy the applicable combination of:

* Relevant focused tests.
* Integration tests when infrastructure behavior matters.
* Manual UI verification for user-facing flows.
* Successful solution build.
* Successful complete test suite.
* Successful CI.
* Updated documentation when behavior materially changes.