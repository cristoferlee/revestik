# ADR-004 — Retain SQL-authoritative public content

**Status:** Accepted, 2026-11-26 (FINAL-02A).

**Context:** Persistent public snapshots were proposed primarily to avoid Serverless SQL wake-ups. Production now uses provisioned Standard S0.

**Decision:** Cancel OPT-03A/B, OPT-04 and OPT-08. Retain SQL as the authority for public content, visibility and current prices. Preserve existing cache and invalidation, server-side validation, independent public catalog and non-persisted preliminary PDFs. Do not introduce snapshot synchronization or a second source of truth.

**Consequences:** Public reads/PDFs may depend on SQL availability; handle failures safely without leaking private data or serving unauthorized content. Availability and performance remain acceptance criteria. Revisit only on demonstrated need and separate approval.
