import { describe, it, expect } from "vitest";
import {
  apiBaseUrl,
  apiScope,
  customerApiScope,
  customerLoginRequest,
  customerMsalConfig,
  loginRequest,
  msalConfig
} from "./authConfig";

describe("authConfig", () => {
  it("uses the workforce (rplus-web) defaults", () => {
    expect(msalConfig.auth.clientId).toBe("f874b196-89e2-4216-88fc-e7c92f05e6b7");
    expect(msalConfig.auth.authority).toContain("login.microsoftonline.com");
    expect(msalConfig.cache?.cacheLocation).toBe("sessionStorage");
  });

  it("requests the rplus-api delegated scope", () => {
    expect(apiScope).toContain("access_as_user");
    expect(loginRequest.scopes).toContain(apiScope);
  });

  it("has an http API base URL", () => {
    expect(apiBaseUrl).toMatch(/^http/);
  });

  it("uses the /admin authenticated console as the redirect URI", () => {
    // The redirect response is processed at /admin; processStaffRedirect (authRedirect.ts) keeps the
    // user there (navigateToLoginRequestUrl:false) instead of bouncing back to the public landing "/".
    expect(msalConfig.auth.redirectUri).toMatch(/\/admin$/);
  });
});

describe("customer (CIAM) authConfig", () => {
  it("uses the rplus-web-ext customer defaults on the CIAM authority", () => {
    expect(customerMsalConfig.auth.clientId).toBe("d3a7f715-1e7f-4c45-bd73-4de5749e1164");
    expect(customerMsalConfig.auth.authority).toContain("ciamlogin.com");
    expect(customerMsalConfig.auth.knownAuthorities).toContain(
      "f963c59e-da79-40f4-a358-1cd77e78ddd0.ciamlogin.com"
    );
  });

  it("requests the rplus-api-ext customer scope", () => {
    expect(customerApiScope).toContain("access_as_customer");
    expect(customerLoginRequest.scopes).toContain(customerApiScope);
  });

  it("is a distinct app from the staff config", () => {
    expect(customerMsalConfig.auth.clientId).not.toBe(msalConfig.auth.clientId);
    expect(customerApiScope).not.toBe(apiScope);
  });
});
