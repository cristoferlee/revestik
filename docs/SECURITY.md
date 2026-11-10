# Security model

> Documentation status: 2026-11-10. This describes the repository-visible security design and the production behavior reported by the project owner. It is not an independent audit of the live Azure environment.

## Security boundary

`Revestik.Api` is the trusted ASP.NET Core host for the modular monolith. The solution contains four application projects: `Revestik.Api`, `Revestik.Client`, `Revestik.Public`, and `Revestik.Shared`; automated checks live in `Revestik.Api.Tests`.

- `Revestik.Client` is the administrative Blazor WebAssembly application. Its code runs in an untrusted browser. Hiding controls or routes is not authorization.
- `Revestik.Public` supplies public-facing Razor Components / server-side rendering (SSR) and restricted interactive editing components. Public rendering must not expose private business data.
- `Revestik.Shared` contains contracts, not an alternate security or persistence boundary.
- The API enforces authorization, domain rules, project scope, financial constraints, file access, and relevant antiforgery checks. Database constraints and transactions reinforce critical invariants.

The common application host serves the administrative UI below `/administracion` and the public site at its public routes. Sharing an origin does **not** imply sharing permissions: administrative endpoints remain protected, and public read endpoints must return only explicitly permitted data.

## Authentication and authorization

- ASP.NET Core Identity manages application users and application sessions, with Google OAuth as the external sign-in provider. A successful Google identity check does not automatically authorize administrative functions.
- Administrative operations use server-enforced roles, policies, and resource-level checks. In particular, public catalog management endpoints under `/api/admin/public-catalog` require the Administrator policy.
- The administrative entry point is `https://revestikcr.com/administracion`; unauthenticated users encounter the Google sign-in flow as appropriate. Authentication callbacks, return URLs, forwarded scheme/host, and cookie behavior must be verified after domain or route changes.
- Sensitive administrative changes and selected security events are recorded in audit entries. Application cookies, not browser-stored bearer tokens, establish the normal authenticated session.
- Cookie-authenticated state-changing endpoints must enforce their configured antiforgery protection. Public catalog mutations use an antiforgery endpoint filter, including staged-media operations; client validation alone is insufficient.

## Public site, publishing, and visual editor

The public product catalog is independent of internal inventory records, even though categories, subcategories, and units of measure are shared. Internal stock, customer, project, payment, Gmail, and fiscal data must not be exposed through public endpoints.

- Public product reads use publication and visibility checks. A product must be published, not archived, and associated with a publicly visible classification. Public product detail and image endpoints additionally verify visibility; unpublished or unavailable resources must not be disclosed by their identifiers.
- The new same-origin inline editor is rendered only on recognized public routes when `PublicEditorAccess.CanEditPublicSite` confirms **both** an authenticated Identity session with the `Administrator` role **and** an edit-mode request or preference.
- `?edit=1` and the `Revestik.Public.EditMode` cookie are **UI preferences, never authentication or authorization**. `?edit=0` exits edit mode. The preference cookie is configured `Secure`, `HttpOnly`, and `SameSite=Lax`.
- Editor-specific HTML is marked `Cache-Control: private, no-store` and `X-Robots-Tag: noindex, nofollow`; shared caches and search indexes must not receive administrative editor markup. Independently authenticated API writes remain mandatory even when editor controls appear.
- The older Client-hosted iframe preview, bridge scripts, and related editor routes still coexist with the newer inline editor. This is transitional legacy code, **not** the target architecture. Remove it only in a separately approved and verified change; do not treat the browser bridge as a security mechanism.

**Public-release gate:** The project owner reports that `https://revestikcr.com` currently returns **HTTP 401** in production, while `/administracion` is operational. The public site is still a restricted beta and must not be opened or indexed until a separate release approval, deployment review, and verification. The repository contains anonymous SSR/public API routes; the reported production 401 must therefore be treated as observed environment behavior, **not** as proof that every public endpoint is globally blocked. Verify the effective ingress and route protections before release.

## Production security controls

Repository-visible controls include secure antiforgery cookies, request rate limiting, HTTPS redirection, HSTS, restrictive response headers, sanitized production exceptions, and explicit reverse-proxy trust configuration. Forwarded-header trust and external OAuth redirects require environment-specific review; do not widen trusted proxies or CORS merely to accommodate a local development issue.

Production Data Protection keys are persisted on a durable private volume and encrypted with a certificate that contains a private key. The API requires the configured durable storage root and certificate material to start in production. Code-level requirements do not independently establish the live certificate lifecycle, backup integrity, or disaster-recovery readiness.

## Sensitive data, images, and file handling

- Do not commit credentials, connection strings with secrets, refresh tokens, customer records, private documents, backups, or local-only configuration.
- Project documents, received-document staging, Gmail synchronization state, and Data Protection keys belong in private storage, not in publicly accessible static files.
- Public catalog media use a separate managed storage area under `RevestikStorage:RootPath/public-catalog/` in production, with pending and active files. The production root must be durable and appropriately permissioned.
- Validate uploaded media content, type, size, and intended role. The public catalog enforces a 5 MiB per-image limit; public reads check the associated product or category's visibility instead of exposing arbitrary storage keys.
- Treat Gmail messages, XML files, uploaded files, and external API responses as untrusted input. Reject unsafe XML behavior, preserve file access scope, and prevent sensitive details from reaching client errors or logs.

## Financial and domain integrity

Authorization alone is insufficient: authenticated users can still request invalid business operations. The API must reject unauthorized state transitions, cross-project references, overpayments, invalid stock movements, and forbidden writes to closed projects. Transactions, database constraints, concurrency safeguards, and server-side validation protect operations spanning multiple records.

## Verification and limitations

For security-sensitive changes, verify anonymous access, authenticated role and resource restrictions, CSRF rejection, OAuth callback and redirect handling, editor-mode gating, cache behavior, public catalog publication filters, rejected writes, audit events, concurrency, and private/public storage boundaries. Re-run relevant automated tests on the exact revision and perform environment-specific smoke checks before any release.

A passing UI smoke test, an HTTP 401 at one route, or green CI does **not** constitute a penetration test or independently audited compliance. Historical test counts are not guarantees for a new revision. See [OPERATIONS.md](OPERATIONS.md), [ARCHITECTURE.md](ARCHITECTURE.md), and the accepted [authentication ADR](adr/002-authentication.md) for related deployment and design responsibilities.