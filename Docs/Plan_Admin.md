# Plan_Admin — Admin Dashboard Discovery & Rewrite Spec

**Date:** 2026-06-11 · **Source:** code analysis of `rotationsplusweb-v4` (`src/routes/dashboard/admin/`, 59 components) + backend custom routes.
**Companion docs:** `Plan_Sales_SDR.md` (SDR/Sales reuse most of these screens with gating), `Plan_Architecture.md` §3.3/§3.5.

---

## 1. Access & login today

- **Entry:** `/rotationsplusadmin` (`src/components/AdminLogin.js`). Two-step 2FA: email+password → `verifyAdmin` sends a 4-digit code by SMS+email → `loginAdmin(email, pass, validCode)`.
- All dashboard routes gate on `user.role.name === 'Administrator'` with a `type` field distinguishing blank (full admin) / `sale` / `sdr` / `institution` (i.e., **all staff share one Strapi role** — a weakness; authorization is scattered in component-level `if`s).
- Hardcoded protected accounts (cannot be deleted in UI): omer@, charles@rotationsplus.org, hantig1986@gmail.com.

**Rewrite:** Entra External ID app roles (`Admin`, `Sales`, `SDR`, `Institution`) + ASP.NET authorization policies enforced **server-side per endpoint** (today most gating is client-only — any sales JWT can call admin APIs!). `/rotationsplusadmin` remains as a route that triggers the Entra staff flow; MFA enforced by policy instead of the homegrown 4-digit code.

## 2. Route inventory (all ~45 routes)

> **Build status.** Slice 9 (PR #16) stood up the SPA foundation the rest of this inventory builds on: React Router + TanStack Query + React Hook Form/zod, a role-gated admin shell (sidebar/topbar, brand `#FF4874`, tablet-usable). Management screens shipped on those patterns: **Specialties CRUD** (`/admin/specialties`, slice 9) and **Programs CRUD** (`/admin/programs`, slice 10 / PR #17 — specialty/type/preceptor dropdowns, capacity + money validation, edit pre-fills from program detail). **Preceptors admin CRUD** is the last core marketplace screen (slice 11). The richer legacy routes (rotations, leads/CRM, honorarium, reports, contacts) come after.

### Core
| Route | Component | Purpose | Key APIs |
|---|---|---|---|
| `/admin/dashboard` | `AdminDashboard.js` | Hub: calendar of rotation start dates with per-day checklist (docs approved / preceptor confirmed / visa), payment & document to-dos, LiveScore metrics (today + historical: programs, registrations, approvals, issues, rotation stages). Tabs: Results / ToDo's / Campaign / Reports / Revenue | `getLiveScore`, `getRotations`, `getOutstandingRotations`, `getCompletedPreceptors`, `getToDoDocuments` |
| `/admin/analytics` | `AdminAnalytics.js` | Tab hub → Leads / Students / Preceptors / SDR / Sales dashboards (see §4) | aggregation queries |
| `/admin/search` | `AdminSearch.js` | Quick student/preceptor lookup table → profile links | `getStudents`, `getPreceptors` |

### CRM / Leads
| Route | Component | Purpose |
|---|---|---|
| `/admin/leads` | `AdminLeads.js` | Student-lead + preceptor-lead lists (tabs), search, filters (status, type Cold/Warm/Hot, assigned SDR). Actions: add/edit lead, **compose email (draft-js)**, notes, delete, reassign, convert to contact |
| `/admin/lead/:id` | `LeadsProfile.js` | Lead profile: tabs Personal / Emails (thread + compose) / Phone recordings / Chats / Notes / History. Status workflow New→In Progress→Qualified→Not Qualified→Converted; reassign to SDR; convert-to-contact modal |
| `/admin/achievements` ("Contacts") | `AdminAchievements.js` | Unified directory: Students / Preceptors / Coordinators / SDRs / Existing students. Paginated, filter modals, reset password, delete, profile links |
| `/admin/contact/:id` | **`ContactDetails.js` (176KB!)** | Deep contact profile — see §3 decomposition |
| `/admin/customer_service` | `AdminCustomerService.js` | Issue triage (student / preceptor tabs), notes, resolve, delete |

### Marketplace / Operations
| Route | Component | Purpose |
|---|---|---|
| `/admin/programs` | `AdminProgram.js` | Program list tabbed by type (InPerson/Research/Consultation/TeleRotation/TeleResearch), debounced search, filters, add/delete |
| `/admin/programs/:slug` | `AdminProgramDetail.js` | Edit program: description, image, type, preceptor assignment, publish state |
| `/admin/rotations` | `AdminRotations.js` | Current + history tabs; full status filter (12 statuses); edit rotation modal (status/payment/dates/docs), change-date modal, delete |
| `/admin/rotations/new` | `AdminRotationsNew.js` | Manual rotation creation: student picker + program picker + date range (auto weeks) |
| `/admin/permission` | `AdminPermission.js` | Preceptor approval queue: activate/reject, send agreement (rewrite: emails generated PDF, tracks signed-copy upload + admin approval — HelloSign removed), request W9, set doc due dates, per-program access |
| `/admin/honorarium` | `AdminHonorarium.js` | Preceptor payouts in 3 stages (Deposit / Start / Evaluation), mark paid, refund flag + history |
| `/admin/data` | `AdminData.js` | Preceptor groups (email-based grouping for batch ops): create/add/move/delete |
| `/admin/admin` | `AdminAdmin.js` | Staff account management: Admins / SDRs / Sales / Coordinators tabs; add user, send email, reset password, delete |
| `/admin/students/:id`, `/admin/preceptors/:id`, `/admin/sales/:id` | profile views | Admin views of member profiles (sales profile incl. program-access list, avatar, institution logo) |

### Reports (`/admin/reports/*`)
| Route | Purpose |
|---|---|
| `/reports/rotated`, `/reports/non-rotated` | Email-campaign builders: filter students (rotated vs prospects, $ spent, weeks), select, compose (draft-js), add extra recipients, send |
| `/reports/ocr` | OCR verification calendar → per-day document list with status/confidence, verify/reject/retry |
| `/reports/leads` | Lead analytics: ingested, converted, personal/SDR-team performance, sources, preceptor-program sales/no-sales |
| `/reports/students` | Registrations, sources, purchases/revenue |
| `/reports/preceptors` | Program sales/no-sales, honorarium summary, approvals timeline |
| `/reports/sdr` | SDR team performance (also the SDR's own dashboard view) |
| `/reports/sales` | Sales rep performance, revenue |

Shared report widgets (13 components): `IngestedLeadsReport`, `ConvertedLeadsReport`, `PersonalPerformanceReport`, `SDRTeamPerformanceReport`, `LeadSourcesReport`, `StudentSourcesReport`, `StudentPurchasesReport`, `PreceptorProgramsSalesReport`, `PreceptorProgramsNoSalesReport`, `PreceptorHonorariumReport`, `ApprovedPreceptorsReport`, `RegisteredStudents`, `IngestedStudentsReport`. All share a date-range filter (3 days/weeks/months/years/custom) and per-person selectors.

## 3. `ContactDetails.js` (176KB) decomposition plan

Today: one file detects contact type (student/preceptor/SDR/sales) and conditionally renders everything. Rewrite as a route shell + lazy feature panels:

```
staff/contacts/ContactPage.tsx           # header (avatar, name, role badge, actions)
  ├─ panels/BioPanel.tsx                 # type-specific profile fields (RHF forms)
  ├─ panels/DocumentsPanel.tsx           # uploads, OCR status, W9/agreement requests
  ├─ panels/EmailThreadPanel.tsx         # thread + TipTap composer (replaces draft-js)
  ├─ panels/CallHistoryPanel.tsx         # logs, recordings player
  ├─ panels/NotesPanel.tsx
  ├─ panels/HistoryPanel.tsx             # server-side audit log (new, real audit table)
  ├─ panels/FinancePanel.tsx             # transactions, honorarium, refunds (preceptor)
  └─ panels/RatingsPanel.tsx
```
Each panel = own TanStack Query hooks + own API endpoints. Same decomposition style applies to the other monoliths (`PreceptorProfile`, `StudentProfile`).

## 4. Backend notes for the rewrite

- **Reports:** legacy fetches up to 10,000 leads to the browser and aggregates client-side. Rewrite: server-side aggregation endpoints (`/api/reports/...`) with SQL/EF group-bys + Redis cache; export-to-CSV server-side.
- **LiveScore/dashboard counters:** replace 1–2 min cron denormalization with on-demand queries + 60s Redis cache.
- **Email compose/threads:** sends via SendGrid with thread persistence in `emails` table; store message-ids for threading.
- **Call history:** legacy stores call logs/recordings (likely Twilio) — verify recording storage location during build; recordings move to Blob.
- **Authorization matrix** (enforced server-side; UI mirrors it):

| Capability | Admin | SDR | Sales | Institution |
|---|---|---|---|---|
| Dashboard (own view) | ✓ full | ✓ SDR view | ✓ sales view | ✓ sales view |
| Analytics | ✓ | ✗ | ✗ | ✗ |
| Leads | ✓ all | ✓ own only | ✗ | ✗ |
| Contacts | ✓ | ✓ (students/preceptors) | ✗ | ✗ |
| Programs | ✓ all | ✓ assigned only | ✓ assigned only | ✓ assigned only |
| Rotations mgmt | ✓ | ✗ | ✗ | ✗ |
| Honorarium / Permission / Customer service / Staff mgmt / Data | ✓ | ✗ | ✗ | ✗ |
| Sales-students | ✗ | ✗ | ✓ | ✓ |
| Reports | ✓ all | ✓ SDR set | ✗ | ✗ |

## 5. Functional gaps / cleanups to carry into the rewrite

1. **Server-side authorization** (today: client-side only for staff sub-types) — must-fix.
2. Audit log is ad-hoc per-feature "history"; new design: central append-only audit (who/what/before/after) surfaced in HistoryPanel.
3. Email campaign sends from the browser loop over recipients — move to Worker job with campaign status tracking.
4. Hardcoded protected-account emails → config/role-based safeguard.
5. OCR report "retry" exists in UI but verify backend support; formalize a re-validation endpoint.
6. Admin is desktop-only today; new staff area must be **tablet-usable** (responsive tables→cards), full phone support not required for admin (explicit decision; staff work on desktop/tablet).

### 5b. SPA hardening backlog (deferred from slice 9 review)

Tracked items from the admin-UI foundation review, to land before the staff console is exposed beyond DEV (none are blockers on DEV):
1. **Token re-auth on expiry** — `api.ts request()` calls `acquireTokenSilent` with no fallback; on an expired session / revoked refresh token it surfaces a raw error banner instead of re-prompting. Catch `InteractionRequiredAuthError` → `acquireTokenRedirect`.
2. **Security headers** — add CSP, `X-Frame-Options`/`frame-ancestors`, `X-Content-Type-Options: nosniff`, `Referrer-Policy` to `staticwebapp.config.json` (admin console handling Entra tokens).
3. **Modal focus management** — trap Tab within the dialog and restore focus to the trigger on close (the reusable `Modal`; benefits every future dialog).
4. **Admin-page query gating** — gate list queries with `enabled: isAdmin` so non-admins don't fire a (currently harmless, StaffOnly) fetch before the forbidden notice; lets the test assert no fetch.
5. **Route-level code-splitting** — `lazy()` routes before the customer portal (LCP < 2.5s budget); the single bundle is acceptable for the internal console now.

## 6. New features (owner-requested, 2026-06-11)

1. **Impersonation / "View as user"** — button on student/preceptor profile pages; opens the member's exact dashboard in a banner-marked, **read-only**, fully audited session (15-min app-issued token; design in `Plan_Architecture.md §3.14`). Solves "user says X isn't showing" support tickets without guesswork.
2. **Program documents management** — "Documents" section on `/admin/programs/:slug`: upload/replace/delete program contracts & application forms (→ Blob), mark required, students see them dynamically. Replaces the hardcoded 27-program file mapping + manual server copies (design in `Plan_Architecture.md §3.15`).
4. **Blog/content management** — new "Blog" admin section: list + TipTap editor (title, editable slug w/ SEO preservation, category, rich body w/ image→Blob upload, featured image, meta/OG fields, Draft/Publish/schedule). Replaces Strapi CMS; auto-regenerates sitemap + prerendered pages on publish (design in `Plan_Architecture.md §3.15b`). Admin role only.
3. Both features get Playwright e2e coverage and appear in the admin audit log.

## 7. Parity checklist (cutover gate)

For each route in §2: load with prod-copy data on PREPROD, compare against legacy side-by-side: row counts, filters, status values, money totals (dashboard revenue, honorarium sums, outstanding payments), one full CRM flow (create lead → email → note → convert), one full ops flow (approve preceptor → send agreement → W9 → activate), honorarium 3-stage pay + refund, each report renders with same numbers (± defined mapping differences).
