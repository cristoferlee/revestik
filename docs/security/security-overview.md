# Security Overview

## 1. Purpose

This document describes the current security model of Revestik.

Security in Revestik is implemented across multiple layers rather than relying on a single mechanism.

The primary security areas currently include:

* Authentication.
* Authorization.
* Cookie security.
* Antiforgery protection.
* Request validation.
* Database integrity.
* CORS configuration.
* Transport security.
* External authentication.
* Server-side trust boundaries.

This document describes implemented behavior. Future security requirements should not be represented as existing protections until they are implemented and verified.

## 2. Trust Boundary

Revestik treats the ASP.NET Core server as the primary application trust boundary.

```text
Browser / Blazor WASM
       │
       │ Untrusted input
       ▼
┌─────────────────────────┐
│    ASP.NET Core API     │
│                         │
│ Authentication          │
│ Authorization           │
│ CSRF validation         │
│ Request validation      │
│ Business rules          │
└────────────┬────────────┘
             │
             │ EF Core
             ▼
┌─────────────────────────┐
│       SQL Server        │
│ Constraints / indexes   │
└─────────────────────────┘
```

Code executing in the browser must not be trusted to protect business data or enforce security-sensitive rules.

Client-side validation is primarily a usability mechanism.

## 3. Authentication

Revestik uses ASP.NET Core Identity and cookie-based authentication.

Authentication answers:

> Who is making this request?

Authenticated users receive an authentication cookie that allows the server to associate subsequent requests with their authenticated identity.

The application also contains an external authentication flow using Google.

External authentication does not mean that any Google account automatically receives access to Revestik.

The server remains responsible for determining whether the authenticated external identity corresponds to an authorized application account.

## 4. Protected-by-Default Access

Revestik follows a protected-by-default authorization model.

Server endpoints require authenticated access unless anonymous access is explicitly configured.

Conceptually:

```text
Endpoint
   │
   ├── Explicit AllowAnonymous
   │       ↓
   │    Public access
   │
   └── Otherwise
           ↓
      Authentication required
```

This reduces the possibility of accidentally exposing a new endpoint because authorization was forgotten during implementation.

Anonymous access should therefore be treated as an explicit security decision.

## 5. Anonymous Endpoints

Only endpoints that require unauthenticated access should be configured with anonymous access.

Current examples include parts of the authentication flow, application health/static hosting requirements, and other explicitly public infrastructure endpoints.

Anonymous endpoints must not expose sensitive business information merely because they are publicly reachable.

## 6. Authorization

Authentication and authorization are separate concerns.

Authentication determines:

> Who is the user?

Authorization determines:

> Is this user allowed to perform this operation?

Revestik supports role and policy-based authorization for operations requiring elevated privileges.

Authorization decisions must be enforced by the server.

Hiding a button or page in the Blazor client is not considered authorization because client-side behavior can be bypassed.

## 7. Cookie Security

Authentication cookies are configured as part of the server-side authentication system.

Security-relevant cookie behavior includes protections such as:

* `HttpOnly`.
* `Secure`.
* Appropriate SameSite behavior.

`HttpOnly` reduces direct JavaScript access to the authentication cookie.

`Secure` requires the cookie to be transmitted through HTTPS where applicable.

Cookie configuration must be reviewed when deployment topology or authentication flows change.

## 8. Cross-Site Request Forgery Protection

Because Revestik uses cookie-based authentication, authenticated state-changing operations require protection against Cross-Site Request Forgery (CSRF).

Revestik uses ASP.NET Core antiforgery functionality.

The general flow is:

```mermaid
sequenceDiagram
    actor User
    participant Client as Blazor Client
    participant API as Revestik API
    participant Google
    participant Identity as ASP.NET Core Identity

    User->>Client: Sign in with Google
    Client->>API: Start authentication
    API->>Google: Authentication challenge
    Google-->>API: External authentication callback

    API->>Identity: Resolve application user
    Identity-->>API: User + roles

    API-->>User: Secure authentication cookie

    Client->>API: Request CSRF token
    API-->>Client: CSRF request token

    User->>Client: Perform protected operation

    Client->>API: POST / PUT / DELETE<br/>Auth cookie + CSRF token

    API->>API: Authenticate
    API->>API: Authorize
    API->>API: Validate CSRF

    API-->>Client: Protected response

A request with a required missing or invalid antiforgery token must be rejected before the protected business operation executes.

CSRF protection and authentication solve different problems.

A valid authentication cookie identifies the user.

A valid antiforgery token helps demonstrate that a state-changing request originated through the expected application flow.

## 9. CSRF Token Handling

Revestik exposes an authenticated mechanism for obtaining the antiforgery request token.

The antiforgery cookie and request token are intentionally handled differently.

The browser can send the relevant cookie automatically, while the Blazor client obtains the request token and sends it using the configured antiforgery request header.

Antiforgery responses should not be cached as reusable public application data.

## 10. Request Validation

All information received from the client must be treated as untrusted input.

Revestik applies server-side validation to incoming business requests.

Examples in the customer domain include validation of:

* Required values.
* Maximum lengths.
* Email format.
* Identification format.
* Costa Rican location codes.
* Identification type.

Client-side validation may mirror these rules to improve user experience, but bypassing the client must not bypass authoritative validation.

## 11. Database Integrity

Security and data integrity continue beyond request validation.

Revestik uses EF Core configuration and SQL Server constraints/indexes to protect important invariants.

Examples include:

* Required database fields.
* Maximum column lengths.
* Supported identification values.
* Unique customer identification.

This provides an additional protection layer if invalid data reaches the persistence boundary.

Database exceptions that represent expected business conflicts should be translated into application-level behavior rather than exposing database implementation details directly to the client.

## 12. SQL Injection

Revestik uses Entity Framework Core for application persistence.

Application queries should continue to use EF Core parameterized query mechanisms rather than constructing SQL commands by concatenating untrusted user input.

Using EF Core does not eliminate the need for secure coding practices if raw SQL is introduced later.

Any future raw SQL must use parameterized values and receive additional security review.

## 13. Cross-Origin Resource Sharing

CORS controls which browser origins may make permitted cross-origin requests to the API.

CORS is not an authentication mechanism and must not be treated as one.

Development may require a separate client origin because the Blazor development server and API can run independently.

Production uses the hosted application model, reducing the need for broad cross-origin access.

CORS configuration should remain as restrictive as practical.

Wildcard production origins should not be introduced simply to resolve client connectivity problems.

## 14. HTTPS

HTTPS protects application traffic while it is transmitted between the browser and server.

Revestik uses HTTPS-oriented hosting and secure-cookie behavior.

Production deployments must terminate HTTPS through the application hosting platform or an appropriately configured trusted reverse proxy.

Sensitive authentication or business traffic must not intentionally be exposed over plaintext HTTP.

## 15. External Authentication

Revestik supports Google as an external authentication provider.

The external provider verifies the external identity, but Revestik still controls application authorization.

The current authentication flow verifies that the external authentication information is valid and that the resulting account is allowed to use the application.

Application account status must continue to be checked independently from the external provider.

An externally authenticated user whose Revestik account is disabled must not receive normal application access.

## 16. Redirect Safety

Authentication flows may contain return paths used to redirect users after successful authentication.

Return paths must be constrained to safe application-local paths.

External or protocol-relative redirect destinations must not be accepted simply because they were supplied by the client.

This reduces open-redirect risk in authentication workflows.

## 17. Secrets and Configuration

Credentials and secrets must not be committed to source control.

Examples include:

* Database credentials.
* External authentication secrets.
* API credentials.
* Production administrative credentials.
* Signing or service secrets.

Secrets should be supplied through environment-specific secure configuration mechanisms.

Development configuration and production secret management should remain separate.

Documentation may describe the name and purpose of a required secret, but should never contain its actual value.

## 18. Error Handling and Information Exposure

Client-facing errors should provide enough information for the client to respond correctly without unnecessarily exposing implementation details.

Sensitive details that should not be intentionally returned to users include:

* Database connection information.
* SQL statements containing sensitive values.
* Internal stack traces.
* Authentication secrets.
* Provider credentials.
* Internal exception details not required by the client.

Expected business conflicts should use appropriate HTTP/application responses.

Unexpected failures should be logged server-side and handled without exposing unnecessary internals.

## 19. Logging

Security-relevant events may require server-side logging.

Examples include:

* Failed authentication flows.
* Disabled-account login attempts.
* External authentication failures.
* Identity management failures.
* Unexpected application exceptions.

Logs must avoid recording secrets or unnecessary sensitive information.

Logging should provide operational visibility without becoming another source of sensitive-data exposure.

## 20. Static Application Hosting

The production ASP.NET Core host serves the compiled Blazor WebAssembly application.

Static application assets are intentionally accessible to browsers.

Blazor static files must never contain secrets because WebAssembly client assets are downloaded to the user's device.

Any value that must remain secret belongs on the server.

Unknown API routes are kept separate from the Blazor SPA fallback so invalid `/api/*` requests do not accidentally receive the application's HTML entry point.

## 21. Security Testing

Automated tests currently verify important security behavior, including antiforgery handling and protected mutable endpoints.

Hosting tests also verify relevant application behavior in development and production configurations.

Security-sensitive behavior should receive automated regression coverage when practical.

Examples include:

* Authentication requirements.
* Authorization policies.
* CSRF rejection.
* Anonymous endpoint behavior.
* Account status handling.
* Security-sensitive hosting behavior.

## 22. Defense in Depth

Revestik does not rely on one security control.

A typical protected mutable operation can pass through several independent controls:

```text
HTTPS
  ↓
Authentication
  ↓
Authorization
  ↓
CSRF validation
  ↓
Request validation
  ↓
Business rules
  ↓
EF Core
  ↓
Database constraints
```

Each layer protects against a different class of problem.

No individual layer should be assumed to make the others unnecessary.

## 23. Security Review Triggers

A security review should be performed when introducing or significantly changing:

* Authentication providers.
* User registration or invitation.
* Roles or permissions.
* Administrative functionality.
* Payment information.
* Electronic invoicing.
* File uploads.
* Public APIs.
* External integrations.
* Raw SQL.
* Production hosting.
* Secret management.
* Personally identifiable information.
* Audit or financial records.

## 24. Known Scope Limitations

This document describes the current application security foundation.

It does not claim that Revestik has completed:

* Formal penetration testing.
* Independent security certification.
* Regulatory compliance certification.
* Full security threat modeling.
* Complete audit logging.
* Enterprise identity governance.

Those capabilities should only be documented as implemented after they actually exist.

## 25. Security Principle

The core security principle for Revestik is:

> Never trust the client to enforce a rule that protects server-side business data.

The frontend may guide the user.

The server authorizes and validates the operation.

The persistence layer protects critical data invariants.

Security-sensitive behavior should remain explicit, testable, and reviewable as Revestik evolves.
