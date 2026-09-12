# Testing Strategy

## 1. Purpose

This document describes the current automated testing strategy for Revestik.

The objective of testing is not to maximize the number of tests or achieve an arbitrary coverage percentage.

Tests should protect important application behavior, business rules, security boundaries, and hosting assumptions so regressions can be detected before changes reach `main`.

## 2. Test Project

Automated server-side tests are located in:

```text
tests/Revestik.Api.Tests
```

The project uses xUnit and is included in `Revestik.sln`.

Tests are executed locally and by the GitHub Actions continuous integration pipeline.

## 3. Current Testing Areas

The current test suite covers three primary areas:

```text
Revestik.Api.Tests
├── Authentication
├── Customers
└── Hosting
```

These areas protect business behavior, security-sensitive requests, API contracts, and application hosting.

## 4. Customer Validation Tests

Customer request validation tests verify important rules before invalid information reaches persistence.

Examples include:

* Required fields.
* Identification type.
* Identification format.
* Identification length.
* Email requirements.
* Phone requirements.
* Province, canton, and district formats.
* Address requirements.

These tests protect business rules documented in:

```text
docs/product/business-rules.md
```

Where practical, tests should remain traceable to the behavior described there.

## 5. Customer Service Tests

Customer service tests verify server-side business and persistence behavior independently from the Blazor user interface.

Relevant behaviors include:

* Customer creation.
* Customer updates.
* Customer retrieval.
* Customer deactivation.
* Identification uniqueness behavior.
* Filtering.
* Search behavior.
* Ordering.
* Pagination-related behavior.

Service tests are important because client-side validation cannot be considered authoritative.

## 6. Pagination Contract Tests

Pagination tests verify the behavior of customer list requests and paginated responses.

Pagination must remain bounded and deterministic.

Tests should protect behavior such as:

* Page normalization.
* Page-size normalization.
* Total result metadata.
* Result boundaries.
* Stable pagination behavior.

Pagination testing becomes increasingly important as the amount of business data grows.

## 7. Authentication and CSRF Tests

Revestik uses cookie-based authentication and antiforgery protection for applicable mutable authenticated operations.

Security tests verify behavior such as:

* Missing required antiforgery tokens are rejected.
* Invalid antiforgery tokens are rejected.
* Protected mutable operations cannot execute without passing the required security checks.

These tests protect an important application security boundary.

A test should verify observable security behavior rather than merely confirming that a particular middleware or method exists in source code.

## 8. Hosting Tests

Revestik includes tests for application hosting behavior.

These tests cover development and production hosting assumptions.

Relevant behavior includes:

* Blazor application hosting.
* Static asset handling.
* SPA fallback behavior.
* Separation between application routes and `/api/*` routes.
* Production static asset behavior.

This protects against configuration regressions that may compile successfully but prevent the published application from working correctly.

## 9. Unknown API Route Protection

Unknown API routes must not accidentally return the Blazor application's `index.html`.

The application verifies unknown API behavior for methods including:

* `GET`
* `POST`
* `PUT`
* `DELETE`

An unknown `/api/*` request should remain an API response, normally `404`, rather than being handled by the SPA fallback.

This distinction prevents invalid API requests from appearing to succeed with an HTML response.

## 10. Static Asset Verification

Production hosting tests and CI verify important published application assets.

Examples include:

* `wwwroot/index.html`
* `_framework/blazor.webassembly.js`
* .NET runtime assets.
* Brotli-compressed assets.

Published asset verification helps detect deployment failures that normal unit tests may not reveal.

## 11. Test Classification

The current suite can be understood using three broad categories.

### Unit / Focused Behavior Tests

These test isolated application behavior such as request validation or business logic.

They should be:

* Fast.
* Deterministic.
* Focused on one behavior.
* Independent from unrelated infrastructure where practical.

### Integration Tests

Integration tests exercise multiple application components together.

Examples include requests executed against an ASP.NET Core test host to verify security or endpoint behavior.

These tests are valuable when the behavior depends on middleware, routing, authentication, antiforgery, or hosting configuration.

### Hosting / Application Tests

These verify assumptions about how the complete application behaves when hosted or published.

They protect concerns that cannot be reliably verified through isolated unit tests.

## 12. Testing Pyramid

Revestik should generally favor many focused tests and fewer expensive end-to-end tests.

Conceptually:

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

This is a guideline rather than a required numerical ratio.

A test should be placed at the lowest level capable of reliably verifying the behavior.

## 13. Running All Tests

From the repository root:

```powershell
dotnet test Revestik.sln
```

For Release configuration:

```powershell
dotnet test Revestik.sln `
    --configuration Release
```

## 14. Running the API Test Project

To run only the API test project:

```powershell
dotnet test tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj
```

Release configuration:

```powershell
dotnet test tests/Revestik.Api.Tests/Revestik.Api.Tests.csproj `
    --configuration Release
```

## 15. Running Tests Without Restore

When dependencies have already been restored:

```powershell
dotnet test Revestik.sln `
    --no-restore
```

CI may combine `--no-restore` and `--no-build` after completing those steps separately.

Local developers should only use those options when the required previous steps have actually completed.

## 16. Test Naming

Test names should describe observable behavior.

A useful pattern is:

```text
MethodOrScenario_Condition_ExpectedBehavior
```

For example:

```text
CreateCustomer_DuplicateIdentification_ReturnsConflict
```

Exact naming may vary where another structure produces clearer tests.

The important requirement is that a failing test should communicate what behavior has regressed.

## 17. Arrange, Act, Assert

Focused tests should generally remain easy to read using the conceptual structure:

```text
Arrange
   ↓
Act
   ↓
Assert
```

### Arrange

Prepare the required state and inputs.

### Act

Execute the behavior being tested.

### Assert

Verify the externally meaningful result.

Tests should avoid unnecessary setup that makes the behavior difficult to identify.

## 18. Business Rule Traceability

Important business rules should have automated verification where practical.

For example:

```text
CUS-004
Physical person identification = exactly 9 digits
        ↓
Customer request validation
        ↓
CustomerUpsertRequestValidationTests
```

Rule IDs do not need to be mechanically inserted into every test name.

The goal is to maintain enough traceability to answer:

> What test protects this rule?

## 19. Bug Fixes and Regression Tests

When a defect is discovered, the preferred workflow is:

```text
Reproduce defect
      ↓
Identify expected behavior
      ↓
Add or update regression test
      ↓
Confirm test fails for the defect
      ↓
Implement fix
      ↓
Confirm test passes
      ↓
Run relevant suite
```

Not every visual or environmental issue can be reproduced efficiently through automated tests.

For business logic, security behavior, persistence rules, and API behavior, regression tests should be strongly preferred.

## 20. Database Testing

Database-related behavior should be tested at the appropriate level.

Mocking or isolated tests may be sufficient for some business logic.

However, behavior that specifically depends on SQL Server should eventually be verified against a real or representative SQL Server environment.

Examples include:

* Unique constraints.
* SQL Server-specific behavior.
* Migrations.
* Transaction behavior.
* Database-generated behavior.

A mocked database cannot prove that a SQL Server constraint actually works.

## 21. Migration Testing

EF Core migrations require special attention because a migration can compile while still being unsafe for existing data.

Before production deployment, relevant migration verification should consider:

* Migration applies successfully.
* Existing data satisfies new constraints.
* Required fields have a valid transition strategy.
* New indexes can be created.
* Data transformations behave correctly.
* Application code remains compatible with the resulting schema.

Formal automated production-like migration testing is a future improvement for Revestik.

## 22. UI Testing

Revestik does not currently rely on a comprehensive automated browser/end-to-end UI suite.

Client behavior should still be verified manually for relevant changes.

Potential future automated UI coverage may include critical workflows such as:

* Authentication.
* Customer creation.
* Customer editing.
* Quotations.
* Payments.
* Other high-value end-to-end business workflows.

UI automation should be introduced when the maintenance cost is justified by the workflows it protects.

## 23. Performance Testing

The current automated suite is not a substitute for load or performance testing.

Performance testing should be introduced when Revestik has measurable workload requirements.

Potential scenarios include:

* Large customer datasets.
* Dashboard aggregation.
* Concurrent business users.
* Inventory queries.
* Reporting.
* External service latency.

Performance optimization should be based on measurements rather than assumptions.

## 24. Security Testing Limitations

Automated security regression tests protect known application behavior but do not constitute a penetration test or security certification.

The current suite does not claim to provide comprehensive coverage for:

* Penetration testing.
* Dependency vulnerability assessment.
* Infrastructure security.
* Secret scanning.
* Dynamic application security testing.
* Full authorization matrix testing.

These may be introduced as the project and deployment requirements evolve.

## 25. Continuous Integration

GitHub Actions runs automated verification for changes targeting the main development history.

The CI process currently performs:

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

CI protects the shared repository from changes that fail basic build, test, or publish verification.

A successful CI run increases confidence but does not by itself prove that a change is production-ready.

## 26. Pre-Review Verification

Before a code change is considered ready for pull-request review, the developer should normally run:

```powershell
dotnet restore Revestik.sln

dotnet build Revestik.sln `
    --configuration Release `
    --no-restore

dotnet test Revestik.sln `
    --configuration Release `
    --no-build
```

For changes affecting hosting or deployment, publishing should also be verified.

## 27. What Should Be Tested

Tests should prioritize behavior where failure would have meaningful impact.

Examples include:

* Business rules.
* Financial calculations.
* Data integrity.
* Authentication.
* Authorization.
* Security boundaries.
* API contracts.
* State transitions.
* Persistence behavior.
* Critical application hosting.

Simple implementation details do not necessarily require dedicated tests if they are already adequately protected by higher-value behavioral tests.

## 28. What Tests Should Avoid

Tests should avoid unnecessary coupling to implementation details.

For example, a test should generally prefer:

> An unauthenticated request receives the expected response.

over:

> A specific private method was called exactly once.

Tests that depend heavily on implementation details become expensive to maintain during legitimate refactoring.

## 29. Deterministic Tests

Automated tests should produce the same result when the application behavior has not changed.

Tests should avoid uncontrolled dependencies on:

* Current local time.
* Random values.
* External network services.
* Developer-specific machine state.
* Execution order.
* Existing local database contents.

When such dependencies are necessary, they should be controlled explicitly.

## 30. Future Testing Priorities

As new Revestik domains are implemented, testing should expand alongside them.

Likely priorities include:

1. Quotation calculations and status transitions.
2. Inventory quantity and movement rules.
3. Accounts receivable calculations.
4. Payment behavior.
5. Authorization by business role.
6. Electronic invoicing integration boundaries.
7. Database migration verification.
8. Critical browser workflows.

New modules should not be considered complete solely because their UI works manually.

## 31. Testing Principle

The core testing principle for Revestik is:

> Test important behavior at the lowest level that can verify it reliably.

Tests exist to provide confidence that business and technical behavior remains correct as the application changes.

The objective is not more tests.

The objective is useful protection against regressions.
