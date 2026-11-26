# Revestik - Optimization Master Plan

> **Closure decision — 2026-11-26 (FINAL-02A, owner approved).** The original plan below is retained as historical evidence, not an active implementation checklist. Where it conflicts with this resolution, this resolution prevails. See [ROADMAP.md](ROADMAP.md) and [final closure plan](REVESTIK-FINAL-CLOSURE-PLAN.md).

| Block | Resolution |
| --- | --- |
| OPT-01A, OPT-02, OPT-14 | COMPLETED |
| OPT-03A/B, OPT-04, OPT-08 | CANCELLED: no persistent publication or SQL-independent public content/PDF requirement |
| OPT-11, ACA-01 | CLOSED — NO CHANGE |
| ACA-02 through ACA-05 | CANCELLED; experimental resources removed, no capacity certification |
| OPT-05/06 | TRANSFERRED to FINAL-03C |
| OPT-07 | TRANSFERRED to FINAL-04; verify existing dashboard cache before changing |
| OPT-09/12 | TRANSFERRED to FINAL-03D |
| OPT-10 | TRANSFERRED to FINAL-03B |
| OPT-13 | TRANSFERRED to FINAL-05 |
| OPT-15/16 | TRANSFERRED to FINAL-06; ordinary cost observation is operational maintenance |

SQL S0 remains the authoritative data source. Preserve existing publication visibility, cache invalidation, security and data integrity. Requirements specific to publication artifacts, regeneration, SQL-free public reads and SQL auto-pause are superseded; ordinary availability/error handling remains required. Other requirements are not silently cancelled. Image optimization and administrator-confirmed orphan cleanup remain approved but unimplemented in this closure. No code, Azure or launch authorization follows from this document.

---

## Historical plan (2026-11-25)


## 1. Document control

- Identifier: OPT-MASTER-01
- Created: 2026-11-25
- Requirements status: Owner approved
- Documentation authorization: OPT-01A
- Implementation status: Planned, except explicitly recorded local changes
- Repository baseline: 880b3d4350c18c4a9cde0147e7d9b4079d47b045
- Baseline commit: perf: optimize sales, fiscal summaries and CAByS queries
- Production deployment: Not independently verified against this baseline
- Working branch: main
- Owner communication language: Spanish
- Repository documentation language: English

This document consolidates the seven owner-approved operational requirements
blocks and establishes the optimization roadmap for Revestik.

It does not authorize application changes, infrastructure changes, database
migrations, DNS changes, resource deletion, commits, pushes or deployments.

Existing normative business rules and accepted architectural decisions remain
effective until explicitly revised through the approved engineering workflow.

## 2. Mandatory engineering workflow

INVESTIGATE -> PLAN -> APPROVE -> IMPLEMENT -> VERIFY -> STOP.

Every implementation block must:

1. Inspect the actual source, contracts, persistence and relevant tests.
2. Identify confirmed facts, assumptions and missing evidence.
3. Define a bounded implementation scope.
4. Obtain explicit owner approval.
5. Preserve unrelated local changes.
6. Execute focused verification and appropriate regression tests.
7. Report verified results and unresolved risks.
8. Stop at the next approval boundary.

No blanket staging, destructive Git operations or production modifications.

Commit, push and deployment remain separately authorized activities.
Accumulated local changes are to be preserved until the final audit.

## 3. Business objectives

Revestik is a corporate presentation, product discovery and lead-generation
website combined with an authenticated business administration application.

It is not currently intended to operate as:

- A high-volume marketplace.
- A public clone of the internal business system.
- A multi-tenant SaaS platform.
- A direct online checkout or payment-processing platform.

The optimization objective is to eliminate unnecessary infrastructure
consumption while retaining a responsive, professional and secure experience.

The existing public visual design, navigation, multilingual experience,
galleries, carousels, project portfolio, material hotspots and editorial
capabilities must not be degraded solely to reduce hosting costs.

## 4. Owner-approved requirements

### 4.1 Block 1 - Commercial operation

| Requirement | Approved target |
| --- | --- |
| Public website purpose | Product presentation and quotation leads |
| Initial daily visitors | Fewer than 50 |
| Concurrent administrators | 1 to 3 |
| Administrative usage | Business hours with occasional off-hours access |
| Idle database behavior | Suspend and reactivate on demand where applicable |
| Public responsiveness | Fast, independently of normal SQL suspension |
| Administrative cold start | Accept a reasonable initial wait |
| Monthly infrastructure budget | USD 15 to 25 |

The owner accepts higher costs when supported by genuine commercial activity.

The owner suspects that a significant portion of current public traffic is
automated because the website is incomplete and has not been formally launched.

This suspicion is not a verified traffic attribution.

### 4.2 Block 2 - Public content and availability

| Requirement | Approved target |
| --- | --- |
| Content updates | Mainly when new products or projects arrive |
| Individual publication latency | Less than 60 seconds |
| First-year public catalog | 100 to 500 products |
| Database-independent browsing | All operations not requiring persistence or authentication |
| Commercial unpublication | May wait until the next scheduled or explicit publication |
| Historical public photographs | Permanent retention not required |
| Image processing | Automatic web optimization |
| Search-engine indexing | Desired |

The owner selected temporary maintenance when SQL is suspended or inaccessible,
while also requesting normal public browsing without waking SQL.

The proposed interpretation is:

- Normal SQL suspension must not prevent browsing valid published content.
- Valid published content should remain available independently of SQL.
- Missing or invalid published content should return a maintenance response.
- Recovery should be explicitly initiated by an authorized administrator.

This interpretation must be confirmed within the publication design.

Search-engine indexing is an approved business objective, but changing the
current launch gate, metadata or production indexing configuration requires
a separately approved release action.

### 4.3 Block 3 - Administrative operations

| Requirement | Approved target |
| --- | --- |
| Daily commercial operations | Fewer than 20 |
| Initial SQL reactivation target | Up to 15 seconds |
| Financial dashboard | Daily refresh |
| Gmail and bank-voucher synchronization | Explicit manual requests |
| Public preliminary proforma | Use public data without SQL persistence where feasible |
| Public Google OAuth | Optional |
| Visitor analytics | Not required |
| Publication failure | Preserve previous valid publication and warn administrator |
| Permanent maintenance | Manual, through administration |

A preliminary public proforma must remain distinct from an official persisted
commercial quotation.

Existing quotation identifiers, commercial calculations, sales, inventory,
fiscal documents and business transactions must retain their established
server-authoritative behavior.

### 4.4 Block 4 - Storage, images and publication

| Requirement | Approved target |
| --- | --- |
| Upload devices | Desktop and mobile |
| Original image size | Variable |
| Original-resolution retention | Not required after verified optimization |
| Individual content editing | Publish automatically after saving |
| Bulk editing | Save multiple changes and publish together |
| Missing or damaged photograph | Hide the photograph and preserve remaining content |
| Animated category GIFs | Replace with static photographs |
| Orphan cleanup | Analyze first, explicitly confirm deletion |
| Storage reporting | Total occupied space only |

The application must preserve the existing visual quality required to
represent porcelain, ceramic and other material textures.

An image optimization operation must not destroy the only valid source file
before its replacement has been generated and validated.

Existing GIF content requires a controlled transition. Removing animated
image support must not silently invalidate existing production content.

### 4.5 Block 5 - Security and recovery

| Requirement | Approved target |
| --- | --- |
| Recovery Point Objective (RPO) | Up to 24 hours of operations |
| Recovery Time Objective (RTO) | Up to 24 hours |
| Dedicated public photograph backups | Not required |
| Commercial and fiscal data protection | Required |
| Independent off-Azure backup | Required for important business data and documents |
| Maintenance authorization | Primary administrator only |
| Administrative devices | Mainly personal desktop and mobile |
| Authentication | Google OAuth, authorized emails and Google MFA |
| Commercial audit history | Preserve commercial operations and sensitive changes |
| Alerts | Email for relevant incidents and costs |

Existing Azure SQL automated backups should be preserved.

Public marketing photographs are distinct from private business documents,
project evidence, fiscal XML files and banking documentation.

The latter remain within the business recovery scope.

RPO and RTO are operational objectives, not verified recovery guarantees.

The MFA requirement must be validated against the actual Google account,
OAuth and application authorization configuration.

### 4.6 Block 6 - Performance and abuse protection

| Requirement | Approved target |
| --- | --- |
| Public page load | Less than one second under defined normal conditions |
| Abusive traffic | Temporary blocking |
| Missing products and images | HTTP 404 without SQL |
| Invalid or missing publication | Maintenance and manual regeneration |
| Excessive anonymous PDF generation | Require authentication |
| CAPTCHA | Adaptive protection against suspicious traffic |
| Web health check | No SQL connection |
| Extended SQL reactivation | Loading feedback and up to 60 seconds waiting |
| HTTP logs | Detailed technical request logging |
| Scaling | Review measured demand before increasing resources |

CAPTCHA must complement, not replace, rate limiting.

Successful CAPTCHA validation and authenticated sessions must not grant
unlimited request volume.

Detailed HTTP logging is operational telemetry, not a visitor analytics system.
It must not use the commercial SQL database for per-request persistence.

Logging must exclude credentials, authentication tokens, session cookies,
secrets and unnecessary personal information.

### 4.7 Block 7 - Infrastructure and costs

| Requirement | Approved target |
| --- | --- |
| Database alternatives | Evaluate Azure and other hosting options |
| SQL auto-pause | Evaluate 15 minutes with verification |
| Container Apps replicas | Compare always-available and scale-to-zero behavior |
| Cloudflare | Evaluate CAPTCHA, edge protection and CDN |
| Budget exceptions | Accept when supported by genuine commercial activity |
| HTTP log retention | 30 days |
| Unnecessary resources | Evaluate removal with separate authorization |
| Alternative providers | Evaluate without migration commitment |
| Post-release measurement | 14 days |

No SKU, hosting provider, DNS or billing configuration change has been
authorized by these requirements alone.

## 5. Architecture to preserve

Revestik remains a .NET 10 modular monolith.

| Component | Responsibility |
| --- | --- |
| Revestik.Api | Trusted host, Minimal APIs, business services and security |
| Revestik.Client | Blazor WebAssembly administration |
| Revestik.Public | Multilingual Razor Components and public SSR |
| Revestik.Shared | Shared transport contracts |
| Revestik.Api.Tests | Automated verification |

Preserve:

- ASP.NET Core Identity and Google OAuth.
- EF Core and SQL Server as the initial database baseline.
- Existing authorized API boundaries.
- Existing CSRF and resource-level authorization safeguards.
- QuestPDF and existing document generation services.
- Existing public DTOs wherever practical.
- FIFO inventory provenance and transactional business rules.
- Commercial and sensitive-operation audit records.
- Existing local development and Testcontainers verification patterns.
- One unified application host and domain unless evidence supports a change.

Do not introduce microservices, a distributed message broker, a second
authoritative business database or a generic abstraction layer without
a separately justified and approved design.

## 6. Confirmed source and audit findings

The following observations originate from the repository inventory and
architecture audit supplied by the owner on 2026-11-25.

### 6.1 Public read paths

Public pages currently use API readers for catalog, projects and hero content.

The HTTP loopback mechanism is already established within the unified host.
No demonstrated benefit currently justifies replacing it wholesale.

Retain the existing public reader contracts while investigating whether their
data source can be changed behind the API boundaries.

The complete anonymous and authenticated request paths still require tracing,
including authentication middleware and session validation.

### 6.2 Existing output cache

Public Home and featured content use approximately 30-minute output-cache
policies. Solutions content uses a 10-minute policy.

Memory caching is already registered.

Time-based expiration alone cannot guarantee that infrequent public requests
will avoid activating SQL.

Existing cache invalidation must be understood before introducing another
content publication or caching mechanism.

### 6.3 Public media

The existing application-controlled storage lifecycle includes pending and
active public media files.

Public media access currently uses database-backed visibility and metadata.

Catalog image retrieval has a known path that can perform two SQL reads
for a referenced image before serving the stored file.

Reducing that path to one SQL query is a possible fallback optimization.
Eliminating SQL from ordinary published-image retrieval is the preferred
design objective, subject to validation.

Private project documents and other administrative media must not be
included in the public media index.

### 6.4 Financial dashboard

DashboardFinancialCache already provides memory caching, manual refresh and
single-flight protection.

The audited implementation uses a 60-second lifetime.

The proposed adaptation is a daily refresh on administrative demand,
with an optional explicit refresh and a visible calculation timestamp.

The design must account for Costa Rica local dates, different reporting
parameters, currency, authorization, application restarts and bounded memory.

No scheduled midnight SQL query is required.

### 6.5 Gmail integration

The existing Gmail synchronization design is manual and request-driven.

Preserve the established background processing mechanism for explicitly
requested jobs, including duplicate prevention, retry behavior, checkpoints
and single-flight safeguards.

A hosted service is not necessarily an unnecessary periodic database consumer.

### 6.6 Health endpoint

The existing /api/health endpoint calls Database.CanConnectAsync.

The proposed operational health check must verify application liveness
without establishing a SQL connection.

Existing automation, deployment checks and monitoring consumers must be
identified before changing the existing endpoint contract.

A separate database connectivity check may be retained for explicit,
authorized operational diagnosis.

### 6.7 Public preliminary proforma

The current public proforma endpoint receives RevestikDbContext,
PublicCatalogMediaStorage and PublicProformaPdfService.

It reads product, unit and image information before rendering the PDF.

The existing renderer can potentially be reused with validated published
data instead of database-backed reads.

Preserve authoritative prices, quantity constraints, currency behavior,
item limits, duplicate validation, origin safeguards, image limits and
non-cacheable personalized PDF responses.

Do not create official commercial quotations or inventory movements from
an anonymous preliminary proforma.

### 6.8 Rate limiting

Existing audited policies include:

| Policy | Current limit |
| --- | --- |
| Global | 600 per minute |
| Login | 5 per minute |
| Health | 60 per minute |
| Gmail synchronization | 3 per 5 minutes |
| Public cart | 60 per minute |
| Public proforma | 6 per minute |
| Document upload | 10 per minute |

Reuse the established ASP.NET Core rate-limiting mechanism.

Investigate middleware order, trusted forwarded headers, internal loopback
requests, partition keys and concurrency before defining new limits.

### 6.9 Media cleanup

The local monthly maintenance implementation currently has:

- A six-hour maintenance check interval.
- A 30-day active orphan cleanup interval.
- Persistent scheduling metadata.
- File-based mutual exclusion.
- Independent pending-file cleanup.
- Database-backed protection of referenced active files.

The owner subsequently selected manual permanent maintenance.

The automatic active cleanup schedule is therefore superseded as a target
requirement, but the local code must be preserved until its replacement
is approved and verified.

Reuse its safety protections wherever appropriate.

## 7. Proposed public publication architecture

### 7.1 Objective

Ordinary anonymous navigation, search, filtering, project browsing,
image retrieval and missing-resource checks should use published content
without issuing SQL commands.

SQL remains the authoritative source for editorial changes, authentication,
private business operations and other persistence requirements.

### 7.2 Candidate approach

Investigate a persistent, versioned public read model stored outside SQL.

The candidate is a bounded set of public DTO projections and a public media
index, derived from validated authoritative content.

Possible publication artifacts include:

- Publication manifest.
- Home content.
- Categories and subcategories.
- Public products and translations.
- Public projects and material references.
- Solutions content.
- Public media index.

Exact serialization formats, file layout, service boundaries, storage
implementation and publication coordination are not yet approved.

### 7.3 Publication behavior

The proposed publication flow is:

1. Authorize and validate the editorial operation.
2. Persist authoritative changes in SQL.
3. Generate a complete candidate public publication.
4. Validate its structure, visibility and media references.
5. Activate the validated publication atomically.
6. Invalidate affected public caches.
7. Report publication success or failure.

For bulk editing, multiple authoritative changes may be grouped into
one explicit publication operation.

The individual save-and-publish path should remain convenient.

A failed publication must not be reported as successful.
The previous valid publication should remain available, subject to
urgent content withdrawal requirements.

The design must explicitly handle the failure boundary between SQL commit
and publication activation.

### 7.4 Publication safety

Mandatory design considerations:

- Export only explicitly authorized public fields.
- Never serialize complete EF Core entities into public storage.
- Preserve existing publication and taxonomy visibility rules.
- Prevent leakage of internal prices, costs, customers or private documents.
- Coordinate overlapping editorial operations and publication attempts.
- Prevent an older publication from replacing a newer publication.
- Preserve consistency across multi-file publications.
- Validate schema compatibility across application versions.
- Survive application restart without unnecessary SQL reconstruction.
- Protect media referenced by the currently active publication.
- Distinguish ordinary commercial unpublication from urgent privacy removal.
- Define safe cache invalidation and CDN purge behavior.
- Avoid unbounded stale content fallback.
- Provide manual recovery when publication artifacts are unavailable.
- Do not automatically query SQL on each failed public request.

Atomic filesystem behavior and locking must be validated on the actual
production storage implementation.

### 7.5 HTTP behavior

| Condition | Target behavior |
| --- | --- |
| Valid public resource | HTTP 200 |
| Missing or unpublished public resource in active publication | HTTP 404 |
| Missing individual photograph | Hide photograph in the page |
| Unavailable or invalid publication | HTTP 503 maintenance |
| Unauthorized internal resource | Preserve current authorization behavior |
| Excessive requests | HTTP 429 or approved challenge response |

A missing public resource must not fall back to a SQL query solely to
confirm its absence.

## 8. Proposed media strategy

Investigate automatic optimization during upload or publication.

Initial technical candidates:

- WebP for compatible static photographs.
- Catalog dimensions around 960 pixels.
- Detail and hero dimensions around 1920 pixels.
- Responsive image selection.
- Lazy loading below the visible viewport.
- Orientation correction.
- Removal of unnecessary embedded metadata.
- No upscaling of small source photographs.
- Validated replacement before original disposal.
- Versioned image URLs where appropriate.

These dimensions and formats are proposals, not approved fixed encoding
parameters.

Evaluate whether one or two variants per photograph are actually required.

Server-side validation must protect against malformed files, excessive
decoded pixel dimensions, resource exhaustion, misleading content types
and unsafe file paths.

Existing image references and production content must be migrated safely.
No destructive bulk conversion is authorized.

Public media responses must be served only through the authorized active
publication or an equivalent verified visibility boundary.

## 9. Proposed maintenance strategy

Permanent active-media cleanup should be manually initiated from administration.

Candidate capabilities:

- Report total multimedia storage usage.
- Analyze potential orphan files.
- Present results before destructive operations.
- Require explicit confirmation.
- Revalidate references immediately before deletion.
- Protect current SQL references and active publication references.
- Coordinate cleanup with upload and publication operations.
- Record the authorized actor and operation result.
- Support manual publication regeneration.

Maintenance requires a server-enforced primary-administrator authorization
boundary. The existing general Administrator role must not automatically
grant this new exclusive capability without approval.

The mechanism used to identify the primary administrator remains a design item.

Temporary pending-file cleanup may remain automatic if it performs no SQL
queries and preserves in-progress uploads.

Confirm this distinction with the owner during the maintenance PLAN.

## 10. Security, CAPTCHA and edge protection

Evaluate Cloudflare Free and Turnstile as optional infrastructure enhancements.

Candidate objectives:

- Edge filtering of abusive requests.
- CDN caching of eligible public media and assets.
- Adaptive browser challenges.
- Server-side CAPTCHA token validation.
- Temporary blocking of excessive traffic.
- Independent limits for expensive PDF generation.
- Authentication after excessive anonymous PDF requests.
- Continued limits for authenticated users.

Do not apply a CAPTCHA to each image request.

Do not allow untrusted forwarded headers to bypass rate limiting.

Validate actual source IP handling, trusted proxies, direct-origin access,
HTTPS, OAuth callbacks, cookies and CSRF behavior.

Do not cache:

- Administrative pages or protected API responses.
- Authenticated personalized content.
- Authentication callbacks.
- Private carts.
- Personalized proforma PDFs.
- Private documents.
- Editor-specific content or privileged data.

Cloudflare integration is a candidate, not an approved provider or DNS change.

CAPTCHA is not a complete defense against distributed denial-of-service
attacks. Origin protection and resource limits remain necessary.

## 11. Logging and operational telemetry

Approved HTTP log retention: 30 days.

Investigate a bounded operational logging destination independent of SQL.

Candidate fields:

- UTC timestamp.
- Request identifier.
- HTTP method.
- Sanitized route.
- Response status.
- Duration.
- Cache outcome where available.
- Rate-limiting or challenge outcome.
- Relevant sanitized exception information.

Avoid logging raw authentication headers, cookies, sensitive query parameters,
tokens, request bodies and unnecessary personally identifiable information.

Define retention enforcement, storage limits and behavior during unusually
high traffic.

Keep operational HTTP logs separate from business audit records.

The owner does not require a separate visitor analytics product.

## 12. Backup and recovery strategy

Preserve existing managed Azure SQL backups while reviewing their current
retention, restore capabilities and cost.

Investigate an encrypted independent backup for essential business data
and important private documents outside Azure.

The recovery plan must include:

- Database restoration.
- Commercial and fiscal document availability.
- Private application storage.
- Required configuration and secrets recovery.
- Data Protection recovery where applicable.
- Application deployment reproducibility.
- Consistency between restored database and associated files.
- Restore verification against the approved RPO and RTO.

Dedicated backups of public marketing photographs are not required.

Avoid unnecessary SQL activations for backup operations where practical,
without violating recovery objectives.

Do not claim a 24-hour RPO or RTO until the corresponding recovery process
has been demonstrated.

## 13. Infrastructure alternatives

### 13.1 Current Azure Serverless

Evaluate retaining:

- Azure Container Apps.
- Azure SQL General Purpose Serverless.
- Existing persistent storage.
- Existing registry and secret-management resources.
- Existing backup services.

Investigate:

- Fifteen-minute SQL auto-pause.
- Actual causes of resume and pause activity.
- Free-offer eligibility and consumption.
- Container replica and idle-consumption behavior.
- Startup database access.
- Storage and network costs.
- Detailed logging with 30-day retention.

### 13.2 Azure SQL Basic and Standard S0

Evaluate provisioned DTU-based alternatives.

Basic is a candidate for predictable database cost and removal of the
serverless database resume delay.

Evaluate:

- Actual database data size and growth.
- Current feature compatibility.
- SQL resource limits.
- CAByS query behavior.
- Financial report performance.
- Fiscal document processing.
- Concurrent administrative operations.
- Migration and restore behavior.
- Regional pricing and backup charges.

Reference estimates discussed with the owner:

- SQL Basic: approximately USD 5 per month.
- SQL Standard S0: approximately USD 15 per month.

These are preliminary planning figures, not verified subscription quotes.

Do not convert the existing production database to another tier solely
for experimentation.

Verify free-offer conversion restrictions and reversibility first.

### 13.3 External hosting alternatives

Evaluate VPS hosting and alternative managed database providers only when
the complete cost and operational responsibility justify further investigation.

Include:

- Compute and memory.
- Database licensing.
- Backups and recovery.
- Firewall and patch management.
- Network transfer.
- Persistent storage.
- TLS and DNS.
- Operational monitoring.
- Migration effort and compatibility.
- Security and support responsibilities.

A PostgreSQL migration is not currently preferred because Revestik is
implemented and tested against SQL Server.

No database-engine migration is approved.

### 13.4 Infrastructure decision criteria

Select the infrastructure only after comparing measured workloads and:

- Total recurring cost.
- Public response latency.
- Administrative performance.
- Security.
- Operational complexity.
- Backup and restore capability.
- Growth capacity.
- Migration and rollback risks.

The monthly target remains USD 15 to 25, excluding any future approved
commercial growth requirements.

## 14. Azure consumption baseline

Owner-supplied Azure evidence reported:

- General Purpose Serverless GP_S_Gen5_2.
- Minimum SQL capacity of 0.5 vCore.
- Auto-pause configured at 60 minutes.
- Free-offer usage enabled.
- Billing over the free limit enabled.
- Container Apps minimum and maximum replicas of one.

These values must be revalidated during the final Azure audit.

Reported app_cpu_billed averages:

| Period | Approximate daily reported value |
| --- | --- |
| November 1-10, 2026 | 2,841 |
| November 11-15, 2026 | 19,922 |
| November 16-24, 2026 | 18,322 |

The significant increase began on November 11.

The owner reported that additional SQL billing began around November 16,
after consumption of the free allocation.

There is an unresolved discrepancy between cumulative reported compute
metrics and the apparent billing start date. Do not assume that the
available metric totals exactly represent the billed free allocation.

Potential contributors include:

- Public requests distributed across the day.
- Database-backed image retrieval.
- Time-based cache expiration.
- Serverless auto-pause configuration.
- Periodic media maintenance.
- Database connectivity health checks.
- Administrative and deployment activity.

The available evidence does not establish a single root cause or prove
that malicious bots account for most database consumption.

Measure actual request paths and SQL behavior before attributing savings.

## 15. Existing rules requiring controlled review

| Rule | Required review |
| --- | --- |
| PUB-004 | Published visibility projection |
| PUB-008 | Individual and grouped publication |
| PUB-009 | Image optimization and static-image transition |
| PUB-010 | Published-media authorization |
| PUB-011 | Deletion and active-publication reference protection |
| PUB-015 | Featured product visibility and ordering |
| PUB-016 | Public launch and indexing authorization |

Other existing public project, cart, taxonomy, authentication and media
rules must also be traced before implementation.

This plan does not replace or silently amend BUSINESS_RULES.md.

Update normative rules only when the corresponding changed behavior has
been explicitly approved.

Preserve accepted historical ADRs. Add a new ADR for a significant new
architecture decision when its design is accepted.

## 16. Implementation roadmap

The identifiers below establish proposed bounded work areas. Individual
implementation approval remains mandatory.

| Block | Scope |
| --- | --- |
| OPT-01A | Master Plan and requirements consolidation |
| OPT-02 | SQL-independent web health check |
| OPT-03A | Investigate and design persistent public publication |
| OPT-03B | Implement and verify the publication foundation |
| OPT-04 | Public content and media reads without SQL |
| OPT-05 | Automatic image optimization |
| OPT-06 | Manual administrative maintenance |
| OPT-07 | Daily financial dashboard refresh |
| OPT-08 | SQL-independent public preliminary proforma |
| OPT-09 | Branded 404 and maintenance responses |
| OPT-10 | Rate limiting, abuse protection and CAPTCHA |
| OPT-11 | Cloudflare and CDN evaluation |
| OPT-12 | SEO, robots, sitemap and authorized indexing |
| OPT-13 | Proven duplication and legacy code cleanup |
| OPT-14 | Complete Azure and alternative-hosting audit |
| OPT-15 | Security, recovery and final regression |
| OPT-16 | Authorized deployment and 14-day measurement |

The order may change when dependencies or evidence justify it.

Do not build a permanent media index or caching layer before determining
whether the accepted publication architecture already solves that need.

Image query consolidation remains a fallback if persistent publication
does not meet the acceptance requirements.

Do not expand the implementation into a full rewrite.

## 17. Previous refactoring checkpoint

### 17.1 Repository baseline

Current audited HEAD:

880b3d4350c18c4a9cde0147e7d9b4079d47b045

Historical verification reported for this committed baseline:

- Release regression: 1,378 passing tests.
- GitHub CI: completed successfully.

These results do not represent a new test execution during OPT-01A.

### 17.2 Accumulated local work

| Block | Status |
| --- | --- |
| REFACTOR-04E.1 | Locally implemented and regression verified |
| REFACTOR-04E.2 | Locally implemented and regression verified |
| REFACTOR-04E.3 | Preserve Home implementation pending publication design |
| REFACTOR-04E.4A | Preserve HTTP reader architecture |
| REFACTOR-04E.4B.1 | Deferred pending publication architecture |
| REFACTOR-04E.4B.2 | Evaluate within publication architecture |
| REFACTOR-04E.4B.3 | Evaluate within public caching and CDN design |
| REFACTOR-04E.5 | Locally implemented, focused tests passed, SQL verification incomplete |
| REFACTOR-04E.6 | Reassigned to OPT-02 |
| REFACTOR-04F | Reassigned to OPT-13 |
| REFACTOR-04G | Reassigned to OPT-09 |

Reported local verification:

- REFACTOR-04E.1: 1,383 passing regression tests.
- REFACTOR-04E.2: 1,386 passing regression tests.
- REFACTOR-04E.5: 7 passing focused tests.

A successful SQL-backed due-cycle integration test for the monthly worker
remains outstanding.

The automatic monthly cleanup design is now subject to replacement by
the approved manual-maintenance requirement.

Do not claim that the accumulated working tree has passed a new complete
regression after all local changes.

### 17.3 Local changes to preserve

- PublicCatalogEndpoints.cs.
- PublicCatalogMediaCleanupWorker.cs.
- PublicCatalogMediaStorage.cs.
- Finishes.razor.
- ProjectDetail.razor.
- PublicSiteHostingExtensions.cs.
- PublicCatalogReader.cs.
- PublicSiteHostingTests.cs.
- PublicCatalogSqlIntegrationTests.cs.
- PublicCatalogMediaCleanupWorkerTests.cs.

Existing local appsettings.json and appsettings.Development.json changes
must remain untouched and undergo a separate sanitized review before
final staging.

## 18. Acceptance criteria

### 18.1 SQL independence

Instrumented integration tests must establish zero SQL commands for
applicable anonymous public paths:

- Home.
- Finishes and product discovery.
- Search and filters.
- Product details.
- Public project browsing and details.
- Solutions content.
- Public image retrieval.
- Missing public resources.
- Application liveness.

Verify both warm and cold application states with a valid persisted
publication.

Public preliminary proforma generation must also target zero SQL commands
when no authentication or persistence is required.

Test authenticated and anonymous paths separately.

### 18.2 Publication consistency

Verify:

- Individual content propagation within 60 seconds under defined conditions.
- Grouped publication behavior.
- Preservation of the previous valid publication on generation failure.
- Concurrent editorial operations.
- Process interruption during publication.
- Restart recovery.
- Invalid or missing publication recovery.
- Media reference preservation.
- Commercial unpublication behavior.
- Urgent withdrawal and cache invalidation behavior.
- Absence of private information from public artifacts.

### 18.3 Performance

Define repeatable test conditions for the public one-second load target.

Measure separately:

- Server response latency.
- Browser rendering performance.
- Largest Contentful Paint or equivalent user-facing metric.
- Image transfer volume.
- Cache hit and miss behavior.
- Application cold start.
- Database cold start.
- Administrative operation latency.

The sub-second public target and 15-second SQL resume target are not
guaranteed for all devices, networks or infrastructure conditions.

Provide loading feedback for administrative database activation and
allow the approved bounded waiting period.

### 18.4 Security

Verify:

- Existing administrative and public authorization boundaries.
- CSRF protections.
- Authorized primary-administrator maintenance access.
- Public media visibility.
- No private information in public caches or publication artifacts.
- Rate-limit enforcement.
- CAPTCHA server validation.
- Replay protection.
- Authentication requirements for excessive anonymous PDF generation.
- Safe handling of trusted forwarded headers.
- Correct 401, 403, 404, 429 and 503 behavior.

### 18.5 Recovery

Verify:

- Managed SQL backup configuration.
- Independent encrypted business backup.
- Database and important document recovery.
- Restore consistency.
- Recovery documentation.
- Demonstrated RPO and RTO against the approved 24-hour targets.

### 18.6 Cost

Record a pre-release baseline and measure at least 14 days after deployment.

Compare:

- SQL app_cpu_billed where applicable.
- Actual SQL charges.
- Database resume and pause behavior.
- Container Apps resource consumption.
- HTTP request volume and statuses.
- Cache behavior.
- Storage usage.
- Network transfer.
- Registry and related services.
- Operational logging cost.
- Total monthly projected infrastructure cost.

Normalize comparisons for actual business activity, traffic, free
allowances, pricing tier and deployment changes.

Do not claim savings solely from reduced SQL query count.

## 19. Final Azure audit

The final infrastructure review must cover:

- Subscription and resource-group cost allocation.
- SQL Server and database configuration.
- Database size and growth.
- SQL compute, free allocation, backups and networking.
- Container Apps revisions, replicas, CPU and memory.
- Container Apps environment configuration.
- Container registry.
- Persistent storage and file-share behavior.
- Key Vault and secret dependencies.
- Managed identities and permissions.
- Public networking and firewall rules.
- DNS, TLS and custom domains.
- Health and readiness probes.
- Logging and diagnostic settings.
- Budgets and alert policies.
- Shared resources and SkillSnap dependencies.
- Potentially redundant resources.
- Backup and restoration procedures.

No resource may be deleted solely because its purpose is not immediately
obvious. Confirm dependencies and obtain separate authorization.

The active production revision, image digest, configuration and migration
state must be independently established before release.

## 20. Release and rollback strategy

Before production deployment:

1. Complete source and configuration audit.
2. Verify all approved implementation blocks.
3. Run the complete Release regression.
4. Perform security and secret-scanning checks.
5. Validate published application artifacts.
6. Verify database and storage compatibility.
7. Establish backup and recovery readiness.
8. Approve the exact commit and production candidate.
9. Approve infrastructure and migration changes separately.
10. Record deployment and rollback procedures.

The publication architecture must have a defined recovery path for
incompatible or damaged artifacts.

Rollback must not restore content withdrawn for security or privacy reasons.

Do not assume that restoring an application revision automatically restores
the database, files or published content to a compatible state.

No irreversible migration or infrastructure conversion without explicit
approval and a verified recovery plan.

## 21. Sources of truth

Repository references:

- AGENTS.md
- docs/ARCHITECTURE.md
- docs/BUSINESS_RULES.md
- docs/DEVELOPMENT.md
- docs/OPERATIONS.md
- docs/SECURITY.md
- docs/ROADMAP.md
- docs/adr/001-architecture.md
- docs/adr/002-authentication.md
- docs/adr/003-database.md
- docs/PUBLIC-CART-01-CHECKPOINT.md
- docs/PUBLIC-WEB-01-LOCAL-CHECKPOINT-20261123.md
- Current source and automated tests

Owner-supplied evidence:

- Seven approved requirements blocks, 2026-11-25.
- REFACTOR-04D handoff.
- Local REFACTOR-04E verification reports.
- Azure consumption and configuration investigation reports.
- Historical Git audit.
- REVESTIK OPTIMIZATION MASTER AUDIT, 2026-11-25.
- REVESTIK-OPT-01-ARCHITECTURE-AUDIT.txt, 2026-11-25.

These external audit reports are evidence references, not files that
must be copied into the repository.

Historical documentation contains outdated deployment statements.
Source presence and local test success must not be treated as proof of
the active production state.

## 22. Current checkpoint and next approval boundary

OPT-01A scope is documentation only.

No application source, application configuration, database schema,
infrastructure or existing documentation changes are authorized.

Pending local verification:

- Confirm this document is the only new working-tree change.
- Confirm protected source and configuration hashes remain unchanged.
- Confirm existing modifications and untracked files are preserved.
- Confirm HEAD and staged state remain unchanged.
- Inspect the created document.

After successful verification:

STOP.

The next proposed investigation is OPT-02: SQL-independent application
health checks, including existing consumers and compatibility requirements.

Do not begin OPT-02 implementation without its own approved PLAN.

The long-term architecture, Azure SQL tier and external-provider decisions
remain pending measured evidence and separate approvals.
