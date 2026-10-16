# Projects — Continuity instructions

**Updated:** 2026-10-16. **Current phase:** B05 payments/balance locally verified; awaiting scoped Git closeout. Next block after owner-approved B05 publication: B06, not yet authorized.

## Authoritative sources

1. `docs/projects/business-rules.md` — original 97 approved answers in PRJ-001–PRJ-037; the functional baseline.
2. `docs/projects/b01-b02-decisions.md` — follow-up approved decisions.
3. `docs/projects/checklist.md` — latest implementation/verification and release status.
4. Historical `docs/projects/CHECKPOINT-2026-10-15-B03.md`, `docs/projects/CHECKPOINT-2026-10-13-B02.md`, `VERIFY_B04_E.md` — historic evidence, not latest status.
5. Current source/working tree and actual Git refs — verify before coding; local uncommitted code can exceed GitHub.

## Latest completed development

B01 Customer-bound Projects; B02 events/agreements/revisions; B03 optional milestone plans/templates; B04 private versioned Project documents; B05 manual operational payments and remaining balance with correction, logical void, audited agreed-amount revisions and document references.

B05 rules approved by owner: reject overpayments; corrections preserve prior values/actor/date/reason; voids logical with reason; CRC/USD payments must match project; evidence optional; agreed amount changes audited and constrained by active payments. No exchange-rate conversion or automated Sales/accounting/inventory/XML effect.

B05-A migration: `20261016183335_AddProjectPayments`. SQL Testcontainers fixture migrates disposable test databases; this does not confirm applying the migration to the developer's database.

**Latest owner-reported regression (2026-10-16):** `dotnet test Revestik.sln --no-build`, 920 tests succeeded, 487 seconds. The B05-D xUnit2031 warning was corrected before the successful final run. Do not claim a newer result without execution.

**Published `main` before B05:** `7dde154` (B04). B05 has not yet been staged/pushed at the time this document was written. Verify the remote SHA.

## Method

INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP. Spanish conversation; English source identifiers/docs, Spanish UI messages. .NET 10 modular monolith, ASP.NET Core Minimal APIs, Blazor WebAssembly, EF Core SQL Server, Identity, antiforgery, xUnit, SQL Server Testcontainers. Keep SOLID pragmatic and avoid unrelated refactoring, microservices, CQRS, generic repositories or duplicated balance logic.

Only responsible user or Administrator modifies a Project and its operational payments; authorized roles can read. All mutation endpoints protect against CSRF; server-side validation and SQL constraints must protect invariants. Financial mutations must synchronize on the Project to prevent concurrent overpayment and agreed-amount races. Payment evidence uses B04 private document storage; avoid cross-Project references and public file access.

Before any release, inspect actual working tree, stage only scoped files, check staged diff and pending model changes. Never commit, push, alter production databases or delete files without owner authorization. ZIP patches preserve relative repo paths and avoid temporary README/scripts/binaries. Do not claim tests, commits or migrations succeeded without evidence.

## Next phase

Finish B05 scoped Git closeout, confirm SHA/remote push, then STOP. B06 tasks, deadlines, lifecycle and Dashboard alerts must begin with a separate INVESTIGATE and approval. Comprehensive Blazor UI and manual smoke tests are deferred until B07/B08.
