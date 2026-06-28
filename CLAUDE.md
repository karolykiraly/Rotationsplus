# Rotations Plus — Project Working Agreement

This file defines how work is done in this project. It encodes the patterns the owner wants carried over from the SkyLimit project and the conventions agreed during planning. Follow it by default; the planning docs in `Docs/` hold the decisions and detail.

## ⭐ Prime Directive — this is a PORT, not a redesign
- **The job is to port the legacy production app to Azure, rewritten in C#/.NET, with the UI/UX (look & feel) matching the *current production* ≥99%.** Production is the **ground truth**; Figma (≈95–98% accurate to production) is a **visual aid**. **When Figma and production disagree, production wins.** The brand (logo + `#FF4874`) stays.
- **No missing features. No considerable UI changes.** Every section, column, field, control, and interaction that production renders must be present. Do **not** silently simplify, drop, or substitute an "improved" interaction (see [[match-production-ui-first]] — the PERM-1 and public-site simplification incidents). If production renders 8 filter dropdowns and a Sort control, the rewrite renders 8 filter dropdowns and a Sort control.
- **Code-level improvements are welcome and expected** — performance, security, access control, real bug fixes, server-pagination, guards, audit. These change *how* it works under the hood, never *what the user sees*.
- **UX/feature improvements over production are DEFERRED until after cutover.** Once the port is live in Production and verified, the owner will ask for enhancements — not before.
- **Operationalize it:** before building/reworking any screen, first enumerate production's sections/columns/fields/interactions from the legacy source (`Live_Code/06102026/rotationsplusweb-v4`). Build to that inventory. **Before declaring any screen done, diff it side-by-side against the production source.** When the legacy code is ambiguous for an ADMIN screen, ask the owner for the live screenshot — don't guess.

## 0. Read first
- **`Docs/`** is the source of truth for decisions: `Plan_Architecture.md`, `Plan_Migration.md`, `Plan_Testing.md`, `Plan_Admin.md`, `Plan_Preceptor.md`, `Plan_Student.md`, `Plan_Sales_SDR.md`, `Figma_Inventory.md`. Read the relevant one before acting; don't re-litigate decisions already recorded there.
- Reference patterns to imitate live in the SkyLimit repo (`D:\CLAUDE_PROJECTS\SkyLimit`): ServiceDefaults, Bicep modules, pipeline templates, Testcontainers/TestAuthHandler, MSAL httpClient. Clone the pattern, scale to this project's 2-app topology.

## 1. Autonomy — act, don't ask about everything
- **Default to acting.** When there's a sensible default, a documented decision, or a clear best practice, do it and report what you did — don't stop to ask.
- **Give a recommendation, not a survey.** If a choice arises, pick the best option, state it briefly with the why, and proceed. Don't enumerate every alternative or ask the owner to choose unless it's genuinely their call.
- **Escalate only genuine owner-decisions:** brand/UX direction, money/vendor/legal trade-offs, anything irreversible or outward-facing, anything requiring the owner's accounts/credentials, or a real fork with business (not technical) consequences. Everything technical, you decide.
- **Don't re-derive settled facts.** The planning docs and prior decisions stand; build on them.
- The owner works ~4h/day and reviews on DEV — optimize for "show working results on DEV," not "ask before each step."
- **Don't ask permission to look or to test.** Searches, read-only commands, running PowerShell/bash, and running the build/test suites inside the `Rotationsplus` project need **no** approval — just do them and report. The only approval gates are: **modifying source/config files** (ask before the edit) and the **push-to-`develop`** decision (§6). Build, run, and test freely.

## 2. Deployment cycle — build once, promote the same artifact
- **Environments:** DEV → PREPROD → PROD. Per-env resource groups, Key Vault, and variable groups. IaC in Bicep, pipelines in Azure DevOps YAML (clone SkyLimit templates).
- **DEV:** every merge to `develop` auto-deploys (images → ACR tagged by build id, Bicep, deploy api/worker/SWA). DEV is the continuous-review environment.
- **PREPROD:** promotes the **exact same image tags + SWA artifact** from DEV — no rebuild. Manual trigger, owner approval gate. Gets a masked prod-copy dataset for parity/perf testing.
- **PROD:** same promotion pattern from PREPROD, double approval gate. Used at cutover and every release after.
- **Hard rule:** nothing reaches PREPROD that didn't run on DEV; nothing reaches PROD that didn't pass the parity suite on PREPROD.
- Secrets only in Key Vault (managed identity); never in code/config. URLs: `dev(-api).rotationsplus.org`, `preprod(-api).rotationsplus.org`, prod `www`/`api.rotationsplus.org`.

## 3. Independent review & quality — tests ship with the code
- **Tests in the same PR as the code. No "test later"** — the legacy system's 0% coverage is exactly what we're escaping. See `Plan_Testing.md`.
- **Definition of done per PR:** code + tests together; new endpoint → integration tests + an authz-matrix row; new background job → time-boundary tests; new screen → component tests (+ e2e step if on a money path); CI green.
- **Gates that block merge:** backend unit + integration (Testcontainers PostgreSQL + WebApplicationFactory + TestAuthHandler), frontend Vitest at **70% coverage**, dependency audit, the authz-matrix suite (every endpoint × role).
- **Independent/adversarial review before merge:** run `/code-review` on the diff; verify findings rather than trusting them; for risk areas (pricing, state machine, auth, payments) prefer a second adversarial pass. Don't self-approve risky changes without verification. **The full playbook — world-class expert reviewer roster (backend/security/data-concurrency/perf/frontend/tests), cross-cutting checklist, severity rubric, the verify→fix→re-review loop, and the findings/remediation writeup format — is `Docs/Review_Process.md`.** Follow it for every push-to-`develop` review.
- **Migration-specific layers:** characterization tests (record legacy API, replay against new), the ETL verification harness (row counts/checksums/money-to-the-cent), per-dashboard parity checklists. These are gates, not nice-to-haves.
- **Performance budgets are enforced** (API p95 < 300ms, mobile LCP < 2.5s) via k6 + Lighthouse CI, not aspirational.

## 4. Engineering conventions (from SkyLimit)
- .NET 9 modular monolith API + separate Hangfire Worker; EF Core + Npgsql; Service Bus for events; Redis for cache. React 18 + TS + Vite, TanStack Query + Zustand, MSAL, central typed httpClient.
- Two-directory Entra: staff in workforce tenant, customers in External ID (see `Plan_Architecture.md §3.5`).
- Time abstracted via `TimeProvider` (no `DateTime.UtcNow` in domain code) so scheduled logic is testable.
- Match surrounding code style; sensitive columns encrypted via EF value converters; soft-delete + audit on key entities.

## 5. Stealth (until offboarding day)
- Confidential project; the outgoing dev team must not learn of it. No changes to the legacy GitLab/Cloudflare/vendor accounts; build on isolated new accounts; secrets rotation deferred to offboarding day. See `Plan_Migration.md §2`. Don't touch the legacy EC2/RDS or do custom DNS for dev/preprod until after offboarding.

## 6. Git workflow & approval gates
- **Always work on a feature branch.** Branch off `develop` (`feat/…`, `fix/…`, …); never commit straight to `develop`. One concern per branch/PR. Conventional, descriptive commit messages. Pushing the feature branch to `origin` (backup / draft PR) is fine without asking — the gate is the **merge into `develop`**, not the branch push.
- **Approval gate (always).** When the work on a feature branch is complete and its tests pass, STOP and ask the owner for approval. Do not merge/push to `develop` on your own initiative — wait for the owner's explicit "push to develop" decision.
- **On the owner's "push to develop" decision, run the review→fix loop until clean:**
  1. Launch **independent adversarial reviewers** (`/code-review`, multiple agents) on the branch diff.
  2. Verify each finding (don't trust blindly), then fix the real ones on the feature branch.
  3. Re-run the reviewers. **Repeat the cycle until a pass yields no code changes.**
  - Throughout the loop: keep the `Docs/` planning docs up to date whenever the change affects them, and after **every** change make sure tests are updated — add new tests for new behaviour (Definition of Done, §3). CI must be green.
- **Then merge to `develop` AND clean up in the same step — never a later turn.** The merge isn't done until the local repo is tidy. The moment the (auto-complete) PR shows merged: `git fetch origin --prune` → `git checkout develop` → `git merge --ff-only origin/develop` → `git branch -D <feature-branch>` (a squash-merge commit isn't an ancestor of the branch, so plain `-d` refuses — `-D` is correct once the PR is confirmed merged) → confirm `git branch` shows only `develop`/`main` (+ any explicitly-parked branch). **Never end a turn with a merged feature branch still checked out, or local `develop` behind origin.** `develop` auto-deploys to DEV.
- **Deployment log:** every change that ships gets a row in `Docs/Deployment_Log.md` — date + time, PR number, environment, and a one-sentence summary.

## 7. Tooling notes
- **Visual target hierarchy (see the Prime Directive):** the **legacy production code** (`Live_Code/06102026/rotationsplusweb-v4`) is the literal live site and the **≥99% ground truth**. **Figma** (read-only MCP, ≈95–98% accurate) is the **visual aid** for layout/spacing/components/styling and the design system (Colors `317:929`, Typography `317:1286`, Buttons `317:925`/`317:927`) — **owner decision 2026-06-19 (reversing the 2026-06-11 "fresh redesign" call), reaffirmed 2026-06-28 as production-first.** When Figma and production disagree, **production wins**; brand (logo + `#FF4874`) stays. Only areas with **no** production/Figma equivalent (Stripe payment states, OCR statuses, impersonation, program-documents upload) are designed fresh. Already-built fresh-styled screens are reworked to match production. Frame map: `Docs/Figma_Inventory.md`.
- Windows/PowerShell environment; prefer the dedicated file/search tools over shell where one fits.
