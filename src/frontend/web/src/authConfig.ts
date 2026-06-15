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
const redirectUri = import.meta.env.VITE_AAD_REDIRECT_URI ?? window.location.origin;

/** Delegated scope exposed by rplus-api; requested when acquiring an access token for the API. */
export const apiScope =
  import.meta.env.VITE_API_SCOPE ??
  "api://c7bd24f1-e55f-4a26-b826-6b1241a5a1bc/access_as_user";

/** Base URL of rplus-api. Local dev defaults to the API on :5099. */
export const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5099";

export const msalConfig: Configuration = {
  auth: {
    clientId,
    authority,
    redirectUri,
    postLogoutRedirectUri: redirectUri
  },
  cache: {
    cacheLocation: "sessionStorage"
  },
  system: {
    loggerOptions: {
      logLevel: LogLevel.Warning,
      loggerCallback: () => {
        /* no-op in P1 */
      }
    }
  }
};

export const loginRequest: RedirectRequest = { scopes: [apiScope] };

export const msalInstance = new PublicClientApplication(msalConfig);
