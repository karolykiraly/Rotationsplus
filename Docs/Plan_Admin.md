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

> **Build status.** Slice 9 (PR #16) stood up the SPA foundation the rest of this inventory builds on: React Router + TanStack Query + React Hook Form/zod, a role-gated admin shell (sidebar/topbar, brand `#FF4874`, tablet-usable). Management screens shipped on those patterns — the marketplace admin trio is complete: **Specialties CRUD** (`/admin/specialties`, slice 9), **Programs CRUD** (`/admin/programs`, slice 10 / PR #17), and **Preceptors CRUD** (`/admin/preceptors`, slice 11 / PR #18 — identity/professional fields, valid-email + duplicate-409 handling, status lifecycle dropdown, edit pre-fills from detail). The richer legacy routes (rotations, leads/CRM, honorarium, reports, contacts) come after, along with the SPA hardening backlog (§5b).

> **Build progress (new admin console):** **`/admin/dashboard`** shipped (PR #27) — the hub: domain totals, the rotation pipeline by status, and upcoming starts (`GET /api/dashboard`, AdminOnly). **PHASE 2d** expanded the same endpoint to back the LiveScore cards end-to-end: the program catalog is now broken out by delivery type (`ProgramsByType`, summed into the InPerson/Consultation/TeleRotation rows the SPA shows — research/sub variants fold into their base family), and a `Today` block returns the day's movement (new programs by type, new students, new preceptors, and the rotation cycle: `Starting`/`InProgress`/`Completing`/`Cancelled`). "Today" is the **business day in US/Pacific** (matching legacy), driven by `TimeProvider`; the cycle buckets are mutually disjoint (a same-day rotation counts only as *Starting*; *Completing* excludes it; *InProgress* is strictly mid-flight) so they sum cleanly to the circle. "Issues Reported" is hard-wired to 0 until an issues subsystem exists. **PHASE 2f** then tuned the dashboard + rotations queries: the four today's-cycle counts collapse into **one** `COUNT(*) FILTER (…)` pass (13 → 10 round-trips), and `rotations` gained a composite **`(Status, StartDate)`** partial index (serving the admin list's status-filter + start-date sort and the dashboard cycle counts) plus a **`StartDate`** partial index (upcoming-starts) — both partial on `NOT IsDeleted` to match the global soft-delete filter. (Server-side **list pagination** is split into its own slice — a breaking response-envelope change touching ~6 SPA pages.) `/admin/specialties`, `/admin/programs`, `/admin/preceptors` (marketplace CRUD) shipped slices 9–11. **`/admin/rotations`** shipped slice 14 (2026-06-16, PR #21) — admin CRUD over the `Rotation` booking with the full legacy status filter. **`/admin/students`** shipped slice 15 (2026-06-16, PR #22) — the student directory (identity + academic/visa classification + lifecycle status, StaffOnly reads / AdminOnly writes). Slice 16 (PR #24) wired the **rotation → student picker**: the rotation form now selects a directory student (FK + snapshot) instead of free-text, and student-delete is guarded while rotations reference it. PR #26 added the **rotation status state machine** — `RotationStatusMachine` enforces legal lifecycle transitions on update (illegal jumps → 400), and the edit form's status dropdown is limited to the current status + its allowed next states (server-enforced). The proposed transition graph lives in `RotationStatusMachine.cs` — adjust there if the business rules differ. **Owner decision pending:** terminal states (Rejected/Refunded/Abandoned, and Completed except →Refunded) currently have no admin "undo" — a fat-fingered terminal status can't be walked back. If admin-correction is wanted, prefer a separate explicitly-audited override path over loosening the graph (which would weaken the forward-flow guarantees). The deep student profile (exam scores, documents, payments) and the dashboard/CRM/analytics routes remain to be built on these patterns.

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
| `/admin/programs` | `AdminProgram.js` | Program list tabbed by type (InPerson/Research/Consultation/TeleRotation/TeleResearch), debounced search, filters, add/delete. **PR-4 (Production UI parity) aligned the list columns to the live screen:** the **Program Name** column derives `"{Specialty} Physician"` (matching the legacy default, which the rewrite has no standalone name field for) so it's no longer a duplicate of the **Specialty** column; the **"Retail Amount"** column now shows the **weekly honorarium** (owner-confirmed 2026-06-26 to match production, which displays `weekly_honoarium` under that label even though a separate retail field exists). Backend: `ProgramSummaryResponse` gains a **staff-only nullable `WeeklyHonorarium`** — both `GET /api/programs` and `/catalog` (MarketplaceViewer; students can call them) **null it for customer callers**, mirroring the detail endpoint, so margin stays hidden. No migration (the column already existed). **Deferred to PR-4b:** wiring the **Filter** modal (a 6-dimension legacy modal — programId/city/specialty/instant-approval/honorarium-range/tags — needs server-side filter params; its own PR per "one concern per PR"); the Filter button remains a placeholder until then. |
| `/admin/programs/:slug` | `AdminProgramDetail.js` | Edit program: description, image, type, preceptor assignment, publish state |
| `/admin/rotations` | `AdminRotations.js` | Current + history tabs; full status filter (12 statuses); edit rotation modal (status/payment/dates/docs), change-date modal, delete. **PR-3 (Production UI parity) reworked this to the live layout:** two stacked **Current Rotations** / **Historical Rotations** sections (each server-paginated with its own search), the production columns **Rotation # · Preceptor · Student · Start–End · Retail Amount · Needs Visa ☑ · Status** (colour-coded, "NotStarted"→"Approved"), dropping the rewrite's Specialty/Type/Weeks columns + the status-filter dropdown + the inline Edit/Delete/Refund (owner decision 2026-06-26 — hide them to match production; the backend DELETE/refund endpoints remain). A **View** button opens the **Selected Rotation** detail panel (Replace program / Change dates / status dropdown / Save), saved via the single reliable update endpoint. **Backend:** `RotationSummaryResponse` +`RetailAmount` (program retail/wk × weeks) +`NeedsVisa` (correlated subquery — the booked student's `VisaStatus == NeedsVisaHelp`); a **`scope=current\|historical`** list param (Current = non-terminal Pending/NotStarted/Active/ToBeEvaluated, Historical = terminal Completed/Cancelled/Refunded/Abandoned/Rejected); `RotationDetailResponse` +`ProgramNumber` +`RetailAmount` (Rotation Cost) +`PaidAmount` (sum of Succeeded payments). No migration. **Deferred to PR-3b/Filter:** the FilterRotation modal (date/amount/status/visa filters) — its own PR like Programs' Filter. |
| `/admin/rotations/new` | `AdminRotationsNew.js` | Manual rotation creation: student picker + program picker + date range (auto weeks) |
| `/admin/permission` | `AdminPermission.js` | Preceptor approval queue: activate/reject, send agreement (rewrite: emails generated PDF, tracks signed-copy upload + admin approval — HelloSign removed), request W9, set doc due dates, per-program access. **PERM-1 built (PR #73), then REWORKED to production (PR #—, UI-parity program):** the queue now matches the live screen exactly — columns **Preceptor Name · Specialty · Scheduled (Yes/No) · Phone Number · Email · Activated ☑ · Reject ☑** + a single **Save** (batch). `Preceptor` gains `MobilePhone` + `CallScheduled` (display-only; the call-booking flow is post-cutover). The per-row Approve-button + Reject-with-reason modal of PERM-1 was REMOVED (it deviated from production — match-first, improve-after-cutover); the audited transition logic is kept behind a batch endpoint **`POST /api/preceptors/permissions { activateIds[], rejectIds[] }`** (Pending-only, activate→MemberActivated / reject→Rejected reason-less, id-in-both→400, stamps reviewer+time). **Deferred to follow-on slices:** agreement-PDF flow (§3.15a — needs QuestPDF + email + the preceptor Documents page), W9 request, doc due dates, per-program access. |
| `/admin/honorarium` | `AdminHonorarium.js` | Preceptor payouts in 3 stages (Deposit / Start / Evaluation), mark paid, refund flag + history. **HON-1 built (PR #—):** the 3-stage payout queue. A rotation's schedule is generated automatically on deposit success (program weekly honorarium × weeks, split 25/25/50, exact-to-the-cent) in `HonorariumGenerator` (called from `PaymentFulfillment`, idempotent + unique (RotationId,Stage) index backstop). Admin pays each stage (server-gated to in-order: Start needs Deposit paid, Evaluation needs Start paid; audited PaidBy/PaidAtUtc), toggles the refunded bookkeeping flag (Deposit tab), and **deletes** an erroneously-generated row (Deposit tab, `DELETE /api/honorariums/{id}` — soft-delete, **refused 409 if Paid**, confirm dialog). **UI matched to production** (owner-supplied screenshot 2026-06-26): exact column labels (Rotation Number / Preceptor Name / Student Name / Honorarium Amount / Rotation Start Date / Refunded / Payment Status), "N items" count, `$450`-style amounts, outline Pay button, Paid badge, Delete button. **Notes/deferred:** marking paid is bookkeeping only (no gateway disbursement — preceptor payouts are external today); generation hooks the deposit-success path only (a manual admin Pending→NotStarted approval without a deposit does not generate a schedule — revisit if that becomes a real flow); preceptor `total_honorarium` roll-up (profile Data tab) is derivable from rows, not denormalized. **PR-2 (Production UI parity) built the Evaluation-tab columns:** the Evaluation tab now matches production — columns Rotation Number / Preceptor Name / Student Name / Honorarium Amount / **Evaluation Upload Status** (legacy hardcodes "Completed") / **Evaluation Due Date** / Payment Status (it drops the Rotation Start Date column the Deposit/Start tabs show). The due date is a snapshot on the honorarium (`EvaluationDueDate`, rotation end date + the legacy 7-day grace, captured at generation; nullable → "-" on rows generated before the column existed). The full evaluation-document subsystem (real upload status) stays a later phase. **Related follow-up (owner decision 2026-06-26):** realign the **Permission** screen (`/admin/permission`) to the production columns (Specialty / Scheduled / Phone Number / Email) + Activated/Reject checkboxes + Save — its own PR (needs a preceptor phone field + a "scheduled" concept). |
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
1. ✅ **Token re-auth on expiry** (PR #28) — `api.ts`/`customerApi.ts` now catch `InteractionRequiredAuthError` from `acquireTokenSilent` → `acquireTokenRedirect` (both staff + customer flows), so an expired session re-prompts instead of erroring.
2. ✅ **Security headers** (PR #28) — CSP (script/style/connect/frame scoped for MSAL), `X-Frame-Options: DENY` + `frame-ancestors 'none'`, `X-Content-Type-Options: nosniff`, `Referrer-Policy`, `Permissions-Policy` in `staticwebapp.config.json`. **Verify on DEV:** redirect sign-in is CSP-independent; if silent renewal's iframe is ever blocked it degrades to a redirect via item 1, so tighten `connect-src`/`frame-src` only after confirming the live MSAL endpoints.
3. ✅ **Modal focus management** (PR #28) — the reusable `Modal` now moves focus into the dialog on open, traps Tab/Shift+Tab, and restores focus to the trigger on close.
4. **Admin-page query gating** — gate list queries with `enabled: isAdmin` so non-admins don't fire a (currently harmless, StaffOnly) fetch before the forbidden notice; lets the test assert no fetch. *(Deferred — minor; pairs naturally with #6.)*
5. **Route-level code-splitting** — `lazy()` routes before the customer portal (LCP < 2.5s budget); the single bundle is acceptable for the internal console now.
6. **Directory read-authz vs UI gate (cross-cutting)** — the Preceptor + Student directory APIs are **StaffOnly read / AdminOnly write**, but every management page (and its nav link) is gated to `isAdmin`, so non-admin staff (Sales/SDR/Coordinator) can't see the directories in the UI despite being authorized to read. Decide the model once for all directories: either (a) render a **read-only** directory for non-admin staff (hide Add/Edit/Delete), honouring the "CRM works the directory" intent, or (b) tighten the reads to **AdminOnly** so API and UI agree. Apply uniformly to specialties/programs/preceptors/students. Fail-closed today (non-admins see less, not more), so not a DEV blocker.

## 6. New features (owner-requested, 2026-06-11)

1. **Impersonation / "View as user"** — button on student/preceptor profile pages; opens the member's exact dashboard in a banner-marked, **read-only**, fully audited session (15-min app-issued token; design in `Plan_Architecture.md §3.14`). Solves "user says X isn't showing" support tickets without guesswork.
2. **Program documents management** — "Documents" section on `/admin/programs/:slug`: upload/replace/delete program contracts & application forms (→ Blob), mark required, students see them dynamically. Replaces the hardcoded 27-program file mapping + manual server copies (design in `Plan_Architecture.md §3.15`).
4. **Blog/content management** — new "Blog" admin section: list + TipTap editor (title, editable slug w/ SEO preservation, category, rich body w/ image→Blob upload, featured image, meta/OG fields, Draft/Publish/schedule). Replaces Strapi CMS; auto-regenerates sitemap + prerendered pages on publish (design in `Plan_Architecture.md §3.15b`). Admin role only.
3. Both features get Playwright e2e coverage and appear in the admin audit log.

## 7. Parity checklist (cutover gate)

For each route in §2: load with prod-copy data on PREPROD, compare against legacy side-by-side: row counts, filters, status values, money totals (dashboard revenue, honorarium sums, outstanding payments), one full CRM flow (create lead → email → note → convert), one full ops flow (approve preceptor → send agreement → W9 → activate), honorarium 3-stage pay + refund, each report renders with same numbers (± defined mapping differences).
