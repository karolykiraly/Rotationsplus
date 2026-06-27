import {
  type Configuration,
  type RedirectRequest,
  LogLevel,
  PublicClientApplication
} from "@azure/msal-browser";

// Defaults target the workforce (staff) tenant + rplus-web. Non-secret IDs; override via VITE_* env.
const tenantId = import.meta.env.VITE_AAD_TENANT_ID ?? "36486bcb-8a3f-4499-b0fc-9a06f510ec0e";
const clientId = import.meta.env.VITE_AAD_CLIENT_ID ?? "f874b196-89e2-4216-88fc-e7c92f05e6b7";
const authority =
  import.meta.env.VITE_AAD_AUTHORITY ?? `https://login.microsoftonline.com/${tenantId}`;
// The workforce instance redirects to /admin (NOT the site root). The root "/" is now the public,
// anonymous landing page with no MsalProvider, so a login-response hash must land on a route that
// IS inside the staff MsalProvider — /admin is that route. This also keeps the staff + customer
// instances from ever contending for the same auth-response hash (customer redirects to /portal).
// This /admin URI must be registered on the rplus-web app registration per environment.
const redirectUri =
  import.meta.env.VITE_AAD_REDIRECT_URI ?? `${window.location.origin}/admin`;
// Staff sign-out lands on the public landing page.
const postLogoutRedirectUri =
  import.meta.env.VITE_AAD_POST_LOGOUT_URI ?? `${window.location.origin}/`;

/** Delegated scope exposed by rplus-api; requested when acquiring an access token for the API. */
export const apiScope =
  import.meta.env.VITE_API_SCOPE ??
  "api://c7bd24f1-e55f-4a26-b826-6b1241a5a1bc/access_as_user";

/** Base URL of rplus-api. Local dev defaults to the API on :5099. */
export const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5099";

/** Stripe publishable key (non-secret). Empty until a Stripe sandbox is provisioned — see
 *  Docs/Vendor_Sandboxes.md. When empty the checkout runs in TEST mode (the DEV simulate path drives
 *  the fake gateway round-trip); when set, the real Stripe Elements card flow is used. */
export const stripePublishableKey = import.meta.env.VITE_STRIPE_PUBLISHABLE_KEY ?? "";

export const msalConfig: Configuration = {
  auth: {
    clientId,
    authority,
    redirectUri,
    postLogoutRedirectUri
  },
  cache: {
    cacheLocation: "sessionStorage"
  },
  system: {
    loggerOptions: {
      logLevel: LogLevel.Warning,
      /* v8 ignore next 3 -- deliberate no-op logger */
      loggerCallback: () => {
        /* no-op in P1 */
      }
    }
  }
};

export const loginRequest: RedirectRequest = { scopes: [apiScope] };

export const msalInstance = new PublicClientApplication(msalConfig);

// --- Customer (CIAM / External ID) sign-in: Student / Preceptor, rplus-web-ext app registration. ---
// Separate authority + client from the staff app above; the customer portal slices consume this.
// Non-secret IDs; override via VITE_CIAM_* env. See infra/ciam/README.md.
const ciamTenantId =
  import.meta.env.VITE_CIAM_TENANT_ID ?? "f963c59e-da79-40f4-a358-1cd77e78ddd0";
const customerClientId =
  import.meta.env.VITE_CIAM_CLIENT_ID ?? "d3a7f715-1e7f-4c45-bd73-4de5749e1164";
const customerAuthority =
  import.meta.env.VITE_CIAM_AUTHORITY ?? `https://${ciamTenantId}.ciamlogin.com/${ciamTenantId}`;

/** Delegated scope exposed by rplus-api-ext; requested when a customer acquires an API token. */
export const customerApiScope =
  import.meta.env.VITE_CIAM_API_SCOPE ??
  "api://75709454-b052-45b4-b9b4-9f3214d487c6/access_as_customer";

// The customer instance redirects to /portal (NOT the staff root) so the customer + staff MSAL
// instances never contend for the same auth-response hash — each is the sole MSAL provider on its
// own route. This URI must be registered on rplus-web-ext (Configure-Ciam.ps1 adds it).
const customerRedirectUri =
  import.meta.env.VITE_CIAM_REDIRECT_URI ?? `${window.location.origin}/portal`;

export const customerMsalConfig: Configuration = {
  auth: {
    clientId: customerClientId,
    authority: customerAuthority,
    // CIAM authorities aren't under login.microsoftonline.com, so MSAL needs them allow-listed.
    knownAuthorities: [`${ciamTenantId}.ciamlogin.com`],
    redirectUri: customerRedirectUri,
    // MSAL's navigateToLoginRequestUrl defaults to true, so a customer who signs in from a deep-linked
    // program is returned to that page (not just /portal) after auth.
    postLogoutRedirectUri: customerRedirectUri
  },
  cache: {
    cacheLocation: "sessionStorage"
  },
  system: {
    loggerOptions: {
      logLevel: LogLevel.Warning,
      /* v8 ignore next 3 -- deliberate no-op logger */
      loggerCallback: () => {
        /* no-op */
      }
    }
  }
};

export const customerLoginRequest: RedirectRequest = { scopes: [customerApiScope] };

export const customerMsalInstance = new PublicClientApplication(customerMsalConfig);
