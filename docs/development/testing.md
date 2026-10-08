# Testing Strategy

## 1. Purpose

This document describes the current automated testing strategy for Revestik.

The objective is not to maximize test count or chase an arbitrary coverage percentage.

Tests should protect important business behavior, security boundaries, persistence assumptions, API contracts, transactional integrity, inventory invariants, and hosting behavior so regressions can be detected before changes reach `main`.

Correctness and regression protection currently take priority over minimizing complete-suite execution time.

## 2. Test Project

Automated server-side tests are located in:

```text

tests/Revestik.Api.Tests

```

The project uses xUnit and is included in `Revestik.sln`.

Tests run locally and in GitHub Actions.

SQL Server-dependent integration tests use Testcontainers so important persistence behavior is verified against SQL Server rather than an in-memory substitute.

## 3. Current Test Baseline

Current complete-suite verified baseline:

**542 passed, 0 failed.**

This number is a snapshot, not a quality target.

The suite has grown as Customers, Quotations, Sales, Inventory, Suppliers, Purchases, Accounts Payable, Electronic Documents, Hacienda XML, accounting classification, and Gmail ingestion were implemented and hardened.

A higher test count is useful only when the tests protect meaningful behavior.

## 4. Current Testing Areas

Conceptually, current coverage includes:

```text
Revestik.Api.Tests
├── Authentication / Security
├── Customers
├── Products
├── Quotations
├── Sales
├── Inventory
├── Physical Counts
├── Inventory Cost Resolution
├── Suppliers
├── Purchases / Accounts Payable
├── Electronic Documents
├── Hacienda XML / CAByS / Classification
├── Gmail Integration
└── Hosting
```

Coverage includes:

* Focused business-rule tests.
* Request-validation tests.
* Service tests.
* HTTP/API integration tests.
* SQL Server integration tests.
* Authorization and antiforgery tests.
* Transactional/data-integrity tests.
* Concurrency tests.
* PDF generation tests.
* XML/XSD validation tests.
* Gmail MIME/attachment/retry tests.
* Received-document period-summary tests.
* Hosting and static-asset verification.

## 5. Customer Tests

Current customer coverage includes:
* Identification-format validation.
* Required customer data.
* Pagination contracts.
* Service pagination behavior.
* Filtering and search.
* Deterministic ordering.
* Deactivation/reactivation behavior.
* Identification uniqueness.
* Relevant persistence and error behavior.
* Authorization where applicable.

## 6. Product and Inventory-Catalog Tests

Product testing is no longer limited to commercial lookup support.

Current coverage includes behavior related to:
* Product request validation.
* Product retrieval and pagination.
* Search and filtering.
* Authorization.
* Product lifecycle state.
* Deactivation.
* Reactivation.
* Logical archival.
* Exclusion of archived products from normal product queries.
* Inventory unit configuration.
* Commercial unit configuration.
* Quantity conversion behavior.
* Whole-unit quantity rules.
* Persistence and concurrency behavior where applicable.

Products remain shared with commercial workflows, but their inventory behavior is tested as part of the Inventory domain.

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
* Quotation-to-sale conversion behavior.

Critical quotation UI workflows are also manually verified.

Quotations intentionally do not consume or reserve inventory.

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
* Sale void behavior.
* Replacement/correction behavior.
* PDF service/endpoint behavior.
* SQL Server-backed VEN generation.
* Sequential VEN generation.
* Concurrent VEN uniqueness.
* Mutable-endpoint antiforgery behavior.
* Product-linked inventory consumption.
* Inventory-shortage behavior.
* FIFO consumption integration.
* Exact inventory restoration when a sale is voided.
* Reversal provenance.
* Reversal data-integrity behavior.

The Sales suite verifies both commercial behavior and the explicit integration boundary between Sales and Inventory.

## 9. Inventory Tests

Inventory coverage protects the physical-stock model and its critical invariants.

Current areas include:
* Initial stock registration.
* Inventory movements.
* Manual stock increases.
* Manual stock decreases.
* Prevention of negative inventory.
* FIFO consumption.
* FIFO remaining quantities.
* Inventory cost layers.
* Product-linked sale consumption.
* Exact sale inventory reversal.
* Reversal idempotency/data-integrity protection.
* Physical inventory counts.
* Physical-count adjustments.
* Inventory unit rules.
* Whole-unit enforcement.
* Unknown inventory cost handling.
* Unknown-cost resolution.
* Preservation of original historical cost.
* Product lifecycle behavior.
* Persistence relationships and database constraints.

Inventory tests are intentionally stronger around transactional behavior because partial or incorrect updates can corrupt operational stock and historical cost information.

## 10. Exact Sale-Reversal Tests

Sale inventory reversal has dedicated coverage because a voided Sale must restore the same inventory provenance originally consumed.

Coverage includes scenarios around:
* Original Sale inventory movements.
* Original FIFO cost layers.
* Restoring consumed quantities to the original layers.
* `SaleId` provenance.
* `ReversesInventoryMovementId`.
* Prevention of duplicate reversal of the same original movement.
* Invalid or inconsistent reversal state.
* Endpoint behavior.
* Persistence integrity.

The reversal workflow must fail rather than apply a partial restoration when its integrity assumptions are not satisfied.

## 11. Physical Count Tests

Physical count coverage includes:
* Count creation.
* Count lifecycle behavior.
* Count lines.
* Product quantities.
* Counted quantities.
* Adjustments generated from physical/system differences.
* Traceability between adjustments and the originating physical count.
* Authorization.
* Endpoint behavior.
* Persistence.

Physical counts are tested as explicit inventory operations rather than as direct overwrites of product stock.

## 12. Inventory Cost Tests

Inventory cost testing includes:
* Historical FIFO cost layers.
* Remaining quantities.
* Known costs.
* Unknown original costs.
* Unknown-cost quantity visibility.
* Explicit cost resolution.
* Preservation of the original `UnitCost`.
* Separate `ResolvedUnitCost`.
* Resolution metadata.
* Effective-cost behavior based on:

```text

UnitCost ?? ResolvedUnitCost

```

Tests should continue to distinguish current product cost from historical inventory cost.

## 17. Supplier, Purchase, and Accounts Payable Tests

Supplier/Purchase coverage includes:

* Supplier lifecycle and validation.
* Supplier-name uniqueness.
* Purchase request validation.
* Purchase calculations and totals.
* Cash and credit Purchase behavior.
* Credit terms and due dates.
* Purchase payments.
* Accounts Payable summaries.
* Upcoming and overdue behavior.
* Supplier Purchase history.
* Purchase-to-Inventory receipt.
* FIFO cost-layer creation from valid Purchase costs.
* Prevention of duplicate Inventory application.
* Transactional integrity across critical Purchase operations.

The Purchase suite protects the explicit boundary between operational Purchase records and physical Inventory effects.

## 18. Electronic Document and Hacienda XML Tests

Received Electronic Document coverage includes:

* Manual XML import.
* Original XML persistence.
* Secure XML parsing.
* Hacienda XML 4.4 family recognition.
* Local XSD validation.
* Recognized-but-not-enabled document behavior.
* Duplicate detection.
* Rejected-document behavior.
* Hacienda response association.
* Issuer/receiver/totals/economic-activity persistence.
* Accounting classification.
* Operational destination.
* Payment-condition classification.
* Document-level and line-level classification.
* Learned classification rules.
* Confidence-based suggestions.

Electronic Document tests also protect the rule that accepting a received fiscal document does not automatically create a Purchase or modify Inventory.

## 19. Received Documents Period Tests

The Received Documents refinement has dedicated regression coverage.

Current verified behavior includes:

* `FechaEmision` as the economic date.
* Current-calendar-month History as the default historical period.
* Explicit historical date-range filtering.
* Inclusion/exclusion by requested period.
* Empty-period behavior.
* Invalid date-range rejection.
* Period accounting and financial summaries.
* Period summaries independent from free-text, processing-status, and accounting-category list filters.
* Global Pending behavior independent from the selected History month.

## 20. Gmail Integration Tests

Current Gmail coverage includes:

* Gmail MIME traversal.
* XML attachment discovery/retrieval.
* Per-attachment processing.
* Duplicate handling.
* Rejected-attachment tracking.
* Controlled retry behavior.
* Manual synchronization.
* Single-flight synchronization protection.
* Integration-state behavior where applicable.

Periodic background synchronization is intentionally not part of the current
automated baseline.

Bank-voucher Gmail coverage additionally includes configured mailbox targeting,
Banco Nacional sender/subject filtering defaults, real voucher parsing patterns,
duplicate-safe ingestion, and rate-limit hardening.

## 21. Expense and Bank Voucher Tests

Current automated coverage includes:

* BankVoucher model/configuration behavior.
* Banco Nacional voucher parsing for real `Voucher Digital` structures.
* Voucher ingestion and Gmail-message duplicate handling.
* Conservative ElectronicDocument candidate matching.
* Accepted voucher returning to NeedsReview when a late matching document appears.
* Valid and invalid manual voucher-to-document matching.
* Known-supplier review signaling.
* Consolidated reporting across manual expenses, OperatingExpense documents, and
  accepted vouchers.
* Exclusion of NeedsReview, Matched, and Ignored vouchers from independent totals.
* Currency-separated consolidated totals.

The current verified suite contains 575 passing tests at this checkpoint.

## 22. Authentication and CSRF Tests

Security tests verify observable behavior such as:
* Missing antiforgery tokens are rejected where required.
* Invalid antiforgery tokens are rejected.
* Protected mutable operations cannot execute without required security checks.
* Authorization protects applicable endpoints.
* Sales mutable endpoints participate in the same CSRF model.
* Inventory mutable endpoints participate in the same CSRF model.
* Product-management operations respect their authorization policies.

Client-side visibility is not treated as authorization.

Server behavior remains authoritative.

## 14. Hosting Tests

Hosting verification covers:
* Blazor application hosting.
* Static asset handling.
* SPA fallback.
* Separation between client routes and `/api/*`.
* Production static assets.

Unknown `/api/*` routes must remain API responses and must not fall through to `index.html`.

## 15. Static Asset Verification

Published application checks include assets such as:
* `wwwroot/index.html`
* `\_framework/blazor.webassembly.js`
* .NET runtime assets
* Brotli-compressed assets
* Required application static assets

Static-asset verification helps detect publish/deployment regressions that a successful compile alone may not reveal.

## 16. Test Classification

### Focused / Unit Tests

Used for isolated behavior such as:
* Request validation.
* Monetary calculations.
* Quantity rules.
* Small business-rule components.

### Service Tests

Used for application-service behavior where a full HTTP request is not required.

These tests are useful for business workflows such as customer operations, commercial logic, and inventory behavior.

### Integration Tests

Used when behavior depends on:
* Routing.
* Middleware.
* Persistence.
* Authorization.
* Antiforgery.
* API contracts.
* Multiple application components working together.

### SQL Server Integration Tests

Used when behavior depends specifically on SQL Server, including:
* Sequences.
* Constraints.
* Filtered indexes.
* Foreign keys.
* EF Core migrations.
* Transactions.
* Persistence behavior.
* Concurrency.
* Inventory data integrity.

### Hosting / Application Tests

Used for published-application and hosting assumptions.

### Manual UI Verification

Used for critical Blazor workflows not yet protected by browser automation.

## 21. Testing Pyramid

```text

             /\\

            /  \\

           / E2E\\

          /------\\

         /Integration\\

        /------------\\

       / Focused Tests \\

      /\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\\

```

A behavior should be tested at the lowest level capable of verifying it reliably.

Not every business rule requires an HTTP integration test.

Not every persistence rule can be verified correctly with a focused/unit test.

## 22. Running Tests

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

When working on a focused area, targeted test execution may be used during implementation before running the complete suite.

The complete suite should still be executed before considering a substantial domain change verified.

## 23. Local Verification Environment

Current development testing uses SQL Server/Testcontainers for isolated integration scenarios.

The developer's normal application database should not be treated as test-suite state.

Automated tests should create and manage their own required state wherever practical.

This helps keep tests independent from local manual-testing data.

## 24. Test Naming

A useful pattern is:

```text

MethodOrScenario_Condition_ExpectedBehavior

```

Names should communicate the scenario and expected outcome rather than only the method under test.

## 25. Regression Workflow

```text

Reproduce

   ↓

Define expected behavior

   ↓

Add/update regression test

   ↓

Confirm failure when practical

   ↓

Implement fix

   ↓

Confirm focused pass

   ↓

Run relevant integration tests

   ↓

Run complete suite

```

A regression test should protect the business behavior that failed rather than merely reproduce incidental implementation details.

## 26. Database Testing

SQL Server-dependent behavior is verified against an isolated SQL Server instance through Testcontainers.

Appropriate scenarios include:
* Sequences.
* Constraints/indexes.
* Filtered indexes.
* Foreign keys.
* EF Core migrations.
* Database-generated behavior.
* Persistence.
* Transactions.
* Concurrency.
* Inventory relationships.
* Inventory reversal integrity.
* Cost-layer persistence.
* Physical-count persistence.

Tests should use SQL Server when behavior depends on actual SQL Server semantics.

An alternative in-memory provider should not be assumed to provide equivalent behavior for relational constraints, sequences, transactions, concurrency, or SQL Server-specific indexes.

## 27. Transactional Integrity Testing

Operations that modify several related records require particular attention.

Important examples include:

* Sale issue plus inventory consumption.
* FIFO layer updates plus inventory movements.
* Sale void plus exact inventory restoration.
* Physical-count adjustment plus movement creation.
* Purchase receipt plus Inventory movement/cost-layer creation.
* Critical Purchase/AP updates that span multiple dependent records.

Tests should verify not only successful final state but also that invalid operations do not leave partial persisted changes.

Where appropriate, failure-path coverage should confirm that transactional boundaries preserve consistency.

## 28. Concurrency Testing

Concurrency tests are used where simultaneous operations could violate important guarantees.

Current examples include:
* COT number generation.
* VEN number generation.
* Product concurrency behavior.
* Critical persistence/inventory scenarios where concurrent modification matters.

Concurrency testing should be added only where the domain has a concrete race-condition risk.

## 29. UI Testing

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
* Inventory management.
* Product lifecycle operations.
* Physical inventory counts.
* Unknown inventory cost resolution.
* Supplier management.
* Purchases.
* Accounts Payable.
* Received Documents.
* Manual XML import.
* Accounting classification.
* Gmail connection/manual synchronization.

Browser automation may be introduced selectively when a workflow becomes expensive or risky to verify manually.

## 30. Performance Testing

The automated regression suite is not a substitute for application load/performance testing.

Performance work should begin when measurable workload requirements exist.

The complete automated suite currently takes materially longer than the earlier project stages because SQL Server integration coverage has expanded.

This runtime is accepted for the current stage because correctness and regression protection are more important than optimizing test duration prematurely.

Test-suite performance optimization is intentionally deferred until deployment, later production hardening, or completion of higher-priority product work such as the public RevestikCR.com experience makes faster feedback materially valuable.

## 31. Test-Suite Performance Strategy

The current approach is:
1. Use focused tests during implementation when possible.
2. Run affected integration tests while iterating.
3. Run the complete suite before closing major changes.
4. Preserve SQL Server/Testcontainers coverage where real database semantics matter.
5. Avoid removing valuable integration coverage solely to reduce runtime.

Future optimization may investigate:
* Shared Testcontainers infrastructure where safe.
* Test fixture lifetime.
* Database reset strategy.
* Parallelization boundaries.
* Expensive repeated application startup.
* Duplicate integration scenarios.
* Test categorization.
* CI-specific execution strategy.

Any optimization must preserve test isolation and confidence.

## 32. Security Testing Limitations

Automated security regression tests do not constitute a penetration test or security certification.

The suite does not claim comprehensive coverage for:
* Infrastructure security.
* DAST.
* Dependency vulnerability assessment.
* A complete authorization matrix.
* External identity-provider security.
* Production network configuration.

Security tests protect known application boundaries but do not replace production security review.

## 33. Continuous Integration

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

CI should remain an independent verification path rather than relying only on successful local execution.

## 34. Pre-Review Verification

```powershell

dotnet restore Revestik.sln

dotnet build Revestik.sln `

    --configuration Release `

    --no-restore

dotnet test Revestik.sln `

    --configuration Release `

    --no-build

```

For focused development work, narrower commands may be used before this full verification.

## 35. What Should Be Tested

Prioritize:
* Business rules.
* Financial calculations.
* Inventory calculations.
* Data integrity.
* Transactional behavior.
* Authentication/authorization.
* Security boundaries.
* API contracts.
* State transitions.
* Persistence.
* Concurrency-sensitive operations.
* Critical hosting assumptions.

Avoid tests whose primary value is asserting private implementation details with no meaningful behavioral guarantee.

## 36. Deterministic Tests

Tests should avoid uncontrolled dependencies on:
* Current wall-clock time.
* Random values.
* External network services.
* Developer-machine state.
* Execution order.
* Existing local database contents.
* Other tests leaving persistent state behind.

Where current time or generated values affect behavior, they should be controlled or asserted in a way that avoids brittle tests.

## 37. Future Testing Priorities

The current baseline already includes Suppliers, Purchases, Accounts Payable, Purchase-to-Inventory receipt, Electronic Documents, Hacienda XML 4.4, accounting classification, Gmail ingestion, and received-document period summaries.

Expected next priorities include:

1. Periodic Gmail background synchronization when implemented.
2. Scheduled execution and single-flight behavior.
3. Retry/recovery behavior across repeated background runs.
4. Startup/restart behavior for the future background worker.
5. Expanded accounts-receivable rules if introduced.
6. Expense classification/storage behavior.
7. Reports and aggregation behavior.
8. Expanded authorization coverage.
9. Broader migration verification.
10. Critical browser workflows where automation becomes justified.

Direct outbound electronic-invoicing tests remain out of scope while that integration is deferred.

## 38. Definition of a Verified Change

A critical change should normally satisfy the applicable combination of:
* Relevant focused tests.
* Service tests where business behavior is isolated there.
* Integration tests when infrastructure behavior matters.
* SQL Server tests when relational semantics matter.
* Transactional integrity tests for critical multi-record operations.
* Manual UI verification for user-facing flows.
* Successful solution build.
* Successful complete test suite.
* Successful CI.
* Updated documentation when behavior materially changes.

Not every change requires every category.

Verification should be proportional to the risk and behavior being changed.
