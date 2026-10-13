# Projects — B01/B02 approved follow-up decisions and implementation facts

Read together with `business-rules.md`. This supplements, **does not overwrite**, the 97-answer baseline. “Implemented” means introduced in source and reported tested locally; do not assume published B02 before commit.

## B01 follow-up decisions explicitly approved by owner

1. New Project starts at `Planning`.
2. Authenticated creator automatically becomes primary responsible user.
3. Only Administrator can reassign the responsible user.
4. Authorized roles can view other responsible users' Projects; only responsible user or Administrator can edit.
5. Agreed amount/budget is optional on creation and can change later.
6. Existing Projects remain readable under an inactive Customer; no new Projects may be created for inactive Customers.

## B01 implemented design

`Project` is linked to exactly one `Customer`, responsible `ApplicationUser` via restrictive foreign keys; `ProjectContact` is a dependent with cascading deletion. Unique SQL index `(CustomerId, NormalizedName)`; status enum; optional structured Costa Rican address, estimated dates, agreed amount with `decimal(18,2)` and CRC/USD coupling; `Version bigint` EF concurrency token; authenticated `ProjectEndpoints` with `ManageProjects` (Administrator, Accountant, Sales), admin reassignment, CSRF. Production B01 classes are published to GitHub as of this checkpoint, independently of B02 uncommitted files.

The *implementation currently restricts budget currency to CRC/USD* and normalizes trimmed Project name with `ToUpperInvariant`; precise future changes must be discussed, not silently assumed. Project lifecycle status transitions and final closure/reopen workflow are not yet fully built.

## B02 grouped delivery explicitly approved

- **A+B together:** category configuration; events/timeline; actual `OccurredOn` date, event title/description/category/author; update and logical void; original-value revisions; filters; server-side access checks.
- **C+D together:** immutable event comments, event-based agreements (`Pending`, `Fulfilled`, `Modified`, `Unfulfilled`), recorded acceptor/date/medium/notes, optimistic version and prior revisions, audit history read for administrators, CSRF and permission checks.
- **E alone:** SQL integration, HTTP authorization/CSRF, concurrency, regression, GitHub checkpoint.

**No UI smoke tests during blocks**. User requires only final UI smoke after B07/B08. Do not add temp README/scripts to ZIPs or source tree.

## B02 structural decisions / observed SQL

A+B migration `20261013215919_AddProjectActivityFoundation` creates `ProjectEventCategories`, `ProjectEvents`, `ProjectEventRevisions`. C+D migration `20261013221354_AddProjectEventDetails` creates `ProjectEventComments`, `ProjectEventAgreements`, `ProjectEventAgreementRevisions`. Categories have a normalized unique name. Event changes/voids preserve prior values in revisions; agreement revisions unique by `(ProjectEventId, PreviousVersion)`. Foreign keys from events to Projects and from histories/comments/agreements to events use Cascade **only when parents are physically removed**; normal event deletion must be *logical void*, retaining history. Category FK is non-cascading. Actor user IDs are stored, not foreign-keyed to AspNetUsers in these migrations; enforce server-authorized IDs and preserve history even if users are retired.

Event attachments are **B04**, agreement due dates and Dashboard alerts are **B06**, weighted milestones **B03**. Event types must be configured rather than used as arbitrary automatic progress. Project data is internal and not sourced automatically from Quotations/Sales.

## Open conditions, do not assume already satisfied

- Valid CSRF + authorized non-owner 403 HTTP tests of events/agreements remain to be confirmed.
- Event filter `hasAttachments` cannot be fully meaningful until B04, though future support was requested.
- Universal audit requirements (project create/edit/reassign/download etc.) exceed the event/agreement revision history currently added; check scope for later blocks.
- No numeric milestone weights approved. No customer portal, public file links, videos or editable SKP.
- Storage limits, payment overpayment/corrections, exact lifecycle permissions and final PDF detail are still undecided.
