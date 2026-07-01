import type { IPublicClientApplication } from "@azure/msal-browser";

/**
 * Process a pending STAFF redirect response with `navigateToLoginRequestUrl: false` so a signed-in
 * staff user lands on the `/admin` redirect URI (the console root) rather than being navigated back to
 * whatever URL started the login. Staff start the login from the public landing's "Login" link
 * (`/rotationsplusadmin`, reached from `/`), and MSAL's default (`true`) bounced them back to `/`.
 *
 * MSAL v5 moved `navigateToLoginRequestUrl` off the global `Configuration` to a per-call option on
 * `handleRedirectPromise`, and msal-react's `MsalProvider` calls it with NO options (so it always uses
 * the default `true`). Running this pre-pass at startup consumes the redirect response first with the
 * option set; the provider's later `handleRedirectPromise()` then finds nothing to navigate.
 *
 * Guarded to the exact `/admin` redirect path AND a response hash, so it never consumes a customer
 * (`/portal`) response — that stays for `CustomerMsalShell`, which deliberately keeps the deep-link
 * return (default `true`). Errors are swallowed: a stale/duplicate hash must not block app startup.
 */
export async function processStaffRedirect(
  instance: IPublicClientApplication,
  location: { pathname: string; hash: string } = window.location
): Promise<void> {
  if (location.pathname === "/admin" && location.hash.length > 0) {
    try {
      await instance.handleRedirectPromise({ navigateToLoginRequestUrl: false });
    } catch {
      // Ignore — the provider will surface any real auth error; startup must not hang on this.
    }
  }
}
