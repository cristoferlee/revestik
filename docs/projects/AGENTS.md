# Projects — Continuity instructions

**Module:** Projects (Customer-linked project dossier)  
**Checkpoint date:** 2026-10-13  
**Status:** Functional INVESTIGATE completed; technical INVESTIGATE completed at integration level. **IMPLEMENTATION NOT STARTED.**

## Read first in every new chat

1. `docs/projects/business-rules.md`: approved business decisions and unresolved details.
2. `docs/projects/checklist.md`: current phase, tasks, tests, Git checkpoints, and next action.
3. Only when needed, the existing repository docs: `docs/product/business-rules.md`, `docs/architecture/architecture-overview.md`, `docs/security/security-overview.md`, `docs/development/testing.md`.
4. Compare documentation with the **current local code**; local changes may be newer than GitHub.

The root `AGENTS.md` was a local Codex-era file and may be ignored by `.gitignore`; do not assume it is published or require it to work on Projects. This file documents the **web ChatGPT + ZIP patches** workflow and does not replace general project architecture.

## Operating agreement

Follow **INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP**.

- INVESTIGATE functional: ask the owner how the module should behave before proposing architecture. This phase is now completed for the initial scope.
- INVESTIGATE technical: read relevant actual source and dependencies, distinguish confirmed facts from assumptions. Avoid broad repository dumps.
- PLAN: present rules, data model, endpoints, contracts, UI, affected files, migrations, risks and test matrix. Await explicit owner approval.
- IMPLEMENT: deliver ZIP patch with complete files and paths relative to repository root; don't overwrite unrelated code. Do not use brittle mass string replacement.
- VERIFY: the owner runs build/tests/SQL migrations and reports the actual output. Validate security, CSRF, authorization, concurrency, relational integrity, regressions and visual smoke tests as appropriate.
- STOP after the approved block. Do not proceed into the next block without a new instruction.
- No git commit/push, migration application, data deletion or modification of historical business documents without explicit authorization.
- Keep commits/checkpoints scoped and documented. Before a new block, inspect `git status` and preserve unrelated changes.

## Technology and architecture

.NET 10, ASP.NET Core Minimal APIs, Blazor WebAssembly, EF Core, SQL Server, Identity + Google OAuth, cookie authentication, CSRF, xUnit and SQL Server Testcontainers. Pragmatic modular monolith; use existing API / Client / Shared boundaries, DTOs and services. Avoid microservices, unnecessary CQRS, generic repository, event bus and speculative abstractions. Server enforces security and business invariants; database protects uniqueness, foreign keys and transactions.

## Projects boundaries

- A Project belongs to exactly one existing Customer, though it may have multiple additional contacts. One Customer can have many Projects.
- Projects is an internal, manual documentary/operational dossier, not a replacement for Customer, Quotation, Sale, Inventory, Payments or fiscal accounting.
- Quotations and sales are manually attached as PDF evidence, not automatically linked/synced; entering a project payment does **not** pay an existing Sale or produce accounting entries.
- Timeline items, agreements, tasks, documents, manual payments/abonos, status, and progress are Project-specific.
- Project is managed by one responsible user, with Administrator intervention privileges. Enforce restrictions server-side.
- The business owner permits permanently deleting a Project and its own attached records/files, but this must never delete independent Customer, Quotation, Sale or other module data.
- No public customer portal, public file link, video attachments or editable SketchUp files.

## Existing source investigation

The owner provided three targeted ZIPs: `Revestik_Projects_Investigate.zip`, `Revestik_Projects_Investigate_02.zip`, `Revestik_Projects_Final_Investigate.zip`. The prior analysis identified: `Customer`, `CustomerService`, `CustomerEndpoints`, `CustomerConfiguration`, `CustomerSelector.razor`, `Quotation`, `Sale`, `RevestikDbContext`, `PolicyNames`, `AuthenticationExtensions`, `CookieHandler`, `CsrfHandler`, `Home.razor`, QuestPDF, and SQL Server integration test fixture. Revalidate each against local source before edits; the investigated HEAD was reported as `ae34ce013a43aa5618c423e813d2477fdbd8430c` and does not prove current GitHub/local equality.

## Communication, documentation and verification

Discuss decisions with owner in Spanish; write English identifiers, code and repository technical documentation; app-facing text in Spanish. Label **approved business rules** versus **technical proposals**, and **implemented** versus **planned**. Never invent test outcomes or assume code is committed. Update the checklist at the end of every verified block, including commit SHA only after it actually exists.

## Next action

Create/review the **detailed PLAN for Block 01 — Project foundation** (Customer FK, responsibility, unique name, states, contacts, optional location/dates, amount/currency, APIs, CSRF, authorization, EF configurations, tests). Resolve the specific open rules in `business-rules.md` before implementing relevant behaviors. **Do not implement until APPROVE.**
