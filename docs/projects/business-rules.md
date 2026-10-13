# Projects — Approved business rules

**Baseline:** Owner's answers 1–97 and subsequent clarifications, consolidated 2026-10-13.  
**Status:** Approved functional requirements, **not implemented**.  
Rules prefixed `PRJ-` are **Planned**, not verified software behavior. Examples and recommendations are not automatically approved numeric rules.

## Scope and Customers

- **PRJ-001** — Projects represents a dossier for works/services supplied to a construction-materials customer: houses, kitchen furniture, renovations, finishes, supplies, sometimes installation or freight. Revestik mainly supplies and coordinates materials, rather than operating an entire construction business. (1,6,7)
- **PRJ-002** — A project may be registered before an accepted quotation or contract; can exist with no quotation, sale or contract, and may be created retrospectively. (2,3,8)
- **PRJ-003** — A Customer may have multiple simultaneously active Projects. Each Project belongs to **exactly one existing Customer**; additional contacts do not become co-owner Customers. (4,5,92)
- **PRJ-004** — Projects is an independent module that selects existing Customers. Customer master data is edited in Customers and reflected in Projects. (9,16,67,68)
- **PRJ-005** — Projects have a free-form name, not a visible mandatory code. The same Customer cannot have two projects with the same name; show a user-friendly duplicate message. Names like 'Casa 1'/'Casa 2' differ. (10,11, final observation)
- **PRJ-006** — Project-specific address optional and should reuse the established Costa Rican structured-location approach; estimated start/end dates optional. (12,13,18)
- **PRJ-007** — One or multiple Project-specific contacts permitted, with their role/contact details. (14,15)
- **PRJ-008** — No mandatory project-type classification. (17)

## Lifecycle and ownership

- **PRJ-009** — States: Planning, PendingApproval, Active, Paused, Completed, Cancelled, Archived; state changes are manual, attributed to a user and date. (19–21)
- **PRJ-010** — Completed means agreed work fulfilled; a completed Project may reopen and receive new warranty/follow-up records. Paused means temporary suspension. Archived means retained without current activity, including projects the client indefinitely postpones; cancellation is distinct. (22–24,87,88,97)
- **PRJ-011** — Project can be permanently deleted along with **its own** files/events/records; never delete its Customer or independent sales/quotations/accounting records. Deactivation of Customer must not automatically erase its dossier. (25,26,93)
- **PRJ-012** — One primary responsible user per Project. **Only responsible user edits**, Administrator may intervene. Creation, visibility, reassignment and read-only access matrix still need precise definition. (76,77,78,96)
- **PRJ-013** — Significant actions (create, edit, status changes, void, download, relevant sensitive access) should support an administrative audit history. (33–35,80)

## Timeline and manual records

- **PRJ-014** — Timeline sorted newest-first, manually added events with title, description, actual event date, type, comments, and multiple attachments. Backdated events allowed. (27,28,31,36)
- **PRJ-015** — Event categories are administrator-configurable: advances, meetings, agreements, scope changes, payments, incidents, deliveries etc. (29,30)
- **PRJ-016** — User-facing event date is the **actual date of occurrence**, not a second visible date. Technical recorded/modified timestamps may exist for audits. Show creator and modifier. (32,33)
- **PRJ-017** — Corrections preserve previous values through auditing. Events can be voided without deleting their supporting history/evidence. Users can append comments without overwriting the original entry. (34,35,37)
- **PRJ-018** — **No automatic entries from Quotations, Sales or other modules.** Events and commercial PDF uploads are manually recorded. Filters: category, date, user and whether attachments exist. (38,39,69–72)

## Documents

- **PRJ-019** — Internal image, PDF, Word and Excel uploads; images should be resized/compressed to reduce hosting/storage cost. Plan captures/screenshots are allowed, including SketchUp screenshots, but **no editable SketchUp documents or videos**. Avoid heavyweight files. (40, final observation)
- **PRJ-020** — Allow linking files to general Project dossier and/or one or multiple events; categorize contracts, plans, photos, receipts, deliveries, other. (42,43,75)
- **PRJ-021** — In-app image/PDF preview; photo gallery and before/after comparisons. Preserve versions when a document is replaced. (44–46)
- **PRJ-022** — Users may permanently remove files. Access is private/internal with authorized download; no public sharing or customer portal. (47,50,81)
- **PRJ-023** — No requirement for advanced per-file metadata, full-text searching, or file approval/rejection workflows. (48,49,51)
- **PRJ-024** — Maximum upload size to be recommended/validated during technical PLAN; no number approved yet. (41)

## Agreements, issues, planning and progress

- **PRJ-025** — Agreement is a normal timeline event, with manually recorded acceptance (who, when, medium) and state Pending/Fulfilled/Modified/Unfulfilled. (52–54)
- **PRJ-026** — Manual changes of scope, project incidents, evidence, responsible party and resolution must be documented. No separate formal client-approval workflow. (55,56,58)
- **PRJ-027** — Agreements/incidents/tasks may have due dates. Upcoming/overdue alerts appear in the existing Dashboard; future activities can have owners. (57,65,66,85)
- **PRJ-028** — All Projects can run in a simple way without mandatory stages. When stages are used, they may include status, owner, dates and documents; reusable stage templates undecided. (59–62)
- **PRJ-029** — Visible progress bar automatically changes according to **predefined event/milestone percentages**, not arbitrary counts or image uploads. Closing the project may complete 100%. Exact milestones, weights and rollback/void rules remain to be decided. No example 5%/30%/45% distribution is approved. (59,63,64,94)

## Commercial references and payments

- **PRJ-030** — Users manually download quotations/sales PDF from their existing modules and upload them into Projects. Multiple quote/sale documents possible; no automatic source linkage or conversion propagation. (69–73)
- **PRJ-031** — Users can record the total amount agreed manually and register individual payments/abonos with real payment date and uploaded payment evidence; abonos reduce the displayed remaining balance. A receipt can evidence several events. (73–75,91,95)
- **PRJ-032** — Project amounts/payments are **manual operational tracking**, separate from authoritative Sales payments or fiscal accounting. Creating a Project payment must not mark Sale as paid, add accounting entries or alter inventory. (71–75,91)
- **PRJ-033** — Currency, overpayment, corrections/voids and changes to agreed amount require final design decisions before financial implementation.

## UX, PDF and delivery

- **PRJ-034** — Modules workflow: Projects → select Customer → select Project → tabs: Summary, Activity, Documents, Agreements, Payment Receipts, Plans. Summary + activity accessible naturally; search by Customer name; responsive for field-photo uploads. (82–86)
- **PRJ-035** — Formal close with date, observation/evidence and later follow-up entries. No client login. (87,88,81)
- **PRJ-036** — Lightweight downloadable Project report PDF is desired if reasonable effort/resource usage; should not embed all heavyweight files by default. (89)
- **PRJ-037** — Full discussed functional scope is desired, but delivery is approved in incremental verified blocks, not one giant change. (90 and subsequent approval)

## Outstanding design decisions (not silently approved)

1. Required fields and initial status at Project creation; status transitions, Completed/Archived/Cancelled policies, close/reopen permissions.
2. Name normalization/case/accent handling and uniqueness among different Customers.
3. Exactly who can create/read/assign/reassign; visibility of other owners' data and access to downloads.
4. Initial event-category catalog and how administrator configures it.
5. Milestone catalog and weights (including non-selling projects, duplicate milestone, rollback, stage optionality).
6. Upload quotas, accepted subtype/MIME, file size, private storage location/cloud portability, virus-risk handling and what deleting a multiply referenced file does.
7. Budget/payments currency policy, overpayment, void and audit, exchange rates, changes to agreed amount and whether evidence is mandatory.
8. Meaning of any formal close approval, re-open, archived vs paused workflows, and PDF report extent.

**Do not treat recommendations from INVESTIGATE as settled business decisions.**
