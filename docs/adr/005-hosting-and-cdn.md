# ADR-005 — Retain Azure hosting without an additional CDN

**Status:** Accepted, 2026-11-26 (FINAL-02A).

**Decision:** Close OPT-11 without change. Retain unified Azure Container Apps hosting, existing ingress and durable storage; do not introduce Cloudflare or another CDN at this time.

**Consequences:** Maintain existing security, abuse protection, caching and image optimization requirements. No additional edge availability or security guarantee is implied. Reconsider only with measured need and separate approval.
