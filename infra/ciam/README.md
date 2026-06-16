# CIAM (Entra External ID) customer-auth setup

Scripts + steps to stand up **customer sign-in** (Student / Preceptor) so the SPA can sign customers
in and call the API. This is the "~30 min of Azure" prerequisite for the student-facing search UI and
the preceptor onboarding flow.

**Division of labour:**
- **You (Azure, this folder):** run `Configure-Ciam.ps1` + a few portal clicks. ~30 min.
- **Me (code):** ✅ **Done — marketplace slice 8 / PR #15.** The API now validates CIAM customer
  tokens via a second JWT-bearer scheme behind an issuer-routing "Smart" policy scheme and maps the
  `Student`/`Preceptor` roles (`GET /api/customer/me` proves the round-trip); the SPA carries the
  customer MSAL config (`rplus-web-ext` / CIAM authority). **Customer sign-in goes live once the two
  portal steps below are done** (user flow + admin consent) — until then the wiring is dormant.

The workforce (staff) side — `rplus-api` / `rplus-web` — is already wired and working; **none of this
touches it.**

---

## What already exists (from the Azure foundation)

| Thing | Value |
|---|---|
| CIAM tenant | `f963c59e-da79-40f4-a358-1cd77e78ddd0` |
| `rplus-api-ext` (API registration) | `75709454-b052-45b4-b9b4-9f3214d487c6` |
| `rplus-web-ext` (customer SPA) | `d3a7f715-1e7f-4c45-bd73-4de5749e1164` |

The registrations exist but aren't configured for the customer flow yet. The script fills that in.

---

## Prerequisites

- **Azure CLI** (`az`) installed and current.
- An account with **Application Administrator** (or owner) in the **CIAM tenant**.
- Your **DEV Static Web App hostname** (no scheme), e.g. `blue-sand-0abc1234.azurestaticapps.net`.
  Find it with (signed into the *workforce* tenant/subscription):
  ```powershell
  az staticwebapp show -g rg-rplus-dev --query defaultHostname -o tsv
  ```

---

## Run order

```powershell
# 1. Sign in to the CIAM tenant (it has no subscription, hence --allow-no-subscriptions)
az login --tenant f963c59e-da79-40f4-a358-1cd77e78ddd0 --allow-no-subscriptions

# 2. PREVIEW first — shows exactly what would change, changes nothing
./Configure-Ciam.ps1 -SwaHostname '<your-swa-host>' -WhatIf

# 3. Apply
./Configure-Ciam.ps1 -SwaHostname '<your-swa-host>'
```

The script is **idempotent** — re-running it only adds what's missing. It prints a summary block at the
end with the values to hand back to me.

### What the script does (scriptable)
1. Exposes the **`access_as_customer`** delegated scope on `rplus-api-ext` and sets its App ID URI
   (`api://75709454-…`).
2. Defines the **`Student`** and **`Preceptor`** app roles on `rplus-api-ext` (emitted as `roles` claims).
3. Configures `rplus-web-ext`: **SPA redirect URIs** (`https://<swa-host>/` + `http://localhost:5173/`)
   and a **delegated permission** to `access_as_customer`.
4. Ensures **service principals** exist for both apps.

---

## Portal-only steps (the script can't do these reliably)

After running the script, in **portal.azure.com → the CIAM tenant → Entra External ID**:

1. **Create the sign-up / sign-in user flow.** External ID → *User flows* → **New user flow** →
   email + password (add **Google** later — deferred). Under the flow's *Applications*, **add
   `rplus-web-ext`**. This is what gives customers the hosted sign-up/sign-in pages.
2. **Grant admin consent** for the API permission: *App registrations → rplus-web-ext → API
   permissions → **Grant admin consent for <tenant>***. (Or, if your account allows it:
   `az ad app permission admin-consent --id d3a7f715-1e7f-4c45-bd73-4de5749e1164` — the portal button
   is usually simpler in a CIAM tenant.)
3. *(Deferred, not now)* Google social login and custom branding/domain — out of scope for this pass.

---

## Output → where each value goes

The script prints these; hand them back to me (most are already in project memory, so just confirm):

| Value | Goes into |
|---|---|
| `CiamAuthority` (`https://<tenant>.ciamlogin.com/<tenant>`) | API second JWT-bearer authority; SPA MSAL authority |
| `CustomerSpaClientId` (`d3a7f715-…`) | SPA MSAL `clientId` (customer app) |
| `ApiAudience` (`75709454-…`) | API token-validation audience for the customer scheme |
| `ApiScope` (`api://75709454-…/access_as_customer`) | SPA token request scope |
| `Student`, `Preceptor` app role ids | (informational) — roles arrive as `roles` claims; the API policies already know the role names |

No secrets are created or stored — the SPA is a public client and the API only validates tokens.

---

## Notes & caveats

- These scripts were written to the documented design (`Plan_Architecture.md §3.5`) but **could not be
  tested against your live CIAM tenant** — that's why step 2 is `-WhatIf`. Review the preview before
  applying.
- Stay signed into the **CIAM** tenant while running (`az account show` should report
  `f963c59e-…`). Switch back to the workforce subscription afterwards for normal `az` work.
- Per the stealth rule (`CLAUDE.md §5`): no custom DNS yet — redirect URIs use the SWA default host.
  When custom domains land, re-run the script with the new `-SwaHostname` to add them.
