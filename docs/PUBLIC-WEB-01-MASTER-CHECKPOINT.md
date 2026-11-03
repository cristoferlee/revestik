# REVESTIK — PUBLIC-WEB-01 Master Checkpoint

> **Status:** INVESTIGATE — product decisions captured; technical/legal/content verification pending. **Not an approved implementation plan.**
> **Date:** 2026-11-02
> **Repository:** https://github.com/cristoferlee/revestik
> **Local workspace:** `C:\Projects\Revestik`
> **Public website:** `https://revestikcr.com` (planned)
> **Existing private enterprise application:** `https://app.revestikcr.com` (production)
> **Owner communication:** Spanish. **Repository documentation:** English.

## 0. Mandatory entry point for every future AI session

**Read this file first whenever the task concerns PUBLIC-WEB-01.** Then read `AGENTS.md`, `docs/ARCHITECTURE.md`, `docs/DEVELOPMENT.md`, `docs/OPERATIONS.md`, `docs/ROADMAP.md`, `docs/SECURITY.md`, `docs/BUSINESS_RULES.md`, and relevant accepted ADRs. Verify the live branch, files, tests, migrations, working tree, and deployment evidence; this document does not replace source code. Do not silently assume an older checkpoint is current.

1. State the current phase and exactly which bounded checklist item is under discussion.
2. Identify facts confirmed in source, user-approved product decisions, proposals, and unresolved evidence separately.
3. Follow **INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP** without skipping steps.
4. Do **not** interpret the decisions recorded here as permission to write code, edit DNS/Azure, run migrations, commit, push, publish or deploy. Each material change needs explicit scope approval; commits/push/deployment need separate authorization.
5. Preserve unrelated changes, production data, secrets and existing internal business invariants.
6. When a question was already answered here, do not ask it again unless new evidence creates a genuine contradiction.
7. Keep this checkpoint updated with evidence, approvals, checklist statuses and links at the end of an authorized block. A checkbox may be checked only after proof is recorded.

## 1. Purpose, audience and product boundaries

Create a professional Costa Rican corporate website emphasizing **finishes and solutions for construction/remodeling projects**, lead generation and proof of work. It is **not** a marketplace, mass catalog, checkout, or a public copy of the enterprise application. Serve Costa Rican customers, but particularly foreigners residing in Costa Rica, vacation-property buyers, investors/developers, and international architects/contractors. Commercial geographic emphasis: **GAM, Guanacaste and Puntarenas**; actual logistical coverage and delivery promises must be confirmed.

Positioning: elegant, professional and approachable across budgets; communicate solutions for building and renovating in Costa Rica. The website should deliver three outcomes: **discover → trust → contact**. No unsupported luxury positioning, installation/construction claims, project quantities, guarantees or geographic promises.

### Domains and trust boundary

- `revestikcr.com`: anonymous public marketing website, multilingual, accessible/indexable.
- `app.revestikcr.com`: existing authenticated enterprise application and future website administration.
- Anonymous users may **submit** a carefully bounded inquiry; only authenticated authorized users may **read/manage** inbox messages or administer content.
- No direct exposure of private clients, projects, purchases, financial records, FIFO inventory, Gmail data, private storage, credentials, or unrestricted internal APIs.
- Architecture and repository topology are **not selected**. Evaluate static/SSG versus ASP.NET Core SSR (Razor Pages/MVC/Blazor Web App static SSR) based on SEO, form integration, security, operational simplicity, costs and maintainability. Do not choose Blazor WASM just because it exists internally.

## 2. Existing stack — confirmed repository context, not a public-stack decision

- .NET 10; ASP.NET Core Minimal APIs; Blazor WebAssembly client; modular monolith; EF Core/SQL Server/Azure SQL; ASP.NET Core Identity, Google OAuth and cookie sessions; QuestPDF; xUnit and Testcontainers.
- Azure Container Apps, Container Registry, Azure SQL; private durable storage, persistent encrypted Data Protection keys, configured security boundaries and rate limits in the internal app.
- Solution projects: `Revestik.Api`, `Revestik.Client`, `Revestik.Shared`, `Revestik.Api.Tests`.
- Business modules: customers, quotations, sales, FIFO inventory, suppliers, purchasing/AP, expenses, electronic received Hacienda documents, projects, dashboard, configuration/audit.
- The current API serves the published WASM client in production. Existing authenticated mutations have authorization and relevant CSRF protections.
- **Do not assume current live Azure settings from `deploy/azure/` or old documentation.**
- GitHub read-only verification on 2026-11-02 returned latest `main` commit `1b7bee7efb81d2c55de75e5954ac50c35a26d6a0` (`fix(auth): handle transient database timeouts during Google sign-in`). The preceding docs commit was `a9b40719296e47f995aecc6f02f71526b67899ce`. Confirm again before any work.
- User checkpoint reports 1,073/1,073 tests and production revision `ca-revestik-prod--0000017` with healthy SQL; the repository documents checked in this investigation still describe AUTH-PROD-01A as locally tested. **Treat this as a documentation reconciliation task**, not a reason to overwrite historical evidence or claim live verification.
- A local untracked Gmail backup was previously reported at `src/Revestik.Api/Services/GmailIntegration/GmailIntegrationService.cs.gmail-quota01.bak`; do not include in Git/Docker/build contexts or delete without permission.
- Existing budget configuration: USD 30/month across current Azure usage, **not** automatically additional available budget for public hosting. Verify actual spending, resource quota sharing, data egress and incremental cost.

## 3. Product decisions: 1–59, captured from owner answers

All are **preliminary functional decisions** guiding investigation and planning; not an implementation approval.

### A. Audience, positioning, language (1–8)

1. **1-E** Foreign-resident, second-home, investor/developer and international professional audiences.
2. **2-B** Geographic emphasis GAM, Guanacaste and Puntarenas.
3. **3-B** Elegant and professional, accessible across price points.
4. **4-C** Communicate building and renovation solutions in Costa Rica immediately.
5. **5-C** Initial language suggested by browser preferences; **always** allow manual override, maintain explicit preference; do not force language solely by IP.
6. **6-C** **Spanish, English and German** in V1. (Public AI chatbot languages are separately limited in #48.)
7. **7-C** AI-assisted first-draft translations with human editorial review before publication.
8. **8-C** Translate the complete public experience and metadata, preserving proper nouns and identifiers when appropriate.

### B. Homepage layout and visual experience (9–14)

9. **9-C** User-provided architectural homepage screenshot is **structural inspiration with creative freedom**, not literal UI/text/media to reproduce. Reference order: minimal nav/header; large architectural hero; four trust attributes; featured projects; immersive solutions band; visual finish categories; add coherent contact/footer/legal sections.
10. **10-A** Existing REVESTIK black/white/gold identity; do not invent another brand.
11. **11-C** Typography coherent with the approved REVESTIK logo and corporate look.
12. **12-B** **Manual** 2–3-image hero carousel, accessible controls; no assumption of autoplay; performance constraints apply.
13. **13-B** Subtle nonessential animations; respect reduced motion.
14. **14-B** Four benefits: experience, quality, personalized attention, coverage — only make verifiable claims.

### C. Projects and products (15–22)

15. **15-D** Residential, commercial and hotel/condominium/development work where genuine projects exist.
16. **16-A** Individual project page with images and general description; project location separately per #18.
17. **17-A** Before/after images when available and authorized; not an obligatory interactive comparer.
18. **18-A** Public province and canton, subject to authorization/privacy check.
19. **19-A** Start with 3–5 finish categories.
20. **20-A** Individual featured-product pages, photos, verified specs and commercial inquiry CTA.
21. **21-D** Owner-controlled updates when appropriate; no artificial monthly publishing schedule.
22. **22-C** Do not prominently advertise manufacturer/supplier brands; factual identification/attribution and image licenses still apply.

### D. Inquiries (23–26)

23. **23-C** WhatsApp and quote/contact form, clear main quote/request CTA.
24. **24-C** Form fields: name, email, WhatsApp/phone, project type, approximate project location, message. Verify required-vs-optional minimization/legal details before implementation.
25. **25-A** No file uploads in V1.
26. **Custom owner answer**: store/manage web inquiries in an **inbox within `app.revestikcr.com`**. This is **part of V1**, not deferred.

### E. Holidays and campaigns (27–31)

27. **27-C** Include Costa Rican and selected international holidays/occasions; exact first set TBD.
28. **28-D, customized** Adaptable templates generated with future integrated ChatGPT: logo treatment, navbar, footer, some H1 titles, seasonal visuals, etc. **Visible yet minimal, controlled and accessible**. Never give the AI unrestricted code/deployment rights.
29. **29-A** Manual activation (with AI assistance when the studio exists); **not** date-driven automatic publication.
30. **30-A** Elegant, restrained seasonal design.
31. **31-C** Seasonal featured products/promotions may be supported in the future, with truthful claims and approval.

### F. Company, technology and process (32–40)

32. **32-B** Company history, experience, working style and actual coverage.
33. **33-D** Consider partner references/credentials individually, with rights and evidence.
34. **34-D** Publish appropriate company/legal/contact details after privacy, local SEO and legal review.
35. **35-D** Architecture determined by investigation. Clarification: `revestikcr.com` public; `app.revestikcr.com` enterprise and website administration; GitHub is up to date as reported by owner (verify).
36. **Custom** Use existing production capabilities where sensible; avoid new infrastructure and technology unnecessarily. Must still preserve trust boundaries.
37. **37-D** Lowest reasonable incremental cost without compromising security, reliability or speed.
38. **38-D** Select analytics only after privacy/cost/need review; Search Console and technical metrics preferred initially.
39. **39-D** Existing mandatory staged workflow and approvals; do not redesign it.
40. **40-D** Wireframes → desktop/mobile mockups → explicit approval → incremental implementation and validation.

### G. AI editorial and eventual public chatbot (41–50)

41. **41-D** Private AI can assist themes, translations, SEO, project and product content.
42. **42-B** Private AI integrated into the **existing authenticated administrative app**, not merely an external ChatGPT editorial chat.
43. **43-C** Generate proposals, prepare bounded reviewable changes and require explicit approval before publication.
44. **44-D** Configurable approved variants of seasonal logo/header/hero/footer/titles/components without arbitrary structure edits or untrusted executable AI output.
45. **45-A** Manual theme activation.
46. **46-D** Later public assistant: general company Q&A, materials/finishes guidance, and help preparing a sales inquiry.
47. **47-C** Later public assistant via discreet floating entry point and dedicated assistant section (mobile review required).
48. **48-A** Public chatbot supports **Spanish and English** initially, despite three-language static website; make its language availability transparent on German pages.
49. **49-D** Voluntary project/contact details only under minimization, necessary privacy notices/consent, retention and secure handling; avoid default transmission of private inquiries to AI.
50. **50-D with clarification** Public chatbot arrives **after stable V1 AND sufficient verified knowledge**. The private AI can be integrated on the administration side in a later phase.

### H. Administration, phased deployment (51–59)

51. **51-B** Only `Administrator` reads/manages the inbox in V1.
52. **52-B** Inbox delivered **with V1**.
53. **53-B** Private AI Studio **after V1**, with restrained architectural forward compatibility; do not pre-build a generic AI platform.
54. **54-A** Only `Administrator` generates and approves AI content initially.
55. **55-B** The admin app prepares proposals; GitHub-driven publication **only after approval**. Separate editorial approval, repository write authorization, and deployment authorization; do not assume one implies another.
56. **56-C** Prepare the theme-system seam in V1, add actual holiday themes after V1.
57. **57-C** Inbox receives messages, changes status and supports follow-up via email or WhatsApp **outside** the system. No in-app email composer/threading in V1.
58. **58-B** Inbox new-message indicator **plus notification email**.
59. **59-A** Administrator approves content and **separately authorizes deployment**; no automatic code or production changes from AI.

## 4. Preliminary V1 boundary

**Public web:** minimalist architectural homepage based on reference (creative freedom), identity-compliant layout; manual hero carousel; subtle motion; four factual trust attributes; about/experience; individual project portfolio; 3–5 finish categories; limited featured products with verified individual detail pages and inquiry CTAs; contact via WhatsApp and form; Spanish/English/German; responsive WCAG 2.2 AA target; multilingual SEO, privacy/legal pages, sensible 404/redirects; no prices, checkout, client registration, bulk inventory or privileged data.

**Existing administrative app extension:** `Administrator`-only website inquiry inbox, states (proposed: new / follow-up / attended / closed; not yet signed-off), inbox count/indicator, email notification, off-system response links, minimal security/audit and retention. No auto-creation of a private customer/project from an anonymous inquiry. No attachments.

**Not V1 features:** private AI Studio runtime, holiday theme packs (only future-compatible seam if justified), public AI chatbot, complex CMS, autonomous publication, customer accounts, full CRM, marketing automation, payment/checkout, real-time stock/prices, content-translation API on every request.

## 5. Architectural investigation decisions to make (not yet approved)

- Compare static generation + protected inquiry intake versus independent ASP.NET Core server-rendered frontend; measure HTML indexability, mobile performance, operational cost, accessibility and deployment safety. Consider Razor Pages/MVC/static SSR as appropriate; don't force a stack based on familiarity.
- Decide repo topology and responsibility boundaries only with a written comparison. Existing admin app remains authenticated; public domain must not share authorization state or leak backend routes.
- If anonymous intake uses the existing API: **only narrowly scoped anonymous POST**, with validation, message size limits, rate limiting, spam controls, origin/CORS and CSRF/cookie behavior reviewed, idempotency/deduplication strategy where needed, structured safe logging, and tests proving protected inbox endpoints remain inaccessible. Public page can be separately hosted.
- Persist inquiry state safely; assess migrations, storage cost, pagination/indexes, access control, retention and deletion. Prevent email notification failures from losing a valid submission; choose safe retry/outbox behavior only if justified, not premature infrastructure.
- Protect private data and uptime from public abuse; avoid exposing private SQL connection/credentials to browser or static hosting.
- For publication: review PR-based workflow, least-privilege GitHub credentials, restricted file paths, preview build, tests, explicit administrator content approval and **distinct production authorization**, environment protections, rollback, audit. No untrusted AI-generated executable changes/deployment workflows.
- For future themes: prefer constrained design tokens/approved image slots and localized strings, preview across devices, versioned assets, alt text, animation controls and manual opt-in activation. Preserve logo identity and contrast.
- For future AI: server-side OpenAI API credentials, usage/cost caps, consent/data policies, public knowledge allowlist, abuse controls, reliable fallback to human contact; do not grant AI direct access to client financial/private data or unrestricted GitHub writes.

## 6. Internationalization, SEO, accessibility and quality bars

- Investigate localized routes, e.g. `/es/`, `/en/`, `/de/`; unique metadata, canonicals and valid `hreflang`/`x-default` strategy; stable linkable URLs; language-switch UX and saved manual preference; avoid forced IP geolocation redirects.
- Translate navigation, projects, products, inquiry forms, validation and legal texts with human review; ensure German pages work even if later chatbot remains ES/EN only.
- Technical SEO: server/static HTML crawlability, headings, titles/descriptions, sitemap, robots, Open Graph, relevant Schema.org LocalBusiness only with verifiable information, 404/redirect strategy, Search Console/Bing tools.
- Content: real image permissions, appropriate geo coverage, descriptive alt text, optimized hero responsive images and dimensions, AVIF/WebP where supported; lazy-load below-the-fold assets, avoid lazy-loading primary LCP image.
- Performance targets to validate on actual production-style mobile testing: LCP <= 2.5s, INP <= 200ms, CLS <= 0.1 at the 75th percentile when field data exists. These are **targets, not current measurements**.
- WCAG 2.2 AA target: keyboard interaction, focus visibility, carousel controls, meaningful labels, language attributes, contrast, reduced motion, semantic structure, error messages and screen-reader flows.
- Do not infer Lighthouse or CWV results from competitor screenshots; benchmark observations need reproducible evidence.

## 7. Costa Rica privacy, security and legal gate

Before launch, verify current applicable **Law 8968**, regulation and PRODHAB guidance with official sources and local legal review. Inventory data processed (form fields, IP/logs, mail notifications, WhatsApp links, hosting, analytics, AI services, vendors), purposes, responsibility, recipients, retention periods, deletion rights, consent/legal basis where applicable and international data transfers. Determine if any database registration or special obligations apply, rather than guessing.

- Display real corporate/legal identity and contact info as required.
- Draft privacy notice from actual implemented treatments; don't copy generic policies. Cookie notice/consent based on tools actually installed; avoid tracking by default.
- Define inquiry retention/deletion policy and who responds to rights requests; protect logs from personal content.
- Review photo, before/after, project location, client-name, logo and supplier-image publication permissions; manufacturer marks not foregrounded.
- Keep truthful ads and claims; any future discount/campaign must be verified.
- Validate spam/security protections and threat model before public exposure. Security testing is not a guarantee of immunity from abuse or litigation.

## 8. Mandatory engineering and collaboration standards

- **KISS, YAGNI, DRY judiciously, separation of concerns and SOLID where beneficial**: readable, well-named, small cohesive units; avoid abstractions/frameworks until a real need exists.
- Simple **and** maintainable **and** scalable: design clear interfaces and data ownership, use appropriate EF Core queries/indexes/pagination; avoid speculative microservices, CQRS, generic repositories, complex queues, premature caching, heavy JS or unnecessary dependencies.
- Server always enforces authorization, validation, invariants and transaction safety. Preserve existing FIFO provenance, purchase/XML distinctions, project boundaries, Gmail state, cookie/OAuth flows, CSRF and Data Protection.
- Follow established app styling and corporate black/white/gold logo (R black, K gold); mobile-first; reusable accessible components; separate public from admin styling where appropriate.
- No exposed secrets, hardcoded credentials, real client fixtures, unversioned local backups, or generated artifacts in commits.
- Document meaningful architecture decisions in an ADR, write repository-facing code/comments/docs in English, communicate in Spanish.
- Before edits: verify `git status -sb`, actual code/contracts/tests, run plan and await approval. Prefer bounded ZIPs with repo-relative complete files if requested; **no installers and no redundant README**. For `.md` updates, distribute a plain Markdown file.
- After approved code changes: restore/build, focused tests, appropriate full regression, SQL migration safety checks, HTTP/auth/CSRF tests, responsive/mobile/SEO/accessibility checks, smoke tests, `git diff --check`, and report exact verified results. Never claim tests executed if not run.
- STOP after each approved block. Do not automatically commit, push, deploy, update production or DNS. Publish only with explicit separate approvals.

## 9. Master task checklist — check only with recorded evidence

### Phase I — Complete INVESTIGATE (current phase)

- [x] Capture target commercial positioning and priority audiences (owner answers).
- [x] Capture homepage screenshot as structural inspiration (owner-provided image in conversation; **not stored in GitHub**).
- [x] Capture 59 functional preference answers, AI rollout and publishing boundary.
- [x] Read current repository guidance and core technical/operations/security documents (read-only GitHub check on 2026-11-02).
- [ ] Archive/attach the reference screenshot in an owner-approved nonsecret location and record canonical path or reference; do not assume the image exists in repository.
- [ ] Inventory real project photos, permissions, category, location (province/canton), descriptions, before/after rights, and minimum launch selection.
- [ ] Inventory 3–5 actual categories, featured product photos/specifications, use rights and initial item count.
- [ ] Verify company legal identity, public contact channels, WhatsApp, official inbox notification recipient, coverage, operating hours, claims and availability of authorized photography.
- [ ] Inspect actual current API endpoint/auth middleware, policies, storage/EF mapping, mail services, alert components, UI conventions, tests and migration history for inbox fit.
- [ ] Evaluate isolated anonymous intake into existing infrastructure versus separate protected intake; document threat model, fallback, failures, cost and no-private-data proof.
- [ ] Compare frontend rendering/hosting strategies, repo topology and build/deploy workflows; estimate incremental monthly costs from actual Azure usage plus future AI API budget separately.
- [ ] Determine if email notifications can reuse an approved delivery service without exposing credentials or dependence on Gmail intake.
- [ ] Define inquiry statuses, optional versus required fields, deduplication, notification retry strategy, privacy retention/deletion, audit and access policies.
- [ ] Verify legal/privacy/cookie requirements and authorized brand/media use using current Costa Rica sources and legal reviewer.
- [ ] Validate multilingual SEO strategy and translation workflow; define German chatbot fallback content.
- [ ] Validate benchmark sample with desktop/mobile checks where feasible; don't invent measured performance.
- [ ] Produce INVESTIGATE closure with verified evidence, unresolved risks and recommendation; **owner confirms closure**.

### Phase II — PLAN, explicit owner approval required

- [ ] Propose recommended architecture, alternatives and decision rationale; ADR for material architectural decision.
- [ ] Document module ownership, paths/files, DTOs, endpoints, anonymous and admin permission boundaries, DB migrations and privacy controls.
- [ ] Specify full page sitemap, content schema, URLs, i18n, image assets, SEO metadata, nav and contact CTA journey.
- [ ] Produce wireframes for homepage and internal pages (mobile and desktop) reflecting structural inspiration, then mockups; request owner approval.
- [ ] Define block-by-block implementation sequence, dependencies, budget cap, acceptance tests, rollback and deployment gates.
- [ ] Obtain explicit owner approval for exact PLAN scope; no code before approval.

### Phase III — V1 implementation blocks (all individually gated)

- [ ] Prepare isolated public web application/hosting skeleton with minimal dependencies and localized routing; test build and clean boundary.
- [ ] Implement design system, header/footer, language selector, accessible manual hero carousel, responsive layouts and subtle motion.
- [ ] Implement homepage sections, real trust attributes, project cards, product categories, solutions banner and conversion CTAs.
- [ ] Implement projects index and individual pages, approved images/location/descriptions and before/after where available.
- [ ] Implement featured products and individual detail pages, verified specs, no prices/stock/shop and clear inquiry linkage.
- [ ] Implement about/company, contact, legal/privacy pages and accessible ES/EN/DE translations.
- [ ] Implement secure public inquiry submit flow, validation, spam/rate limits, safe persistence and confirmation/error UX.
- [ ] Implement `Administrator`-only inbox in existing authenticated admin app, state transitions and new-message count.
- [ ] Implement reliable authorized email notifications, safe logs and external mail/WhatsApp follow-up links (not in-app messaging).
- [ ] Implement technical SEO: sitemap/robots/canonicals/hreflang/Open Graph/schema when verified, 404/redirects and webmaster setup.
- [ ] Add only minimal justified compatibility for future theme packs; do not implement AI or themes prematurely.
- [ ] Validate security/access rights, accessibility WCAG 2.2 AA target, mobile/carousel, i18n, performance and legal readiness.
- [ ] Run build/focused/full appropriate regression, privacy/security smoke tests, and review scoped diff; obtain sign-off.
- [ ] Authorize Git commit/push separately; authorize deploy separately; verify DNS/TLS and live post-release health separately.
- [ ] Record exact commit, build, tests, artifact revision, measured results and rollback path.

### Phase IV — Post-V1 enhancements (NOT authorized for V1)

- [ ] Investigate and approve private AI Studio API/service and cost/token controls; `Administrator`-only.
- [ ] Implement constrained editorial drafting: AI-assisted ES/EN/DE translations, SEO copy, product/project drafts and approval workflow.
- [ ] Implement safe proposal/preview/approved GitHub publication flow; editorial and deploy authorizations remain distinct.
- [ ] Implement season-theme tokens and reviewed asset slots, manually activated; minimal logo/nav/footer/H1/hero variations; test reduced-motion/perf/accessibility and restore defaults.
- [ ] Evaluate approved seasonal campaign/product variants with truthful verified messaging.
- [ ] After V1 stability and verified source content, evaluate public ES/EN chatbot knowledge base, guardrails, privacy, cost and contact escalation; German webpage must remain usable without German chatbot.
- [ ] Measure conversion and maintenance outcomes; add analytics only after privacy review and clear business value.

## 10. Evidence and checkpoint maintenance

For each completed item append: date; commit/ref (if any); scope/decision; factual evidence (test results, URLs, screenshots, measurements); approved by; remaining concerns; phase. Leave boxes unchecked for proposals and conversation-only assumptions.

### Initial evidence as of 2026-11-02

- Owner answers: 1–59 captured in ChatGPT conversation, consolidated in §3.
- Site inspiration: owner-supplied screenshot in conversation, reference only (not yet versioned).
- GitHub documents read: `AGENTS.md`, `docs/ARCHITECTURE.md`, `docs/DEVELOPMENT.md`, `docs/OPERATIONS.md`, `docs/ROADMAP.md`, `docs/SECURITY.md`. `docs/BUSINESS_RULES.md` and relevant ADRs still must be revisited before implementation.
- Production and 1,073-test details: **owner checkpoint report**, not independently re-tested here.
- No code, configuration, Azure, DNS or production changes have been performed in PUBLIC-WEB-01 INVESTIGATE.

### Session handoff format

On every continuation, start with: current phase; latest verified Git SHA and worktree state; current approved task only; checklist item IDs; new evidence; outstanding approvals; exact STOP point. Read this file plus `AGENTS.md` before proposing implementation.

**Final reminder: a documented preference is NOT authorization to implement. Investigate first; keep code small, safe, maintainable and cost-conscious.**

## 11. Update — 2026-11-02: Local homepage implementation, templates and AI direction

> **Status:** PUBLIC-WEB-01 is under local implementation. The owner has approved HOME-01 design and confirmed successful smoke tests for HOME-01-REVIEW-FIX. Interior pages, content administration, contact intake, legal/privacy work, and final tests remain unfinished. **Do not deploy any public feature until the entire public website is complete, tested, and expressly approved.**
>
> **Evidence boundary:** Delivered as incremental ZIP patches applied to `C:\Projects\Revestik\src\Revestik.Public`. Their contents are not verified as committed to the GitHub default branch. Verify the local tree, `git status -sb`, and actual source before further changes. Owner-reported smoke tests must not be represented as independently executed tests.

### 11.1 Locally approved milestones

| Block | Owner-reported status | Scope |
| --- | --- | --- |
| HOME-01A / A.1–A.5 | Approved by local smoke tests | Separate .NET 10 SSR public project; ES/EN/DE navigation, white header and full REVESTIK wordmark, text-only footer branding, RK favicon. |
| HOME-01B / B.1 / B.2 | Visually approved | Three architectural image placeholders; text and CTAs overlaid; **6-second autoplay**, previous/next, pause, reduced-motion considerations; compact ~440–500 px desktop hero. |
| VISUAL-01 / VISUAL-02 | Approved | Audited existing Revestik.Client CSS tokens; consolidated public CSS instead of stacking overrides. |
| HOME-01C | Approved | Three featured project placeholders and localized example copy. |
| HOME-01D | Approved in local workflow | UI heading **“Nuestros productos”** (while retaining existing finish URLs for compatibility); five category placeholders; each featured project links to an individual example detail route. |
| HOME-01E/F/G | Local smoke tests approved | Editorial Solutions, four brief benefits and final contact CTAs. |
| HOME-01-REVIEW | Static audit performed | Multilingual SEO, carousel/mobile accessibility, fonts, links and actual performance require final checks. |
| HOME-01-REVIEW-FIX | Owner explicitly confirmed successful smoke tests | Canonical/hreflang preparation without invalid localhost links; language switch preserves project slug; `noindex,follow` remains enabled during development. |

Visual approval is **not** a security, accessibility, legal or production sign-off. No public deployment or production changes have been authorized.

### 11.2 Superseding visual decisions

- Keep the **white/translucent horizontal public navbar** and full wordmark. RK is for favicon/loader, not public navbar branding. Reserve charcoal-blue `#18222D` mainly for footer; do not import the internal dark sidebar aesthetic.
- Reuse existing brand and UI tokens: logo gold `#FFBD59`, amber `#F59E0B`, darker hover/accent `#D97706`, text `#1F2937`, background `#F3F4F6`, surface `#FFFFFF`, border `#E5E7EB`. Apply amber/gold sparingly to buttons, borders and details, not saturated panels.
- Avoid excessive dashboard-style cards: favor spacious architectural editorial sections and actual photographs. Cards are justified for project/product previews, with discreet borders and little shadow.
- **Decision 12-B is superseded:** the owner approved a three-slide **autoplay** hero at 6-second intervals, with manual navigation and a pause control. Ensure keyboard support, screen-reader clarity and reduced-motion behavior at final verification.
- Consolidate CSS definitions at their canonical location; eliminate obsolete rules rather than appending conflicting overrides, `!important` or extra cascading files.
- Preserve a single H1, meaningful section headings, semantic HTML, mobile responsiveness and human-reviewed ES/EN/DE copy.

### 11.3 Content, project routes and async boundary

- All current images and text for projects and products are **placeholders**, not assertions of actual REVESTIK work or products. Replace only with approved, licensed, accurate content before launch.
- Every featured project tile opens its **own public detail page**, with a future gallery, description, verified characteristics, permitted general location and materials. Preserve its slug across ES/EN/DE language switching.
- “Nuestros productos” is a curated three-to-five-category visual presentation, **not** a mass catalog, checkout, real-time prices or live internal stock.
- Future async content reads must expose **only independently approved and published public data**. Never directly expose internal customer projects, CRM, inventory, financial information or private media. Draft and unpublished content must remain inaccessible to anonymous visitors.
- Do not implement fake async endpoints or speculative database infrastructure before an approved WEB-CONTENT-01 investigation/plan.

### 11.4 Video inspiration: constrained templates and private AI editing

The owner shared a video showing an e-commerce website editor with visual templates, product management and AI content tooling. It serves as **inspiration for the private editing workflow**, not as a mandate to build a marketplace or clone a third-party platform.

- **WEB-CONTENT-01:** inside authenticated `app.revestikcr.com`, initially `Administrator`-only. Edit hero images/slides, localized text, published project detail/photos, curated product sections and approved section order. Provide draft/preview/review/publish flow and reasonable versioning. The anonymous site contains **no edit controls**.
- **Curated template variants:** a few accessible, brand-locked arrangements for projects, products and later seasonal styling. No unrestricted drag-and-drop builder, arbitrary HTML/CSS/JS injection or AI-generated executable changes.
- **AI-CONTENT-01 (separately scoped):** optional server-side assistant proposing project/product descriptions, ES/EN/DE drafts and SEO titles/descriptions. Outputs are untrusted drafts; Administrator reviews/edits/approves before publication. Never fabricate specifications, real projects, prices or testimonials; protect secrets and customer information and enforce usage budgets.
- Workflow: `Administrator → draft/template → optional AI proposal → human review and preview → explicit publish → anonymous read-only published content`.
- **Timing reconciliation:** A protected content editor/publication path is planned **before** the owner's all-at-once public launch, but the full private AI Studio remains listed as post-V1 in original decision #53 until a separately approved scope changes that decision. Do **not** silently mark AI as implemented or mandatory for launch. The public AI chatbot remains a later phase.

### 11.5 Next authorized workflow and launch gate

1. `PUBLIC-PROJECTS-01` — INVESTIGATE actual local project index and detail pages, reuse existing routes, define public content fields and privacy-safe async publication contract.
2. Complete curated product detail pages, Solutions, About, Contact and accurate localized legal/privacy/cookie pages.
3. Implement bounded public contact intake, antispam validation, `Administrator`-only inbox, message notifications and retention controls.
4. Develop approved WEB-CONTENT-01 with authenticated editing, version/preview/publish and public read-only content delivery.
5. Complete SEO including sitemap/robots/canonical/hreflang/Open Graph, keyboard/screen-reader checks and actual Core Web Vitals testing with optimized **real** images.
6. Final full local regression, privacy/security/legal/media-usage review and explicit complete-site acceptance.
7. **Only after steps 1–6** consider a separate, explicit deployment authorization. No partial public deployment, DNS/Azure changes or indexing now. Git commits/pushes also require separate authorization.

### 11.6 Engineering and handoff guardrails

Follow **INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP**. Preserve unrelated local files, internal business logic and secrets. No overlapping CSS override layers. Use small maintainable code, responsive semantic HTML, accessible controls and genuinely verified facts. Owner-facing communication in Spanish; repository-facing documentation in English. Report owner smoke-test evidence separately from agent-executed verification.

**Current STOP:** Documentation synchronization; next development task is `PUBLIC-PROJECTS-01` investigation. Do not infer that local ZIP implementations were committed or deployed.

## 12. Update — 2026-11-03: Public Projects, Glassy viewer and independent Public Products design

> **Status / authority:** `PUBLIC-PROJECTS-01A/01B/01B1`, `PUBLIC-GLASS-01A/01B` are reported by the owner as locally integrated/smoke-tested. `PUBLIC-PRODUCTS-01` is **investigated and architecturally approved in conversation, NOT implemented**. The next bounded implementation is `PUBLIC-PRODUCTS-01A1 — Data Foundation`, requiring separate scope approval before changes to migrations/SQL and separate authorization for Git commit/push or deployment. **Never deploy the unfinished public site.**
>
> **GitHub verification:** On 2026-11-03, `main` pointed to `91f7f33ad187de2d5fab82179a51af15bd9a42b7` (`test(purchases): align due alert dates with Costa Rica timezone`). This is a source reference, **not** proof that local `src/Revestik.Public` code was committed. Recheck HEAD and working tree at start of next chat. The present checkpoint is an exported update and has **not been pushed to GitHub**.

### 12.1 Completed local UI work since §11 — owner-reported evidence

| Block | Scope / outcome | Evidence boundary |
| --- | --- | --- |
| `PUBLIC-PROJECTS-01A` | Sample public project catalog, shared `PublicProjectCatalog.cs`, reusable `ProjectGallery.razor`; three example projects reused in Home featured area and Projects index | Owner-approved local integration/smoke; demo data only |
| `PUBLIC-PROJECTS-01B` | Individual project detail pages in ES/EN/DE; gallery, project features/materials, contact CTA, localization/SEO and 404 handling for unknown project slugs | Owner-approved local smoke; placeholder photos/content, not live CMS/database |
| `PUBLIC-PROJECTS-01B1` | Compile correction (`using Revestik.Public` in public `Program.cs`) | Owner reported successful local result |
| `PUBLIC-GLASS-01A` | Global reusable Glassy lightbox on project-detail imagery: dark/blur backdrop, thin golden edge (`#FFBD59`), controls, keyboard/touch support, focus/reduced-motion considerations | Owner approved local smoke; full automated accessibility suite not yet proven |
| `PUBLIC-GLASS-01B` | Glassy viewer added to Home featured projects, five featured finishes/categories, Solutions editorial image and Projects index through reusable `PublicZoomAction.razor`, keeping navigation links separate; hero remains unchanged | Owner reported successful local tests |

Existing Home decisions from §11 remain binding: architectural/editorial look; approximately five curated featured categories on Home; three project placeholders; SEO-first responsive SSR; ES/EN/DE; visual palette; Glassy only where appropriate. **Do not claim any photograph, sample project or product is a real/authorized published asset.**

**Local integration caveat:** Previously delivered incremental ZIP patches were applied under `C:\Projects\Revestik\src\Revestik.Public` and checked by the owner. Public site was previously described as local/untracked, with `Revestik.sln` modified and `src/Revestik.Public/` untracked. Those states must be rechecked, never asserted current without a new `git status -sb`. Protect untracked Gmail `.gmail-quota01.bak` and unrelated working files. No PUBLIC-PRODUCTS implementation, new product API, schema, migration, xUnit run, or Azure change occurred in this conversation.

### 12.2 Approved commercial scope — `PUBLIC-PRODUCTS-01`

The product administrator described **a hierarchical, searchable, editable brochure/catalog**, not a fixed 10–20-product showcase and not an e-commerce checkout. Home retains about five featured editorial categories, but the interior `Nuestros productos` section can grow well beyond 20 products. Categories and subcategories are editable content, not hard-coded enums. Initial category families, subject to editorial naming review:

1. **Pisos exteriores:** porcelanatos, cerámicas, WPC/decking and exterior applications.
2. **Pisos interiores:** cerámicas, porcelanatos, vinyl/plastic flooring and interior applications.
3. **Azulejos:** bathrooms, kitchens, decorative walls and applicable tile/porcelain.
4. **Grifería:** faucets, showers, washbasin/kitchen fixtures and accessories.
5. **Morteros:** adhesives, grouts, leveling and related products; **brand is an attribute**, not automatically a subcategory.
6. **Fachadas:** fachaletas, façade cladding and exterior coverings.
7. **Muebles:** finished furniture and suitable room/application subcategories.

Every main category may have a **localized, editable hero** with architectural/ambient images, title, descriptive copy, order and supporting images. Individual public products have a commercial name, optional reference price **with currency**, description, imagery/gallery, brand and later relevant specifications. Full public ES/EN/DE content and SEO metadata require reviewed localized text; no automatic publication of AI drafts. `Consultar precio` and `Consultar disponibilidad` are suitable CTAs; do not imply guaranteed immediate stock or prices where not verified.

**Critical structural rule — many-to-many:** One public product may belong to multiple subcategories in *different* parent categories, e.g. a verified wood-look porcelain tile suitable for interior **and** exterior can appear in both `Pisos interiores → Porcelanatos` and `Pisos exteriores → Porcelanatos`. **One product record, one canonical product URL, one set of editorial content/images**; `PublicCatalogProductSubcategory` contains membership. Product suitability must be supported by manufacturer's real specifications; category membership cannot invent exterior/slip-resistance suitability.

**Hard separation from inventory:** `Revestik.Api` existing operational `Products`, `ProductCategories`, stock, purchasing, sales, FIFO layers and movements remain for internal business only. Public catalog is an **entirely independent data domain**, **without a foreign key, synchronization, automatic copying or creation of inventory records**. It represents lines normally commercialized, not necessarily currently in stock. Reuse general infrastructure only (EF Core, SQL Server, Identity, server authorization, audit, upload patterns); do not reuse internal inventory permissions or product data.

**Permissions:** Only users authorized by existing `PolicyNames.AdministratorOnly` may create, edit, organize, preview, publish, unpublish, archive, upload/replace category/product imagery, or otherwise mutate any public-catalog content. Anyone may read **published** content only. The existing `PolicyNames.ManageProductCatalog` controls an internal product function and **MUST NOT** authorize public website edits. Never rely on hiding UI controls as access control.

**No V1 cart, checkout, mass marketplace, live stock sync or product auto-publication.** Category/product public GETs and admin write endpoints are future proposals only; these endpoints do **not** exist yet in GitHub-verified source.

### 12.3 `PUBLIC-PRODUCTS-01A` SQL Server architecture — proposed and agreed for planning

Use the **existing SQL Server / EF Core infrastructure**, including `RevestikDbContext` with `ApplyConfigurationsFromAssembly`; no additional production database by default. Environments: local SQL Server Express (`localhost\SQLEXPRESS` in owner-reported local configuration), xUnit integration with SQL Server via Testcontainers, and Azure SQL Database (`RevestikDb`, serverless) in production. Validate actual connection settings per environment; no implicit production migrations. SQL images are **metadata only**; binary files use separately secured durable media storage with a vetted public delivery path/cache.

**Initial logical domain tables** (names provisional; finalize public snapshot/revision structure before migrations):

| Table | Responsibility / important constraints |
| --- | --- |
| `PublicCatalogCategories` | Category identity, immutable Id, unique stable slug, ordering, enabled state, version/concurrency; hero content via translations and media |
| `PublicCatalogSubcategories` | Parent CategoryId FK, `(CategoryId, Slug)` unique, ordering, enabled state |
| `PublicCatalogProducts` | Stable Id, unique public slug, optional brand/price, currency, featured/order, timestamps and concurrency |
| `PublicCatalogProductSubcategories` | `(ProductId, SubcategoryId)` composite PK, FKs, reverse lookup index; no duplicate links |
| `PublicCatalogCategoryTranslations` | `(CategoryId, LanguageCode)` unique, localized name/description/SEO |
| `PublicCatalogSubcategoryTranslations` | `(SubcategoryId, LanguageCode)` unique, localized name/description/SEO |
| `PublicCatalogProductTranslations` | `(ProductId, LanguageCode)` unique, commercial name/description/SEO |
| `PublicCatalogImages` | Stored-media key, owner kind/parent association, content type, dimensions, role (cover/gallery/hero), order, localized alt text; enforce exactly one valid owner |

**Open design requirement, not yet resolved into final SQL schema:** Support a **draft/editing revision that is invisible to public GET** while retaining a **stable prior published snapshot**. Category, product, subcategory membership, translations, price and images must publish together as one coherent revision, using a SQL transaction and server-side invariant validation. Proposed approaches include immutable publication snapshots or revision tables plus a published-version pointer. **Do not create the initial eight tables as if status flags alone provide this behavior**; the revision/snapshot schema must be finalized first. `rowversion` guards concurrent editing; returned conflicts must be handled rather than overwriting changes. Treat images and multi-record publication as a consistency problem: validate staged media, publish DB pointers atomically, and use cleanup/recovery for orphan files.

Desired SQL rules: PK/FK integrity, uniqueness for slugs and language/entity pairs, membership uniqueness, nonnegative `decimal(18,2)` optional price, valid 3-letter currency when price is present, controlled publication status, reasonable lengths, UTC `datetime2`, useful indexes for published listings/sorting and category queries, limited cascading deletes, and soft removal/archiving of public content. Slugs are not primary keys; define SEO redirects for later slug changes. Avoid excessive indexes before query evidence. Reuse proven patterns from `CompanyProfileConfiguration` (`IsRowVersion`) and `ProjectDocumentConfiguration` (SQL Server identity, concurrency and FKs).

### 12.4 Backend-first security — non-negotiable acceptance criteria

**Every business invariant, mutation, publication and permission check is authoritative in the backend.** Client-side validation is user-experience only. For the public catalog and all subsequent website modules (projects, images, inquiries, CMS, contacts), enforce:

- Server-side authentication and **`AdministratorOnly`** authorization on **every** administrative route, including read-only draft/preview and uploads; anonymous/other-role access rejected, regardless of UI.
- Server-side DTO and domain validation: required/malformed fields, length limits, valid locales (`es/en/de`), sanitized plain text or strictly vetted markup, price/currency rules, existing category/subcategory associations, uniqueness and verified publication completeness.
- Antiforgery for cookie-authenticated writes; existing `AntiforgeryValidationFilter` and endpoint conventions are reusable. `DisableAntiforgery` is not a bypass: verify accompanying explicit filter where applicable.
- CSRF, rate limits, bounded pagination and search, file-size and image dimensions/content sniffing with hardened decoder/processing, safe server-generated file names, no upload-to-public-executable directory, EXIF/metadata privacy handling, safe content delivery headers.
- No anonymous draft reads; public SSR/API GETs project **only approved/published snapshots**, with no private record leakage. Explicit cache invalidation on publish/unpublish; no static/memory cache exposing restricted state.
- Concurrency control (`rowversion`, HTTP 409/appropriate error response), SQL transactions for multi-entity publishing, idempotent safe commands where justified; SQL constraints are a defense-in-depth backstop, not a substitute for API checks.
- Record content-management security audit (actor user id, action, entity, timestamps) via existing `SecurityAudit.Record`, in same business transaction when applicable; avoid logging secrets, private content or entire binary files.
- Restrict public website server-to-API/data access and minimize granted identities; never expose internal admin services or database credentials publicly. Production security settings must be explicitly verified; do not infer security from development behavior.

**Verified source patterns as of repository read:** `RevestikDbContext` uses SQL Server and assembly-scanned configurations; `PolicyNames.AdministratorOnly` exists; `PolicyNames.ManageProductCatalog` is **internal**; `CompanyLogoEndpoints` uses `RequireAuthorization(AdministratorOnly)` and explicit `AntiforgeryValidationFilter` on writes; `CompanyLogoStorage` validates PNG/JPEG/WebP structure and uses private durable storage; `SecurityAudit.Record` persists audit records via the current context. Reuse patterns, not company-logo paths or restricted assets.

### 12.5 Automated tests / cross-environment verification — mandatory

**xUnit is required where appropriate**, not only as a correctness check but also to reduce the risk of environment-specific failures before deployment. **However, passing xUnit on Windows/local SQL Server or a SQL Server Testcontainer does NOT prove an Azure Container Apps + Azure SQL production deployment will run correctly.** Treat tests as one gate alongside environment-config validation, release build, database migration rehearsal, and explicit post-deployment smoke/health tests after deployment is separately authorized.

`PUBLIC-PRODUCTS-01A1` focused xUnit integration tests against actual SQL Server via **Testcontainers** must cover:

1. One product attached to multiple subcategories/categories; no duplicate membership.
2. SQL FK rejection of nonexistent parent/subcategory/product and appropriate delete policies.
3. Unique category/product slugs, scoped subcategory slugs, unique `(entity, language)` translations.
4. Valid/invalid and nullable prices, currency consistency, data length/check constraints.
5. Draft vs published snapshot isolation, including edit-after-publish, publish/unpublish and atomic rollback on failure; if revision logic is scoped to later block, explicitly mark tests pending, not passed.
6. Concurrency behavior using separate EF Core DbContext instances and `rowversion`.
7. Repeatable migrations from fresh SQL Server and model/schema validation; test migration rollback/recovery as appropriate.
8. Confirm public catalog models have **no FK or service coupling** to operational Products/Inventory/FIFO.

When HTTP endpoints are implemented in `01B`, add xUnit API/integration tests for `401/403`, CSRF, privilege escalation attempts, malformed/tampered DTOs, draft/public leakage, invalid subcategory membership, unknown slugs/404, rate limiting, pagination, re-publication and audit outcomes. When image uploads are implemented in `01C`, test oversized/corrupt/polyglot files, unsupported formats, media ownership, staged-orphan cleanup, safe image retrieval and access restrictions. Validate ES/EN/DE routing/SEO, real responsive/mobile and a11y by appropriate checks in `01D–01E`.

**Environment matrix (separate evidence):**

| Gate | Required verification |
| --- | --- |
| Local development | `dotnet build` + focused xUnit + SQL Server Express smoke/migration rehearsal |
| CI | Deterministic xUnit Testcontainers SQL Server integration on compatible runner, portable paths and environment variables, no reliance on workstation-local secrets or `localhost\\SQLEXPRESS` |
| Release build | `dotnet publish -c Release` with Linux/container target constraints and configuration tests where practical; verify case-sensitive paths and persistence assumptions |
| Preproduction / approved deployment | Correct connection strings/identities, migration approval/backup plan, SQL permissions, persistent media paths and public/private routing |
| Production (only after explicit authorization) | Health check, actual Azure SQL connectivity, static/media serving, OAuth/admin security, anonymous published content, 401/403/CSRF, basic performance and observability; rollback plan |

Last existing owner-reported full-suite outcome from earlier work was **1,073/1,073 xUnit tests passed**. There is also a user-reported `CI-FIX-01` for a Costa Rica timezone mismatch in `PurchaseDueAlertTests.cs`, with a passed GitHub Actions run `41555083649` on commit `91f7f33...`; verify CI run and any fresh regression evidence at the time of implementation. **No product-catalog xUnit tests have been authored or executed yet.** Never report a build, integration or deployment as passed without the actual output.

### 12.6 Bounded implementation roadmap / next gate

1. `PUBLIC-PRODUCTS-01A1 — Data Foundation`: finalize immutable snapshots/revisions and all invariants, then implement models, EF Core configurations/DbSets, indexes/checks, migration and focused xUnit/SQL Server Testcontainers tests. **Requires explicit approval of exact file/migration scope.** Do not apply to production.
2. `PUBLIC-PRODUCTS-01B — API + security`: authenticated Administrator-only content operations and anonymous published-only GET; DTO validation, CSRF, audit, server-side publish transaction, concurrency, xUnit integration.
3. `PUBLIC-PRODUCTS-01C — Media`: secure upload and optimized public delivery, per-category ambient hero/gallery, product images, content versioning and cleanup.
4. `PUBLIC-PRODUCTS-01D — Admin UI`: editor in existing authenticated `app.revestikcr.com` for categories, subcategories, products, assigned memberships, translations, preview and publication.
5. `PUBLIC-PRODUCTS-01E — Public SSR`: localized category and product listing/detail routes in untracked/local `src/Revestik.Public`, Glassy viewer reuse, SEO canonical/hreflang, fast responsive rendering and public-only projection.
6. Following website tasks: Solutions/About/Contact/legal/privacy/cookies and safe inquiry inbox; final media/legal/security/performance audits, **entire-site acceptance before any public deploy**. Owner separately authorizes commits/push, migration execution and each deployment.

**STOP / next-chat handoff:** The current conversation concluded product architecture and backend security requirements, but **no PUBLIC-PRODUCTS files were implemented, no migration run, no product xUnit run, and no deploy**. Start by reading this entire checkpoint and `AGENTS.md`; inspect live GitHub SHA and local worktree, especially untracked public-project files and Gmail backup. Move to `PUBLIC-PRODUCTS-01A1` with a short final schema/revision PLAN and exact files/tests, obtain explicit approval, then IMPLEMENT → VERIFY → STOP. Use incremental ZIPs relative to `C:\Projects\Revestik` with no unrelated files and preserve working tree. No commit/push/deployment without separate authorization.
