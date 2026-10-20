# Security model

The ASP.NET Core backend is the trusted boundary. The browser is untrusted even when its Blazor controls are hidden or disabled.

## Access and session

- ASP.NET Core Identity, application-managed cookies and Google external authentication are separate identity concerns.
- Authorization is policy/role- and resource-dependent; Project ownership and closed-state locks are checked server-side.
- Cookie-authenticated mutations use antiforgery validation where required. Cookie settings, CORS and return URLs must be reviewed when deployment topology changes.

## Data protection

- Validate requests on the server; use EF Core parameterization, SQL constraints, transactions and concurrency handling for critical invariants.
- Never commit tokens, passwords, connection secrets, personal records or private project attachments.
- Store private project documents away from static web roots; authorize each upload, download, link and unlink by project and caller. Apply storage path protections and file validation.
- Treat inbound Gmail/XML/attachment content as untrusted; prohibit DTD/external XML fetching. Preserve intake and review boundaries.
- Avoid leaking stack traces, database internals and sensitive records in HTTP responses or logs.

## Financial protections

Revestik's sales, inventory, expenses, reconciliation and Projects have different business effects. Backend validation must reject cross-project references, unauthorized state changes, overpayments and closed-project finance/evidence mutations even for Administrator when prohibited by the domain.

## Verification

For sensitive changes verify anonymous access, authorization, CSRF, project scope, state transitions, concurrency, rejection without writes and auditing as relevant. A successful UI smoke test alone is not a security test. This description must be verified against actual code before it is treated as a compliance assertion.
