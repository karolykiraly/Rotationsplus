import { describe, it, expect, vi, beforeEach } from "vitest";

const h = vi.hoisted(() => ({
  acquireTokenSilent: vi.fn(),
  getActiveAccount: vi.fn(),
  getAllAccounts: vi.fn()
}));

vi.mock("./authConfig", () => ({
  apiBaseUrl: "http://api.test",
  loginRequest: { scopes: ["api://x/access_as_user"] },
  msalInstance: {
    getActiveAccount: h.getActiveAccount,
    getAllAccounts: h.getAllAccounts,
    acquireTokenSilent: h.acquireTokenSilent
  }
}));

import { getMe } from "./api";

describe("getMe", () => {
  beforeEach(() => {
    h.acquireTokenSilent.mockReset();
    h.getActiveAccount.mockReset();
    h.getAllAccounts.mockReset();
  });

  it("throws when not signed in", async () => {
    h.getActiveAccount.mockReturnValue(null);
    h.getAllAccounts.mockReturnValue([]);
    await expect(getMe()).rejects.toThrow("Not signed in");
  });

  it("sends a bearer token and returns the parsed identity", async () => {
    h.getActiveAccount.mockReturnValue({ homeAccountId: "a" });
    h.acquireTokenSilent.mockResolvedValue({ accessToken: "tok123" });
    const body = {
      objectId: "oid-1",
      name: "Jane",
      username: "jane@x",
      roles: ["Admin"],
      isStaff: true
    };
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => body });
    vi.stubGlobal("fetch", fetchMock);

    const me = await getMe();

    expect(me).toEqual(body);
    expect(fetchMock).toHaveBeenCalledWith("http://api.test/api/me", {
      headers: { Authorization: "Bearer tok123" }
    });
  });

  it("throws on a non-OK response", async () => {
    h.getActiveAccount.mockReturnValue({ homeAccountId: "a" });
    h.acquireTokenSilent.mockResolvedValue({ accessToken: "t" });
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 401 }));
    await expect(getMe()).rejects.toThrow("401");
  });
});
