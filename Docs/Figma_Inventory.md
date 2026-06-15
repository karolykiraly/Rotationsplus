# Figma_Inventory — RotationsPlus Design File Map

**Date:** 2026-06-11 · **File:** `figma.com/design/pqMajeWlbrVBsj4AMbMXi7/RotationsPlus` (read-only PAT via `figma-developer-mcp`)
**Purpose:** index of the owner's mockups (~90% accurate to current site). **Role: functional/content reference only — NOT the visual target** (owner decision 2026-06-11: design is 6–7 years dated; the rewrite gets a fresh design system, see `Plan_Architecture.md §3.12`). Use node IDs with `get_figma_data(fileKey, nodeId)` for screen/field/state inventories and notification template content. Source-of-truth order on conflict: **legacy code/live behavior → Figma (content/flows) → docs**.

## Pages

| Page | Node | Contents |
|---|---|---|
| RotationsPlus Web Design | `0:1` | ~350 frames — desktop screens, all user types, design system (Colors `317:929`, Typography `317:1286`, Buttons `317:925`/`317:927`, Icons `317:2082`) + **150+ email/SMS/WhatsApp template mockups** |
| RotationsPlus Mobile | `1117:4120` | ~113 frames — dedicated mobile designs for public site, student, preceptor, and admin-lite flows |
| Design_temp | `359:1306` | ~150 items — working page: flows with arrows, intermediate states, user personas (`450:2849`), sales/recruiter/institution/customer-success dashboard concepts |

## Key screens by area (web page node IDs; mobile variants exist for most)

- **Public:** Landing `317:3611`/`820:4179`, Blog `317:1969` (+detail `1597:9736`), FAQ `317:1878`, Our process `317:1352`, About `317:1525`, For Preceptors `317:1634`, Consulting `4044:15094`, legal `733:3328`/`736:3398`
- **Auth:** Student/Preceptor login `317:5802`/`317:6689`, signup steps (student `317:5492→317:4874`, preceptor `317:6986→317:5184` incl. preceptor-group variant `4272:16634`), full password-reset suite, OTP/resend `3014:13910`
- **Student:** onboarding variants — IMS/IMG `322:1376`+, US pre-med `317:12474`+, Dental `8298:15400`+, DO `8447:12907`+; Dashboard `317:12776`; Search `317:9278`; Favorites `317:9643`; Rotation detail (deposit `317:9785`, no-deposit `1549:9674`, consultation `4511:17492`); Cart `317:10294` (+credit apply `4718:18242`, ACH `2782:14081`, outstanding `1494:9695`); Profile panels `317:14583`/`317:14702`/`317:14831`/`1455:9214`; Documents incl. reminder states (7d `8073:12781`, 24h `4673:17390`, 48h `5981:19470`, rejected `6624:20850`, PAL `2556:13568`); Unlocked `317:14326`; Rating set `789:3681`+
- **Preceptor:** onboarding (8+ variants per program type, Design_temp `1435:9121`+ and web `317:9213`; dental `8295:14041`+); Dashboard `317:13024`; Profile panels (personal/professional/academia/commitments `4427:*`); Documents (my docs `317:13368`, W9 upload `1716:12528`, agreement `1716:12638` — HelloSign frames now obsolete: integration removed; student docs `317:13231`); Help `317:14406`; Cancel rotation `1506:9961`
- **Admin:** Dashboard variants `317:18081`+; Programs per type `2115:*`/`3821:*` (+details); Rotations `591:3116` (+add `4735:18212`, replace `4617:17211`); Leads full CRUD `317:18668`–`317:21323` (+display `8619:12963`); Contacts `317:18840`/`2196:10922`/`6055:19888`; Permission `959:3853`+; Honorarium `317:18453`; Data/preceptor groups `4259:16474`+`7751:12995`; Customer service `2228:35060`+; Staff mgmt `2228:35537`+ (add SDR/Sales/Institution/Customer-Success `2443:*`, reset pwd `3200:13908`); member profile "Navigate To" suite `317:22534`+ (student/preceptor/sales panels incl. per-program-type tabs); Reports `6505:20370`+, Revenue `6847:12310`, Email campaigns `4905:18690`/`5252:19059`; Transactions `1027:3973`; To-dos `1494:10141`/`1988:10390`; Filters `4803:*`–`5335:*`
- **Sales/SDR/Coordinator:** Sales login `4716:18214`, Sales dashboard `6373:20174`+, Sales students `6387:21103`+ (+add `6388:21764`), Sales program detail `6313:19633`; Coordinator students `317:13473`/`6075:20691` (+add `6053:19446`); Design_temp concepts: Recruiters `3444:14644`, Institutions `3444:15763`, Customer Success `3444:15563`
- **Notification templates (150+ frames — maps to the Hangfire job inventory):** 10%-deposit automation suite (email/SMS, 48h/72h, YES/NO responses `3015:*`/`3042:*`/`3053:*`); Instant-Approval program suite `4526:*`; new-student onboarding (email/SMS/WhatsApp `5880:*`/`3685:14498`); lead follow-up 72h email+WhatsApp `7040:12273`/`7057:12430`; rotation lifecycle (7d-before `3053:13980`, 14d-after `7918:12781`, to-be-evaluated `4992:*`, done `7538:12661`); cancel/refund email sets (student/preceptor/admin `5245:*`/`7137:16513`/`7189:*`); document deadline escalations (13d `5144:18993`, 3d `7886:12798`, day-of `8081:12781`); W9 reminders `1568:11052`/`1568:11077`; visa-interview automation `5684:*`; **Methodist applications suite `5704:*`–`5827:*`**; consultation purchase notices `5976:*`; unlock-purchase notices `3746:*`; ask-for-review `7918:12495`

## New discoveries from Figma (not surfaced by code discovery — verify in code during build)

1. **"Methodist Applications" automation** — a special-program workflow (US vs international student variants, faculty supervision form). Find its implementation in cron-tasks/templates and include in the job inventory if live.
2. **"Instant Approval Program"** — named concept with its own notification suite; corresponds to `availability_type=open`/instant flags in code. Use Figma naming in the rewrite UX.
3. **Customer Success / Recruiters / Institutions dashboards** (Design_temp) — design concepts beyond current build; treat as future-feature reference, not migration scope.
4. **ACH payment option in cart** (`2782:14081`) — verify whether ACH was ever live (code showed Stripe cards only + dead Dwolla config); default: out of scope.
5. Mobile admin designs exist (admin-lite on phone) — current app never shipped them; rewrite targets tablet for staff (per Plan_Admin), mobile-admin frames are reference if we ever want more.

## Confirmed gaps (design from live site/new specs instead)

- Stripe payment element states (processing/error/3DS) — design during build
- Document OCR pipeline statuses (Verification_In_Progress etc.) — partial coverage only; status vocabulary comes from code (Plan_Student §6)
- Detailed admin analytics/report widgets — basic frames only; build from legacy report data shapes
- Impersonation UI (new feature — design fresh)
- Program-documents admin upload (new feature — design fresh)
- HelloSign frames exist but are obsolete (integration removed → in-app agreement upload flow per Plan_Architecture §3.15a)

## Usage notes for the build

- Pull individual frames on demand: `get_figma_data(fileKey: pqMajeWlbrVBsj4AMbMXi7, nodeId: <id>)`; export PNG refs with `download_figma_images` when implementing a screen.
- Design tokens: extract Colors `317:929` + Typography `317:1286` (PT Sans/Serif, Fuchsia/Iris palette, FF0057 accent) into the new design-token file as the brand baseline.
- The token (read-only) was pasted in chat/terminal — **revoke and regenerate after the project** (or sooner; reconnect takes 2 minutes).
