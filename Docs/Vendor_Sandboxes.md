# Vendor TEST Sandboxes — Provisioning Guide

Step-by-step to stand up **isolated test/sandbox accounts** for the four external vendors the money-and-documents phase (P3) depends on. These are **brand-new accounts**, not the legacy production ones — zero footprint in the dashboards the outgoing team can see (stealth, `Plan_Migration §2/§5`). PROD switches to the real vendor accounts only at cutover, via a Key Vault config change — **no code change** (`Plan_Migration §6 / §12`).

**Ground rules**
- **Owner provisions** (these accounts need your email/credentials — `CLAUDE.md §1` escalation). I wire the config and write the integration once the values exist.
- **Every secret goes to Key Vault**, never code/config (`CLAUDE.md §2`). The DEV Key Vault is `kv-rplus-dev` (managed-identity access). Drop each value at the named secret below; I read them via config binding.
- **Use a dedicated project email** you control (e.g. an alias on the new domain), not your legacy vendor logins — keeps the new accounts unlinked from anything the team can see.
- Until each real value lands, DEV/tests run on the deterministic **`FakePaymentGateway`** (and equivalent fakes for email/SMS as those slices land), so nothing here blocks the build.

The DEV API base URL for webhook endpoints is `https://dev-api.rotationsplus.org`.

---

## 1. Stripe (payments) — **needed next** (checkout + webhooks)

Stripe gives you test mode inside any account, but to stay fully isolated we create a **separate account** in test mode.

1. Go to <https://dashboard.stripe.com/register>. Sign up with the project email. Business details can stay minimal — **you never activate live payments on this account**, so no bank/EIN is required for test mode.
2. Confirm the toggle top-left reads **"Test mode"** (orange). Everything below is test-mode only.
3. **API keys** → Developers → API keys:
   - Copy the **Secret key** (`sk_test_…`) → Key Vault secret **`stripe-secret-key`**.
   - Copy the **Publishable key** (`pk_test_…`) → this is non-secret; give it to me for the SPA config (it ships in the frontend).
4. **Webhook endpoint** → Developers → Webhooks → **Add endpoint**:
   - Endpoint URL: `https://dev-api.rotationsplus.org/api/webhooks/stripe`
   - Events to send (select these four families):
     - `payment_intent.succeeded`
     - `payment_intent.payment_failed`
     - `charge.refunded`
     - `charge.dispute.*` (created/closed)
   - Create, then **Reveal** the **Signing secret** (`whsec_…`) → Key Vault secret **`stripe-webhook-secret`**.
5. **Test cards** for the eventual UI: `4242 4242 4242 4242` (succeeds), `4000 0000 0000 0002` (declined), any future expiry + any CVC.

> Until step 3/4 land, the fake gateway signs webhooks with `PaymentsOptions.WebhookSecret` (dev default). When the real `whsec_…` is in KV, the real Stripe adapter validates the `t=`/`v1=` signature **with the 5-minute timestamp tolerance** (the replay-freshness check the fake intentionally omits — see `IPaymentGateway` XML doc).

**KV secrets:** `stripe-secret-key`, `stripe-webhook-secret`. **Non-secret to me:** publishable key.

---

## 2. SendGrid (email)

A separate **free** SendGrid account (100 emails/day is ample for DEV/PREPROD). Do **not** touch the legacy SendGrid domain auth — the production DNS (SPF/DKIM in Cloudflare) stays untouched until cutover (`Plan_Migration §12`).

1. Go to <https://signup.sendgrid.com/>. Sign up with the project email; free plan.
2. Complete **Single Sender Verification** (Settings → Sender Authentication → *Verify a Single Sender*) using a from-address you control. This is enough for test sending — **skip domain authentication** in the sandbox (domain auth is a cutover-day step against the real account, DNS untouched now).
3. **API key** → Settings → API Keys → **Create API Key**:
   - Name: `rplus-dev`
   - Permissions: **Restricted Access**, grant only **Mail Send → Full Access** (least privilege).
   - Copy the key (shown once, `SG.…`) → Key Vault secret **`sendgrid-api-key`**.
4. Tell me the verified **from-address** — I keep it in config so DEV emails send from an address that won't bounce.

**KV secret:** `sendgrid-api-key`. **Non-secret to me:** verified sender from-address.

---

## 3. Twilio (SMS / WhatsApp)

A new Twilio **trial** account for test credentials. We deliberately do **not** replicate the WhatsApp sender/templates in the sandbox — those carry over unchanged from the legacy account at cutover (changing them triggers Meta re-approval, `Plan_Migration §12`). The sandbox covers **SMS** + Twilio's **WhatsApp sandbox** for round-trip testing only.

1. Go to <https://www.twilio.com/try-twilio>. Sign up with the project email; verify your personal mobile (trial requirement).
2. From the **Console dashboard**, copy:
   - **Account SID** (`AC…`) → Key Vault secret **`twilio-account-sid`**.
   - **Auth Token** (reveal) → Key Vault secret **`twilio-auth-token`**.
3. **Get a trial phone number**: Phone Numbers → Manage → Buy a number (trial credit covers it; pick one SMS-capable). Tell me the number → I keep it as the test `from`.
   - Trial caveat: a trial account can only send to **verified** numbers. Add any tester mobiles under Phone Numbers → Verified Caller IDs.
4. **WhatsApp round-trip (optional, for the WhatsApp slice):** Messaging → Try it out → **WhatsApp sandbox**. Join by texting the given `join <code>` to the sandbox number. This exercises the WhatsApp send/receive path without touching the real sender/templates.
5. For inbound replies (preceptor YES/NO approvals), the messaging webhook will point at `https://dev-api.rotationsplus.org/api/webhooks/twilio` — I'll give you the exact value to paste when that slice lands. (The **inbound-webhook repoint is the known cutover trap** — `Plan_Migration §12 C5`.)

**KV secrets:** `twilio-account-sid`, `twilio-auth-token`. **Non-secret to me:** trial phone number.

---

## 4. Innodata SFTP (OCR documents) — **no self-serve sandbox**

Innodata is the **top external dependency** and has no self-service test account — access is account-manager-mediated, and per your guidance (`Plan_Migration §13 #2`) **you own that relationship**. So the sandbox here is two-track:

**Track A — stand-in SFTP for DEV/PREPROD integration (I provision, no Innodata contact):**
- I stand up a controlled SFTP endpoint (Azure Blob Storage **SFTP-enabled**, or a small containerized SFTP) that speaks the **same CSV contract** Innodata uses (filepath + rotation keys → `ocr_validations`, per `Plan_Migration` mapping). The `InnodataSftpIngestJob` is written and tested against this stand-in, so the ingest pipeline is fully exercised without the real vendor.
- Credentials for the stand-in are generated by me and stored at Key Vault **`innodata-sftp-host`**, **`innodata-sftp-username`**, **`innodata-sftp-password`** (DEV points at the stand-in; PROD points at the real Innodata — same secret names, different values, set at cutover).

**Track B — real Innodata test window (you own, scheduled ≥3 weeks before cutover):**
- Open the channel with the Innodata account manager and confirm: (a) do they **IP-allowlist** our source IP? (b) register our new **Azure egress IP** (we provision a NAT Gateway so PROD has one stable IP); (c) agree a **test window** to connect from PREPROD; (d) rotate the SFTP password (offboarding P0). Tested **from PREPROD ≥1 week before cutover** (`Plan_Migration §11 V1`).

> What I need from you for Track B is just the **kickoff** — an intro/confirmation that the relationship is live and the IP-allowlist question is answered. Track A needs nothing from you; I provision it when the Documents slice starts.

**KV secrets (same names DEV→PROD):** `innodata-sftp-host`, `innodata-sftp-username`, `innodata-sftp-password`.

---

## Summary — what I need from you

| Vendor | You do | Hand me / drop in `kv-rplus-dev` |
|---|---|---|
| **Stripe** | New account (test mode), add webhook endpoint | KV: `stripe-secret-key`, `stripe-webhook-secret`; tell me publishable key |
| **SendGrid** | New free account, verify a single sender, scoped key | KV: `sendgrid-api-key`; tell me the from-address |
| **Twilio** | New trial account, buy a trial number | KV: `twilio-account-sid`, `twilio-auth-token`; tell me the number |
| **Innodata** | Kick off the account-manager channel (Track B) | nothing for Track A; KV host/user/pass set at cutover |

**Priority order:** Stripe first (it unblocks the live checkout slice — everything else is later P3 sub-phases). SendGrid + Twilio when the Notifications slice starts. Innodata Track A when the Documents slice starts; Track B kickoff whenever you have the bandwidth (it's an elapsed-time item, so earlier is better).

Once Stripe's two secrets are in `kv-rplus-dev`, I swap the DEV registration from `FakePaymentGateway` to the real Stripe adapter and we exercise a real test-mode charge end-to-end on DEV.
