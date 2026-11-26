# Revestik — Final closure plan

**Baseline:** 2026-11-26, `68638b8118c9583662a85599736031eea080b365`. FINAL-02A decisions accepted; implementation of each remaining block requires separate approval. This document does not authorize launch.

## Closed scope

- Persistent publication, SQL-independent public reads and PDF (OPT-03A/B, OPT-04/08): CANCELLED. SQL remains authoritative.
- Additional CDN/Cloudflare and ACA downsizing: CLOSED — NO CHANGE; experimental benchmark cancelled and cleaned up.
- Legacy iframe editor and previous administrative domain: COMPLETED. Preserve independent PDF preview.
- SQL health hotfix, migration to S0 and Azure audit: COMPLETED within recorded evidence; see Azure checkpoint.
- Historical Git staging/push obligations for already committed features: SUPERSEDED by current main; final acceptance not implied.

## Remaining finite scope

| Block | Acceptance / evidence required |
| --- | --- |
| FINAL-02B | Review documentation-only diff, preserve history, verify links and separately authorize commit/push. |
| FINAL-03A | Resolve PublicCartItems clustered key width while preserving FK, uniqueness, ownership, concurrency and existing data. Review migration and rollback; production application separately approved. |
| FINAL-03B | Decide and implement explicit public login/registration controls, returning-account behavior, employee isolation and revocation; verify anonymous/customer A/customer B/employee/admin, CSRF, OAuth redirects, cache and abuse protections. Reuse existing SQL ownership tests. |
| FINAL-03C | Define bounded image optimization, formats/limits and invalid-image handling; preserve visibility and media associations. Replace scheduled active orphan deletion with administrator-confirmed maintenance, revalidate references and concurrency before deletion. Temporary upload cleanup may remain automatic. Verify storage and safe failure behavior. |
| FINAL-03D | Obtain approved privacy/cookie/terms content and legal review; controlled SEO/indexing, canonical/robots/sitemap, public 404 and maintenance behavior; ES/EN/DE and accessibility acceptance. Noindex is not access control. Confirm actual prelaunch access posture. |
| FINAL-04 | Verify administrative commercial workflows, writes and invariants, public pages including Finishes, editor, guest/cart/PDF, responsive layouts and dashboard freshness. Assess existing cache before changing refresh behavior. |
| FINAL-05 | Evidence-driven duplicate/dead-code review, dependencies and maintainability; preserve business/security rules, verify each bounded change. No rewrite or deletion based only on search matches. |
| FINAL-06 | Exact candidate build/regression/security checks, recovery procedure and evidence (SQL PITR and file recovery gaps), independent backups, deployment/rollback plan, production smoke and explicit launch/indexing approval. |

## Workflow and limitations

Use terminal states from AGENTS.md. Transferred blocks are tracked only here, not as separate obligations. Historical test counts are evidence for their exact baseline. Existing public cart SQL ownership tests and media cleanup tests are not proof of complete release acceptance. Do not silently cancel remaining requirements; record a specific owner decision if any scope becomes unnecessary. No automatic code changes, commit, push, migration, deploy, DNS, OAuth or indexing authorization. Routine cost and capacity observation is operational maintenance, not an unfinished feature.
