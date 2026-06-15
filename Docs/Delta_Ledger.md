# Delta Ledger — legacy → new ingestion (until cutover)

Tracks every legacy change re-implemented into the new stack. The legacy dev team keeps shipping
until cutover; the owner drops a **weekly, working-files-only** snapshot of **both repos** under
`Live_Code/<yyyymmdd>/`. This ledger is the audit trail guaranteeing nothing is lost. Process detail:
approved P1 plan, Part D.

## Mechanism

- `Live_Code/` is a **dedicated local git repo** (separate from the app repo; never pushed) that
  archives each snapshot. Baseline committed + tagged `snapshot/2026-06-10`.
- On each new snapshot `Live_Code/<date>/`: add + commit + tag `snapshot/<date>`, then compute the delta:
  ```bash
  # both repos, previous vs new dated folder
  git -C Live_Code diff --no-index --stat <prevdate>/rotationsplus-backend-v4-main <newdate>/rotationsplus-backend-v4-main
  git -C Live_Code diff --no-index --stat <prevdate>/rotationsplusweb-v4          <newdate>/rotationsplusweb-v4
  ```
  (`--no-index` diffs the two dated trees directly; the commits/tags are the durable archive.)
- Classify → triage → implement (auto for clear deltas; escalate business-logic forks) → tests →
  `/code-review` → characterization replay if a covered domain changed → DEV PR. One row per delta below.
- Each week the owner gets a digest: what changed, what was auto-implemented, what's escalated.

## Disposition legend

`mirror` (re-implement in new module) · `schema` (DataMigrator mapping + EF) · `feature` (new work item) ·
`fix-forward` (rewrite already supersedes; noted) · `skip` (dead code / removed vendor / cosmetic) · `escalate` (owner decision)

## Ledger

| Snapshot | Area / file(s) | Classification | Disposition | PR | Status | Parity impact |
|---|---|---|---|---|---|---|
| 2026-06-10 | _baseline_ — `rotationsplus-backend-v4-main` + `rotationsplusweb-v4` | baseline | n/a | — | baseline | The plans + parity checklists were authored from this snapshot. Deltas measured forward. |

_Next snapshot (~weekly) appends rows here._
