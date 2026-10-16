# Projects — Implementation checklist / current handoff

**Date:** 2026-10-16  
**Stage:** B05 completed and verified locally; Git staging/commit/push pending.  
**Baseline published before B05:** `7dde154` (`main`, B04 document management).  
**Source of approved rules:** `docs/projects/business-rules.md` (PRJ-001–PRJ-037), supplemented by `docs/projects/b01-b02-decisions.md` and owner's approved B05 decisions. Do not replace the original 97-answer baseline.

## Verified locally (owner-reported)

- [x] B01 — Customer-linked Projects foundation, ownership, optional agreed amount, versioning, CSRF.
- [x] B02 — Activity, agreements, comments, revisions and authorization.
- [x] B03 — Optional weighted milestones, templates and progress without automatic project closure.
- [x] B04 — Private documents, revisions, links to events, endpoints and file storage. Published in `7dde154`.
- [x] B05-A — Models/configurations for ProjectPayment, ProjectPaymentRevision, ProjectAgreedAmountRevision, ProjectPaymentDocumentLink; migration `20261016183335_AddProjectPayments` and EF snapshot. SQL foundation tests.
- [x] B05-B+C — Manual payment service, balance, correction/void history, audited agreed-amount updates, transaction coordination, document links, contracts and protected API endpoints. Initial focused tests passed.
- [x] B05-D — Additional SQL/HTTP tests including overlapping payment attempts and CSRF/authorization. xUnit2031 assertion form corrected in `ProjectPaymentAdvancedSqlTests.cs`.
- [x] Full solution build passed after B05-D correction (owner-reported).
- [x] Full regression **920 succeeded** in **487 seconds** (owner-reported, 2026-10-16).
- [x] Prior foundation regression after B05-A: 894 succeeded (owner-reported).
- [x] EF model/snapshot synchronization checked after `AddProjectPayments` scaffolding (owner-reported).

## B05 approved business choices

1. Reject overpayments.
2. Edit payment corrections only with prior values, actor, time and reason preserved.
3. Logical payment void with mandatory reason, excluded from balance.
4. Payments use same currency as Project (CRC/USD; no FX conversion).
5. Payment evidence optional, using private B04 documents.
6. Agreed-amount changes require motive/history and recalculate remaining balance; cannot reduce below active payments.

**Boundary:** Project payments are operational tracking only: never automatically change Sales, SalePayments, tax/XML, accounting, inventory or bank reconciliation. Regular Project edits must not silently change monetary fields. No separate payment infrastructure or duplicate stored balance.

## Release closeout — pending

- [ ] Confirm working branch and local HEAD, and inspect `git status --short` for unrelated edits.
- [ ] Recheck `dotnet ef migrations has-pending-model-changes` if source has changed since verification.
- [ ] Stage only B05 source, SQL migration + Designer + snapshot, B05 tests, and the two updated continuity documents.
- [ ] Review `git diff --cached --name-only`, `git diff --cached --stat`, `git diff --cached --check` and relevant staged source diffs.
- [ ] Confirm no temporary ZIPs, build outputs, secrets or unrelated modules are staged.
- [ ] Obtain explicit owner authorization for commit and push; record actual SHA and successful remote status.
- [ ] Confirm whether the development database needs `AddProjectPayments` applied; Testcontainers verification is not proof of dev DB application. Do not run production migrations without separate approval.

## Known limitations / future hardening

- B04 private file cleanup/orphan reconciliation must not run concurrently with uploads until safely coordinated.
- B04 image compression/redimension, production format safety and security hardening require a dedicated readiness review.
- Permanent Project deletion must coordinate external private files and dependent history.
- A full end-to-end manual Blazor UI smoke test is deferred until B07/B08.
- The 920 tests show only the scenarios they exercise; not a production-readiness certificate.

## Roadmap after checkpoint

- [ ] B06 — Tasks, due dates, lifecycle/close/reopen, alerts in existing Dashboard.
- [ ] B07 — Coherent responsive Blazor Projects UI.
- [ ] B08 — Lightweight PDF, final regression, security review and UI smoke tests.

## Working method

**INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP.** No unsanctioned commits/pushes, destructive migrations, extra frameworks, microservices or cross-module payment side effects. Preserve all unrelated local changes.
