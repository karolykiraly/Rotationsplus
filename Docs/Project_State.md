# Project_State — Start Here

**Updated:** 2026-06-14 · Read this first in a new session, then `../CLAUDE.md` and the `Plan_*.md` docs.

## Where we are
**Azure foundation complete; P1 in progress.** Full discovery + planning done (all decisions locked with the owner). The owner has created the Azure tenants, subscription, app registrations, resource groups, ACR, Azure DevOps org/project/repo, ARM service connection (workload identity), variable groups, environments, and RBAC — recorded in **`Azure_Foundation.md`** (IDs in `memory/azure_ids.md`). **P1 (foundation/infra) is now being implemented**: solution scaffold (clone SkyLimit layout), Bicep for DEV, build-all + deploy-dev pipelines, skeleton api/worker/SPA to DEV, staff Entra login round-trip. Approved P1 plan is the working spec.

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
**P1 foundation deployed to DEV (2026-06-15).** Solution scaffolded, Bicep + pipelines (`build-all`, `deploy-dev`) in the `Rotationsplus` DevOps repo, api/worker/SPA live in `rg-rplus-dev`, staff Entra app config done (scope/consent/Admin role). URLs + bootstrap detail in `Azure_Foundation.md`. Frontend toolchain: **Vite 8 + Node 22**.

Remaining to close P1: confirm the **interactive staff sign-in round-trip** on the SPA (`/api/me` returns Admin identity). Then P1 → done; next is the first domain module (and the weekly delta loop once snapshots arrive).
