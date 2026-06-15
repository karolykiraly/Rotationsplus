# Deployment Log — Rotations Plus

Running record of every change that ships. One row per merge/deploy: date + time, PR number, target environment, and a one-sentence summary. Newest first. (Process: `CLAUDE.md §6`.)

| Date / Time (PT) | PR | Env | Summary |
|---|---|---|---|
| 2026-06-15 12:13 | [#1](https://dev.azure.com/rotationsplus/Rotationsplus/_git/Rotationsplus/pullrequest/1) | DEV | P2 identity spine — EF data foundation (audit + soft-delete), `StaffProfile` provisioning (`/api/me` now DB-backed), authz-matrix harness, and EF migrations applied in the deploy pipeline. Merged as `6311823`; deployed by `rplus-deploy-dev` build 9. |
| 2026-06-15 ~11:00 | — (pre-PR-workflow) | DEV | P1 foundation — api/worker/SPA skeleton live on DEV; staff Entra (workforce) login round-trip verified (`/api/me` returns Admin identity). Deploy build 3. |
