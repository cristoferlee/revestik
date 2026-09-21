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

**194 passed, 0 failed, 0 skipped.**

This number is a snapshot, not a quality target. New tests should be added when they protect meaningful behavior.

## 4. Current Testing Areas

The suite currently covers areas including:

```text
Revestik.Api.Tests
├── Authentication
├── Customers
├── Products
├── Quotations
└── Hosting
```

Coverage includes focused behavior, HTTP/API integration, SQL Server integration, security, concurrency, and hosting verification.

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

Client-side validation is not considered authoritative.

## 6. Product Tests

Product support currently exists to assist quotation entry.

Automated coverage includes:

* Product authorization behavior.
* Product pagination request contracts.
* Product service pagination behavior.

These tests protect the supporting lookup capability without implying that the complete Inventory domain exists.

## 7. Quotation Tests

Quotation coverage includes:

* Request and nested-request validation.
* Decimal quantities and quotation-line rules.
* Monetary calculations.
* 0% and 13% IVA behavior.
* Percentage/fixed discounts.
* Additional charges.
* Service behavior.
* Draft/Issued behavior where exercised by current endpoints/services.
* Customer snapshot behavior.
* Authorization.
* API contracts.
* SQL Server persistence.
* SQL Server sequence-based COT generation.
* Concurrent quotation-number generation.
* Product-linked/manual quotation behavior.
* PDF endpoint/service boundaries where covered by the current suite.

The Blazor UI is also manually verified for critical quotation workflows because the project does not currently maintain a comprehensive browser E2E suite.

## 8. Authentication and CSRF Tests

Security tests verify observable behavior such as:

* Missing antiforgery tokens are rejected where required.
* Invalid antiforgery tokens are rejected.
* Protected mutable operations cannot execute without required security checks.
* Authorization protects applicable endpoints.

Tests should verify behavior rather than merely confirming middleware configuration exists.

## 9. Hosting Tests

Hosting verification covers development and production assumptions such as:

* Blazor application hosting.
* Static asset handling.
* SPA fallback.
* Separation between client routes and `/api/*`.
* Production static assets.

Unknown `/api/*` routes must remain API responses and must not fall through to `index.html`.

## 10. Static Asset Verification

Published application checks include assets such as:

* `wwwroot/index.html`
* `_framework/blazor.webassembly.js`
* .NET runtime assets
* Brotli-compressed assets

These checks detect deployment regressions that ordinary unit tests may miss.

## 11. Test Classification

### Focused / Unit Tests

Used for isolated behavior such as request validation and monetary calculations.

They should be fast, deterministic, and independent from unnecessary infrastructure.

### Integration Tests

Used when behavior depends on multiple application components such as routing, middleware, persistence, authorization, or API contracts.

### SQL Server Integration Tests

Used when behavior depends specifically on SQL Server, including sequences, constraints, migrations, persistence behavior, or concurrency.

### Hosting / Application Tests

Used for published-application and hosting assumptions.

### Manual UI Verification

Used for current critical Blazor workflows that are not yet protected by browser automation.

## 12. Testing Pyramid

Revestik generally favors many focused tests and fewer expensive end-to-end tests:

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

## 13. Running Tests

From the repository root:

```powershell
dotnet test Revestik.sln
```

Release configuration:

```powershell
dotnet test Revestik.sln `
    --configuration Release
```

API test project only:

```powershell
dotnet test tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj
```

When restore/build already completed, `--no-restore` or `--no-build` may be used intentionally.

## 14. Test Naming

Test names should communicate observable behavior.

A useful pattern is:

```text
MethodOrScenario_Condition_ExpectedBehavior
```

The exact naming style may vary when another form is clearer.

## 15. Arrange, Act, Assert

Focused tests should remain easy to read conceptually:

```text
Arrange
   ↓
Act
   ↓
Assert
```

Tests should avoid unnecessary setup that obscures the behavior being protected.

## 16. Business Rule Traceability

Important rules should have automated verification where practical.

Rule identifiers do not need to appear mechanically in every test name.

The goal is to be able to answer:

> What test protects this behavior?

## 17. Regression Workflow

When a defect affects testable business or technical behavior, prefer:

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

Not every visual/environmental defect requires automated reproduction.

## 18. Database Testing

Behavior that depends on SQL Server is verified against a real isolated SQL Server instance through Testcontainers.

The integration-test infrastructure:

* Starts an isolated SQL Server container.
* Uses a dedicated integration database.
* Applies EF Core migrations.
* Creates application `DbContext` instances against that database.
* Avoids dependence on developer-local database contents.

SQL Server integration is appropriate for:

* Sequences.
* Constraints/indexes.
* EF Core migrations.
* Database-generated behavior.
* SQL Server-specific persistence.
* Relevant concurrency behavior.

Testcontainers complements focused tests rather than replacing them.

## 19. UI Testing

Revestik does not currently maintain a comprehensive automated browser/E2E suite.

Important UI changes should receive manual verification.

Potential future browser automation should focus on high-value workflows such as:

* Authentication.
* Customer management.
* Quotations.
* Sales.
* Inventory movements.
* Payments.

UI automation should be introduced when its maintenance cost is justified.

## 20. Performance Testing

The automated suite is not a substitute for load/performance testing.

Performance work should begin when measurable workload requirements exist.

Potential scenarios include:

* Large customer/product datasets.
* Dashboard aggregation.
* Concurrent users.
* Inventory queries.
* Reporting.
* External-service latency.

## 21. Security Testing Limitations

Automated security regression tests do not constitute a penetration test or security certification.

The current suite does not claim comprehensive coverage for:

* Penetration testing.
* Dependency vulnerability assessment.
* Infrastructure security.
* Secret scanning.
* Dynamic application security testing.
* Complete authorization-matrix verification.

## 22. Continuous Integration

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

CI protects shared history from changes that fail core verification.

A successful CI run increases confidence but does not by itself prove production readiness.

## 23. Pre-Review Verification

Before pull-request review, the normal verification sequence is:

```powershell
dotnet restore Revestik.sln

dotnet build Revestik.sln `
    --configuration Release `
    --no-restore

dotnet test Revestik.sln `
    --configuration Release `
    --no-build
```

Hosting/deployment changes should also verify publishing.

## 24. What Should Be Tested

Prioritize behavior where failure would have meaningful impact:

* Business rules.
* Financial calculations.
* Data integrity.
* Authentication/authorization.
* Security boundaries.
* API contracts.
* State transitions.
* Persistence.
* Critical hosting assumptions.

Avoid tests that are coupled unnecessarily to private implementation details.

## 25. Deterministic Tests

Tests should avoid uncontrolled dependencies on:

* Current local time.
* Random values.
* External network services.
* Developer-machine state.
* Execution order.
* Existing local database contents.

Control such dependencies explicitly when they are required.

## 26. Future Testing Priorities

As the next domains are implemented, priorities include:

1. Sales conversion and state-transition rules.
2. Inventory quantity and movement invariants.
3. Purchase-to-stock behavior.
4. Accounts receivable and payment transitions.
5. Expense classification/storage behavior.
6. Expanded authorization coverage.
7. Broader migration verification.
8. Critical browser workflows where automation is justified.

Direct electronic invoicing tests are intentionally not a near-term priority while that integration remains deferred.

## 27. Definition of a Verified Change

A critical change should normally satisfy the applicable combination of:

* Relevant focused tests.
* Integration tests when infrastructure behavior matters.
* Manual UI verification for user-facing flows.
* Successful solution build.
* Successful complete test suite.
* Successful CI.
* Updated documentation when behavior materially changes.