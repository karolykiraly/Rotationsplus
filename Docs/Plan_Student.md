# Plan_Student — Student Funnel & Dashboard Discovery + Rewrite Spec

**Date:** 2026-06-11 · **Source:** code analysis of `rotationsplusweb-v4` (public + student routes) + backend (`student`, `rotation`, `stripe`, `document`, `unlock`, `promo-code` APIs, cron reminder/drip jobs).

---

## 1. Student journey (current behavior to preserve)

```
Public site / blog / map search (limited, blurred without login)
  → Signup modal (3 steps incl. email OTP) → complete-profile (3 variants: MD / dental / DO)
  → full search (map + filters) → program detail → select weeks on calendar → cart
  → Stripe checkout (10% deposit for non-open programs; promo codes; credits)
  → rotation created (Pending) → preceptor approves → documents upload → OCR validation
  → outstanding balance paid by due date → rotation Active → completes → rate & review
Side flows: favorites, pay-to-unlock preceptor contact, WhatsApp/email onboarding drip
```

## 2. Public site (also the SEO surface — prerendered in rewrite)

- **Live homepage = `NewHome.js` (1,213 lines)**; legacy `Home.js` and `mobile/MHome.js` are dead code (remove).
- NewHome: search bar + 8 filter dropdowns (Specialty, Program Type, Clinical Needs multi-select, Rating, City, State, Duration, Pricing — **interdependent**: selections narrow other filters' options) + **Leaflet map** (US-bounded, center [39.8283,-98.5795] z4, `react-leaflet-cluster`, hospital markers with program counts, flyTo on filter/marker) + sortable program list (`NewRotationItem`). Search supports **program-code prefixes** (`IP…` InPerson, `TL…` TeleRotation, `CS…` Consultation) and keyword search with stop-word filtering across name/specialty/city/state/tags/hospitals.
- **Anonymous users:** search disabled + blurred mock results with "Unlock Full Search Results" CTA (`/limited-results`); logged-in → `/search-results` (3-col grid, sort, show-more pagination).
- Info pages: `/our-process` (6-step flow), `/our-team`, `/resources`, `/consultingservices`, `/faq`, legal. **Blog `/blog` + `/blogs/:slug`: API-driven from backend `blog-content`** (category filters: clinical_rotations, specialty, lors, residency, med_school). The 70KB `src/services/blogs.js` is dead legacy duplicate — remove; backend table is the source of truth and migrates to `blog_posts`.
- Footer: LA address, info@rotationsplus.org, +1 657-214-7174, socials (FB/IG/YouTube/Reddit).

**Rewrite notes:** keep Leaflet (+clustering); program-code and keyword search move **server-side** (today filtering happens largely client-side over big payloads); filters become a faceted-search endpoint; map pins endpoint with viewport-bounded queries. Prerender all public routes; keep slugs identical.

## 3. Signup & profile completion

- **Signup modal** (`modal/StudentSignup.js`, 645 lines), 3 steps:
  1. General: first/last name, phone (intl input), **academic status** (7: DO Student, Dental Student, IMG, Int'l Medical Student, NP Student, PA Student, US Pre-med), **visa status** (citizen/green card, valid visa, interview scheduled, needs help…), specialties (academic-status-dependent lists), preferred locations (98 cities + Other + custom), email (uniqueness check), referral code, password, "how did you hear" (16 sources).
  2. Six-digit email OTP (resend).
  3. Success → "Search rotations" or profile-completion link routed by academic status.
  - Marketing-source signups create/convert a **lead** (`createLead`, auto-converted if email matches an existing lead) — preserve this CRM hook.
- **Complete-profile variants:** `/student-complete-profile`, `/dental-student-complete-profile`, `/do-student-complete-profile` — education (school, country, graduation, year, undergrad program), exam scores (USMLE Step 1/2CK/2CS incl. pass/fail era handling, attempts; MCCQE for IMGs), specialties + "important features" preferences, visa/work authorization, languages, photo, CV upload, LinkedIn/publications. `StudentProfile.js` (121KB) shares this structure for later editing.

**Rewrite:** Entra signup (email verify by Entra, Google sign-in) followed by an in-app onboarding wizard for the business profile (the Entra flow only handles identity). Variant logic = one wizard with academic-status-driven steps; React Hook Form + zod; save-per-step. Lead-conversion hook moves server-side (on user-created event).

> **Customer MSAL wiring — RESOLVED (slice 13 / PR #20).** The dual-instance collision the slice-8 caveat warned about is handled: the SPA uses **per-route MSAL providers** (no shared root provider) — `StaffMsalShell` (workforce) on `/` + `/admin`, `CustomerMsalShell` (CIAM) on `/portal` — so each instance is the sole provider on its route and never contends for an auth-response hash. The customer instance has its **own `redirectUri` = `<origin>/portal`** (registered on `rplus-web-ext` by `infra/ciam/Configure-Ciam.ps1`; re-run after this slice to add it). Token acquisition is instance-scoped: `portal/customerApi.ts` uses `customerMsalInstance`, staff `api.ts` uses `msalInstance` — no issuer-blind account picking across them.

**Search MVP shipped (slice 13):** `/portal` browse (specialty/type/price/text filters → program cards) + a customer program-detail view (honorarium hidden). Booking, unlocks, the onboarding wizard, and the student dashboard are later slices.

## 4. Search → program detail → booking

- **`/programs/:slug`** (`RotationDetail.js`): header (name, prefixed program ID, specialty, hospital image, rating + review count, price/week, min weeks), description + tags, **masked preceptor identity until unlocked**, week-based DayPicker calendar (Monday weeks; disables past + `unavailable_dates` + seat-limit weeks; 1–16 week selection), consultation-hours slider for consultation type, paginated reviews, pricing box (**10% deposit for non-open programs, 100% for open; hourly for consultation_sub**), add-to-cart, favorite toggle.
- **`/favorite-rotations`**: grid of favorited programs.
- **`/cart`** (`Cart.js`/`CartWithStripe.js`): items with weeks + price, remove; **promo code** validate/apply; **student credits** auto-apply; Stripe Elements card form (number/expiry/cvc with brand detection); T&C + privacy acceptance; `createPaymentMethod` → `handleCardPayment` → `createRotationsFromCart`. **Dashboard re-entry path**: outstanding payments arrive with `location.state={from:'dashboard', ids, totalAmount}` → `completeOutstandingPayment`.

**Rewrite notes:** pricing rules (deposit %, open vs manual, consultation hourly) extracted into a tested server-side `PricingService` — today they're duplicated client/server; client only displays server-computed quotes. Stripe flow: PaymentIntent created server-side, confirmed with Stripe Elements, **fulfillment driven by webhook** (today fulfillment happens from the browser callback — silent-loss risk). Idempotency keys on checkout.

## 5. Unlocks

`/student/unlocked` (`StudentUnlocked.js`): paid unlocks reveal preceptor contact (per `/our-process` step 3: "pay a fee to view your physician's name & address"); lists unlocked preceptors (hospital image, name, program #) with "SELECT DATES" → booking. `unlock` table + payment type `unlock`. Preserve exact reveal semantics (what's masked pre-unlock: name, address; what's visible: specialty, city, rating).

## 6. Student dashboard (`/student/*`)

| Route | Component | What it does | Rewrite notes |
|---|---|---|---|
| `/student/dashboard` | `StudentDashboard.js` | **Rotations tracker** table (program ID, status [NotStarted shown as "Approved"], preceptor, docs complete?, dates, evaluation, rating, "Rate it!" action; show-more paging); incomplete-documents widget; credits widget; **outstanding payments** (rotations w/ outstanding_amount: select-many → Pay Now → cart) | status display-name mapping preserved; outstanding due-dates prominent |
| `/student/profile` | `StudentProfile.js` (121KB) | Edit everything from §3 profile | decompose into panels shared with onboarding wizard |
| `/student/documents` | `StudentDocuments.js` | Tabs **My Documents** / **Evaluations**. Per selected rotation: required doc list with statuses (**Upload_Needed / Pending "Under review" / Verification_In_Progress / Approved / Rejected (+reason, reupload) / Expired**); ~15 doc types (LOR, license, insurance, background check, SSN/tax, passport, state ID, vaccination, transcript, **hospital application form**, CV, USMLE, MCCQE, certification); **program-specific application form downloads hardcoded for 27 program IDs** in `getDocumentPath()`; **PAL documents** for IMG/visa-needing students (request + Paid/Requested states) | statuses come from Innodata OCR pipeline — keep exact status vocabulary. **Hardcoded 27-program form mapping → data**: `program_documents` table (admin-manageable, files in Blob). Doc viewer via SAS |
| `/student/help` | `StudentHelp.js` | support form → issues | keep |
| `/student/unlocked` | see §5 | | |

## 7. Rotation lifecycle (student view) & rating

- Visible statuses (display-mapped): Approved (NotStarted) / Active / Pending / Completed; hidden from tracker: Cancelled, Refund, Abandoned, Rejected.
- **`/rating?id&name&rotationId`** (`StudentRating.js`): 5 stars (labels Please Rate→Fantastic!), review textarea (required), content rules ("don't name the physician/address", moderation disclaimer), `createRotationReviewByStudent`. Feeds program rating aggregation. Re-rate allowed.
- Lifecycle notifications a student receives (preserve): onboarding email/WhatsApp drip (30-min sweep + daily multi-stage WhatsApp TEMPLATE_SID_1/2/3), document upload reminders (24h/48h, 7-day pre-due at 2h sweep), outstanding-payment due dates, rotation start/completion notices, visa-interview automation emails (daily 9am PT), review reminder (14-day).

## 8. Mobile-first requirements (this is the highest-traffic mobile surface)

Current gaps: filter row wraps awkwardly <480px; map touch targets small; dashboard/doc tables use scroll-x "responsive-row" hack; multi-selects clumsy on phones; date pickers small touch targets.

Rewrite mandates: map+list becomes **bottom-sheet pattern** on mobile (map full-bleed, swipe-up list); filters in a slide-over panel with applied-filter chips; all tables → card lists under `md`; calendar/date pickers with ≥44px touch targets; checkout single-column with sticky pay button; Playwright viewport suite (375/768/1280) on the money paths in CI.

## 9. Parity checklist (cutover gate)

Anonymous: home renders prerendered, limited results blurred, blog slugs 1:1. Auth: signup variant routing, profile wizard for all 3 academic paths. Search: same result set for 10 canonical queries (recorded from legacy), code-prefix search, every filter narrows identically. Booking: calendar blocks the same weeks for 5 sampled programs; deposit math identical (10%/open/consultation-hourly); promo + credits math identical; Stripe test checkout → rotation Pending. Documents: upload → Pending → (simulated OCR CSV) → Approved/Rejected with reason; PAL flows for an IMG test user; 27 program-form downloads resolve. Dashboard: tracker statuses, outstanding payment flow to cart and back. Rating submit + program aggregate updates.
