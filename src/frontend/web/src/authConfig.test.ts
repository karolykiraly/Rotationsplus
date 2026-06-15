import { describe, it, expect } from "vitest";
import { apiBaseUrl, apiScope, loginRequest, msalConfig } from "./authConfig";

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
});
