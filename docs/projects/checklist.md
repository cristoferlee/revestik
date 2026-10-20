# Projects — Implementation checklist / current handoff

**Date:** 2026-10-20
**Stage:** B01–B07 published; B08 PDF, UX and finance-security improvements implemented and locally smoke-tested; Git closeout pending.
**Latest confirmed published baseline:** `8f13989` (`main`, B07). Re-check the actual remote HEAD before publishing.
**Approved business baseline:** `docs/projects/business-rules.md` (97 original answers in PRJ-001–PRJ-037), supplemented by `b01-b02-decisions.md` and subsequent approved decisions.

## Completed implementation and user-reported validation

- [x] B01 — Customer-linked Projects foundation, responsible user, versioning and permissions.
- [x] B02 — Activities, category management, comments, agreements, revisions and authorization.
- [x] B03 — Weighted milestone plans/templates and progress without automatic closure.
- [x] B04 — Private versioned documents, evidence and event links.
- [x] B05 — Manual CRC/USD abonos, correction, void, agreed amount audit, balances, linked documents.
- [x] B06 — Tasks, deadlines, lifecycle/formal close and dashboard alerts.
- [x] B07 — Projects Blazor workspace; published `main` baseline.
- [x] B08 / PRJ-PDF-01 — Project dossier PDF, executive/financial summary, progress bar, activities, agreements, tasks, pagination and document register. User approved 7/7 PDF smoke tests and subsequently confirmed official logo and final report work.
- [x] UX improvements — Project listing/status labels, category management, milestone/template management, linked-document selector by name.
- [x] PRJ-UX-06.1 — Linked payment-document query, per-document unlink and candidate filtering; 4/4 user-reported smoke tests, including closed-project read-only behavior.
- [x] SEC-UX06-01 — Closed-project finance lock for Completed/Cancelled/Archived, including Administrator; build and 26/26 focused tests passed (user-reported).
- [x] Projects test regression, **193 succeeded, 0 failed, 0 skipped**, duration **335.9 s**, build succeeded **337.4 s** (user-reported, 2026-10-20).
- [x] Existing documents/gallery flows and project PDF manually tested by user; do not re-test merely to repeat completed checks.

## Project payments — invariants

1. Abonos are manual operational records, not SalePayments or bank transactions.
2. Reject overpayments; retain previous values, actor, date and reason on corrections.
3. Logical voids require a reason and do not contribute to active totals.
4. CRC/USD amounts match the project currency with no FX conversion.
5. Agreed-amount changes are audited and cannot reduce below current active payments.
6. Evidence is project-scoped, private and optional; unlinking leaves the source document intact.
7. Completed/Cancelled/Archived projects permit reads but prohibit financial and payment-evidence writes, even by Administrator.

## GH-01 — Documentation cleanup

- [x] `git mv` initiated for three historical documents:
  - `VERIFY_B04_E.md` → `docs/archive/projects/VERIFY_B04_E.md`
  - `docs/projects/CHECKPOINT-2026-10-13-B02.md` → `docs/archive/projects/CHECKPOINT-2026-10-13-B02.md`
  - `docs/projects/CHECKPOINT-2026-10-15-B03.md` → `docs/archive/projects/CHECKPOINT-2026-10-15-B03.md`
- [x] Prepare updated `docs/projects/AGENTS.md` and `docs/projects/checklist.md` (this documentation patch).
- [ ] Verify the three renames and refreshed documentation in the *staged* diff before commit.
- [ ] Review untracked `src/RevestikPrivateDocuments/`; **do not stage it without reviewing privacy and contents**.
- [ ] Verify all local B08/UX/SEC source and test files for staging, preserving migrations/legacy code and current frontend.
- [ ] Verify `git status -sb`, branch tracking and current remote SHA; then `git diff --check` and `git diff --cached --check` after staged selection.
- [ ] Perform repository-wide regression if chosen for release; the 193 Tests result covers Projects only.
- [ ] Obtain explicit owner approval for the final commit and push; verify resulting SHA and `main...origin/main` status.

## Known boundaries / follow-ups

- B04 private-storage cleanup/orphan reconciliation needs concurrency-safe coordination before production automation.
- File format validation, storage safety, image processing and production readiness merit dedicated review when releasing externally.
- Closed-project financial protection must remain server-side; hiding Blazor actions is insufficient.
- Before release, verify permissions and CSRF for the newly added linked-evidence GET and write endpoints with the actual local code and tests.
- Do not claim full-product production readiness or full-suite success from the Projects-only regression.

## Working method

**INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP.** No automatic migration, commit, push, destructive cleanup, blanket staging or unrelated refactoring. Preserve checkpoints and ZIP deliverables; do **not** modify `.gitignore` as part of GH-01.
