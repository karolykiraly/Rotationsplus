# Azure_Foundation — Tenant & Platform Setup Record

**Created:** 2026-06-14 · **Status:** Foundation complete (with noted deferrals) · **Owner-executed** over several sessions with step-by-step guidance.

This document records the one-time Azure foundation that gates P1. It is the durable record of *what exists* and *what was deliberately deferred*. Resolved IDs live in the owner's auto-memory (`memory/azure_ids.md`) — duplicated here for the non-sensitive identifiers only. **No secrets (client secrets, connection strings) are recorded anywhere but Key Vault.**

---

## 1. What was created

### 1.1 Tenants & subscription
- **Workforce Entra tenant** (staff identity) — internal domain `charlesrotationsplus.onmicrosoft.com`. This is also the **infrastructure tenant** that owns the Azure subscription.
- **Subscription** `rotationsplus-main` — pay-as-you-go, created under an *organization* account type (not personal).
- **CIAM tenant** (Entra External ID — customer identity for Student/Preceptor) — separate directory, resource group `rg-rplus-ciam` (East US 2).

> Identifiers (non-secret): Workforce tenant ID, Subscription ID, CIAM tenant ID — see `memory/azure_ids.md`.

### 1.2 App registrations
**Workforce tenant (staff):**
- `rplus-api` — backend API; exposes scopes; app roles for staff: `Admin`, `Sales`, `SDR`, `Institution`, `Coordinator`. Manifest `requestedAccessTokenVersion = 2`.
- `rplus-web` — SPA (auth-code + PKCE via MSAL); configured for the workforce authority.

**CIAM tenant (customers):**
- `rplus-api-ext` — backend API (customer-facing); app roles: `Student`, `Preceptor`.
- `rplus-web-ext` — SPA for customers.

> All four client IDs are in `memory/azure_ids.md`.

### 1.3 Resource groups (East US 2, subscription `rotationsplus-main`)
- `rg-rplus-shared` — ACR (shared across environments)
- `rg-rplus-dev` — DEV resources
- `rg-rplus-preprod` — PREPROD resources
- `rg-rplus-prod` — PROD resources
- `rg-rplus-ciam` — CIAM tenant resource group

### 1.4 Container Registry
- ACR login server: `rotationsplusacr-cvgsceanh0fpbdh3.azurecr.io` (Basic SKU). The generated suffix is from the "Tenant" domain-name-label scope chosen at creation — normal; pipelines reference the full login server.

### 1.5 Azure DevOps
- Org: `https://dev.azure.com/rotationsplus`
- Project + Git repo + variable groups + environments (DEV/PREPROD/PROD with approval gates).
- **ARM service connection** via **workload identity federation** (secretless) — service principal `rotationsplus-Rotationsplus-c5c444e6-3348-48b2-910f-44b66b31f8f6`.

### 1.6 RBAC
- Service principal granted **Contributor** on all four environment resource groups + **AcrPush** on `rg-rplus-shared`.

### 1.7 Social login (CIAM)
- Google OAuth 2.0 client created (External audience) for Student/Preceptor social login. **Completion status: verify** (see deferred list).

---

## 2. Deliberately deferred (NOT blocking P1)

| Item | Why deferred | When |
|---|---|---|
| **Entra custom domain** (`rotationsplus.org` as staff UPN suffix) | No real staff accounts during build; nothing in P1 depends on it. 5-min step later. | Post-offboarding, at first real staff account |
| **CIAM custom URL domain** (`auth.rotationsplus.org`) | Feature requires a paid plan + a Cloudflare CNAME (stealth: can't touch Cloudflare). Build uses default Microsoft sign-in URL. | Post-offboarding |
| **Conditional Access / enforced MFA** | Requires Entra P1 (~$6/user/mo); no staff accounts yet. Security Defaults (free) in the interim. | When real staff accounts are created |
| **Google OAuth credential wiring into CIAM** | Customer login path; P1 round-trip proves *staff* (workforce) login. | Before customer-facing phase |
| **ACR Docker-registry service connection** | Not needed — the ARM SP already holds **AcrPush**, so pipelines push via `az acr login` through the existing ARM connection. | N/A (superseded) |
| **Custom DNS for DEV/PREPROD** | Stealth: Cloudflare is held by the outgoing team. Use Azure default hostnames. | Post-offboarding (cutover) |

---

## 3. Stealth constraints in force (from `Plan_Migration.md §2`)
- Work from **local code snapshots only** — no GitLab/EC2/RDS/Cloudflare access until offboarding.
- All new infra in **owner-only accounts**; isolated vendor sandboxes; no footprint in live vendor dashboards.
- Secret rotation deferred to offboarding day.

---

## 4. Owner bootstrap for P1 deploy (one-time)

**Status — completed 2026-06-15** (via `az` under the owner's signed-in session; script `infra/bootstrap/Bootstrap-Dev.ps1`):
- ✅ 4.1 UAMI `id-rplus-dev` created (principalId `7e472b6f-cccc-46d1-b108-aa923de2d472`) + **AcrPull** on `rotationsplusacr`.
- ✅ 4.2 Variable group `rplus-dev` (id 4) with secret `POSTGRES_ADMIN_PASSWORD`; service connection `azure-rotationsplus` + `acrName=rotationsplusacr` set in `variables.dev.yml`. (DevOps Environment `rplus-dev` auto-creates on first deploy run.)
- ✅ 4.3 `access_as_user` scope present on `rplus-api`; granted + admin-consented to `rplus-web`; owner assigned the `Admin` app role on `rplus-api`.
- ✅ 4.3 step 3 (SWA redirect URI) — registered `https://lively-field-00b389d0f.7.azurestaticapps.net` (+ `http://localhost:5173`) on `rplus-web` after the first successful DEV deploy (build 3, 2026-06-15).

**Staff login round-trip verified (2026-06-15):** signed in as charles@rotationsplus.com on the SPA → `/api/me` returned Name "Karoly Kiraly", Roles `["Admin"]`, isStaff `true`, oid `c9f28e33-afef-4b0a-b3de-5fe06be37985`. P1 is complete. (`Username` blank = guest account has no `preferred_username`/`upn` claim; non-blocking.)

**DEV is live (2026-06-15):**
- SPA: `https://lively-field-00b389d0f.7.azurestaticapps.net`
- API: `https://ca-rplus-api-dev.graypond-8dfef1bd.westus2.azurecontainerapps.io` (`/health` = Healthy)
- Worker (Hangfire dashboard): `https://ca-rplus-worker-dev.graypond-8dfef1bd.westus2.azurecontainerapps.io/admin/jobs`
- Resource group `rg-rplus-dev`; images tag `:3`. API runs `minReplicas: 0` (scale-to-zero → first request cold-starts).

These required Owner/RBAC or app-registration rights the pipeline service principal (Contributor) does not have.

### 4.1 Per-environment managed identity (enables ACR pull + Key Vault without pipeline role-assignments)
The pipeline SP is **Contributor**, which cannot create role assignments. So create a user-assigned MI per env and grant it AcrPull once:
```bash
az identity create -g rg-rplus-dev -n id-rplus-dev -l eastus2
PRINCIPAL=$(az identity show -g rg-rplus-dev -n id-rplus-dev --query principalId -o tsv)
ACR_ID=$(az acr show -n rotationsplusacr --query id -o tsv)   # verify the ACR resource name
az role assignment create --assignee-object-id $PRINCIPAL --assignee-principal-type ServicePrincipal \
  --role AcrPull --scope $ACR_ID
```
Key Vault access is granted automatically by Bicep (access-policy mode) to this identity — no manual step.

### 4.2 DevOps wiring
- **Variable group `rplus-dev`** with secret **`POSTGRES_ADMIN_PASSWORD`** (choose a strong value).
- **Environment `rplus-dev`** (add approval checks as desired).
- In `infra/pipelines/variables/variables.dev.yml`, set **`azureServiceConnection`** to the ARM service connection name and verify **`acrName`**.

### 4.3 Entra wiring for the staff login round-trip
1. **Expose an API scope on `rplus-api`** (workforce tenant): *Expose an API → Add a scope* → `access_as_user` (admins+users consent). Application ID URI: `api://c7bd24f1-e55f-4a26-b826-6b1241a5a1bc`.
2. **Grant `rplus-web` the delegated permission** to `rplus-api`'s `access_as_user`, then **grant admin consent**.
3. **`rplus-web` redirect URI:** after the first DEV deploy, read the Static Web App hostname (`https://<swa>.azurestaticapps.net`) and add it as a **SPA** platform redirect URI on `rplus-web`. (Cannot be pre-registered — the host doesn't exist until the SWA is deployed.) The pipeline prints the host at the end of the deploy.
4. **A staff test user** in the workforce tenant with an app role (e.g. `Admin`) assigned on `rplus-api`/`rplus-web`, to exercise the round-trip.

---

## 5. Sign-off
Azure foundation **complete** as of 2026-06-14. P1 (solution scaffold → DEV deploy → staff login round-trip) is unblocked. See `Project_State.md` and the approved P1 plan.
