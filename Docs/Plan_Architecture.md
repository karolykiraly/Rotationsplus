# Plan_Architecture — Rotations Plus Rewrite on Azure

**Date:** 2026-06-11 · **Status:** Approved direction (decisions locked with owner)
**Scope:** Everything wrong with the current system + the proposed target architecture for the React + .NET rewrite on Azure.

---

## 1. Executive summary

Rotations Plus is a production marketplace matching medical/dental/NP/PA students with clinical-rotation preceptors. The current system is a React 17 SPA on Bluehost shared hosting talking to a **Strapi 4.12 CMS that serves as the entire backend** (auth, 37 tables, ~60 endpoints, all integrations, and a 1,610-line cron file) on AWS EC2 + RDS PostgreSQL.

The rewrite replaces this with:

| Layer | Current | Target |
|---|---|---|
| Frontend | React 17 + Webpack 5 + Redux, single 4.4MB bundle, Bluehost | React 18 + TypeScript + Vite SPA with prerendered public pages, Azure Static Web Apps |
| Backend | Strapi 4.12 (Node) on EC2 | **.NET 9 modular monolith API + Worker service** on Azure Container Apps |
| Auth | Strapi users-permissions, JWT in localStorage, bcrypt | **Microsoft Entra External ID (CIAM)** + MSAL, app roles, social login |
| Jobs | 1,610-line cron-tasks.js inside Strapi | **Hangfire** (Postgres storage) in the Worker + Service Bus events |
| DB | PostgreSQL on AWS RDS (Strapi-shaped schema) | PostgreSQL on **Azure Database for PostgreSQL Flexible Server** (clean redesigned schema, EF Core code-first) |
| Files | EC2 local disk (`public/uploads`) + one S3 bucket (static assets) | **Azure Blob Storage** (private containers + SAS) |
| CMS | Strapi (admin panel) | **None** — blog + reference data in own DB, edited in new Admin dashboard |
| Edge | Cloudflare (kept) | Cloudflare (kept; re-evaluate Front Door post-migration) |
| Secrets | Hardcoded in source code | **Azure Key Vault** + managed identity, zero secrets in code/config |
| CI/CD | GitLab CI (ssh/scp deploys) | **Azure DevOps** repos + YAML pipelines + Bicep IaC, DEV/PREPROD/PROD |

Architecture decisions follow the SkyLimit project's proven patterns (solution layout, ServiceDefaults, Bicep modules, pipeline templates, testing stack) scaled down to a 2-app topology.

---

## 2. Everything wrong with the current system

### 2.1 Security (CRITICAL — act before migration)

| # | Finding | Where | Risk |
|---|---|---|---|
| S1 | **Live Stripe secret key hardcoded** | `rotationsplus-backend-v4-main/config/server.js` | Full payment-account compromise |
| S2 | **SendGrid API key hardcoded** | `config/server.js`, `config/plugins.js`, `student_marketing_query.py` | Domain-spoofed mail, reputation loss |
| S3 | **Twilio SID + auth token hardcoded** | `config/server.js` | SMS/WhatsApp abuse, toll fraud |
| S4 | **HelloSign API key hardcoded** | `config/server.js` | Forged signature requests |
| S5 | **Innodata SFTP password hardcoded** | `config/server.js`, `src/utils/sftpDownload.js` | PII document exfiltration |
| S6 | **Production RDS host + postgres password hardcoded** | `student_marketing_query.py` (repo root) | Full database compromise |
| S7 | Dwolla + Calendly credentials hardcoded (appear unused) | `config/server.js` | Latent exposure |
| S8 | Stripe **publishable + test keys in frontend source** (`src/config.js`) — publishable is by design public, but live/test mixed in code with env switching | frontend repo | Config errors, accidental test-mode prod |
| S9 | JWT stored in `localStorage` | frontend | XSS → session theft |
| S10 | Raw SQL with string interpolation (`strapi.db.connection.raw(...id=${x})`) | e.g. `src/api/document` controllers | SQL injection pattern (currently mitigated by `Number()` casts — fragile) |
| S11 | Preceptor `bank_info` stored as plain JSON in DB | `preceptors` table | Unencrypted financial PII |
| S12 | No Stripe webhook handler — payment state relies on the browser completing the flow | backend | Lost/duplicate payments, no reconciliation |
| S13 | Admin BCC and test addresses hardcoded (personal Gmail as test address) | `config/plugins.js` | Data leakage to personal accounts |

**Resolution in target:** All secrets in Key Vault, accessed via managed identity. Bank info and other sensitive columns encrypted with EF Core value converters (SkyLimit `PhiValueConverter` pattern). MSAL session handling (tokens in memory, silent renewal). Parameterized queries only (EF Core). Stripe webhooks with signature verification. The P0 rotation of every exposed credential is specified in `Plan_Migration.md §2`.

### 2.2 Backend code health

- **Strapi lock-in:** business logic scattered across content-type lifecycle hooks, custom controllers, and one giant cron file; Strapi-conventions schema (link tables, components, users-permissions tables) makes the DB hard to reason about.
- **`config/cron-tasks.js` is 1,610 lines** mixing scheduling, business rules, SQL, and notification templating; magic thresholds (48h/72h/7d/14d) scattered inline; everything in `US/Pacific` hardcoded.
- **Zero tests** of any kind.
- **Silent failures:** numerous empty `catch(e) {}` blocks.
- **Denormalized metrics updated by raw SQL on a 1-minute cron** (`students_precepted`, `revenue_raised`, dashboard counters) — race-prone and unauditable.
- **Dead integrations:** Dwolla configured, never called. Calendly token configured server-side, no endpoint uses it (the frontend embeds Calendly directly). **Owner confirmed (2026-06-11): Dwolla, HelloSign, and Calendly are all unused — none are carried into the rewrite.**
- **`html-pdf` 3.0.1** (abandoned 2018, bundles old PhantomJS) generates visa letters/evaluations/agreements from 14 EJS templates.
- **Uploads on EC2 local disk** (`public/uploads`) — Strapi default provider; a single-instance, unreplicated, backup-unclear file store for legal documents.
- **`student_marketing_query.py`** — an interactive marketing-email script at the repo root with production credentials, run by hand against prod.

### 2.3 Frontend code health

- React 17 / Router v5 / Redux+thunk callbacks / Bootstrap 4 — all one+ major versions behind; `node-sass` deprecated.
- **Single 4.4MB unsplit bundle** including a **1.5MB `constants.js`** of hardcoded specialties/cities/hospitals/programs that should be server data.
- **Monolithic components:** `ContactDetails.js` (176KB), `PreceptorProfile.js` (167KB), `StudentProfile.js` (121KB), `CompletePreceptorProfile.js` (117KB) — each a whole feature area in one file.
- **Callback-based API layer** (`api.method(data, (err,res)=>{})`) — 32KB `api.js`, error handling duplicated everywhere.
- **Dead code:** legacy `Home.js` vs live `NewHome.js`; unused `src/routes/mobile/MHome.js`; **70KB `blogs.js` hardcoded blog copies** (live blog uses the backend API); commented-out blocks.
- 4 date libraries (moment, moment-timezone, date-fns, react-nice-dates), 7 input/select component variants, no form library.
- ~1 test file. No error boundaries, no centralized error handling.
- Mobile: responsive media queries exist but are bolt-on; admin is desktop-only; map/filters/tables degrade on small screens — while the customer base is heavily tablet/mobile.

### 2.4 Infrastructure

- Frontend on **Bluehost shared hosting**, deployed by `scp` from GitLab CI. No CDN-aware caching strategy, no atomic deploys, no rollback.
- Backend on a **single EC2 instance** (snowflake server, ssh deploys, no containers, no autoscaling, no health-based restart) with user files on its local disk.
- No staging parity: preprod exists (`preprod.rotationsplus.com` / `preprod-api...`) but deploys are manual and environment config is hardcoded `APP_ENV` numbers compiled into the bundle.
- No IaC, no observability stack (no APM/log aggregation visible), no alerting.

---

## 3. Target architecture

### 3.1 Topology — right-sized "modular monolith + worker"

Full microservices was considered and **rejected** for this system size (~60 endpoints, 37→~30 tables, <5k MAU): it would multiply infra cost, deployment surface, and contract maintenance without a scaling need. The chosen topology keeps the distributed building blocks (Service Bus, Redis, containers) so individual modules can be split out later if ever needed.

```
                      Cloudflare (DNS, CDN, WAF, bot protection)
                            │
        ┌───────────────────┼──────────────────────┐
        │                   │                      │
  Static Web App      Container Apps Env           │
  (React SPA +        ┌──────────────────┐   Entra External ID
  prerendered         │  rplus-api       │   (login, tokens,
  public pages)       │  ASP.NET Core 9  │   social IdPs, MFA)
        │             │  modular monolith│
        └── /api ────►│                  │──► PostgreSQL Flexible Server
                      ├──────────────────┤──► Azure Blob Storage
                      │  rplus-worker    │──► Azure Cache for Redis
                      │  Hangfire jobs + │◄─► Azure Service Bus (topics)
                      │  SB consumers    │──► SendGrid / Twilio /
                      └──────────────────┘    Stripe / Innodata SFTP
                      Key Vault + Managed Identity · App Insights + Log Analytics · ACR
```

- **`rplus-api`** — one ASP.NET Core app, internally organized by domain module (below). Scale 1→N replicas on Container Apps.
- **`rplus-worker`** — Hangfire server (recurring jobs replacing cron-tasks.js) + Service Bus consumers (event-driven work). Runs the SFTP/OCR ingestion, notification queue, and state-machine sweeps. Scale 1 replica (Hangfire handles intra-process concurrency; can scale out later — Hangfire supports multiple servers natively).
- Both are Docker images built by `dotnet publish`, pushed to ACR, deployed by Bicep + pipeline (SkyLimit two-stage pattern).

### 3.2 Solution layout (clone of SkyLimit conventions)

```
rotationsplus/
├── RotationsPlus.sln
├── Directory.Build.props              # central package versions, net9.0, nullable, warnings-as-errors
├── src/
│   ├── api/RotationsPlus.Api/         # the modular monolith
│   │   ├── Modules/
│   │   │   ├── Identity/              # user profiles, roles, Entra claims mapping, OTP-era compat
│   │   │   ├── Marketplace/           # programs, preceptors, specialties, hospitals, search, favorites
│   │   │   ├── Rotations/             # booking lifecycle, state machine, confirmations, evaluations
│   │   │   ├── Payments/              # Stripe, webhooks, promo codes, unlocks, credits, honorarium
│   │   │   ├── Documents/             # uploads, document types, agreement flow, OCR statuses, PDF generation
│   │   │   ├── Crm/                   # leads, lead logs, contacts, call history, email threads, issues
│   │   │   ├── Notifications/         # templates, outbound email/SMS/WhatsApp requests (enqueue only)
│   │   │   ├── Reporting/             # dashboards, live score, analytics queries
│   │   │   └── Content/               # blog posts, reference data (cities/specialties/lookup)
│   │   ├── Infrastructure/            # RotationsDbContext, EF configs, converters, migrations
│   │   └── Program.cs
│   ├── worker/RotationsPlus.Worker/   # Hangfire host + Service Bus consumers
│   ├── shared/
│   │   ├── RotationsPlus.ServiceDefaults/   # OTel, resilience, health checks (copy SkyLimit)
│   │   ├── RotationsPlus.Contracts/         # events + DTOs shared api↔worker
│   │   └── RotationsPlus.Common/            # auth policies, audit, errors, encryption converters
│   ├── aspire/RotationsPlus.AppHost/  # local dev orchestration (Postgres, Redis, api, worker)
│   └── frontend/web/                  # React 18 + TS + Vite
├── tests/
│   ├── unit/RotationsPlus.Api.Tests/  RotationsPlus.Worker.Tests/
│   ├── integration/RotationsPlus.Integration.Tests/   # Testcontainers PG + WebApplicationFactory
│   └── e2e/ (Playwright)
├── infra/
│   ├── bicep/ (main.bicep + modules + .bicepparam per env)
│   └── pipelines/ (build-all.yml, deploy-dev/preprod/prod.yml, templates/, variables/)
├── tools/RotationsPlus.DataMigrator/  # the AWS→Azure ETL console app (see Plan_Migration)
└── docs/
```

Single `DbContext` (one database) with schema-per-module table prefixes is sufficient at this size; module boundaries are enforced in code (folder + internal visibility), not by separate databases.

### 3.3 Domain modules — mapping from current system

| Module | Absorbs current Strapi content-types | Absorbs current endpoints/logic |
|---|---|---|
| Identity | users-permissions users/roles, `otp` | login/register/2FA flows (replaced by Entra), profile linkage |
| Marketplace | `preceptor`, `program`, `specialty`, `hospital`, `favorite`, `preceptor-group` | search/filter endpoints, program CRUD, map data, favorites |
| Rotations | `rotation`, `rotation-tracker` | 30+ custom rotation endpoints: findPage, cart→rotations, confirm-by-token, start-confirm, cancel, evaluation/rating, status transitions |
| Payments | `payment`, `payment-transaction`, `promo-code`, `promo-code-usage`, `unlock`, `honorarium`, `adjust-payment` | `chargeStripe`, outstanding payment, refunds, honorarium stages, **new: Stripe webhooks** |
| Documents | `document`, `sign-document`, `docu-sign`, `pal-document`, `inno-document`, `validation-result` | agreement document flow (generate PDF → signed-copy upload → admin approval; replaces HelloSign), OCR result ingestion, PAL docs, PDF letter generation (visa/evaluation/completion). Legacy `sign-document`/`docu-sign` rows migrate as archived records only |
| Crm | `lead`, `lead-log`, `contact`, `issue`, `email`, `email-campaign` | lead CRUD/assignment/conversion, email compose/threads, call logs, customer-service issues, campaign sends |
| Notifications | `sms`, `sms-job`, `sms-log`, `whatsapp` | sendSms/sendWhatsapp, deferred SMS queue (Mon–Fri 8a–4p PT window), drip campaigns — all become queued messages |
| Reporting | `dashboard`, `sales-dashboard-data`, `health` | live score, analytics tabs, SDR/sales reports — computed via queries/materialized views instead of 1-minute-cron denormalization |
| Content | `blog-content` + frontend `constants.js` data | blog CRUD (admin-editable), reference data API (specialties, cities, hospitals, document types) |

### 3.4 Worker: Hangfire job inventory (replaces cron-tasks.js 1:1)

Hangfire chosen over Quartz/Azure Functions: dashboard UI for free (mounted under `/admin/jobs`, admin-policy protected), Postgres storage (no extra infra), retries/dead-letter semantics, and trivially testable job classes. Each job below is a small class with injected services — the 1,610-line file becomes ~15 focused, unit-tested classes.

| Job | Schedule (from current cron) | Notes for rewrite |
|---|---|---|
| `RotationStateMachineJob` | every minute | Status transitions (NotStarted→Active→To Be Evaluated…). Make transitions idempotent + audited; emit `RotationStatusChanged` to Service Bus |
| `DashboardMetricsJob` | every 2 min → **delete** | Replace denormalized counters with real-time queries + Redis cache (60s TTL) |
| `ProgramRatingAggregationJob` | every 5 min | Keep, or compute on rating-write event |
| `StartConfirmationReminderJob` | every 10 min | Emits notification messages |
| `PendingApproval48hSmsJob` | every 20 min | Twilio SMS via Notifications queue |
| `PendingApproval72hJob` + onboarding drip | every 30 min | Email/WhatsApp via queue |
| `SmsQueueDispatchJob` | hourly (respect Mon–Fri 8a–4p PT send window) | Window logic preserved exactly; timezone configurable |
| `ReviewReminder14dJob`, `DocumentUploadReminderJob`, `RotationCompletionJob` | hourly | |
| `DocumentDueDate7dEmailJob` | every 2h | |
| `InnodataSftpIngestJob` | every 4h | SFTP download → CSV parse → document status update + `DocumentValidated` event. Isolated behind `IOcrIngestionProvider` so the Phase-2 Azure Document Intelligence swap is one implementation change |
| `VisaInterviewTrackingJob` | daily 9am PT | |
| `WhatsAppCampaignJob` | daily midnight | Multi-stage templates (TEMPLATE_SID_1/2/3) preserved |

**Service Bus topics** (events, not RPC): `rotation-events` (status changes → notifications, reporting), `payment-events` (Stripe webhook results → rotation activation, receipts), `document-events` (OCR/agreement-upload results → reminders, rotation readiness), `notification-requests` (api/worker → outbound send with retry + dead-letter). Consumers follow SkyLimit's `BackgroundService` + `ServiceBusProcessor` pattern with dead-letter handling.

**Redis usage:** reference-data cache, live-score/dashboard cache, rate limiting counters, idempotency keys for webhook processing. Not used as a message bus (Service Bus owns that).

### 3.5 Authentication & authorization (two-directory Entra model)

**Decision (2026-06-11, confirmed against SkyLimit's actual setup):** staff and customers live in **separate Entra directories** — the same pattern SkyLimit uses (its workforce directory lists only employees/clinicians; patients are DB-only). Rotations Plus differs in that customers *do* log in, so they need a directory of their own:

- **Workforce Entra tenant** → **staff only**: `Admin`, `Sales`, `SDR`, `Institution`, `Coordinator`. Few accounts (~10–25), type Member, visible in the Azure portal Users list (mirrors the SkyLimit screenshot). Gets **Conditional Access + enforced MFA** (see §3.5 MFA decision). **No student/preceptor ever appears here.**
- **Entra External ID (CIAM) tenant** → **customers only**: `Student`, `Preceptor`. Separate directory, email+password + **Google social login**, branded sign-in pages, Microsoft-managed credential security. Customers are credential objects here (a directory you rarely open); all profile/business data lives in the DB `users`/`students`/`preceptors` tables, linked by object id.
- **Infrastructure (Azure subscription) tenant** = the workforce tenant; **no app user of any role appears as an Azure-resource principal** — Azure Portal/RBAC access is granted only to owners (+ build team). App `Admin` role ≠ any Azure access.
- **App registrations:** one pair per directory — staff: `rplus-web` (SPA, auth-code+PKCE via MSAL React) + `rplus-api` (exposes `access_as_user`); customers: `rplus-web-ext` (SPA) + `rplus-api-ext` (exposes `access_as_customer`). The API validates tokens from both issuers via two JWT-bearer schemes behind an issuer-routing **"Smart"** policy scheme (`AuthSchemeSelector` peeks the token issuer and forwards to the matching scheme), mapping both to a single internal role model (`RoleNames`). *(Implemented: marketplace slice 8 / PR #15. Runtime customer sign-in still needs the CIAM portal user-flow + admin consent — see `infra/ciam/README.md`.)*
- **App roles:** customer roles (`Student`,`Preceptor`) from External ID; staff roles (`Admin`,`Sales`,`SDR`,`Institution`,`Coordinator`) from the workforce tenant — emitted in tokens; ASP.NET policies form the hierarchy (Admin ⊃ Sales/SDR gating per `Plan_Admin.md`/`Plan_Sales_SDR.md`).
- **MFA enforced for staff roles** (Admin/Sales/SDR) — **replaces the current homegrown 2FA entirely**: today (`AdminLogin.js` → `verifyAdmin`/`loginAdmin`) email+password triggers a Twilio SMS + email 4-digit code that the backend validates. In the rewrite, Entra performs the second factor — the custom code AND the Twilio dependency for login disappear (a security upgrade: policy-enforced, can't be bypassed). The dedicated staff URLs (`/rotationsplusadmin`, `/rotationsplussales`, `/rotationsplussdr`, `/rotationsplusinstitutions`) are kept as routes that trigger the Entra flow.
  - **MFA method — owner decision (2026-06-11): PHASED.** Phase 1 (cutover period): **Entra SMS code** — closest to today's texted-4-digit feel, keeps staff login familiar during the highest-stress window. Phase 2 (post-stabilization): **switch staff to authenticator-app (TOTP/push)** — Microsoft-recommended, phishing-resistant, "approve on phone"; addresses SMS's SIM-swap weakness + Microsoft's SMS-MFA deprecation. Both are Entra-sent, **not** via your Twilio account. Switch = an Entra authentication-methods policy change + a one-time re-enroll prompt; no code change.
  - Enrollment: each staff member sets up SMS **once** at first sign-in (during PREPROD dogfooding → zero cutover disruption); re-enrolls the authenticator app at the Phase-2 switch.
- Domain user record (`users` table) keyed by Entra `oid`, holding profile/business fields; created on first token via claims-mapping middleware (SkyLimit ClaimsEnrichment pattern simplified — single tenant, no tenantId).
- **Migration:** bulk pre-provision via Microsoft Graph; passwords cannot be imported (bcrypt) → one-time reset campaign (accepted by owner; runbook in `Plan_Migration.md §8`).
- Integration tests use SkyLimit's `TestAuthHandler` header-scheme pattern; Playwright uses MSAL mocking.
- **Tokened deep links** (preceptor confirm/start links in emails) remain app-issued single-purpose tokens — they are anonymous-but-tokened endpoints, independent of Entra.

### 3.6 Data layer

- **Azure Database for PostgreSQL Flexible Server**, one database `rotationsplus`; EF Core 9 + Npgsql, code-first migrations.
- **Clean schema redesign** (~30 tables): Strapi's `_links` join tables → real FKs; users-permissions tables → `users` + Entra; duplicate document tables (`document`, `sign_document`, `docu_sign`, `pal_document`, `inno_document`) → one `documents` table + `document_kind` + provider-specific detail tables only where needed; JSON columns (`detail`, `pal_info`, `expertise_area`, `privilege_hospitals`) → `jsonb` with typed POCO mapping where shape is stable.
- **Soft delete + global query filters** for rotations/documents/users; **audit table** (append-only, SkyLimit AuditService pattern) for admin actions, payment changes, status transitions.
- **Encrypted columns** (EF value converters, key in Key Vault): `bank_info`, SSN-bearing fields if any, signature data.
- Rotation **state machine made explicit**: allowed-transition map in code, every transition recorded in `rotation_status_history` (replaces scattered boolean flags like `sent_72hours_reminder` → reminder ledger table).
- Full old→new mapping table lives in `Plan_Migration.md §6`.

### 3.7 Frontend architecture

- **React 18 + TypeScript 5 + Vite 8 on Node 22** (owner override 2026-06-14; was Vite 6 — Node 22 is current LTS and Vite 8 requires Node ≥22.12, pinned via `engines`/`.nvmrc`); React Router 6; **TanStack Query** for server state + small **Zustand** stores for UI/session state (kills Redux boilerplate); **React Hook Form + zod** for the form-heavy profile/onboarding screens; central typed `httpClient` with MSAL token acquisition and safe error mapping (copy SkyLimit `httpClient.ts`).
- **One app, four areas** by route-level code splitting (`React.lazy`): `public/`, `student/`, `preceptor/`, `staff/` (admin+sales+SDR). Admin code never ships to students (fixes the 4.4MB bundle).
- **Prerendering:** public routes (`/`, `/our-process`, `/our-team`, `/for-preceptors`, `/consultingservices`, `/resources`, `/faq`, `/blog`, `/blogs/:slug`, legal) emitted as static HTML at build (vite prerender step) → SEO without a second framework. `react-helmet-async` per-page meta; regenerate sitemap.xml at build; 301 parity list in Plan_Migration.
- **Mobile-first mandate:** design tokens + a single component library (recommend **Mantine** or **MUI** — pick once, no Bootstrap), all layouts built from 360px up; tables become card lists under `md`; the Leaflet map gets a mobile mode (bottom-sheet list over map); touch-target ≥44px audit in Playwright viewport tests (375px, 768px, 1280px run in CI).
- Keep: **Leaflet + react-leaflet + clustering** (works, free), **Stripe Elements**, GA4/GTM/FB pixel (same IDs). (Calendly embed removed — unused per owner.)
- Replace: draft-js email composer → **TipTap**; moment et al → **date-fns only**; react-slick → Swiper or CSS scroll-snap.
- Reference data (the 1.5MB constants) fetched from Content API with long-lived cache headers.

### 3.8 PDF generation

Replace `html-pdf`/EJS with **QuestPDF** (free Community license under $1M revenue) for the 14 letter/evaluation templates — code-first, testable, no headless browser. Static assets (logo, signature image) move from the public S3 bucket into Blob/embedded resources.

### 3.9 Azure resources & SKUs (per environment)

| Resource | DEV | PREPROD | PROD | Notes |
|---|---|---|---|---|
| Container Apps Env | Consumption | Consumption | Consumption (api min 1–max 3 replicas; worker 1) | scale-to-zero allowed in DEV |
| PostgreSQL Flexible Server | B1ms, 32GB | B1ms | B2s or D2ds_v4, zone-redundant HA optional, PITR 7→35 days | |
| Azure Cache for Redis | (omit — in-memory fallback) | C0 Basic | C1 Standard | |
| Service Bus | Basic→Standard | Standard | Standard | topics need Standard |
| Storage (Blob) | LRS | LRS | GRS, soft delete + versioning | containers: `program-images` (private, read via SAS — PHASE 2b), `documents` (private), `avatars`, `public-assets`. Account-key connection string in Key Vault (Contributor pipeline can't grant Blob RBAC). |
| Key Vault | 1 per env | 1 | 1 (purge protection) | |
| Static Web Apps | Free | Free | Standard | custom domains via Cloudflare CNAME |
| ACR | Basic (shared across envs) | — | — | |
| App Insights + Log Analytics | 30d retention | 30d | 90d | alerts: error rate, job failures, SB dead-letters |
| Entra External ID | 1 tenant (free <50k MAU) | — | — | separate test users for dev/preprod |

Estimated run cost: **DEV ≈ $40–70/mo, PREPROD ≈ $80–120/mo, PROD ≈ $250–450/mo** — materially below the EC2+RDS+Bluehost baseline is not guaranteed, but in the same band with far more capability. Env-aware SKU map in `main.bicep` (SkyLimit pattern).

### 3.10 Testing strategy (the "no regression" backbone) — full detail in `Plan_Testing.md`

| Layer | Tooling | Gate |
|---|---|---|
| Backend unit | xUnit + Moq + FluentAssertions; EF InMemory for pure logic | PR pipeline, required |
| Backend integration | Testcontainers PostgreSQL + WebApplicationFactory + TestAuthHandler; covers every controller route + Hangfire job classes + SB consumers (in-memory transport) | PR pipeline |
| **Characterization tests** | Recorded request/response suites against the OLD Strapi API for the ~25 business-critical endpoints; replayed against the new API (field-level diff with mapping rules) | migration gate — see Plan_Migration §10 |
| Frontend unit | Vitest + React Testing Library, 70% coverage gate (SkyLimit standard) | PR pipeline |
| E2E | Playwright: the 8 money paths (signup→search→cart→pay [Stripe test mode], preceptor confirm link, doc upload, admin lead flow, honorarium) × 3 viewports | nightly + pre-release |
| Load smoke | k6 against PREPROD (search + dashboard endpoints) | before cutover |

### 3.11 Observability & operations

OpenTelemetry via ServiceDefaults → Application Insights: distributed traces (api↔worker via SB), Hangfire job success/duration metrics, SB dead-letter alerts, Stripe webhook failure alerts, uptime tests on `/health` + the public site. Structured logging (Serilog) with PII scrubbing. Cloudflare analytics retained for edge view.

---

### 3.12 UI/UX approach

> **⚠️ SUPERSEDED 2026-06-19 — Figma is now the VISUAL TARGET.** Owner reversed the 2026-06-11 "fresh redesign" decision: **forms and screens are built to match their Figma frames** (layout, spacing, components, styling), using the **Figma design system** (Colors `317:929`, Typography `317:1286`, Buttons `317:925`/`317:927`). The `#FF4874` brand + logo stay. Workflows/fields/states still come from legacy behavior + Figma content (unchanged). Areas with **no** Figma frame (Stripe payment element states, OCR statuses, impersonation, program-documents upload) are designed fresh and called out per `Docs/Figma_Inventory.md`. Already-built fresh-styled screens (admin console + portal forms) are being reworked to match Figma. The 2026-06-11 text below is kept for history but no longer governs the visual approach; its still-valid parts (mobile-first, accessibility, consistent form/table/empty/loading/error states, navigation-may-be-reorganized-but-never-removes-capabilities) continue to apply *within* the Figma-matched look.

**[SUPERSEDED 2026-06-11] Full visual redesign; preserved workflows.** (Owner decision 2026-06-11: the existing design is 6–7 years old, mobile poorly designed, desktop dated — do NOT reimplement it visually.) Information architecture, screen inventory, flow steps, and labels stay recognizable (regression protection + zero retraining); the **look, layout, and interaction patterns are designed fresh, in code**, to current standards: one component library (Mantine or MUI) + design tokens; mobile designed FIRST (bottom-sheet map search, card dashboards, slide-over filters, skeletons, ≥44px targets, accessibility); consistent form/table/empty/loading/error states.

**Design approval workflow (replaces a designer/Figma cycle):** before any style rolls out broadly, build **concept screens on DEV** — landing, search+map, student dashboard, one admin screen, each desktop + phone — owner iterates on real clickable pages until approved; the approved system then applies everywhere.

**Brand (owner decision 2026-06-11): logo and brand color (`#FF4874`) STAY — that's the brand.** Typography/secondary palette may modernize around them. **Navigation and presentation MAY be rethought** (menus, grouping, dashboard layouts, where actions live) — proposed and approved at the concept-screen stage. Constraint: navigation redesign reorganizes how functions are reached, never removes them — every capability in the parity checklists must remain findable.

**Figma's role = functional reference, not visual target** (connected 2026-06-11 via `figma-developer-mcp`, read-only PAT; file `pqMajeWlbrVBsj4AMbMXi7`; frame map with node IDs in **`Figma_Inventory.md`** — ~615 frames). Use it for: screen/field/state inventories, flow arrows, and the **150+ email/SMS/WhatsApp template mockups** (still the spec for notification content). Source-of-truth order: **legacy code/live behavior (functional truth) → Figma (content/flow reference) → per-dashboard docs (spec)**; visual decisions come from the new design system, not Figma.

### 3.13 Performance engineering (owner-reported pain: slow queries, long UI waits)

Root causes today → fixes in target:

| Today | Why slow | Target fix |
|---|---|---|
| Homepage/search loads ALL programs, filters client-side | huge payloads, slow first paint | server-side faceted search endpoint, paginated; Postgres indexes incl. trigram/tsvector for keyword search; map-pin endpoint scoped to viewport |
| Reports fetch up to 10,000 rows to the browser and aggregate in JS | network + CPU on client | server-side aggregation endpoints + Redis cache (60s); CSV export server-side |
| 4.4MB single bundle incl. 1.5MB constants.js | seconds of JS parse on mobile | route-level code splitting (students never download admin code), constants → cached reference API; ~85% smaller initial load |
| Strapi auto-CRUD deep-populates relations | over-fetching | explicit EF projections (DTO-shaped queries), no lazy-load surprises |
| No caching anywhere; 1-min cron hammers DB with raw SQL counter updates | DB contention | Redis for reference data/live-score; counters computed on read; HTTP cache headers + Cloudflare edge caching for public/static responses |
| html-pdf blocks the Node event loop | API stalls during letter generation | PDF generation async in Worker (QuestPDF), download via Blob SAS |
| Single EC2 box | no headroom | Container Apps autoscale on the API |

**Performance budgets (enforced, not aspirational):** API p95 < 300ms for search/dashboard endpoints; public landing page LCP < 2.5s on 4G mobile; route transitions < 500ms; budgets verified by k6 (API) + Lighthouse CI (frontend) against PREPROD; App Insights + Postgres slow-query log watched in hypercare and beyond.

### 3.14 Admin impersonation ("view as user") — new feature (owner-requested)

Support pain: students/preceptors report "something isn't showing on my dashboard" and staff can't see what they see. Design:

- "**View as user**" button on admin contact/student/preceptor profile pages (Admin role only; optionally Sales/SDR for their own accounts — default: Admin only).
- API endpoint `POST /api/admin/impersonation/{userId}` mints a **short-lived (15 min, renewable) app-issued impersonation token**: subject = target user, `impersonator` claim = admin's Entra oid, `mode=read-only`. Accepted by a dedicated auth scheme alongside the normal Entra JWT scheme — no Entra involvement, no password knowledge, works even if the user has never reset their password.
- Frontend opens the target's dashboard in a new tab with a persistent banner: "Viewing as Jane Doe (read-only) — Exit". All mutating endpoints reject read-only impersonation tokens **server-side**.
- Full audit: session start/end, impersonator, target, every API call made — surfaced in the admin audit log.

### 3.15 Program documents management — new feature (owner-requested)

Today ~27 programs' application forms are **hardcoded file paths in frontend code** (`getDocumentPath()` in StudentDocuments) — the dev team manually copies files onto the server to change them. Target: `program_documents` table + Blob storage + admin UI on the program-detail screen — **upload new, replace, and remove old documents** (mark required, effective dates). Replace/remove semantics: the old file is soft-deleted and archived (removed from student view immediately, retained in Blob + audit log so documents already downloaded/used by in-flight rotations stay traceable); the new file is effective immediately. Students' document page lists current documents dynamically; downloads via SAS. Zero developer involvement for content changes. (Also referenced in Plan_Admin §6 and Plan_Student §6.)

### 3.15b Blog / content management — new admin capability (replaces Strapi CMS)

Today blogs live in the Strapi admin panel (being removed); the public blog reads them via `blog-content` API. New design: blog editing becomes a first-class **"Blog"/"Content" section in the new Admin dashboard** (Admin role only).

- **Blog list:** table (title, category, Draft/Published status, date), search, New Post, edit/delete, optional feature/pin.
- **Editor:** title; **editable slug** (auto from title — **existing posts keep exact current slugs for SEO**); category dropdown (existing five: Clinical Rotations, Specialty, LORs, Residency, Med School); **TipTap rich-text body** with image upload → Blob (no manual server file copies); featured image/thumbnail; **SEO fields** (meta description, social-share/OG preview); publish controls (Save Draft vs Publish, optional scheduled publish date).
- **On publish:** live at `rotationsplus.org/blogs/{slug}`, listed under category, **sitemap regenerated**, and the prerendered public blog pages rebuild automatically (~1–2 min, no manual step). Data: `blog_posts` table (migrated from `blog_content`, slugs preserved). Parity/admin coverage noted in Plan_Admin §6.

### 3.15a Preceptor agreement flow (replaces HelloSign)

Admin triggers "send agreement" → system generates the agreement PDF (QuestPDF, preceptor details merged) and emails it → preceptor signs (print-sign-scan or external e-sign of their choice) and **uploads the signed copy** in their Documents page → admin reviews and approves (status: Sent → Uploaded → Approved/Rejected, with reminders via the existing notification jobs). All historic signed agreements exported from HelloSign into Blob during migration and attached to the preceptor record.

## 4. What we deliberately did NOT do

- **No microservices** — modular monolith + worker (rationale §3.1). Revisit only on real scale signals.
- **No Next.js/SSR** — prerendering covers SEO for a mostly-static public surface; one build system.
- **No CMS** (Strapi removed, nothing SaaS added) — blog editing moves into the new Admin dashboard; templates live in the repo.
- **No Azure Front Door** for now — Cloudflare stays (owner decision); revisit after stabilization.
- **No ASP.NET Identity** — Entra External ID chosen (owner decision: outsource credential security; one-time reset accepted).
- **No immediate Innodata replacement** — kept working as-is through migration; Azure AI Document Intelligence is the planned Phase-2 follow-up behind `IOcrIngestionProvider`.
