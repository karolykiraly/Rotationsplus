import { describe, it, expect, beforeEach, vi } from "vitest";

const h = vi.hoisted(() => ({
  acquireTokenSilent: vi.fn(),
  getActiveAccount: vi.fn(),
  getAllAccounts: vi.fn(),
  apiFetch: vi.fn()
}));

vi.mock("../authConfig", () => ({
  customerMsalInstance: {
    getActiveAccount: h.getActiveAccount,
    getAllAccounts: h.getAllAccounts,
    acquireTokenSilent: h.acquireTokenSilent
  },
  customerLoginRequest: { scopes: ["api://x/access_as_customer"] }
}));
vi.mock("../api", () => ({ apiFetch: (m: string, p: string, t: string, b?: unknown) => h.apiFetch(m, p, t, b) }));

import { getCustomerMe, browsePrograms, getCustomerProgram } from "./customerApi";

describe("customerApi", () => {
  beforeEach(() => {
    Object.values(h).forEach((m) => m.mockReset());
    h.getAllAccounts.mockReturnValue([]);
  });

  it("throws when no customer is signed in", async () => {
    h.getActiveAccount.mockReturnValue(null);
    await expect(getCustomerMe()).rejects.toThrow("Not signed in");
  });

  it("acquires a customer token and calls the customer-me endpoint", async () => {
    h.getActiveAccount.mockReturnValue({ homeAccountId: "a" });
    h.acquireTokenSilent.mockResolvedValue({ accessToken: "ctok" });
    h.apiFetch.mockResolvedValue({ objectId: "o", roles: ["Student"], isStudent: true, isPreceptor: false });

    const me = await getCustomerMe();

    expect(me.isStudent).toBe(true);
    expect(h.acquireTokenSilent).toHaveBeenCalledWith(expect.objectContaining({ scopes: ["api://x/access_as_customer"], account: { homeAccountId: "a" } }));
    expect(h.apiFetch).toHaveBeenCalledWith("GET", "/api/customer/me", "ctok", undefined);
  });

  it("browses the catalog with the query string and the customer token", async () => {
    h.getActiveAccount.mockReturnValue({ homeAccountId: "a" });
    h.acquireTokenSilent.mockResolvedValue({ accessToken: "ctok" });
    h.apiFetch.mockResolvedValue([]);

    await browsePrograms("?q=internal&specialtyId=s1");

    expect(h.apiFetch).toHaveBeenCalledWith("GET", "/api/programs?q=internal&specialtyId=s1", "ctok", undefined);
  });

  it("fetches a single program detail", async () => {
    h.getActiveAccount.mockReturnValue({ homeAccountId: "a" });
    h.acquireTokenSilent.mockResolvedValue({ accessToken: "ctok" });
    h.apiFetch.mockResolvedValue({ id: "p1" });

    await getCustomerProgram("p1");

    expect(h.apiFetch).toHaveBeenCalledWith("GET", "/api/programs/p1", "ctok", undefined);
  });
});
