# Plan_Preceptor — Preceptor Dashboard & Flows Discovery + Rewrite Spec

**Date:** 2026-06-11 · **Source:** code analysis of `rotationsplusweb-v4` (preceptor routes/components) + backend (`preceptor`, `program`, `document`, `honorarium`, `rotation` APIs, HelloSign utils, cron reminders).

---

## 1. Preceptor journey (current behavior to preserve)

```
/for-preceptors (marketing) → Signup modal (3 steps incl. email OTP)
  → status 'registered' → /preceptor-complete-profile (or /dental-preceptor-complete-profile)
  → admin approval queue (/admin/permission): agreement (PDF sent → signed copy uploaded; HelloSign removed) + W9 + activation
  → status 'member_activated/validated/signed' → /preceptor/dashboard
  → rotations: tokened email confirm links + SMS reminders → precept → evaluate student
  → honorarium paid in 3 stages (deposit / start / completion+evaluation)
```

Status enum today: `registered, pending, member_profile_completed, member_activated, member_validated, member_signed`. Login behavior depends on it (registered → onboarding; pending → "admin hasn't activated your account"; else → dashboard).

## 2. Public & auth surfaces

- **`/for-preceptors`** (`src/routes/ForPreceptors.js`): hero, 5 benefits, 5-step onboarding pipeline, 6-item FAQ (incl. honorarium staging explanation: % at purchase, % at start, balance at completion), 3 "ONBOARD TODAY" CTAs → PreceptorLogin modal.
- **Signup** (`modal/PreceptorSignup.js`): Step 1 general info (name auto-capitalize, phone, email, password, referral code) → Step 2 six-digit email OTP (resend, spam-folder hint; **group emails skip OTP** via `checkPreceptorGroupEmail`) → Step 3 success + link to onboarding. APIs: `isValidEmail`, `addOTP`, `registerPreceptor`, `resendCode`.
- **Login** (`modal/PreceptorLogin.js`): email/password, forgot-password, student toggle.

**Rewrite:** signup/login become Entra External ID flows (email verification handled by Entra; Google sign-in available). The status-based routing after login is preserved in the app (claims + `users.status`). Preceptor-group skip-OTP behavior re-implemented as pre-provisioned Entra accounts for group members.

> **Customer MSAL wiring caveat (from slice 8):** the dormant customer (CIAM) MSAL instance shares an origin + `sessionStorage` with the staff instance — when activating preceptor sign-in, give it a distinct `redirectUri` and make API token acquisition issuer/instance-scoped. Full detail in `Plan_Student.md §3`.

## 3. Onboarding — `CompletePreceptorProfile.js` (~117KB, the big one)

Multi-step stepper; every step below is a rewrite-spec section (decompose into per-step components + React Hook Form + zod, save-per-step):

1. **Personal** — names, username, mobile, email/password, referral.
2. **License & education** — specialty + conditional sub-specialty, residency program, fellowship, medical license number + state, disciplinary-history checkbox.
3. **Professional** — office phone, address (street/city/state/zip), practice manager name+email.
4. **Program setup** (most complex) — select program types (InPerson / Consultation / TeleRotation; InPerson-Research & Tele-Research are "coming soon"/disabled; dental variant has single type). Per type, tabbed config:
   - capacity: max students/rotation, min weeks/rotation
   - **availability calendar** (6-month week-based selection) + availability mode `Open` (published schedule) vs `Manual` (contact each time)
   - letterhead preference (Clinic/Hospital/Other)
   - privileges: clinical privileges, privilege hospitals (async hospital select), research involvement, inpatient/outpatient
   - designations (9 checkboxes: Program Director, Faculty, Chief Hospitalist, Hospitalist, Professor, Medical Director, Department Head, Clinical Instructor, Physician)
   - program name + rich-text description (draft-js → TipTap)
   - required documents (standard checkboxes + custom document types)
   - patient flow: patients/week, telemed %, telemed format (Zoom/FaceTime/Doximity/custom)
5. **Availability & scheduling** — unavailable-dates picker; text-approval mobile number. (**Calendly opt-in/embed removed in rewrite** — owner-confirmed unused 2026-06-11.)
6. **Rates** — retail amount/week, weekly honorarium, notes.
7. **Documents & agreements** — W9 upload; **preceptor agreement: in-app flow replacing HelloSign** (system emails generated agreement PDF → preceptor uploads signed copy → admin approves; statuses Sent → Uploaded → Approved/Rejected; see `Plan_Architecture.md §3.15a`); license upload.
8. **Bank info** — ACH details (**must be encrypted at rest in the rewrite** — today plain JSON).

Dental variant (`CompleteDentalPreceptorProfile.js`): dental specialties/sub-specialties (`helper/dentalSpecialty.js`), `dentalLicenseNumber`, DDS/DMD + advanced residency program fields, single `dental` program type.

**Auto-generated program tags** (24 possible: "Most Popular", "Hands On", "Hospital Letterhead LOR", "Research", "Instant Approval", "Residency Audition", "Inpatient", "Academic Affiliation"…) derived from these selections — used heavily by student search filters; tag-derivation rules must be ported exactly (extract into a tested domain service).

## 4. Dashboard routes (`/preceptor/*`)

| Route | Component | What it does | Rewrite notes |
|---|---|---|---|
| `/preceptor/dashboard` | `index.js` | Pending evaluations (Finished/To-Be-Evaluated rotations: download evaluation form, send evaluation email); upcoming-rotations calendar (8 months); active rotations list with show-more | evaluation PDF → QuestPDF; calendar componentized |
| `/preceptor/profile` | **`PreceptorProfile.js` (167KB)** | Tabbed: Programs methods / Commitments (per-type config mirroring onboarding §3.4) / Personal / Professional / Academia & affiliations / Data (read-only metrics: total honorarium, approval rate, response time, cancellations) / Documents | decompose like onboarding; share step components between onboarding & profile (same forms!) — single source of truth |
| `/preceptor/students` | `PreceptorStudents.js` | Rotation students (sortable: date/name/evaluation/status, honorarium per rotation, show-more) + **coordinator-only** tab: institution's pending students + AddNewStudent modal + delete | coordinator concept = preceptor with `institution` field; model as explicit role/flag |
| `/preceptor/documents` | `PreceptorDocuments.js` | Own docs (W9 status/upload/download; agreement: legacy HelloSign request with 30-second status polling) + student documents per rotation (search, view modal) | **HelloSign removed** → agreement becomes upload-signed-copy flow (§3 step 7); no polling needed; document viewer via SAS URLs |
| `/preceptor/help` | `PreceptorHelp.js` | Support form: 6 issue categories + message → `contactUs` → issues table | keep; routes into Crm issues |
| `/preceptor/search` | `PreceptorSearch.js` | **Stub with hardcoded fake students — dead code** | drop (or build real student-search if product wants it; default: drop) |

## 5. Rotation confirmation & evaluation flows

- **`/confirm-rotation?code&answer`** (`ConfirmRotation.js`): tokened email link → `confirmRotationByPreceptor` → approval recorded (states: confirmed / already approved / error). Driven by cron reminders: SMS at 48h (every-20-min sweep) and 72h (every-30-min sweep) until answered.
- **`/start-confirmation?code`** (`StartConfirmRotation.js`): start-of-rotation readiness confirm, reminded by 10-min sweep job.
- **Evaluation:** when rotation ends (status To-Be-Evaluated, hourly sweep + 72h notification), preceptor downloads/sends evaluation; rating/review via `Rating.js` modal (numeric + text). Feeds program ratings (5-min aggregation today).

**Rewrite:** single-purpose signed tokens (purpose, rotationId, expiry) issued by Rotations module; anonymous endpoints; all reminders from Hangfire jobs with a **reminder ledger** (idempotent, auditable) instead of `sent_*` boolean columns.

## 6. Payments (preceptor side)

- Honorarium staged 3 ways (deposit on student purchase / start / completion+evaluation) — admin-driven today (`/admin/honorarium`), preceptor sees read-only metrics in profile Data tab (`total_honorarium`, `weekly_honorarium`, `bonus_spent`, approval metrics).
- ACH payout appears **manual** (admin marks paid; bank info stored for reference — no payment-provider payout API found in code). Rewrite keeps manual marking + audit, with bank details encrypted; a Stripe Connect payout automation is a *possible future feature, not in scope*.

## 7. Notifications a preceptor receives (must keep firing identically)

| Trigger | Channel | Source job |
|---|---|---|
| New rotation request pending 48h / 72h | SMS (+email) | PendingApproval jobs |
| Start confirmation due | email link | StartConfirmationReminderJob |
| Documents/W9/agreement requests + due dates | email | admin actions + DocumentDueDate jobs |
| Rotation completed → evaluate (72h) | email | RotationCompletionJob |
| 14-day review reminder | email | ReviewReminder14dJob |
| Honorarium paid | email | Payments module events |

## 8. Parity checklist (cutover gate)

Signup→onboarding full pass (medical + dental variants); agreement flow round-trip (PDF sent → signed copy uploaded → admin approved → visible to preceptor; historic HelloSign-signed agreements visible after migration import); W9 upload; availability calendar correctness (week boundaries, 6-month window, unavailable dates block student booking); confirm + start tokened links from real emails; evaluation download/submit; students list + coordinator add-student; profile edits persist across all tabs; Data-tab metrics match legacy values for the migrated dataset.
