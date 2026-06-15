# API modules

The `rplus-api` is a **modular monolith**: one ASP.NET Core app, organized internally by domain
module. Module boundaries are enforced by folder + visibility (not separate databases). Each module
owns its endpoints, domain types, and EF entity configurations (discovered by `RotationsDbContext`).

Planned modules (see `Docs/Plan_Architecture.md §3.2`–`§3.3`):

| Module | Responsibility |
|---|---|
| `Identity` | user profiles, role/claims mapping, Entra linkage |
| `Marketplace` | programs, preceptors, specialties, hospitals, search, favorites |
| `Rotations` | booking lifecycle, state machine, confirmations, evaluations |
| `Payments` | Stripe, webhooks, promo codes, unlocks, credits, honorarium |
| `Documents` | uploads, document types, agreements, OCR statuses, PDF generation |
| `Crm` | leads, lead logs, contacts, call history, email threads, issues |
| `Notifications` | templates, outbound email/SMS/WhatsApp (enqueue only) |
| `Reporting` | dashboards, live score, analytics queries |
| `Content` | blog posts, reference data (cities/specialties/lookup) |

Module folders are created as work on each begins. P1 ships only the skeleton (`/api/me`, health, OpenAPI).
