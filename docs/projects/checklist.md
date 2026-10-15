# Projects — Implementation checklist / current handoff

**Checkpoint:** 2026-10-15, `CHECKPOINT-2026-10-15-B03.md`. **Stage:** B03-E closeout, pending scoped staged review and Git publication.

## Confirmed (owner-reported)

- [x] Original 97-answer functional rules approved; preserved in `business-rules.md`.
- [x] B01 Projects/contacts/ownership/CSRF/versioning foundation and migration completed.
- [x] B02 event categories, timeline, revisions, comments, agreements and authorization completed; prior full regression 835/835 and B02 previously published.
- [x] B03 A+B: models, SQL EF configurations, `20261015183929_AddProjectMilestones`, progress service, completion/reversal/version/audit.
- [x] B03 C+D: contracts, HTTP endpoints, CSRF/authorization and reusable template service.
- [x] B03 SQL tests 10/10 (6 initial + 4 advanced).
- [x] B03 HTTP tests 12/12 (7 initial + 5 additional).
- [x] Full API suite **857/857** successful (including 22 B03 tests).
- [x] `dotnet ef migrations has-pending-model-changes`: no pending model changes.
- [x] `git diff --check`: no whitespace errors; harmless LF→CRLF notice.

## B03-E closeout pending

- [ ] Review `git diff --cached --name-only`, `git diff --cached --stat`, `git diff --cached --check` after staging only B03 and these closeout documents.
- [ ] Owner expressly approves commit/push; capture actual SHA and successful push before marking published.
- [ ] Optional strict simultaneous SQL race test remains an explicitly documented engineering gap. Stale-version conflicts are tested; do not claim strict overlapping concurrency is verified.

## Upcoming blocks — no implementation approval assumed

- [x] B01 — Customer-linked Projects foundation.
- [x] B02 — Timeline, comments, agreements and audit history.
- [x] B03 — Optional milestones, weighted progress and reusable templates: locally implemented/tested; Git closeout pending.
- [ ] B04 — Private images/files, previews, versions, links, compression and deletion decisions.
- [ ] B05 — Manual payments and remaining balance; independent of accounting/Sales.
- [ ] B06 — Tasks, deadlines, lifecycle and existing Dashboard alerts.
- [ ] B07 — Responsive coherent Projects UI and navigation.
- [ ] B08 — Lightweight Project PDF, end-to-end verification and **only then** manual UI smoke tests.

## Protocol

INVESTIGATE current relevant code → PLAN → explicit APPROVE → IMPLEMENT controlled source ZIP → VERIFY build, SQL, HTTP authorization/CSRF, migration, regression as applicable → STOP. No unapproved commits/pushes, database update or destructive changes. Do not include unrelated code or temp files in ZIPs or commits.
