# Projects — Implementation checkpoint and checklist

**Checkpoint:** 2026-10-13  
**Current phase:** PLAN pending detailed Block 01 approval; no implementation.  
**Last confirmed local HEAD in initial investigation:** `ae34ce013a43aa5618c423e813d2477fdbd8430c` (**historical reference only**, re-check local HEAD). 

## Status legend
- [x] Completed and documented
- [ ] Not completed / pending verification

## Discovery and approval
- [x] Functional INVESTIGATE: owner's answers 1–97, including final corrections
- [x] Technical INVESTIGATE: Customers, Quotation/Sale relation, API and Client setup, Identity, CSRF, Dashboard, PDF and integration tests at conceptual scope
- [x] Conceptual eight-block sequence proposed
- [ ] Approve detailed technical PLAN for Block 01 before changing application code
- [ ] Verify actual local Git status, migration state and working tree before any patch
- [ ] Preserve existing uncommitted fiscal reconciliation changes; don't stage or overwrite them accidentally

## Planned blocks — none implemented
- [ ] **B01 — Foundation:** Project, Customer FK, responsible Identity user, unique name per Customer, optional address/dates, contacts, status, agreed amount; DTOs/API/services/EF configs/migration and tests
- [ ] **B02 — Events and audit:** configurable categories, timeline, comments, corrections, void, audit
- [ ] **B03 — Automatic progress:** milestone weights, duplicate prevention, reversal and completion behavior
- [ ] **B04 — Documents:** private storage, thumbnails/optimization, versions, references, preview, download, safe deletion
- [ ] **B05 — Manual payments/abonos:** receipts, amounts by currency, remaining balance, audit and no Sales side effects
- [ ] **B06 — Tasks/Dashboard:** due dates, responsible owner, upcoming and overdue alerts
- [ ] **B07 — Blazor UI:** Customer selector, Project selection, tabs, timeline, mobile upload, full navigation
- [ ] **B08 — PDF and full verification:** summary PDF, regression/smoke and documentation closeout

## Mandatory verification checklist for EACH implementation block
- [ ] Owner explicitly approved block scope
- [ ] Changed files list and migration review complete
- [ ] Build result captured (do not invent)
- [ ] Targeted xUnit tests passed
- [ ] SQL Server Testcontainers tests passed where required
- [ ] Security, authorization, CSRF and concurrent-write tests where applicable
- [ ] Full regression suite and responsive smoke tests at appropriate milestone
- [ ] Git diff reviewed; no unrelated changes staged
- [ ] Updated rules, checklist and technical decisions if changed
- [ ] Commit and push performed **only with owner's explicit authorization**; save real commit SHA here
- [ ] STOP: document next action and await instruction

## Current evidence
- Projects source code added: **none confirmed**
- Projects migration: **none created/applied**
- Projects build/tests/smoke: **not run**
- Project module commits: **none confirmed**
- Documentation checkpoint: **ZIP prepared; publication to GitHub must be verified after local commit/push**

## Reuse and boundaries from investigation
- Existing: `CustomerSelector`, `CustomerService`, `CustomerEndpoints`, `RevestikDbContext`, `ApplicationUser`, `PolicyNames`, `AuthenticationExtensions`, `CookieHandler`, `CsrfHandler`, `Home.razor`, QuestPDF and SQL integration fixture.
- Customer, Quotation and Sale remain authoritative in their own modules. Attach commercial PDF manually, no model changes without approval.
- File-storage infrastructure for Projects is **not yet established**; plan before coding. Avoid public URLs and nonpersistent container-local paths.
- Keep `docs/product/business-rules.md`, general architecture, security and testing docs untouched unless later approved.

## NEXT ACTION
1. Install this documentation-only checkpoint in repository `docs/projects/`.
2. Review `git status` and `git diff -- docs/projects`; stage **only** the three new files.
3. With owner's authorization, make a documentation-only commit/push to `main`, then record its actual SHA in this checklist during a future approved update.
4. Begin the **detailed B01 PLAN**, resolve critical outstanding B01-specific decisions and obtain approval. No entity/migration changes yet.

## Handoff template
Date / branch / local commit / remote commit / block / approved changes / files changed / migration status / targeted test results / full suite result / smoke result / known risks / next action. Never label unverified items completed.
