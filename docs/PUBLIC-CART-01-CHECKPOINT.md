# Revestik — PUBLIC-CART-01 Closure Checkpoint

**Recorded:** 2026-11-20
**Scope:** `PUBLIC-CART-01A`, `01B`, `01C`, `01D`, and `01D.1`
**Status:** Locally implemented and owner-verified. GitHub synchronization and production release are separate states; neither is established by this document.
**Runtime:** .NET 10 modular monolith; `Revestik.Api` hosts `Revestik.Public` and `Revestik.Client`.

## 1. Implemented scope

- **01A — Per-account cart:** SQL Server `PublicCartItems` migration `20261120210000_AddPublicCartItems`, scoped to the authenticated public customer's Identity user ID. Product/quantity validation, row-version concurrency, antiforgery on mutations, authorization, and per-route rate limiting. The public catalog is separate from operational inventory.
- **01B — Cart interface:** Multilingual public cart route and navigation, add/update/remove actions, product-level controls, visual feedback, and accessible state announcements.
- **01C — Guest cart and import:** Browser-side temporary selection with a seven-day expiry. Import to the signed-in public customer cart requires an explicit action; already-present quantities are preserved and partial failures allow safe retry. No guest SQL cart is introduced.
- **01D — Preliminary PDF:** Anonymous or authenticated requests send only selected product IDs, quantities, and language. The API re-queries currently visible published catalog products, their active selling units and current public prices. QuestPDF renders US Letter pages in memory with half-inch margins. Currency totals are grouped independently (CRC/USD); unavailable prices are not fabricated. Optional covers use bounded media reads. The PDF is returned to the browser for download or native sharing where supported. WhatsApp remains a user-confirmed, text-based breakdown, not an automatic document attachment. No public PDF, formal quotation, sale or fiscal document is persisted.
- **01D.1 — Hardening:** SQL-backed anonymous HTTP happy-path tests, unpublished product rejection, damaged-image text fallback, all-page Letter checks, and a German download filename. English customer-facing terminology uses **Quotation** / **Preliminary Quotation**, while internal endpoint/type names may retain `Proforma`.
- **Internal PDF paper size:** Existing quotation, sale, and project report PDF templates were separately changed from A4 to US Letter with half-inch margins. This is an independent change from the public-cart database migration.

## 2. Owner-reported local verification

- Full .NET regression: **1,336 passed, 0 failed, 0 skipped**, runtime reported **978.1 s**, on **2026-11-20** (output supplied by repository owner).
- Release build and hardening command completed successfully, per owner output (`HARDENING VERIFICADO`).
- Focused cart/PDF and JavaScript checks were completed during earlier blocks; JavaScript hardening suite contained nine checks. Do not substitute this historical result for a future production smoke test.
- Owner confirmed local browser testing of guest/persisted cart, isolated Google accounts, PDF download/sharing, and Letter printing before hardening. Repeat relevant smoke tests if release configuration differs.
- `git diff --check` returned no formatting errors in the earlier local review. Git emitted LF→CRLF notices, not failures.

## 3. Known boundaries and release gates

1. **Not deployed.** GitHub push/merge, Azure deployment, production database migration, public OAuth enablement and public launch are independent approvals. Production registration may still be disabled.
2. **Do not persist public PDF files.** Browser download is a user-side operation. The public document is not an internal `COT`, invoice, tax document, accepted quotation or stock reservation.
3. **Public user authorization:** Never grant customer carts to employee/editor/admin roles. Resolve owner from the server-side authenticated Identity principal, never a browser-supplied user ID.
4. **Pricing and product visibility:** Trust only server-side current catalog data, not browser names, image URLs or prices. Handle unpublished products, expired guest carts, stale rows, unsupported units, and malformed media.
5. **WhatsApp limitations:** `wa.me` prepares text but does not automatically transmit a PDF. Web Share API support and selected application are browser/device dependent.
6. **Paper size:** Printer-specific clipping can still occur with hardware margin constraints; confirm actual print driver uses Letter and fitting as needed. The PDF templates are designed for Letter.
7. **Git scope:** Review exact staged diffs and references across shared `Program.cs`, `PublicLayout.razor`, `Home.razor`, `SiteLinks.cs`, catalog pages, and `WhatsAppContactLink.razor`. These are shared with previous public-site/contact work; staging them may include unrelated changes. Stage only reviewed paths/hunks.
8. **Exclude sensitive/unrelated data:** Do not blanket-stage `appsettings.json`, `appsettings.Development.json`, User Secrets, arbitrary untracked files, malformed PowerShell-named files, generated binaries or local media. Independently inspect intended configuration for the WhatsApp number before release; local settings do not automatically propagate to production.
9. **Security scan:** Gitleaks scan of the exact staged material and inspection of the commit contents are required before commit/push. Never claim GitHub synchronization until the remote ref matches the created commit.

## 4. Next checkpoints

- Prepare and review a scoped Git staging manifest, confirm that shared public UI changes are intentionally included, and scan staged material for secrets.
- Create a reviewed commit and push to the chosen feature branch; record the final SHA and remotely observed SHA. No implicit merge to `main`.
- Independently plan production rollout, database migration review, public login availability, Azure configuration, smoke tests and rollback. Keep the public site release gated until separately approved.
- A comprehensive evidence-driven refactor/duplication cleanup is a **separate future workstream** and not part of this completed functional block.

**Evidence status:** This checkpoint records verified local development and owner-reported testing. It is not proof of live public availability or production readiness.
