# Independent Code Review — Process & Reviewer Briefs

How adversarial code review is run on this project. Adopted from the SkyLimit review practice
(`D:\CLAUDE_PROJECTS\SkyLimit\docs\Review_Findings_*.md`) and scaled to this repo's 2-app
modular-monolith topology. This is the detailed playbook behind `CLAUDE.md §3` (independent review)
and `§6` (the review→fix loop on the push-to-`develop` decision).

**Core principle:** the reviewers are *world-class, adversarial, independent experts* — backend,
frontend, security, performance, and data/correctness. They are briefed to **find everything**, not
to bless the diff. Findings are then **verified before they are trusted** (the author may be wrong;
so may the reviewer). We fix the real ones, re-review, and repeat until a pass yields no code change.

---

## 1. When this runs

- **Always before a merge to `develop`** — on the owner's "push to develop" decision (`CLAUDE.md §6`).
- **On every risk-area change** regardless of size: pricing/honorarium, payments, the rotation/
  application state machine, authorization, identity/provisioning, audit/soft-delete, money math.
- **On request** — any time the owner asks for a review of a branch or the working tree.

Scope is the **branch diff vs `develop`** (or the named PR), plus the files those changes touch.

---

## 2. The loop (run until clean)

1. **Fan out independent reviewers** (§3) — one expert agent per dimension/module, launched in
   parallel via the `Agent` tool (`/code-review`). Each gets the diff, the touched files, and its
   brief. Reviewers do **not** see each other's output — independence is the point.
2. **Triage + verify each finding** (§5). Do **not** trust blindly. Open the cited `file:line`,
   confirm the issue is real and reachable, and discard false positives with a one-line reason.
3. **Fix the real findings** on the feature branch, Critical→Low, with tests in the same change
   (Definition of Done, `CLAUDE.md §3`). Update the affected `Docs/` planning docs.
4. **Independent adversarial verification pass** (§6) — a fresh agent per fixed area confirms each
   fix actually closes the issue and introduced no regression, and judges whether the new tests are
   real (not hollow). Verifiers are told to try to *break* the fix.
5. **Re-run the reviewers.** **Repeat the whole cycle until a pass produces no code change.**
6. **Gate** (§7): full build clean, all tests green, coverage gate met, authz-matrix green.
7. **Write the remediation status** (§8) and proceed to merge.

For **risk areas** (pricing, state machine, auth, payments), run a **second adversarial pass** even
if the first was clean — these are the findings that look plausible and are subtly wrong.

---

## 3. Reviewer roster (one agent each, parallel, independent)

Launch these as separate `Agent` calls in a single message so they run concurrently. Each agent is
a senior specialist instructed to be exhaustive and adversarial, to cite `file:line`, assign a
severity, and prescribe a concrete fix. Skip an agent only if the diff has nothing in its domain.

| # | Reviewer | Owns |
|---|----------|------|
| 1 | **Backend / correctness** | API modules, EF model + migrations, domain logic, endpoint behavior, transactions, error/cancellation semantics, DI wiring. |
| 2 | **Security / authorization** | Authn (two-directory Entra: workforce staff vs External ID customers), authz policies, the authz-matrix coverage, IDOR/role-confusion, secrets handling, input validation, injection (SQL/CSV/log), PII exposure. |
| 3 | **Data / concurrency / money** | Lost updates, read-modify-write races, unique-violation/sequence collisions, idempotency (Service Bus consumers in the Worker), monetary precision/rounding, soft-delete + audit-interceptor correctness, query-filter leaks. |
| 4 | **Performance** | N+1 queries, unbounded/unpaged reads, in-memory aggregation that belongs in SQL, missing indexes, async hot paths, API p95 < 300ms budget, payload size. |
| 5 | **Frontend** | React data-fetch races/cancellation, MSAL init + token/401 refresh, PII-safe error rendering, authenticated downloads, accessibility, error-vs-empty states, state mgmt, bundle/LCP < 2.5s budget. *(Only when the diff touches `src/frontend`.)* |
| 6 | **Tests / DoD** | Are new behaviors tested? Authz-matrix row per new endpoint × role? Time-boundary tests for jobs? Are the tests real or hollow? Coverage gate. |

For a small, single-domain diff, collapse to the relevant 2–3. For a broad change, run the full
roster (and split #1 per module the way SkyLimit ran one agent per codebase).

---

## 4. Cross-cutting themes — always check these (Rotations Plus)

Carried from SkyLimit and re-pointed at this domain. Every reviewer flags these wherever they apply:

1. **Authorization matrix is complete.** Every new/changed endpoint has a row in
   `ApiAuthorizationMatrix` for *every* role across **both** Entra directories; allowed roles get
   through, everyone else is rejected (401/403). No endpoint ships un-matrixed.
2. **Cross-directory / role IDOR.** A staff (workforce) caller must not reach customer-scoped data
   by id, and vice versa; a lower role must not act through a higher role's path. Derive the
   operating identity from the validated token (`ICurrentUser`), never from a URL/body field.
3. **Audit + soft-delete correctness.** Mutations stamp audit (the `AuditSaveChangesInterceptor`);
   deletes convert to soft-delete; global query filters hide soft-deleted rows; upsert-past-filter
   uses `IgnoreQueryFilters` deliberately (e.g. provisioning restore, recreate-after-soft-delete).
   No accidental hard delete of an `ISoftDeletable`; no filter leak that exposes deleted rows.
4. **Concurrency / lost updates / collisions.** Read-modify-write on any running total, balance,
   capacity count, or sequence number needs a row lock, concurrency token, or DB constraint. Unique
   keys that can race (specialty name, program identity, invoice/PO-style numbers) must catch the
   `23505` unique-violation and return 409 / retry — not 500. *(We already hit this on specialty
   create; it's the canonical example.)*
5. **Idempotency on at-least-once delivery.** Worker / Service Bus consumers dedupe on a stable key
   and only complete the message on success; abandon (transient) / dead-letter (poison) otherwise.
6. **Money math.** Pricing (retail amount per week), weekly honorarium, and any payment amount:
   record the *actual* charged/received amount, not a recomputed expectation; round once at 2dp with
   a documented policy; payment confirm/refund is idempotent and transactional with an over-refund
   guard; no divide-by-zero or denominator hacks in derived KPIs.
7. **PII minimization.** Student/preceptor PII never lands in logs, URLs, error messages, or
   unencrypted persisted payloads. Sensitive columns use the EF value-converter encryption.
8. **Cancellation + failure semantics.** Never `catch (Exception)` in a way that swallows
   `OperationCanceledException` or masks an upstream failure as valid-empty data. Use
   `catch (Exception ex) when (ex is not OperationCanceledException)`. Don't `.catch(() => {})` in
   the SPA — distinguish "failed to load (retry)" from "no data".
9. **Injection.** Parameterized SQL only; any CSV/Excel cell beginning with `= + - @ \t \r` is
   neutralized (leading `'`) for non-numeric content; never interpolate raw server bodies into
   thrown `Error.message` shown in the UI.
10. **Time is abstracted.** No `DateTime.UtcNow` in domain code — `TimeProvider` only, so scheduled
    logic stays testable (`CLAUDE.md §4`).
11. **Tests ship with the code.** No "test later". New endpoint → integration + authz-matrix row;
    new job → time-boundary tests; new screen → component tests (+ e2e on a money path).

---

## 5. Triaging a finding (verify before you trust)

For each finding the reviewers return:

- **Open the cited `file:line`.** Confirm the code does what the finding claims and the bad path is
  actually reachable (not guarded upstream, not dead code).
- **Reproduce in your head or in a test.** For Critical/High, prefer adding a failing test that
  demonstrates it, then make the fix turn it green.
- **Discard false positives explicitly** with a one-line reason (kept in the findings doc) — don't
  silently drop them; the reasoning is part of the audit trail.
- **Classify** real ones by severity (§ below) and fix in dependency order: shared/foundation first
  (Common, interceptor, DbContext), then per-module.

### Severity rubric

- **Critical** — security/authz bypass (IDOR, privilege escalation), data corruption/loss, money
  incorrect or double-charged, PII leak. Blocks merge unconditionally.
- **High** — correctness bug on a real path, missing audit on a mutation, concurrency race that
  loses data, missing authz-matrix coverage, perf budget breach on a hot path. Blocks merge.
- **Medium** — narrower correctness/robustness/perf issue, maintainability/DI smell with a real
  failure mode, a11y gap. Fix in-PR unless explicitly deferred with a logged reason.
- **Low** — cosmetic, dead code, minor inconsistency, nice-to-have test. Fix if cheap; otherwise log.

---

## 6. Adversarial verification pass

After fixes land, spawn a **fresh** agent per fixed area (independent of the agent that fixed it).
Its brief: *try to break this fix.* For each originally-reported issue it returns one of
**CONFIRMED FIXED / STILL BROKEN / REGRESSION**, and a judgment on whether the added tests are real
(exercise the behavior) or hollow (assert nothing meaningful). Anything not CONFIRMED FIXED feeds
the next fix round. Verification is mandatory for every Critical/High and spot-checked for Mediums.

---

## 7. Merge gate (must all be green)

- Backend `dotnet build -c Release` clean (0 warnings — warnings-as-errors).
- Backend unit + integration (Testcontainers Postgres + WebApplicationFactory + TestAuthHandler).
- The **authz-matrix** suite (every endpoint × every role).
- Frontend Vitest at **70% coverage**; `tsc --noEmit` + `vite build` clean.
- Dependency audit.
- Performance budgets where the change touches a measured path (k6 p95, Lighthouse LCP).

*(Integration tests that need Docker run in **CI**, not locally — no Docker on the dev machine. The
local gate is build + unit + frontend; CI is the source of truth for the Testcontainers layer.)*

---

## 8. Findings & remediation writeup

For a substantial review (a release, a full-codebase sweep, or any risk-area change), record it like
SkyLimit's `Review_Findings_*.md`:

- **Header:** branch, scope, severity legend, fix order.
- **Cross-cutting themes** to fix consistently everywhere.
- **Per-module sections**, each finding as: `[Severity] Dimension — short title.` `path:line` — what's
  wrong, why it matters, **Fix:** the concrete remedy.
- **Remediation Status** at the bottom: what was fixed, the cross-cutting fixes delivered, migrations
  added, the green build/test gate numbers, and an honest **Residual / deferred** list (what was *not*
  done and why — e.g. "integration tests compile but run in CI only"). Disclose, don't paper over.

Small reviews don't need a standalone doc — the verified findings + fixes go in the PR and the
`Docs/Deployment_Log.md` row. The standalone writeup is for sweeps and risk-area changes.

---

## 9. How to launch (operational)

- Reviewers and verifiers run via the `Agent` tool (`/code-review`) — execution needs **no** approval
  per `CLAUDE.md §1`; only the resulting **file edits** are gated.
- Launch the roster in **one message, multiple `Agent` calls** so they run concurrently and
  independently. Give each: the diff (`git diff develop...HEAD`), the list of touched files, its
  brief from §3, the §4 cross-cutting checklist, and the §5 severity rubric + output format.
- Keep reviewer output as structured findings (severity, `file:line`, fix) so triage in §5 is
  mechanical. Then verify, fix, re-run — until a pass yields no code change.
