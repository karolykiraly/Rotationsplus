# Plan_Migration — AWS/Bluehost/GitLab → Azure/Azure DevOps

**Date:** 2026-06-11 · **Companion:** `Plan_Architecture.md`
**Goal:** Move Rotations Plus to Azure with a rewritten frontend/backend, **without regressions and with under one hour of user-visible downtime.**

---

## 1. Migration at a glance

| What moves | From | To | Method |
|---|---|---|---|
| Source code | GitLab | Azure DevOps Repos | `git push --mirror` (history preserved) |
| Frontend hosting | Bluehost shared hosting (scp) | Azure Static Web Apps | new build artifact + DNS flip |
| Backend hosting | AWS EC2 (Strapi/Node) | Azure Container Apps (.NET 9 api + worker) | rewrite + cutover |
| Database | AWS RDS PostgreSQL (us-west-1) | Azure Database for PostgreSQL Flexible Server | **transform-ETL at cutover** (rehearsed) |
| User files | EC2 local disk `public/uploads` + S3 bucket `rotationsplus` (us-west-1, static assets) | Azure Blob Storage | azcopy/rsync pre-sync + delta at cutover |
| Secrets | hardcoded in source | Azure Key Vault | rotate + store (P0, before everything) |
| Identity | Strapi users-permissions (bcrypt) | Entra External ID | Graph bulk import + one-time password reset |
| CI/CD | GitLab CI (ssh/scp) | Azure DevOps YAML + Bicep | new pipelines |
| Edge/DNS | Cloudflare | **Cloudflare (kept)** | TTL-based cutover mechanism |

Strategy: **big-bang cutover after a full parallel run on PREPROD**, not a strangler. Rationale: both tiers are rewritten and auth changes provider — a strangler would require keeping Strapi JWTs and the new Entra tokens interoperable across a long mixed period, which is more risk than a rehearsed cutover for a system this size.

---

## 2. Secrets & stealth-mode plan (revised 2026-06-11)

**Constraint (owner decision):** this project is confidential — only the owners know; the current dev team is being transitioned out. Immediate secret rotation would tip them off (and requires touching the EC2 config they manage), so **rotation is deferred to offboarding day**. Accepted risk, stated honestly: the exposed credentials below remain live and known to the outgoing team until that day; the riskiest window is between "team learns" and "access revoked" — the §2.1 runbook exists to make that window hours, not days.

### 2.0 Stealth rules while building (until offboarding)

1. **Isolated vendor sandboxes for DEV/PREPROD:** new Stripe test sandbox, separate SendGrid free account, Twilio test creds — zero footprint in the real vendor dashboards the team can see. PROD switches to the real accounts only at cutover (Key Vault config change). (HelloSign/Calendly/Dwolla: no sandboxes needed — integrations removed entirely, owner-confirmed unused 2026-06-11.)
2. **No custom DNS for DEV/PREPROD** until offboarding — anyone with Cloudflare access would see new records. Use Azure default hostnames (`*.azurestaticapps.net`, `*.azurecontainerapps.io`); attach `dev/preprod.rotationsplus.org` post-offboarding.
3. **No GitLab access changes / no repo mirror** until offboarding — build from the local code snapshots already in hand; mirror history to Azure DevOps after.
4. **All new infrastructure in owner-only accounts** (new Azure tenant, new DevOps org, sandbox vendor accounts).
5. **Quiet access inventory NOW (owner):** who on the current team holds access to Cloudflare, AWS, Stripe, Twilio, SendGrid, HelloSign, GitLab, Bluehost, Google Admin. This list becomes the §2.1 script.
6. Legacy prod fixes continue through the current team as normal (they must not see behavior changes).

### 2.1 Offboarding-day runbook (the new P0 — execute within hours, in order)

1. Revoke GitLab members + deploy keys + CI variables access; 2. AWS IAM users/access keys; 3. Cloudflare seats; 4. vendor dashboard seats (Stripe, Twilio, SendGrid, HelloSign); 5. Bluehost FTP/SSH; 6. Google Admin if any; 7. **then rotate every credential in the table below** and update the legacy EC2 `.env` (small config edit + service restart — owner executes with step-by-step dictation); 8. verify legacy site healthy post-rotation (smoke: login, search, one test email/SMS).

Live credentials are in source control **today** (see `Plan_Architecture.md §2.1`). Anyone with repo access (3 generations of dev shops!) has them.

| Credential | Where to rotate | Post-rotation home |
|---|---|---|
| Stripe live + test secret keys | Stripe Dashboard → Developers → API keys (roll) | KV secret `stripe-secret-key` |
| SendGrid API key | SendGrid → Settings → API Keys (delete + recreate, scoped) | KV `sendgrid-api-key` |
| Twilio auth token | Twilio Console → promote secondary token | KV `twilio-auth-token` |
| HelloSign API key | HelloSign/Dropbox Sign settings — revoke; **export all historic signed documents to Blob archive BEFORE closing the account** (legal records) | — (integration removed) |
| Innodata SFTP password | request change via Innodata contact | KV `innodata-sftp-password` |
| RDS `postgres` password | AWS RDS → modify master password (coordinate with EC2 app config!) | KV `legacy-rds-password` (needed for ETL) |
| Dwolla, Calendly tokens | revoke (unused server-side) | — |
| GitLab CI ssh keys | rotate after DevOps migration | — |

Update the EC2 `.env`/config by hand with the new values (the only place they should now live until cutover). Also: purge `student_marketing_query.py` credentials and **treat git history as compromised** — the new Azure DevOps repos start from the new codebase; the mirrored legacy repos remain access-restricted archive.

**Owner actions needed:** access to Stripe/SendGrid/Twilio/HelloSign/AWS consoles; Innodata account manager contact; **scheduling the offboarding date** (rotation timing is keyed to it, ideally well before cutover so the team-transition risk and the cutover risk don't stack on the same day).

---

## 3. Foundation setup (Week 0–1)

1. **Azure tenant + subscription** (new, separate from SkyLimit). Resource groups: `rg-rplus-dev`, `rg-rplus-preprod`, `rg-rplus-prod` (+ `rg-rplus-shared` for ACR).
2. **Entra External ID tenant**: user flows (email+password, Google), app registrations `rplus-web` + `rplus-api`, app roles (Student/Preceptor/Admin/Sales/SDR/Institution/Coordinator), MFA policy for staff roles.
3. **Azure DevOps org + project** `RotationsPlus`: repos (`rotationsplus` mono-repo for new code; `legacy-web`, `legacy-backend` mirrored read-only), variable groups per env, service connections (ARM via workload identity federation, ACR), environments with approval gates on PREPROD/PROD.
4. **Bicep IaC** (clone SkyLimit `main.bicep` + modules, scaled to: Container Apps env, 2 apps, PG Flexible, Redis, Service Bus, Storage, Key Vault, SWA, App Insights). Deploy DEV first; PREPROD/PROD from the same template + parameter files.
5. **Pipelines — explicit promotion model** (clone SkyLimit templates):
   - `build-all.yml` — PR validation: dotnet build+unit+integration (Testcontainers), frontend typecheck+vitest+build.
   - `deploy-dev.yml` — **every merge to `develop` auto-deploys to DEV** (images → ACR tagged with build id, Bicep, deploy api/worker/SWA). DEV is the continuous review environment — the owner can look at every change there.
   - `deploy-preprod.yml` — **promotes the exact same image tags + SWA artifact from DEV to PREPROD** (no rebuild — what you approved on DEV is bit-for-bit what runs on PREPROD). Triggered manually; Azure DevOps **environment approval gate** (owner approves). PREPROD has its own Key Vault/variable group and gets a masked prod-copy dataset for parity testing (refresh pipeline `refresh-preprod-data.yml` runs the DataMigrator against an RDS snapshot).
   - `deploy-prod.yml` — same promotion pattern PREPROD→PROD, double approval gate (owner). Used at cutover and for all releases after.
   - Promotion rule: **nothing reaches PREPROD that didn't run on DEV; nothing reaches PROD that didn't pass the parity suite on PREPROD.**
6. Local dev: .NET Aspire AppHost (Postgres+Redis containers, api, worker) — single `dotnet run`.
7. **Environment URLs** (custom domains on `.org` — no collision with legacy, which uses `.com` for staging):

| Env | Site (SWA) | API (Container Apps) | Notes |
|---|---|---|---|
| DEV | `dev.rotationsplus.org` | `dev-api.rotationsplus.org` | noindex + access-restricted (Cloudflare Access/IP allowlist) |
| PREPROD | `preprod.rotationsplus.org` | `preprod-api.rotationsplus.org` | same restrictions; prod-copy data |
| PROD | `www.rotationsplus.org` (flips at cutover) | `api.rotationsplus.org` (**new name**) | legacy `api.rotationsplus.com` stays untouched → only the `www` record flips on cutover night |

   Azure auto-generates default hostnames (`*.azurestaticapps.net`, `*.azurecontainerapps.io`) at Bicep deploy; I then add Cloudflare CNAMEs + register custom domains (free managed TLS on both services) and add every URL to the Entra app registration redirect URIs. Legacy `preprod.rotationsplus.com` / `preprod-api.rotationsplus.com` / `api.rotationsplus.com` keep running untouched for characterization testing until decommission.

---

## 4. Build phases (rewrite roadmap with regression protection)

| Phase | Content | Exit criteria |
|---|---|---|
| **P1 Foundation** (wk 0–2) | §3 above + skeleton api/worker/SPA deployed to DEV through pipeline; Entra login round-trip works | green pipeline, login on DEV |
| **P2 Core domain** (wk 2–6) | Schema + EF model; Identity/Marketplace/Content modules; search + map API; reference-data API (kills constants.js); public site + prerendering | characterization tests for search/program endpoints pass |
| **P3 Money & documents** (wk 6–11) | Rotations lifecycle + state machine; cart/checkout + Stripe (+ **webhooks**, new); promo/unlocks/credits; Documents module (Blob, agreement upload flow, Innodata SFTP ingest, QuestPDF letters, program-documents admin upload); Notifications + all Hangfire jobs | E2E money paths green in DEV with test-mode vendors |
| **P4 Dashboards** (wk 10–16, overlaps) | Student dashboard; Preceptor dashboard + onboarding; Admin (45 routes — see Plan_Admin); Sales/SDR; CRM/leads/reports | per-dashboard parity checklists signed off |
| **P5 Migration readiness** (wk 14–18) | DataMigrator ETL + 2 timed rehearsals; user import dry-run; PREPROD parallel run with masked prod copy; load smoke; cutover runbook drill | rehearsal ETL < 30 min, parity suite green on PREPROD |
| **P6 Cutover** (1 evening + hypercare wk) | §9 runbook | prod live on Azure, rollback window closed |
| **P7 Decommission + Phase-2** | §11; then Azure AI Document Intelligence to replace Innodata | AWS/Bluehost/GitLab off |

(Calendar estimates recalibrated 2026-06-11 for actual staffing: **owner at ≤4 h/day + AI pair doing implementation**. The constraint is owner review bandwidth + elapsed-time items (Innodata coordination, 2-wk dogfooding, rehearsals, T-7d comms), not code production. **Realistic: ~23–26 weeks (≈6 months) to cutover; aggressive ~4.5–5 months.** Compression levers: staff recruited as PREPROD testers from start of P4 (biggest lever, ~3–4 wks), vendor/tenant/secret items fired in week 1, parity effort risk-ranked (money paths exhaustive, low-traffic reports lighter). Do NOT compress the P5 rehearsal program — it's the basis of cutover confidence.)

---

## 5. Source control migration (GitLab → Azure DevOps)

```bash
git clone --mirror git@gitlab:.../rotationsplusweb-v4.git
cd rotationsplusweb-v4.git && git push --mirror https://dev.azure.com/<org>/RotationsPlus/_git/legacy-web
# same for backend
```
Legacy repos = read-only reference. New mono-repo starts clean (no leaked-secret history). GitLab kept read-only 90 days, then archived/exported.

---

## 6. Database migration (AWS RDS → Azure PG Flexible) — the core trick

**Why not logical replication / DMS:** the schema is being *transformed* (Strapi link tables → FKs, users-permissions → Entra-linked users, 5 document tables → 1). Continuous replication would require an online transform layer — high complexity to save minutes. The DB is small (<5k MAU marketplace; well under 10 GB). A **rehearsed transform-ETL** is simpler, testable, and fast.

**Tool:** `tools/RotationsPlus.DataMigrator` (.NET console): reads legacy RDS via Npgsql, maps to the new EF model, writes to Azure PG. Features: per-entity mapping classes, row-count + checksum report, idempotent re-run (truncate-and-load), `--dry-run`, `--since` for delta of append-only tables.

**Mapping highlights** (full mapping maintained in the migrator itself):

| Legacy (Strapi) | New | Transform |
|---|---|---|
| `up_users`, `up_roles`, `*_links` | `users` (+ Entra object id column, filled by user-import step) | role name → app-role enum; bcrypt hash dropped (Entra reset) |
| `students`, `students_*_links` | `students` with real FKs | enum strings preserved |
| `preceptors` (`bank_info` JSON) | `preceptors` + `preceptor_bank_details` (encrypted) | encrypt on load |
| `rotations` (+ `sent_*_reminder` booleans) | `rotations` + `rotation_status_history` + `reminder_ledger` | synthesize history from dates/flags |
| `documents`, `sign_documents`, `docu_signs`, `pal_documents`, `inno_documents` | `documents` (+kind) + `signature_requests` + `ocr_validations` | de-dupe by filepath+rotation |
| `payments`, `payment_transactions`, `adjust_payments` | `payments` + `payment_adjustments` | amounts verified vs Stripe export |
| `leads`, `lead_logs`, `contacts`, `issues` | same shape, cleaned | |
| `blog_contents` | `blog_posts` | slugs preserved **exactly** (SEO) |
| files in `public/uploads/...` paths | Blob URLs `documents/{id}/...` | path rewrite table used by file copy step |

**Rehearsals (early + repeated):** always ETL **from an RDS snapshot restore** (frozen, consistent, zero risk/load on live PROD — never read live), and **into DEV** for rehearsals (PROD Azure stays empty/clean until real cutover so its verification report is unambiguous).
- **Rehearsal #0 — early (end P2/early P3), as soon as schema stabilizes:** snapshot → DEV. Purpose: surface data dirt (orphan rows, case-duplicate emails, inconsistent JSON, draft/published dupes) when it's cheapest to handle. Earlier = cheaper.
- **Rehearsals #1/#2 — P5:** full-data, timed, < 30 min, clean verification report. (Stealth: a restored RDS instance is visible to anyone with AWS console access — if the outgoing team still has it, restore briefly + delete, or defer full-data rehearsals to post-offboarding; small-subset schema rehearsals can run anytime.)
Verification report each run: row counts (source vs target through mapping), checksums, payment/honorarium sums to the cent, rotation-status distribution, orphan-FK + sequence-vs-max(id) scans. Target: **ETL < 30 minutes** end-to-end.

**Connectivity:** RDS appears publicly reachable behind a security-group IP allowlist (the marketing Python script connects directly) — so access = an SG rule for the migration runner's IP, no VPN needed. Verify early.

**Owner actions (total ~2–3h across all rehearsals):** AWS IAM user (S3 read keys), RDS SG rule + read-only ETL user, snapshot create/restore clicks per rehearsal (dictated), EC2 SSH key post-offboarding, review each verification report, the two cutover GO/NO-GO calls.

### 6.1 DB migration failure modes & how each is caught

| # | Failure mode | Caught/prevented by |
|---|---|---|
| 1 | Dirty data: orphan rows, case-duplicate emails (breaks Entra import), nulls, inconsistent JSON, Strapi draft/published duplicate rows | Rehearsal #1 exists to find these at zero stakes; migrator dedupe/cleansing rules added per finding |
| 2 | Connectivity (SG blocks runner, ISP IP change) | early connectivity test; SG maintenance |
| 3 | Identity/sequence collisions after load | harness checks max(id) vs sequence per table |
| 4 | Timezone misinterpretation of legacy timestamps (US/Pacific assumptions) | date spot-checks in verification report vs live UI |
| 5 | Forgotten writer during cutover ETL (stray cron/script) | C10 EC2 crontab audit; stop entire Strapi process not just web |
| 6 | Financial totals ≠ Stripe export | hard stop at GO/NO-GO #1 → abort, investigate, retry another night |

---

## 7. File storage migration (EC2 uploads + S3 → Azure Blob)

Discovery finding: Strapi uses the **local provider** — user documents live on the EC2 disk under `public/uploads/`, *not* S3. The S3 bucket (`rotationsplus`, us-west-1) holds static assets (logo.png, sign.png) referenced by PDF templates.

1. **Pre-sync (days before):** 
   - EC2 → Blob: `azcopy copy` from a mounted/rsynced dump of `public/uploads` → container `documents` (or rsync EC2→VM→azcopy). Preserve relative paths.
   - S3 → Blob: `azcopy copy 'https://rotationsplus.s3.us-west-1.amazonaws.com/...' 'https://strplusprod.blob.core.windows.net/public-assets' --recursive` (azcopy supports S3 source with AWS creds).
2. **Delta at cutover:** re-run azcopy/rsync with `--overwrite=ifSourceNewer` during the maintenance window (minutes — few new files per day).
3. App references: DataMigrator rewrites stored paths; QuestPDF templates use embedded/Blob assets. Private docs served via short-lived SAS through the API (no public ACLs — an upgrade over today's web-served uploads).

**Method detail (pre-copy early, sync delta at cutover — both target REAL PROD Blob; blobs have no "is this real data" ambiguity, so pre-staging in PROD is safe and intended):**
- **S3 assets → PROD Blob: anytime** — `azcopy copy` with S3 as native source (AWS access key), no server access needed.
- **EC2 `public/uploads` → PROD Blob: post-offboarding** (the real user documents; don't install azcopy on the server while the old team administers it). Bulk `azcopy sync` pre-copy starts the day after offboarding; **plan offboarding with enough runway before cutover** for this.
- **At cutover:** re-run `azcopy sync` — it re-validates the whole set and moves anything new **or changed**, so the weeks-old pre-copy stays correct and the delta is minutes.
- First post-offboarding task on EC2: `du -sh public/uploads` to size pre-sync lead time.

### 7.1 File migration failure modes & how each is caught

| # | Failure mode | Caught/prevented by |
|---|---|---|
| 1 | DB references files missing on disk (or files with no DB record) — years of rot | pre-audit script cross-references DB filepaths ↔ disk listing both directions; orphan decision list |
| 2 | Uploads folder much larger than assumed | day-one `du -sh` sizing; pre-sync started days early |
| 3 | Documents accidentally public in Blob (breach) | integration test asserts container ACLs + SAS expiry (Plan_Testing §3.5) + manual external probe |
| 4 | Files uploaded during cutover gap | maintenance mode blocks uploads; final `azcopy sync` delta before reopening |
| 5 | SAS/credential expiry mid-copy; missing content-type metadata | long-lived SAS for the window; post-copy metadata fix-up pass + sample download check |

---

## 8. User/identity migration (→ Entra External ID)

1. Export users from legacy DB (email, name, role, status), **split by tier**.
2. **Staff (Admin/Sales/SDR/Institution/Coordinator) → workforce Entra tenant**: small number, provisioned first (Graph or manual), MFA enrolled, used for PREPROD UAT. These are the accounts visible in the Azure portal Users list.
3. **Customers (Student/Preceptor) → Entra External ID (CIAM) tenant**: **bulk create via Microsoft Graph** (batch API) — email as sign-in identity, role assigned; bcrypt passwords can't import → random unusable password + reset-required → password-reset campaign (below). De-dup **case-insensitively on email first** (legacy dirty data may have case-variant duplicate emails — a known ETL hazard, §6.1 #1).
4. DataMigrator links `users.entra_object_id` by email (per tier, against the correct directory).
4. **Reset campaign:** T-7 days announcement email; at cutover, "Welcome to the new Rotations Plus — set your password" email (SendGrid, existing domain rep) with deep link to the Entra reset flow; reminder at T+3 and T+10 days. Support macro prepared. Google social login offered on the new sign-in page (matching email auto-links).
5. Staff (Admin/Sales/SDR): provisioned first, MFA enrollment before cutover, used for PREPROD UAT.
6. Dormant accounts (>2 yrs inactive): imported but excluded from campaign (reset on demand).

---

## 9. Production cutover — full detail (who does what, rollback, confidence)

The operating model: build → continuous deploy to **DEV** (owner reviews everything there) → promote to **PREPROD** via approval-gated pipeline → run the parity/rehearsal program on PREPROD → **owner picks the cutover day** → execute this section.

### 9.1 Information to collect BEFORE the cutover (owner ↔ vendors)

Collect these during P5 (migration readiness), not on cutover night. Each item lists why it's needed.

**From your own accounts (grant access or do alongside me):**

| # | Item | Why | Owner action |
|---|---|---|---|
| C1 | AWS console access (or: an RDS snapshot export + a read-only DB user + the EC2 SSH key or a tar of `public/uploads`, + S3 read keys) | ETL source, file migration, rehearsals | create IAM user / hand over artifacts |
| C2 | Cloudflare account access (DNS edit + Workers) and a full export of the current DNS zone | cutover flips only `www`/apex/`api` records. **Mailboxes confirmed (2026-06-11): @rotationsplus.org email lives in Google Workspace** — the Google MX/SPF records in Cloudflare are untouched by the cutover and independent of Bluehost/AWS. Email risk: near zero (only rule: don't edit MX/SPF/DKIM records while flipping the web records) | invite me / export zone |
| C3 | Bluehost account audit: list everything hosted there (sites, cron jobs, FTP users — mail already ruled out: it's in Google Workspace). Domain registrations confirmed (2026-06-11) at **Namecheap** (DNS delegated to Cloudflare) — Bluehost cancellation carries no domain risk. Namecheap housekeeping: auto-renew ON, domain lock ON, account 2FA | so cancelling it later kills nothing else | 30-min review together |
| C4 | Stripe dashboard access (or you click while I dictate) | create the new webhook endpoint, reveal signing secret, run the live $1 test charge + refund | grant restricted-role access |
| C5 | Twilio console: confirm phone number(s), WhatsApp sender, 3 template SIDs, messaging-service config, **A2P 10DLC registration status, AND every inbound webhook URL** (number messaging webhook, Messaging Service webhook, voice/status/recording callbacks) | new code references identical senders/templates; **webhook URLs must be repointed at cutover** or preceptor SMS replies vanish silently | inventory screenshot incl. all webhook URLs |
| C6 | HelloSign account: **export ALL historic signed documents** (preceptor agreements — legal records) → Blob archive; then account is closed at decommission. No new-system integration (removed) | signed-document preservation | grant access for the export |
| C7 | SendGrid: confirm domain-authentication DNS records (they live in Cloudflare — don't touch), full list of sender addresses in use | deliverability continuity | inventory |
| C8 | GitLab maintainer access | repo mirror | invite |
| C9 | Google Search Console ownership (+ GA4/GTM admin) | post-cutover SEO monitoring; verify new prerendered pages | add me as user |
| C10 | The legacy EC2 crontab + any non-repo scripts on the box | catch automation that lives only on the server (like `student_marketing_query.py` siblings) | `crontab -l` + home-dir listing |

**From vendors (you initiate, I prepare the technical content):**

| # | Vendor | Ask | Deadline |
|---|---|---|---|
| V1 | **Innodata** (top external dependency) | (a) confirm whether their SFTP allowlists our source IP; (b) register the new Azure egress IP (we will provision a NAT Gateway so PROD has ONE stable IP to give them); (c) agree a test window where we connect from PREPROD; (d) rotate the SFTP password (P0) | ≥3 weeks before cutover; **tested from PREPROD ≥1 week before** |
| V2 | Stripe | nothing to ask — but export the last 90 days of charges pre-cutover for reconciliation | T-1 day |
| V3 | Twilio/Meta | none if sender unchanged (deliberate) | — |
| V4 | Bluehost | cancellation terms + mailbox migration path if C3 finds mailboxes there | before decommission, not cutover |

### 9.2 What YOU must do manually (I cannot do these)

1. **Create/own the accounts:** Azure tenant + subscription + billing, Azure DevOps organization, Entra External ID tenant admin consent. (I generate every script/Bicep/pipeline; you run the first privileged grant.)
2. **Approve the gates:** PREPROD and PROD pipeline approvals are yours.
3. **Pick the cutover date** (recommend Sat ~22:00 PT) and **be available for the ~2h window** — there are two go/no-go decisions only you can make (below).
4. **Vendor dashboard changes at cutover** (Stripe webhook activation) — either you click while I dictate the exact values, or you pre-authorize me per action in the session.
5. **Send the user comms** (I draft all of them): T-7 announcement, cutover-night notice, password-reset campaign, T+3/T+10 reminders.
6. **The live $1 payment test** at T+45 (your card, immediately refunded).

### 9.3 What I do (everything else)

All technical execution: rehearsing and running the ETL, azcopy file sync, deploying/verifying the Azure stack, the Cloudflare maintenance Worker and DNS changes (with your account access), Entra user import, smoke suites, monitoring dashboards, drafting comms and support macros, the verification reports at each gate, and post-cutover hypercare watching (App Insights alerts, Stripe webhook health, SB dead-letters, Search Console).

### 9.4 Go/no-go preconditions (cutover is BLOCKED until all are green)

- ✅ Two timed ETL rehearsals from RDS snapshots, < 30 min, verification report clean (row counts + checksums + money totals)
- ✅ Parity suite green on PREPROD against prod-copy data (per-dashboard checklists in Plan_Admin/Preceptor/Student/Sales_SDR signed off by you)
- ✅ Staff dogfooding on PREPROD ≥ 2 weeks; staff Entra accounts + MFA enrolled
- ✅ Innodata connectivity proven **from PREPROD through the PROD NAT egress IP**
- ✅ Stripe webhook tested end-to-end in test mode on PREPROD
- ✅ C1–C10 collected; V1 confirmed; mail hosting (C2) answered
- ✅ Rollback drill executed once on PREPROD (flip DNS to a dummy origin and back, restore-from-snapshot exercised)

### 9.5 Cutover runbook (target ≤ 60 min maintenance, expect ~30–45)

Roles: **[ME]** = I execute, **[YOU]** = owner action, **[BOTH]** = I dictate/verify, you click.

| T | Step | Who | Rollback at this point |
|---|---|---|---|
| T-7d | Announcement email to all users; freeze legacy deploys | [YOU] sends (I draft) | n/a |
| T-24h | Cloudflare TTLs for `www`, apex, `api` → 60s; verify Azure PROD stack healthy (full smoke suite); stage Entra user import (accounts pre-created, reset-required) | [ME] | n/a |
| T-2h | Blob pre-sync delta #1 (EC2 uploads + S3 assets); export Stripe last-90d for reconciliation; final go/no-go check of §9.4 board | [ME] | n/a |
| **T+0** | **Maintenance ON** — Cloudflare Worker serves branded "back in ~30 min" page for www + api (legacy stack still running underneath, now receiving no traffic) | [ME] | turn Worker off → site back on legacy in <2 min |
| T+5 | Stop Strapi process + cron on EC2 (freezes all state changes) | [ME] | restart Strapi, Worker off |
| T+10 | Run DataMigrator full ETL → Azure PG; auto-verification report (per-table counts, checksums, payment sums, rotation status distribution) | [ME] | abort: nothing changed on legacy |
| T+25 | **GO/NO-GO #1** — review verification report together. Any red number → abort tonight, retry another day (cost: ~30 min outage, zero data risk) | [BOTH] | abort = restart Strapi, Worker off |
| T+30 | Blob delta #2; flip Cloudflare DNS: `www` → Static Web App, `api` → Container Apps ingress | [ME] | flip records back (60s TTL) |
| T+35 | Vendor switch: Stripe webhook endpoint enabled (live mode); **Twilio inbound webhooks repointed** to new api (number + Messaging Service + status/recording callbacks) | [BOTH] | Stripe: disable new webhook; Twilio: restore old webhook URLs (saved) |
| T+40 | Maintenance OFF. Smoke suite: staff login (MFA), test-student login via reset link, search + map, program page, **live $1 charge + refund** [YOU], document upload → Blob, admin dashboard numbers, outbound email + SMS test | [ME] + [YOU] for payment | re-enable Worker, DNS back, vendors back — full return to legacy in ~10 min |
| T+60 | **GO/NO-GO #2** — declare success. Password-reset campaign wave 1 sent; hypercare monitoring on (App Insights alerts live; I watch the first hours) | [BOTH] | from here, rollback enters the §9.6 windowed procedure |
| T+72h | Rollback window closes; legacy becomes read-only cold standby (30 days), then decommission per §11 | [ME] | fix-forward only |

### 9.6 Rollback plan (detailed)

**Design principle: the legacy stack is never modified.** Strapi, RDS, EC2 files all stay intact and stopped-but-startable until T+72h. Rollback is therefore always "go back to exactly what ran yesterday," and old passwords still work after rollback (we never touched legacy credentials — Entra resets affect only the new world).

| Phase | Trigger examples | Procedure | Time to restore | Data loss |
|---|---|---|---|---|
| **A. Before DNS flip** (T+0→T+30) | ETL verification fails, any infra wobble | Worker off, restart Strapi/cron | **< 5 min** | none |
| **B. After flip, before reopen** (T+30→T+40) | smoke test failures | DNS records back, vendors re-pointed, Worker off, restart Strapi | **~10 min** | none |
| **C. Open, early window** (T+40→~T+4h) | severe functional/payment defect with no fix-forward | maintenance ON; DNS + vendors back; restart Strapi; export Azure-side writes made during the window (new users/rotations/payments/doc uploads — a prepared `delta-report` query) and re-enter manually (expected volume at Sat-night traffic: a handful) | **~15 min** + manual re-entry | none if delta re-entered; Stripe is independent source of truth for any payments taken |
| **D. Late window** (T+4h→T+72h) | only for catastrophic issues | same as C but the delta grows; decision = rollback-with-reconciliation vs fix-forward. Default: **fix-forward** — we built the whole pipeline to ship fixes to PROD in minutes | ~15 min + reconciliation effort | bounded by delta report |
| **E. After T+72h** | — | no rollback; fix-forward only (legacy snapshot retained for data recovery, not for serving) | — | — |

Standing safeguards: Cloudflare TTL stays at 60s until T+7d; the legacy webhook/callback values are written down in the runbook sheet; an RDS final snapshot is taken at T+5 regardless; the Azure DB gets PITR so even fix-forward mistakes are recoverable.

**One-way doors to be aware of:** (1) password-reset campaign — users who reset get Entra passwords; on rollback their *old* legacy passwords still work, but comms would be confusing → that's why the campaign fires only **after** GO/NO-GO #2. (2) New uploads/payments during window C/D need the delta re-entry. There are no other irreversible steps in the runbook.

### 9.7 Confidence assessment (honest)

**Cutover-night mechanics: 9/10.** The mechanism is deliberately boring — a rehearsed ≤30-min ETL of a small database plus a 60-second-TTL DNS flip, with the legacy stack untouched and restartable. Every step before reopening has a < 10-minute, zero-data-loss rollback, and we will have drilled both the ETL (twice, timed) and the DNS flip (once on PREPROD). The residual 1/10 is the class of surprises rehearsals can't fully kill: an unexpected Cloudflare behavior, a vendor hiccup in the window — all of which land in rollback phases A/B where the cost is "try again next Saturday."

**Functional parity of a full rewrite: 7.5/10 at cutover, by design pushed to ~9 before it.** The real risk of this project was never the migration night — it's "the new system behaves subtly differently." That's why the gate list in §9.4 exists: characterization tests recorded from the live API, the per-dashboard parity checklists, PREPROD running on a prod-data copy, staff dogfooding for two weeks, and the side-by-side notification-output comparison for the cron rewrite. Cutover does not happen until those are green — the confidence number is something we *earn* through P5, not assume.

**Net:** with the §9.4 preconditions met, I'd proceed without hesitation: worst realistic outcome on the night is a ~30-minute maintenance window followed by "we go again next week," not a damaged production system.

---

## 10. Integration cutover checklist (per vendor)

| Vendor | Stays | Changes at cutover | Risk/owner action |
|---|---|---|---|
| **Stripe** | same account, same customers/saved methods, same publishable key domain | new webhook endpoint `https://api.../webhooks/stripe` (signing secret → KV); **webhook handling is NEW functionality** — enable events: payment_intent.succeeded/failed, charge.refunded, charge.dispute.* | Test-mode parity in PREPROD; live $1 test at cutover |
| **Twilio** | same account/number/Messaging Service/WhatsApp sender + approved templates (TEMPLATE_SID_1/2/3); **A2P 10DLC registration carries over** (tied to unchanged account+number) | API called from .NET SDK; SMS send-window preserved; **inbound webhook URLs repointed to new api** (see below) | **(a)** Do not change WhatsApp sender/templates → Meta re-approval (days–weeks). **(b) Inbound-webhook trap:** preceptor SMS reply (YES/NO approval) + call status/recording callbacks POST to a webhook configured in the Twilio Console pointing at the OLD api. If not repointed, **outbound SMS works but preceptor replies silently vanish** (approvals drive the rotation pipeline). Mitigate: inventory all Twilio webhooks (C5), repoint at cutover as an explicit step (old URLs saved for rollback), and **test the SMS reply round-trip on PREPROD before cutover** |
| **HelloSign** | **REMOVED** (owner-confirmed unused) | export historic signed docs → Blob (C6), revoke key, close account at decommission | agreement signing replaced by in-app upload flow (`Plan_Architecture.md §3.15a`) |
| **Innodata OCR** | SFTP host/creds/CSV format unchanged; our side becomes `InnodataSftpIngestJob` | outbound source IP changes (Container Apps egress) | **They may allowlist our EC2 IP — confirm and register new egress IP (or NAT gateway for stable IP) BEFORE cutover.** Top external risk |
| **SendGrid** | same account, same authenticated domain (SPF/DKIM in Cloudflare DNS untouched) → deliverability preserved | API key rotated (P0); templates re-implemented as Razor/QuestPDF equivalents | keep `from` addresses identical |
| **Calendly** | **REMOVED** (owner-confirmed unused) | revoke token; embed not carried into new frontend | — |
| **Dwolla** | **REMOVED** (never used) | revoke keys | — |
| **Analytics/SEO** | GA4 `G-EWKEGGCCDY`, GTM `GTM-KP4XC7Q`, FB pixel, BBB seal — same IDs in new app | prerendered pages improve crawlability; sitemap regenerated; **301 map** for any changed URLs (keep `/blogs/:slug` slugs identical); Google Search Console re-verify | watch Search Console 4 weeks post-cutover |
| **Cloudflare** | DNS/CDN/WAF kept | new origins (SWA + Container Apps); Rocket Loader **OFF** for the new app (it breaks module scripts); cache rules for static assets; optionally Turnstile on signup | owner: Cloudflare account access |

---

## 11. Decommission (after 30-day cold-standby)

1. Bluehost: download final backup of `public_html`, remove site files, cancel hosting — **only after** (a) the C3 audit confirms no stray sites/cron/FTP, and (b) domain registrations are confirmed elsewhere or transferred (→ Cloudflare Registrar) if they're held at Bluehost. Email already confirmed safe (Google Workspace).
1b. **GitLab** (confirmed 2026-06-11): at project start — mirror repos to Azure DevOps, inventory CI/CD variables (Bluehost SCP user/host, `SSH_PRIVATE_KEY_02`, `SSH_ENTRY_DEV/PROD` — both the legacy-deploy documentation and secrets-to-rotate), check for scheduled pipelines and other projects in the account, cancel the leftover CircleCI account if still active. GitLab stays the emergency-fix path for legacy prod until the T-7d freeze. At cutover+72h: revoke deploy keys + CI variables, disable pipelines/runners, archive repos. ~90 days post-cutover: GitLab project export (zip → Azure Blob archive), close/downgrade account. Mirrored read-only copies live on in Azure DevOps.
2. AWS: final RDS snapshot → export to Azure Blob archive (7-year retention for financial records); terminate EC2; empty+delete S3 after asset verification; remove IAM users.
3. GitLab: archive/export, then close.
4. Strapi: nothing survives; admin panel gone (replaced by new Admin dashboard).
5. Rotate any credential that ever lived in legacy git history one final time.

---

## 12. Answers to the owner's questions

**Q1 — How do we migrate without hours of downtime? Estimated downtime?**
Parallel-build everything on Azure; the only "moment of truth" is data. Because the DB is small and the ETL is rehearsed (<30 min), we take a **single maintenance window of ~30–45 minutes** (worst case 60) at low traffic, behind a Cloudflare maintenance page with 60-second DNS TTLs. Users see a branded "back in a few minutes" page, not an outage. Rollback before go-live confirmation is a DNS flip back to the untouched legacy stack.

**Q2 — Anything you cannot implement?**
Code: nothing — all current functionality is reimplementable in .NET/React, including webhooks Strapi never had. Constraints that are *not code*: (a) Entra cannot import bcrypt hashes → one-time password reset (accepted); (b) Innodata IP-allowlist + SFTP coordination needs their account manager; (c) Stripe/Twilio/HelloSign/Cloudflare/AWS console actions need your credentials; (d) Azure tenant + DevOps org creation needs you; (e) WhatsApp template/sender changes would need Meta re-approval — so we don't change them; (f) SEO ranking continuity can be strongly protected (same URLs, prerendering, 301s) but no one can guarantee Google's reaction.

**Q3 — How do we take the frontend off Bluehost?**
Bluehost only serves static files today. The new SPA deploys to Azure Static Web Apps via pipeline; cutover is the Cloudflare DNS change for `www.rotationsplus.org` → SWA. Bluehost is then cancelled (after checking nothing else — especially mailboxes — lives on it).

**Q4 — How do we migrate Postgres from AWS to Azure?**
Not by replication — by a **rehearsed transform-ETL** (`RotationsPlus.DataMigrator`), because the schema is redesigned away from Strapi conventions at the same time. Rehearse ≥2× against RDS snapshots with row-count/checksum verification, then run the real one inside the cutover window. Legacy RDS stays untouched as instant rollback, then becomes an archived snapshot.

**Q5 — How do we remove Strapi as CMS?**
Strapi was never just the CMS — it's the whole backend, and the rewrite replaces all of it. Specifically for content: blog posts ETL into our own `blog_posts` table (slugs preserved) and get an editor in the new Admin dashboard; the 1.5MB of hardcoded frontend constants become reference-data tables + API; the 14 email/PDF templates become Razor/QuestPDF templates in the repo (versioned, reviewable). No third-party CMS is needed — and none is added.

---

## 12a. Weakest spots / lowest-confidence areas (with owner guidance)

The three areas carrying the most *residual* risk after mitigations. None are cutover-night mechanics (those are ~9/10); all are "does the new system behave like the old one" risks, rooted in the legacy system being undocumented + untested. Tracked here as the project progresses.

### #1 — Background-job / notification engine equivalence (the 1,610-line cron file)
**Risk:** time-based background behavior can't be characterization-tested like the API; undocumented rules from 3 teams; the week-long side-by-side run only catches scenarios that occur during that week — rare quarterly branches can slip to production. **Residual confidence ~6.5/10.**
**Owner guidance (2026-06-11):** *Not the biggest concern. We can test it, and if issues surface after cutover we fix them.* → Accepted as fix-forward. Action: still run the side-by-side diff, but treat post-cutover hypercare on notifications as expected, not a failure. Keep the reminder-ledger audit so any missed/duplicate message is traceable.

### #2 — Innodata OCR (external dependency, outside our control)
**Risk:** depends on a third party to allowlist our new Azure egress IP and respond on our timeline; least-documented integration; quiet failure mode (validation just never returns). **Residual confidence ~6/10** (variance is external).
**Owner guidance (2026-06-11):** *I will communicate with Innodata directly; we should be ok.* → Owner owns the relationship/coordination. Action: owner opens the channel early (P5 or sooner); we provide the technical asks (stable NAT egress IP, PREPROD test window, SFTP password rotation) and test from PREPROD weeks before cutover.

### #3 — Verification bandwidth vs. full-rewrite parity
**Risk:** "no regressions" ultimately rests on human eyes catching subtle differences; ~4h/day reviewer bandwidth, possibly owners-only until late (stealth keeps domain-expert staff out); dirty data (3 teams) unknown until first rehearsal. Subtle wrongness can pass review for lack of hours/domain memory. **Residual confidence ~7/10.**
**Owner guidance (2026-06-11):** *We must shoot for a better system even with some regressions. Weigh it against the current liabilities — exposed public/secret keys, performance problems, constant issues, and Strapi's exposure.* → **Strategic reframe: the status quo is NOT a safe baseline to preserve — it is a liability to escape.** Some regressions are an acceptable price for eliminating leaked secrets, Strapi attack surface, and perf issues. Parity effort stays risk-ranked (money paths exhaustive; low-traffic surfaces lighter, fix-forward tolerated). Best lever remains: get experienced staff into PREPROD testing as early as the secret allows.

## 13. Risk register (top 10)

| # | Risk | Mitigation |
|---|---|---|
| 1 | Leaked secrets exploited before rotation (rotation deferred for project confidentiality — outgoing team holds live creds until offboarding day) | tightly-scripted §2.1 offboarding runbook (revoke + rotate within hours); quiet access inventory prepared in advance; vendor alerts (Stripe radar, AWS billing alarms) watched in the interim |
| 2 | Innodata SFTP blocked from new IPs | confirm allowlist early; static egress IP (NAT GW); test from PREPROD weeks ahead |
| 3 | Password-reset churn | comms campaign, Google sign-in option, support macros, staged waves |
| 4 | Hidden behavior in 1,610-line cron not captured | line-by-line job mapping (done, §3.4 Arch); characterization period: run legacy + new worker side-by-side on PREPROD comparing outbound message logs |
| 5 | Payment discrepancies during cutover | maintenance mode blocks checkout during ETL; Stripe export reconciliation post-cutover |
| 6 | ETL misses data (Strapi link-table edge cases) | row-count+checksum report per table; spot-check UI on PREPROD with prod copy |
| 7 | SEO dip | identical URLs, prerendering, sitemap, Search Console monitoring |
| 8 | Email deliverability dip | same SendGrid domain auth; no DNS mail changes; warm hypercare monitoring |
| 9 | Single new platform bug blocks all users (big-bang) | PREPROD parallel run + staff dogfooding 2 weeks before cutover; 72h rollback window |
| 10 | Bluehost cancellation kills unnoticed cron jobs/other sites (mail ruled out — lives in Google Workspace) | pre-cancel audit of Bluehost account contents |
