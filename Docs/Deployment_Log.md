# Deployment Log — Rotations Plus

Running record of every change that ships. One row per merge/deploy: date + time, PR number, target environment, and a one-sentence summary. Newest first. (Process: `CLAUDE.md §6`.)

| Date / Time (PT) | PR | Env | Summary |
|---|---|---|---|
| 2026-06-15 13:09 | [#3](https://dev.azure.com/rotationsplus/Rotationsplus/_git/Rotationsplus/pullrequest/3) | DEV | Observability fix — wire the Azure Monitor OpenTelemetry exporter in `ServiceDefaults` so app traces/metrics/logs reach App Insights (the `APPLICATIONINSIGHTS_CONNECTION_STRING` was previously never read; 0 telemetry). |
| 2026-06-15 12:30 | [#2](https://dev.azure.com/rotationsplus/Rotationsplus/_git/Rotationsplus/pullrequest/2) | develop only | Working agreement — feature-branch workflow + approval gates + autonomy rules (`CLAUDE.md`) and this deployment log. No DEV deploy (docs are path-excluded from `deploy-dev`). |
| 2026-06-15 12:13 | [#1](https://dev.azure.com/rotationsplus/Rotationsplus/_git/Rotationsplus/pullrequest/1) | DEV | P2 identity spine — EF data foundation (audit + soft-delete), `StaffProfile` provisioning (`/api/me` now DB-backed), authz-matrix harness, and EF migrations applied in the deploy pipeline. Merged as `6311823`; deployed by `rplus-deploy-dev` build 9. |
| 2026-06-15 ~11:00 | — (pre-PR-workflow) | DEV | P1 foundation — api/worker/SPA skeleton live on DEV; staff Entra (workforce) login round-trip verified (`/api/me` returns Admin identity). Deploy build 3. |
