# Plan_Testing — Quality Strategy for the Rotations Plus Rewrite

**Date:** 2026-06-11 · **Baseline:** SkyLimit's proven test stack, extended with migration-specific layers.
**Principle:** every layer exists to catch a *specific class* of bug before a human sees it. Tests are written **with** the code (same PR), never "later" — the legacy system's 0% coverage is exactly how it became unmaintainable.

---

## 1. The pyramid (what runs where)

| Layer | Tooling | Catches | Gate |
|---|---|---|---|
| Backend unit | xUnit + Moq + FluentAssertions | logic bugs: pricing math, state-machine transitions, tag derivation, validators | **PR — blocks merge** |
| Backend integration | Testcontainers PostgreSQL + WebApplicationFactory + TestAuthHandler | wiring bugs: EF mappings, query filters, authz policies, controller contracts, migrations | **PR — blocks merge** |
| Frontend unit/component | Vitest + React Testing Library, **70% coverage gate** (SkyLimit standard) | component logic, hooks, form validation, API-client error mapping | **PR — blocks merge** |
| E2E | Playwright, MSAL auth mock (SkyLimit pattern), **3 viewports: 375 / 768 / 1280** | broken user journeys, mobile layout regressions | nightly + pre-release |
| Characterization | recorded legacy-API request/response suites replayed against new API | **rewrite drift** — the migration-specific risk | P5 gate, then pre-release |
| Data-migration verification | DataMigrator built-in report (row counts, checksums, money sums) | ETL bugs | every rehearsal + cutover night |
| Performance | k6 (API budgets) + Lighthouse CI (frontend budgets) | perf regressions vs §3.13 budgets (p95 <300ms, LCP <2.5s) | PREPROD, pre-release |
| Security | dependency scanning (NuGet/npm audit in pipeline), authz-matrix tests (see §3.4) | known CVEs, privilege escalation | PR + nightly |

## 2. Backend conventions (copied from SkyLimit, adapted)

- **Project layout:** `tests/unit/RotationsPlus.Api.Tests`, `tests/unit/RotationsPlus.Worker.Tests`, `tests/integration/RotationsPlus.Integration.Tests`. Naming: `MethodName_Condition_ExpectedResult`.
- **Unit:** EF InMemory only for pure-logic tests; anything touching Postgres-specific behavior (jsonb, filtered indexes, trigram search) goes to integration.
- **Integration factory:** `RotationsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime` with `PostgreSqlContainer` (`postgres:16-alpine`), `Database.MigrateAsync()` on init, `TestAuthHandler` header-scheme replacing Entra JWT (`X-Test-UserId`, `X-Test-Role`) — direct port of SkyLimit's `PatientApiFactory`/`TestAuthHandler`.
- **Every controller route gets at least:** happy path, validation failure, authz denial (wrong role), and not-found. Generated coverage report published in pipeline (SkyLimit `run-tests.yml` template).
- **Time is abstracted everywhere** via .NET `TimeProvider` — no `DateTime.UtcNow` in domain code. This is what makes the reminder/escalation logic testable (§3.2).

## 3. Rotations-Plus-specific deep-test targets (the risk hotspots)

These five areas get exhaustive suites because they're where the legacy system's behavior is subtle and the money/trust lives:

### 3.1 Rotation state machine
Explicit transition map (12 statuses) → property-style tests: every *allowed* transition succeeds + is audited + emits its event; every *disallowed* transition throws; idempotent re-application is a no-op. The `RotationStateMachineJob` sweep tested with `FakeTimeProvider` across date boundaries (start date arrives → Active; end date → To Be Evaluated), including timezone edges (US/Pacific vs UTC — a classic legacy-cron bug source).

### 3.2 Reminder/notification scheduling (the cron rewrite)
Each Hangfire job class is a plain class with injected `TimeProvider` + repositories → unit-testable without Hangfire. Suites cover: 48h/72h/7d/14d threshold boundaries (one minute before/after), the **Mon–Fri 8am–4pm PT SMS send window** (Friday 3:59pm vs 4:01pm vs Saturday), reminder-ledger idempotency (job runs twice → one message), and per-job "who qualifies" queries against seeded Testcontainers data. Plus the **side-by-side comparison** in P5: legacy cron and new Worker run against the same PREPROD data snapshot for a week; outbound message logs (recipient, template, timestamp-bucket) are diffed — any unexplained difference is a bug.

### 3.3 Pricing & payments
`PricingService` unit-tested over the full rule matrix: 10% deposit vs open-program 100% vs consultation hourly × promo codes (valid/expired/usage-limit) × credits (partial/full cover) × outstanding-balance flows — assertions to the cent. Stripe integration tested three ways: mocked client in unit tests; **Stripe test-mode + stripe-cli webhook forwarding** in DEV/PREPROD e2e (payment_intent.succeeded/failed, refund, dispute); **webhook idempotency tests** (same event delivered twice → one fulfillment) and out-of-order delivery.

### 3.4 Authorization matrix
The legacy system's worst hole (client-side-only staff gating) gets a dedicated integration suite: for **every** endpoint × every role (Student, Preceptor, Admin, Sales, SDR, Institution, Coordinator, impersonation-token, anonymous) assert allow/deny per the documented matrix (Plan_Admin §4, Plan_Sales_SDR §3–4). Sales must not read another sales rep's students; SDR must not read unassigned leads; impersonation tokens must fail every mutating endpoint. This suite is generated from a declarative table so adding an endpoint without classifying it **fails the build**.

### 3.5 Document pipeline
Innodata CSV ingestion: parser fuzzed with real-shape fixtures (missing columns, `Fail` suffix rows, duplicate document names, BOM/encoding quirks); status transitions (Pending → Verification_In_Progress → Approved/Rejected with reason) integration-tested; SFTP client behind `IOcrIngestionProvider` faked in tests (and swappable for Azure Document Intelligence in Phase 2). Agreement flow: Sent → Uploaded → Approved with reminder interactions. Blob SAS: expiry + container privacy asserted.

## 4. Frontend conventions

- Vitest + RTL, jsdom; co-located `*.test.tsx`; 70% line coverage gate enforced in `build-all.yml` (CI fails below).
- MSW (mock service worker) for API mocking — tests exercise the real `httpClient` path incl. error mapping.
- Form-heavy screens (onboarding wizards, profile panels) get validation-matrix tests per field rule (they're where 6 years of user data quirks live).
- **Playwright e2e — the eight money paths**, each at 375/768/1280px:
  1. Student signup → onboarding wizard (one variant per run, rotating) → search → program detail
  2. Cart → promo code → Stripe test payment → rotation appears Pending
  3. Document upload → simulated OCR result → status change visible
  4. Preceptor tokened confirm link + start-confirm link
  5. Preceptor onboarding (medical) through agreement upload
  6. Admin: lead create → email compose → convert
  7. Admin: preceptor approval → W9 request → activate; honorarium stage marking
  8. Impersonation: enter, verify banner + read-only enforcement, exit
- Touch-target audit (≥44px) runs as a Playwright assertion on the mobile viewport for nav/forms/calendar.

## 5. Migration-specific layers (what SkyLimit never needed)

### 5.1 Characterization tests (anti-drift)
During P2–P4, record from the **live legacy API** (read-only calls, plus test-account writes on legacy PREPROD): ~25 critical endpoints — search results for 10 canonical queries, program details, rotation lists per status, pricing quotes, document status payloads, lead lists. Stored as fixtures with a field-mapping table (old name → new name). Replayed against the new API in CI: values must match through the mapping. This is the objective answer to "does the rewrite behave the same," independent of anyone's memory.

### 5.2 ETL verification harness
DataMigrator emits a machine-checked report per run: per-table row counts (source vs target through mapping), checksums on key columns, **financial invariants** (sum of payments, sum of outstanding amounts, honorarium totals — to the cent), rotation status distribution, orphan-FK scan, encrypted-column round-trip sample. A red value fails the rehearsal (and on cutover night, triggers GO/NO-GO #1 = abort).

### 5.3 Parity checklists (human layer)
Per-dashboard checklists already written into Plan_Admin/Preceptor/Student/Sales_SDR §last — executed on PREPROD against the prod-copy dataset by owner + (if cleared for the secret) staff. Risk-ranked: money paths exhaustive; low-traffic reports lighter.

## 6. Pipeline integration (when each layer runs)

| Trigger | Runs |
|---|---|
| Every PR (`build-all.yml`) | dotnet build + unit + integration (Testcontainers); frontend typecheck + Vitest (70% gate) + build; dependency audit; authz-matrix suite |
| Merge to develop (DEV deploy) | the above + smoke suite against DEV after deploy |
| Nightly | Playwright e2e (3 viewports) against DEV; characterization replay; npm/NuGet audit |
| PREPROD promotion | full e2e + characterization + k6 + Lighthouse budgets against PREPROD |
| P5 / pre-cutover | ETL rehearsals w/ verification report ×2; notification side-by-side week; parity checklists; load smoke; rollback drill |
| Cutover night | ETL verification (GO/NO-GO #1); post-flip smoke suite incl. live $1 charge (GO/NO-GO #2) |

**Definition of done for every PR:** code + tests in the same PR; new endpoint → integration tests + authz-matrix row; new job → time-boundary tests; new screen → component tests (+ e2e step if on a money path); CI green. No "test later" backlog — that's how the legacy codebase got here.

## 7. Test data

- Deterministic seed kit (`tools/seed-dev-data`-style, like SkyLimit's): N students across all academic statuses, preceptors across all program types/statuses, rotations in **every one of the 12 statuses**, documents in every OCR state, leads per pipeline stage, promo codes (valid/expired/exhausted). Used by DEV, integration tests, and e2e alike — one vocabulary of test fixtures everywhere.
- PREPROD: masked prod copy via the refresh pipeline (real shapes, anonymized PII) — where parity and perf testing happen.
