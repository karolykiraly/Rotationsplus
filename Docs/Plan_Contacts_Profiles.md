# Plan — Admin Contacts Profiles (Student / Preceptor / Sales / SDR)

Ports the four legacy admin **profile** screens (the name-link targets from the Contacts hub tabs) to
the rewrite, faithfully, tab by tab. Legacy source is the ≥99% ground truth
(`Live_Code/06102026/rotationsplusweb-v4/src/routes/dashboard/...`); owner-provided screenshots confirm
layout. Full field-level inventory captured from source (StudentProfile.js, PreceptorProfile.js,
SalesProfile.js).

## Owner decisions (2026-06-30)
- **Password field → OMITTED.** Auth is Entra (staff) + Entra External ID/CIAM (customers); the app
  stores no passwords. Credentials are IdP-owned. (A Graph-API "send reset" action can be a later slice.)
- **Scope → build everything fully, in order** (every sub-tab + its backing data, not field-first-defer).
- **Order → Student → Preceptor → Sales/SDR.**
- **Email is read-only on the profile** (CIAM/Entra-linked identity; same rationale as the password).

## Cross-cutting conventions
- **Per-tab save endpoints** matching production's independent per-tab Save (`onSaveProfileN`):
  e.g. `PUT /api/students/{id}/personal-info`, `/needs`, `/education`, `/sales`. AdminOnly.
- Profile loads via the existing `GET /api/students/{id}` → `StudentDetailResponse` (extended per tab).
- Name links: Contacts tabs + (later) Rotations/Dashboard rows → `/admin/students/:id`, `/admin/preceptors/:id`.
- List-tab rows become **Delete-only** (production), Edit/Documents move onto the profile.
- New enums stored `HasConversion<string>()`. New nullable columns (profile data is optional/partial).
- `VisaStatus` (coarse, 4-value) stays for the needs-visa filter/dashboard; the granular
  `ImmigrationStatus` (11 + Other) is the profile field. **Post-port cleanup:** unify (derive the coarse
  flag from the granular value, retire `VisaStatus`).

## Accepted deviations (reviewed, intentional — not silent simplifications)
- **"Other" is a selectable Immigration-Status option.** Production surfaces the free-text override only
  for legacy `visa_status` values outside its 11-item list. The rewrite models immigration as an enum, so
  a selectable `Other` (paired with the free-text field) is the clean equivalent path to that free-text —
  removing it from the menu would strand the value and break the controlled `<select>`.
- **`AcademicStatus` includes `MdStudent`** (8 values vs the legacy add-student modal's shorter list) — a
  pre-existing enum decision covering the MD track from public signup; used across the app + seed.
- Passport-issued-country is a text input (country-dropdown = a small follow-up); avatar upload deferred
  to its own slice; Password omitted + Email read-only (auth is IdP-owned).

## Slice breakdown

### Student profile (route `/admin/students/:id`, 7 tabs)
- **A. Shell + Personal Information** — tabbed page + Personal Info tab. New Student fields: Birthdate,
  Gender, ImmigrationStatus(+Other), VisaInterviewDate, PassportIssuedCountry, PassportNumber,
  SelectedIdType/IdNumber (DO), AvatarBlobName. Name-link + Delete-only rows. *(avatar upload = A2)*
- **A2. Avatar upload** — blob upload/serve for the profile photo (reuse program-image blob infra).
- **B. Needs** — interests (specialty grid), specialty-locations (multi-select), importants (priorities).
- **C. Education** — branches by academic status: USMLE (IMG/IMS), COMLEX (DO), undergrad/year (pre-med),
  school/grad-date/TOEFL/INDBE (dental), ECFMG/applied-match.
- **D. Rotations** — per-rotation table (preceptor, dates/weeks-or-hours, dollars, docs-approved,
  preceptor-confirmed, status, discount, evaluation score/doc, PAL generate/upload). *Depends on:
  discount, evaluation docs/scores, PAL generation subsystems.*
- **E. Achievements** — read-only rollups (referrals, total spent, total weeks).
- **F. Documents** — per-rotation document table (reuse the existing admin StudentDocumentsModal content).
- **G. Sales** — institution (ro), creation date (ro), source, opt-out, lead type, campaign, sales note.

### Preceptor profile (route `/admin/preceptors/:id`, 7 tabs)
- Programs Methods · Commitments (per-program: capacity, unavailability calendar, letterhead, expertise) ·
  Personal · Professional (license/practice/address) · Academia & Affiliations (designations, tags,
  rich-text description) · Data (pricing, required docs, privilege hospitals) · Documents (W9 + e-signed
  Agreement). *Depends on: rich-text editor, unavailability calendar, W9/e-signature subsystems.*
- Backend for the list tab (parallel): sequential `PreceptorNumber` (= production "Preceptor ID") +
  Honorarium/Retail rollups on the summary; list row → Name-link/ID/Specialty/Location/Honorarium/Retail/Delete.

### Sales & SDR profile (routes `/admin/sales/:id`, `/admin/sdr/:id`, 2 tabs, same component)
- Personal information (+ Institution) · Sales (institution logo, program assignment, notes).
- Needs a new staff-user "detail" model (sales/SDR are workforce users).

## Deferred deep subsystems (their own slices, called out where a tab needs them)
Student credit/discount · evaluation docs + scores · PAL letter generation · rich-text program
descriptions · preceptor unavailability calendar · W9 + e-signature Agreements · avatar/logo blob uploads.
