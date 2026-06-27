# Plan — Public Marketing Site

The rewrite only ever built the **authenticated app** (admin console + student/preceptor portal); the site root `/` force-redirected into staff login. The legacy `www.rotationsplus.org` is a full **public marketing site** — the front door where **students, preceptors, and leads land, register, and onboard** — and was entirely missing. This program builds the public site to match the legacy site + the Figma frames, **before** resuming the remaining Admin Dashboard nodes (owner decision 2026-06-27).

## Decisions (owner, 2026-06-27)
- **Scope:** build the **entire public marketing site first**, across PRs, before pivoting back to admin nodes.
- **Leads:** **deferred** — landing CTAs open the CIAM student/preceptor sign-up; lead-capture + Leads/CRM is the separate Tier-2 admin program.
- **Blog:** **deferred** (content-source TBD) — ship every other marketing page now.
- **Fly-low / exposure:** DEV is the **Azure SWA default hostname** (`https://lively-field-00b389d0f.7.azurestaticapps.net`), not a `rotationsplus.org` URL; the offshore team is on a separate stack with no overlap. Deploying the public site to DEV is **not public** and needs **no access gate**. Going live on `www.rotationsplus.org` is the owner-triggered **cutover** (post-offboarding). *(Other Docs that say `dev.rotationsplus.org` are inaccurate — the SWA hostname is the live DEV URL.)*

## Routing / auth restructure (LP-1, the foundation)
Three independent router branches, each with its own (or no) MSAL provider — preserving the no-root-provider invariant so the two MSAL instances never contend for an auth-response hash:
- **Public** (`PublicLayout`, **no MsalProvider**): `/` → `LandingPage`; `/about`, `/our-process`, `/our-team`, `/for-preceptors`, `/consulting-services`, `/faq`, `/resources`, `/privacy-policy`, `/terms` (placeholders until their LP-PR). *(Blog deferred.)*
- **Staff** (`StaffMsalShell` → `Outlet`, workforce MSAL): login launchers `/rotationsplusadmin`, `/rotationsplussales`, `/rotationsplussdr` (legacy URLs preserved) + the authenticated `/admin/*` console. `/admin` index → `PostLoginRedirect` → `roleHome(roles)` (admin→dashboard, sales→programs, sdr→dashboard fallback).
- **Customer portal** (`CustomerMsalShell`, CIAM): `/portal/*`, unchanged.

**Key change:** the workforce `redirectUri` moves from `/` (origin) to **`/admin`** so the anonymous root never has to process an MSAL hash. CIAM still redirects to `/portal`. **Staff sign-out** lands on the public `/`. CTAs ("Search Programs"/"Sign Up"/"Join as Preceptor") are `<Link to="/portal">` → the existing rplus-susi CIAM flow.

**Owner action (Entra, not code):** register `/admin` SPA redirect URIs on the `rplus-web` app registration (client `f874b196-89e2-4216-88fc-e7c92f05e6b7`) per environment (`http://localhost:5173/admin`, the DEV SWA `/admin`, PREPROD/PROD at cutover). Staff login on an env only works once its `/admin` URI is registered. CIAM (`rplus-web-ext`) unchanged.
- **Keep the origin `/` redirect URI registered *permanently*** — it is now the workforce `postLogoutRedirectUri` (sign-out target). It is no longer a *login* redirect URI, so don't let a future "cleanup" remove it, or staff **sign-out** breaks (Entra logout-URI validation).
- **Known limitation (until the scoped dashboards exist):** non-admin staff with no built console (SDR / Coordinator) land on `/admin/dashboard`, which is AdminOnly and 403s (the header sign-out still works; no data leaks — server-enforced). Sales → `/admin/programs` loads. Proper role-scoped landings come with the follow-on Admin Dashboard nodes.

## PR sequence
- **LP-1** — public shell + routing restructure + Landing page *(this PR)*.
- **LP-2** — For Preceptors + Consulting Services.
- **LP-3** — About + Our Process + Our Team.
- **LP-4** — FAQ + Resources.
- **LP-5** — Privacy Policy + Terms.
- *(LP-6 — Blog + Blog-detail: deferred.)*

Each follows CLAUDE.md §6 (feature branch, tests in-PR at the 70% gate, full adversarial review — LP-1 is an auth/routing risk area, ask-before-push, squash PR, DEV deploy, post-merge cleanup, deploy-log row, GitHub mirror sync). Reviewed on the DEV SWA URL.

## Frame map (Figma)
Landing `317:3611` / mobile `820:4179` · For Preceptors `317:1634` · Consulting `4044:15094` · About `317:1525` · Our Process `317:1352` · FAQ `317:1878` · Legal `733:3328`/`736:3398` · Blog `317:1969` / detail `1597:9736` (deferred). Design system: Colors `317:929`, Typography `317:1286`, Buttons `317:925`/`317:927`.

## Follow-on
After the public site: finish the **Admin Dashboard nodes** — remaining Tier-1 parity (Preceptors/Students directories, admin forms, Dashboard) and Tier-2 net-new screens (Analytics, Contacts, **Leads/CRM** — where deferred lead-capture lands, Sales-Students, Customer Service, Admin/Staff mgmt, Data, Reports).
