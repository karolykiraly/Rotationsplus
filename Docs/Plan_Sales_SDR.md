# Plan_Sales_SDR — Sales & SDR Dashboards Discovery + Rewrite Spec

**Date:** 2026-06-11 · **Note:** this document was not in the original requested list, but the Sales (`/rotationsplussales`) and SDR (`/rotationsplussdr`) areas are real, distinct user surfaces — they need their own spec. They reuse Admin components with role gating, so read alongside `Plan_Admin.md`.

---

## 1. Who they are

- **SDR (Sales Development Rep):** works the **lead pipeline** — qualifies student/preceptor leads, emails/calls them, converts to contacts. Sees only their own leads.
- **Sales:** manages **assigned programs and institution students** — a program-scoped seller/account-manager view.
- **Institution:** logs in at `/rotationsplusinstitutions`; behaves like Sales (no dedicated institution UI exists — it redirects to `/admin/programs`). Treated as a Sales-variant role.
- All three are stored today as `role=Administrator` with a `type` discriminator (`sdr` / `sale` / `institution`) — gating is client-side only. **Rewrite: distinct Entra app roles + server-side policies.**

## 2. Login

`/rotationsplussales` and `/rotationsplussdr` share `SalesLogin.js`; `/rotationsplusinstitutions` uses `InstitutionLogin.js` — all run the same 2-step code flow as admin (`verifyAdmin` → 4-digit code → `loginAdmin`). Redirects: SDR → `/sdr/dashboard`; Sales/Institution → `/admin/programs`.
**Rewrite:** all three URLs preserved as entry routes triggering the Entra staff flow (MFA enforced); post-login routing by app role.

## 3. SDR surface (`/sdr/*`)

| Route | Reuses | Differences vs admin |
|---|---|---|
| `/sdr/dashboard` | `AdminDashboard` | calendar/to-dos hidden; renders `SDRReports` (personal + team performance) |
| `/sdr/leads` | `AdminLeads` | **only leads `assigned_to === me` or created by me**; full CRM actions on those (add/edit, draft-js email compose, notes, convert) |
| `/sdr/achievements` | `AdminAchievements` | students/preceptors/existing-students tabs only (no coordinators/SDR tabs) |
| `/sdr/programs`, `/sdr/programs/:slug` | `AdminProgram(Detail)` | restricted to `user.detail.includedPrograms[]` |
| `/sdr/rotations`, `/sdr/permission` | admin components | nominally routable but menu-hidden; **server must deny** in rewrite |
| `/sdr/:id` | `SalesProfile` | own profile edit only |

SDR-relevant reports: `IngestedLeadsReport`, `ConvertedLeadsReport`, `PersonalPerformanceReport` (leads created/converted, time-to-convert), `SDRTeamPerformanceReport`, `LeadSourcesReport`.

## 4. Sales / Institution surface

| Route | Reuses | Behavior |
|---|---|---|
| `/admin/dashboard` | `AdminDashboard` | `AdminSalesDashboardData` view (their rotations/students only); no to-dos |
| `/admin/programs` (+detail) | `AdminProgram` | only `includedPrograms[]`; tabs limited to InPerson + TeleRotation |
| `/admin/sales-students` | `AdminSalesStudents.js` | **current rotations + historical students of their institution**; add new student (`AddNewStudent` modal), view, delete, sort (date/name/evaluation/status) |
| `/admin/sales/:id` | `SalesProfile.js` | profile: names, contact, avatar, **institution logo**, password, note, and (admin-editable) program-access list |
| `/admin/contact/:id` | `ContactDetails` | their students' detail pages |

Blocked for both (client-side today, server-side in rewrite): permission, rotations mgmt, honorarium, achievements, customer service, admin mgmt, data, analytics.

## 5. CRM data model touched

`lead` (type Hot/Warm/Cold, status New→In Progress→Qualified→Not Qualified→Converted→Turned-into-contact, source, assigned_to, academic fields), `lead_log` (history/audit), emails (threads + draft-js compose), call recordings/history (`AdminCallHistory` — verify recording provider/storage during build), notes, `sales_dashboard_data` (precomputed metrics — replaced by query+cache in rewrite). Lead auto-conversion hook from student signup (see Plan_Student §3) attributes conversions to the owning SDR — **preserve attribution logic**.

## 6. Rewrite decisions

1. **Server-side enforcement** of the §3/§4 matrices (new; closes the privilege-escalation hole).
2. One staff SPA area (`staff/`) with role-driven navigation — no duplicated `/sdr/*` component tree; routes stay aliased for bookmark compatibility.
3. `includedPrograms` moves from a JSON blob on the user to a `user_program_access` join table (auditable, queryable).
4. Lead assignment/reassignment events recorded in audit log; SDR performance reports computed server-side (today: 10k-row client-side aggregation).
5. Email compose → TipTap; sends via Notifications module with thread persistence.
6. Tablet-responsive (sales reps demo programs on tablets); phone support best-effort.

## 7. Parity checklist (cutover gate)

SDR login → sees only own leads (verify with a second SDR's data invisible **at the API level**); lead lifecycle end-to-end incl. email send + note + convert with attribution; SDR reports match legacy numbers for migrated data; Sales login → only included programs visible (API-level check); sales-students add/view/delete; institution login lands and functions as sales; admin sees everything both see.
