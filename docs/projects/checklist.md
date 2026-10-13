# Projects — Implementation checklist / current handoff

**Checkpoint dated:** 2026-10-13. **Current stage:** B02-E closeout, before B02 commit. **Source of detail:** `CHECKPOINT-2026-10-13-B02.md`.

## Confirmed progress (user-reported local results)

- [x] Original business discovery: 97 answers and later B01 clarifications; documented in `business-rules.md` and `b01-b02-decisions.md`.
- [x] Initial technical investigation; actual B01 code later verified from GitHub.
- [x] B01 foundation: Project, ProjectContact, EF Core, API, auth, optimistic Version, SQL tests. Migration `20261013205336_AddProjectsFoundation` user-reported applied. Full suite 787/787 passed at B01.
- [x] B01 code later confirmed present on GitHub `main`; **do not infer or invent B01 commit SHA**.
- [x] B02 A+B code: configurable categories, events, timeline, event revisions, update/void. Build passed. Migration `20261013215919_AddProjectActivityFoundation` user-reported applied. Targeted SQL 3/3 passed (~100s).
- [x] B02 C+D code: append-only comments, event-linked agreements and agreement revisions, administrative audit consultation and authorization endpoints. Build passed. Migration `20261013221354_AddProjectEventDetails` user-reported applied.
- [x] B02-E targeted SQL: ProjectEventDetailSqlTests 5/5, two compiler nullable warnings found; user received exact fix; later regression succeeded.
- [x] B02 HTTP authorization baseline: filtered test run 33/33, 0 failed, duration 57.9s (test grouping may include preexisting cases).
- [x] **Full regression 828/828**, 0 failed/skipped, test duration 318.9s, build duration 319.5s.
- [x] `git diff --check` no formatting errors; CRLF informational warnings only.
- [x] User requires no B01/B02 temp README, scripts, or patch archives in project; latest status had none.

## B02 outstanding; do not mark closed prematurely

- [ ] HTTP *non-owner* authorization with **valid CSRF**, exercising event/agreements writes and 403 (separate from role-level 401/403/CSRF 400 checks). Service-layer non-owner behavior has tests.
- [ ] Recheck local `git status --short`, final staged diff, migration state and whether the owner installed nullable-warning fix. Last full suite succeeded; compiler warnings not explicitly reverified separately.
- [ ] Update/publish `docs/projects` and B02 code in a scoped commit, on owner authorization. Capture real SHA and remote status.
- [ ] STOP and request authorization for B03.

## Future blocks

- [x] **B01** Foundation (implemented and tests/migration verified; UI deferred).
- [ ] **B02** Events/audit (implemented/tested; closeout checklist above still open).
- [ ] **B03** Optional milestones and weighted automatic progress, idempotence, rollback and terminal states; numeric weights **not approved**.
- [ ] **B04** Private file storage, photo compression, versions, multi-event linking, preview, download, secure deletion. File-size cap/storage choices open.
- [ ] **B05** Manual project payments/receipts, balance, audit, currency/overpayment decisions. No Sales/accounting/inventory effects.
- [ ] **B06** Due tasks, responsibility, alerts on existing Dashboard, lifecycle/close/reopen workflows.
- [ ] **B07** Cohesive responsive Blazor Projects UI and navigation; Customer selector, tabs Summary/Activity/Documents/Agreements/Payment Receipts/Plans.
- [ ] **B08** Lightweight PDF, final regression, documentation and **only then** manual UI smoke test.

## Required protocol each block

INVESTIGATE actual current relevant code → PLAN → APPROVE → IMPLEMENT ZIP code-only → VERIFY build, targeted xUnit, SQL/authorization/CSRF/concurrency, migration SQL inspection, regression as appropriate → STOP. No unrelated changes or commits without authorization. Local working tree may be newer than GitHub.
