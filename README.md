# Rotations Plus

Ground-up rewrite + AWS→Azure migration of the Rotations Plus clinical-rotations marketplace.
React 18 + TypeScript SPA → .NET 9 modular-monolith API + Hangfire Worker on Azure.

> Planning & decisions: see [`Docs/`](Docs/) (start with `Docs/Project_State.md`). Working agreement: [`CLAUDE.md`](CLAUDE.md). Azure foundation record: [`Docs/Azure_Foundation.md`](Docs/Azure_Foundation.md).

## Topology

Modular monolith + worker (not microservices — see `Docs/Plan_Architecture.md §3.1`):

- **`rplus-api`** — ASP.NET Core 9, internally organized by domain module.
- **`rplus-worker`** — Hangfire recurring jobs + Service Bus consumers.
- **`web`** — React 18 + TS 5 + **Vite 8** SPA (Static Web Apps).
- PostgreSQL Flexible Server · Key Vault + Managed Identity · ACR · App Insights. Service Bus + Redis added per-module as needed (Redis omitted in DEV — in-memory fallback).

## Repository layout

```
src/
  shared/   RotationsPlus.ServiceDefaults | .Common | .Contracts
  api/      RotationsPlus.Api
  worker/   RotationsPlus.Worker
  aspire/   RotationsPlus.AppHost          # local dev orchestration
  frontend/web                            # React 18 + Vite 8
tests/
  unit/         RotationsPlus.Api.Tests | RotationsPlus.Worker.Tests
  integration/  RotationsPlus.Integration.Tests   # Testcontainers PG + WebApplicationFactory
infra/
  bicep/        # IaC
  pipelines/    # Azure DevOps YAML
```

## Prerequisites

- **.NET SDK 9** (`global.json` pins the band).
- **Node 22** (Vite 8 requires Node ≥22.12). On the owner's machine Node 22 is at `D:\Programs\nodejs22` — ensure `node --version` reports v22.x before building the frontend.
- Docker (for integration tests via Testcontainers) — optional locally; CI agents provide it.

## Build & test

```bash
dotnet build                 # whole solution
dotnet test                  # unit + integration (integration needs Docker)
cd src/frontend/web && npm ci && npm run build && npm run test
```

## Branching

Branch off `develop`; never commit straight to it. Merge to `develop` auto-deploys to DEV. See `CLAUDE.md §6`.
