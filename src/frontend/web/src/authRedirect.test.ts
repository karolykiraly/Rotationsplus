import { describe, it, expect, vi } from "vitest";
import type { IPublicClientApplication } from "@azure/msal-browser";
import { processStaffRedirect } from "./authRedirect";

function fakeInstance(handle = vi.fn().mockResolvedValue(null)) {
  return { handleRedirectPromise: handle } as unknown as IPublicClientApplication & {
    handleRedirectPromise: ReturnType<typeof vi.fn>;
  };
}

describe("processStaffRedirect", () => {
  it("processes a staff redirect on /admin with navigateToLoginRequestUrl:false", async () => {
    const instance = fakeInstance();
    await processStaffRedirect(instance, { pathname: "/admin", hash: "#code=abc" });
    expect(instance.handleRedirectPromise).toHaveBeenCalledWith({ navigateToLoginRequestUrl: false });
  });

  it("does NOT touch a customer redirect on /portal (left for CustomerMsalShell's deep-link return)", async () => {
    const instance = fakeInstance();
    await processStaffRedirect(instance, { pathname: "/portal", hash: "#code=abc" });
    expect(instance.handleRedirectPromise).not.toHaveBeenCalled();
  });

  it("does nothing on /admin without a response hash (normal navigation)", async () => {
    const instance = fakeInstance();
    await processStaffRedirect(instance, { pathname: "/admin", hash: "" });
    expect(instance.handleRedirectPromise).not.toHaveBeenCalled();
  });

  it("swallows errors so app startup never hangs on a stale hash", async () => {
    const instance = fakeInstance(vi.fn().mockRejectedValue(new Error("stale state")));
    await expect(processStaffRedirect(instance, { pathname: "/admin", hash: "#code=abc" })).resolves.toBeUndefined();
  });
});
