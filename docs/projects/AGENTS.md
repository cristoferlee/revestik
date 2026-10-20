# Projects — Continuity instructions

**Updated:** 2026-10-20. **Current phase:** B01–B07 published; B08 PDF and post-B07 UX/security improvements verified locally, pending Git closeout. The current working tree is newer than the published GitHub branch.

## Authoritative sources

1. `docs/projects/business-rules.md` — original 97 approved answers in PRJ-001–PRJ-037; authoritative functional baseline.
2. `docs/projects/b01-b02-decisions.md` — subsequent approved decisions; retain as historical design context.
3. `docs/projects/checklist.md` — latest implementation, validation, and release status.
4. `docs/archive/projects/` — historical B02/B03 checkpoints and B04-E verification notes, **not** current work instructions.
5. Current local source and `git status`, current GitHub refs, tests and migrations — verify before editing; newer local work must take precedence over old GitHub files.

## Implementation status

- **B01** — Customer-linked Projects, owner/administrator rules, agreed amount and versioning.
- **B02** — Activities, configurable categories, comments, agreements and history.
- **B03** — Optional weighted milestone plans, reusable templates and progress. Completing milestones never automatically closes a project.
- **B04** — Private versioned documents, event links and document storage.
- **B05** — Manual CRC/USD operational payments, audited correction and void, balance, agreed-amount history, documentary evidence.
- **B06** — Tasks, deadlines, lifecycle/closure and dashboard alerts.
- **B07** — Projects Blazor workspace, published in GitHub `main` at `8f13989` (verify remote HEAD again before release).
- **B08 and UX** — Local PDF dossier using QuestPDF with professional layout and official SVG logo; UX refinements for Projects listing, activity categories, milestones/templates, payment evidence selection and linked-document lookup. These local changes have not yet been confirmed published.
- **SEC-UX06-01** — Local finance write-lock guard for Completed/Cancelled/Archived projects; the user reported 26/26 focused tests and 193/193 Projects tests passed on 2026-10-20. Closed projects may be read but must reject financial/document-link writes even from Administrator.

## Non-negotiable business and security rules

Projects remain linked to Customers. Only the responsible user or Administrator may modify operational data, subject to project-state restrictions; authorized roles may read. All mutations must enforce authentication, CSRF, server-side authorization and project-scoped ownership. A closed project's records remain available for consultation while operational writes are rejected; this rule takes precedence over Administrator privileges.

Payment records are operational only; no automatic effects on Sales, SalePayments, accounting, bank reconciliation, inventory, XML or tax. Reject overpayments; preserve correction and void history; CRC and USD cannot mix or auto-convert. Agreed-amount modifications require audit history and cannot reduce below active payments. Documents remain private and must not be linked across projects. The existing private file storage must never be published with source.

## Working method

**INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP.** Spanish conversation, English code identifiers/documentation, Spanish user-facing UI. .NET 10 modular monolith, ASP.NET Core Minimal APIs, Blazor WebAssembly, EF Core / SQL Server, Identity, antiforgery, xUnit/Testcontainers and QuestPDF. Do not invent migrations, add unrelated frameworks or change business rules without approval.

User runs PowerShell in `C:\Projects\Revestik`; patches must preserve current local source and repository-relative paths. No commit, push, deletions or production migration without explicit approval. Verify staged files individually, especially **`src/RevestikPrivateDocuments/`**, which is untracked and must remain uncommitted unless explicitly reviewed and approved. Never use blanket `git add .` for this closeout. No automatic `.gitignore` additions for ZIPs, checkpoints or Markdown: these may be meaningful deliverables.

## Current next steps

1. Complete GH-01 documentation-only update and archive historic instructions; preserve their history.
2. Confirm `git status -sb`, local HEAD, actual remote HEAD, clean `git diff --check`, and staged scope; inspect any untracked directories before staging.
3. Confirm tests as needed; the user reported **193/193 Projects tests passed** (335.9 s) and build success (337.4 s) on 2026-10-20. This is **not** a full Revestik solution regression.
4. Obtain explicit approval for staging, commit and push of local B08/UX/security code and documentation; verify SHA and remote synchronization afterward.
5. STOP and prepare a current master checkpoint. Do not repeat already completed UI smoke tests without a specific change or regression reason.
