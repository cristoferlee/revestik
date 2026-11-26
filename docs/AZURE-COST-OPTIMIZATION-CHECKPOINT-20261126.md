# Azure cost optimization closure — 2026-11-26

## Verified owner-reported evidence

- Source `68638b8118c9583662a85599736031eea080b365`; Release regression 1,409 passed, zero failed/skipped; Gitleaks and [CI](https://github.com/cristoferlee/revestik/actions/runs/45735248651) passed.
- Production revision `ca-revestik-prod--hotfix-sql-68638b8`, deployed 2026-11-26 01:40:07 UTC; 100% traffic; executable image digest `sha256:86dde5f18a6224f97af50c1be9ee49497cea4ef56f42df40a8b14f16e0973f79`.
- 58/58 migrations verified after four approved production migrations; five roles preserved. PublicCartItems clustered key maximum 904 bytes is tracked for FINAL-03A.
- SQL free offer disabled and existing database converted to Standard S0 (10 DTU), Online, 250 GiB configured maximum. No replacement database or new application deployment during conversion. Final objective verified 2026-11-26 around 18:25 UTC. Initial capacity-based CLI attempt failed; explicit service objective succeeded.
- SQL-independent liveness and development-only Identity seeding deployed. Health requests verified while SQL was paused before conversion. Database diagnostic is Administrator-only.
- After S0: 15/15 anonymous HTTP checks passed; root followed redirect to `/es` HTTP 200. Owner verified 11/12 manual navigation scenarios; Finishes outstanding. No commercial write acceptance or authenticated performance certification.
- First nine one-minute SQL samples: CPU average 1.31%, max 17%; physical reads average 0.35%, max 6%; workers average 0.83%, max 10%. Direct DTU metric unavailable then; no load certification.
- Former `app.revestikcr.com` OAuth references, custom hostname/certificate and DNS records retired; main domain and tested integrations retained. Default Azure FQDN remains an alternate entry.

## Recovery

Independent BACPAC export `RevestikDb-PRE-S0-20261126-181115.bacpac`, SHA256 `0D74995E3EA8CFAA0B920B82077FCDCFD5785AEDFF9B81BA4F5420C824529A22`, 1,515,473 bytes. Local SQL Server restore succeeded: 84 tables, 58 migrations, five roles, DBCC CHECKDB reported no errors. Not a full production count comparison. BACPAC excludes Azure Files. SQL retention 7 days, differential 12 hours, local redundancy; actual Azure PITR and file restore not tested. Keep independent backups protected outside Git. Temporary firewall rules removed.

## Economics (USD, estimated 30-day periods, pretax)

| Component | Estimate |
| --- | ---: |
| SQL S0 | 14.52 |
| Container Apps | 16.71 |
| Public IP | 3.68 |
| Backup | 2.97 |
| Storage and Key Vault | 0.87 |
| Revestik total | 38.75 |
| SkillSnap (unchanged) | 5.06 |
| Combined | 43.81 |

Original Serverless scenario Revestik approximately 95.17; projected reduction 56.42. Estimates use historical cost normalization and regional S0 pricing, not invoices; actual usage, free registry allowance and taxes may differ. Routine observation through 2026-12-10, not a release blocker by itself.

## ACA closure

Runtime/architecture audit complete. Current 0.5 vCPU / 1 GiB / one replica retained. Local benchmark was cancelled before comparative measurements; temporary SQL container and network removed, Git clean. No code or Azure capacity changes from ACA investigation. See ADR-006.

## Remaining boundaries

No commercial launch approval. Public Google registration/login remains disabled in Production. Public indexing, legal review, full functional acceptance, recovery procedures and database key remediation remain in final closure plan. Earlier unknown-path probes returned 401 for some paths; verify intended public 404 behavior without weakening private API authorization. No new infrastructure changes authorized.
