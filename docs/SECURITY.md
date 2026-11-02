# Security model

## Security boundary

The ASP.NET Core API is the trusted authority. Blazor WebAssembly runs in an untrusted browser; hiding controls is not authorization. The server enforces user permissions, domain states, project scope, financial constraints, and relevant antiforgery checks.

## Authentication and authorization

- ASP.NET Core Identity manages application users and sessions.
- Google OAuth is the external sign-in provider; a Google identity does not automatically grant unrestricted application access.
- Role/policy/resource checks protect privileged operations; permissions are enforced server-side.
- Sensitive administrative changes and selected access events are recorded in security audit entries.
- Cookie-authenticated state-changing operations use antiforgery protection as required by their endpoints.
- OAuth return paths must be normalized to avoid open redirects; login and integration flows require dedicated verification.

## Production security controls

Repository-visible controls include secure antiforgery cookies, request rate limiting, HTTPS redirection, HSTS, restrictive response headers, sanitized production exception responses, and explicit reverse-proxy trust configuration.

Production Data Protection keys are persisted on a durable private volume and encrypted with a certificate containing a private key. The application requires this configuration to start in production. The presence of code does not independently verify the live certificate lifecycle or backup process.

## Sensitive data and file handling

- Do not commit credentials, connection strings with secrets, refresh tokens, customer records, private documents, or environment-specific secret values.
- Project documents and received-document staging are private, not public static assets.
- Validate file type, size, ownership, project scope, and access permissions for relevant upload/download workflows.
- Treat Gmail messages, XML files, attachments, and external API responses as untrusted input.
- Avoid XML DTD/external-entity retrieval and prevent sensitive diagnostic information from reaching clients or logs.

## Financial and domain integrity

Authorization alone is insufficient: valid users can still request invalid business operations. The API must reject unauthorized state transitions, cross-project references, overpayments, invalid stock changes, and forbidden mutations to closed projects. Transactions, constraints, and concurrency safeguards protect operations spanning multiple records.

## Verification and limitations

For security-sensitive changes, verify anonymous access, role/resource permissions, CSRF, input validation, rejected writes, audit events, concurrency, and private storage boundaries. A UI smoke test is not a security assessment. This documentation does not claim external penetration testing, regulatory certification, or independently audited compliance.
