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
