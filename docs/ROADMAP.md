# Roadmap and delivery status

This roadmap describes project direction, not a guarantee of delivery dates or a claim that every planned feature is already implemented. Changes are approved through the project's documented workflow.

## Phase 1 — Core internal business platform (implemented)

A modular .NET 10 / Blazor WebAssembly application covering customer records, quotations, sales, products, FIFO inventory, suppliers, purchases, accounts payable, expenses, electronic document intake, reconciliation, project operations, reporting, and administration.

## Phase 2 — Production deployment (deployed; ongoing stabilization)

The internal application is deployed on Azure Container Apps with Azure SQL Database. Production-aware configuration includes HTTPS, Identity/Google sign-in, private persistent storage, certificate-protected Data Protection keys, and a SQL health check.

Ongoing work includes operational verification, performance assessment, authentication resilience, Gmail synchronization reliability, documentation, and release hygiene. The local AUTH-PROD-01A recovery change passed a reported 1,073-test regression but must not be labeled production-deployed without release verification.

## Phase 3 — Public customer-facing website (next planned phase)

Develop the public website for the Revestik business. The site is intended for customers to learn about the company, products, services, and ways to contact the team. Its detailed functional scope, information architecture, branding, SEO, hosting, and integration with the internal application will be planned and approved separately.

**Boundary:** The public site is not the internal operations application, and its development does not imply public access to private business data or a multi-tenant SaaS offering.

## Future opportunities (not committed)

Potential work may include dashboard performance optimization, session-experience improvements, progressive-web-app/offline capabilities, and other enhancements identified through real operational needs. No timeline or delivery commitment is implied.

## How progress is verified

A feature is considered **implemented** when it exists in the relevant code and passes appropriate verification; **deployed** only after the production artifact and smoke checks are confirmed; and **planned** when its scope is agreed but it is not yet delivered. Track concrete changes through Git history, tests, ADRs, and release evidence rather than an unbounded status diary.
