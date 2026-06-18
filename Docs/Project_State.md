# Project_State — Start Here

**Updated:** 2026-06-18 · Read this first in a new session, then `../CLAUDE.md` and the `Plan_*.md` docs.

## Where we are
**P1 complete; P2 largely shipped; P3 (Money & documents) underway.** Azure foundation + P1 (solution scaffold, DEV Bicep, build-all + deploy-dev pipelines, api/worker/SPA skeleton, staff Entra round-trip) are done and live on DEV — see `Azure_Foundation.md` (IDs in `memory/azure_ids.md`). **P2 core domain** shipped across PRs #1–#29 (Deployment_Log): identity spine, the marketplace admin trio (Specialties/Programs/Preceptors), Rotations + Students admin, admin Dashboard, the rotation status state machine, the customer portal MVP (browse + My-rotations) and CIAM customer auth (live 2026-06-16), and an SPA-hardening sweep. `develop` auto-deploys to DEV and was promoted to `main` (release line) on 2026-06-17. **P3 has begun** with the payments **pricing foundation**: a tested server-side `PricingService` (10% deposit for non-open programs / 100% for open) + a `GET /api/programs/{id}/quote` endpoint and an `IsOpen` program flag. Next P3 slices: Payment entity + Stripe PaymentIntent + webhook fulfillment (test-mode behind an `IPaymentGateway` abstraction), promo/credits/unlocks, then the Documents/Notifications/Hangfire sub-phases.

A second standing workstream is now active: **weekly legacy delta ingestion** (the outgoing dev team keeps changing the legacy code until cutover) — working-files-only snapshots dropped under `Live_Code/<date>/`, diffed and re-implemented into the new stack, tracked in `Delta_Ledger.md`. See the approved plan, Part D.

## What this project is
Ground-up rewrite + AWS→Azure migration of Rotations Plus (clinical-rotations marketplace, www.rotationsplus.org). React 17 SPA on Bluehost + Strapi 4 backend on AWS EC2/RDS → React 18 + .NET 9 modular monolith + Worker on Azure. Legacy code: `Live_Code/`. Plans: `Docs/`.

## Locked decisions (detail in the named docs)
- **Architecture** (`Plan_Architecture.md`): .NET 9 modular monolith API + Hangfire Worker on Container Apps; Service Bus + Redis; PostgreSQL Flexible Server; clean schema redesign.
- **Auth** (`§3.5`): two-directory Entra — STAFF in workforce tenant, CUSTOMERS (students/preceptors) in separate External ID/CIAM directory. Staff MFA phased: SMS through cutover → authenticator app after.
- **Frontend**: React 18 + TS + Vite, fresh modern UI (brand kept: logo + `#FF4874`), navigation may be rethought, mobile-first. Figma = content/flow reference only (`Figma_Inventory.md`).
- **Edge/DNS**: keep Cloudflare; low-TTL DNS flip = cutover mechanism.
- **Migration** (`Plan_Migration.md`): rehearsed transform-ETL (rehearse into DEV early; PROD only at cutover); blobs pre-copied to PROD + delta at cutover; ~30–45 min downtime target.
- **Removed vendors**: HelloSign, Calendly, Dwolla. **Kept**: Stripe (+new webhooks), Twilio (watch inbound-webhook repoint), Innodata OCR (Phase-2 → Azure Document Intelligence), SendGrid, Leaflet.
- **New admin features**: read-only impersonation, program-document upload/replace/remove, blog/content management.
- **Testing** (`Plan_Testing.md`): SkyLimit stack + characterization tests + ETL verification + parity checklists; tests ship with code.

## Stealth (active constraint)
Confidential; outgoing dev team must not learn of it. Build on new isolated accounts; **secret rotation + EC2/legacy access deferred to offboarding day** (`Plan_Migration.md §2`). No custom DNS for dev/preprod, no GitLab/Cloudflare changes until offboarding.

## Open items on the OWNER (gate P1 / early phases)
1. ~~Create Azure tenant + subscription, Azure DevOps org, Entra workforce + External ID tenants.~~ ✅ **Done 2026-06-14** — see `Azure_Foundation.md`.
2. Quiet access inventory for offboarding day (who holds Cloudflare/AWS/Stripe/Twilio/SendGrid/HelloSign/GitLab/Bluehost/Google Admin).
3. Open the Innodata channel (IP allowlist + SFTP password rotation) — owner-owned.
4. Pick offboarding date (well before cutover; leaves runway for blob pre-sync).
5. Decide MFA Phase-1 method already set (SMS); confirm timeline only.

## Top risks (owner-acknowledged, `Plan_Migration §12a`)
1. Cron/notification equivalence → fix-forward accepted. 2. Innodata (external) → owner owns comms. 3. Full-rewrite parity vs review bandwidth → owner's call: status quo is a liability to escape, some regressions acceptable.

## Immediate next step when work resumes
**P1 foundation COMPLETE on DEV (2026-06-15).** Solution scaffolded, Bicep + pipelines (`build-all`, `deploy-dev`) in the `Rotationsplus` DevOps repo, api/worker/SPA live in `rg-rplus-dev`, staff Entra app config done (scope/consent/Admin role). URLs + bootstrap detail in `Azure_Foundation.md`. Frontend toolchain: **Vite 8 + Node 22**.

✅ **Staff sign-in round-trip verified (2026-06-15):** signed in as charles@rotationsplus.com on the SPA → `/api/me` returned Name "Karoly Kiraly", Roles `["Admin"]`, isStaff `true`, oid `c9f28e33-…`. Full chain proven (MSAL PKCE → workforce token w/ `access_as_user` → audience/issuer validation → `StaffOnly` authz → `Admin` app-role claim). `Username` shows blank only because the test account is a *guest* (no `preferred_username`/`upn` claim); members will populate it post-offboarding — non-blocking.

**Next:** first domain module (and the weekly delta loop once legacy snapshots arrive under `Live_Code/<date>/`). A small CD hygiene follow-up is open: add a path filter to `deploy-dev.yml` so docs-only commits don't trigger a full DEV redeploy.
