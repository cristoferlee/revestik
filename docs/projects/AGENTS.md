# Projects — Continuity instructions (read first)

**Updated:** 2026-10-15. **Current phase:** B03 milestone progress/templates implemented and locally verified (22 targeted tests; full suite 857/857). Git publication is pending until confirmed. **Next block after closeout: B04**, not yet authorized.

## Read order

1. `docs/projects/CHECKPOINT-2026-10-15-B03.md` — authoritative latest checkpoint, exact paths, test counts, limitations, next actions.
2. `docs/projects/checklist.md` — current implementation and closeout status.
3. `docs/projects/business-rules.md` — original approved functional baseline; preserve unchanged.
4. `docs/projects/b01-b02-decisions.md` — approved B01/B02 follow-up choices; preserve unchanged.
5. `docs/projects/CHECKPOINT-2026-10-13-B02.md` — historical B02 handoff, superseded for current status.
6. Current source and Git working tree; local uncommitted changes can be newer than GitHub.

Do **not** repeat the 97-question discovery or reopen settled business decisions. New blocks need only targeted technical investigation and explicit approval.

## Operating rules

**INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP.** Communicate with user in Spanish. Keep technical docs and C# identifiers in English and UI copy in Spanish. Pragmatic .NET 10 modular monolith (ASP.NET Core Minimal APIs, Blazor WASM, EF Core SQL Server, Identity, CSRF, xUnit + SQL Server Testcontainers). Prefer existing services and contracts; no speculative microservices, generic repositories or duplicate progress calculations.

Source patch ZIPs contain only real repo-relative files; no temporary scripts, README, or ZIPs in repo. Protect unrelated user edits. Server enforces authorization/invariants; mutating APIs require CSRF. User executes PowerShell in `C:\Projects\Revestik`, reports actual results. Never invent test/migration/push outcomes. Do not commit, push, apply migrations or perform destructive work without authorization. Before commit examine precise staged paths, `git diff --cached --check` and `git diff --cached --stat`.

**Manual Projects UI smoke testing is postponed to complete coherent responsive B07/B08 UI.** Continue automated tests. Performance tuning of the SQL test suite is deferred until Projects is complete.

## Boundaries / current decisions

Each Project belongs to exactly one Customer. Responsible user or Administrator edits; other permitted roles may read. Project notes/files/payments do not automatically change Sales, Quotations, inventory, accounting or Customers. B02 events/agreements preserve history. B03 optional progress milestones have manually assigned weights (100% required before activation); completion/reversal updates progress and audit history, without automatically closing the Project. Template application creates independent Project milestones. No parallel progress engine.

## Immediate next step

Finish **B03-E scoped documentation staging and Git publication** only after owner approval and verification of staged diff. Full suite 857/857 and B03 22/22 passed; EF snapshot synced. Strict simultaneous race still not directly tested. Following closeout, STOP and investigate B04 only after owner requests it.
