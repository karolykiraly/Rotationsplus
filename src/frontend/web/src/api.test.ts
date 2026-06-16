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

import { getMe, createSpecialty, deleteSpecialty, ApiError } from "./api";

function signedIn() {
  h.getActiveAccount.mockReturnValue({ homeAccountId: "a" });
  h.acquireTokenSilent.mockResolvedValue({ accessToken: "tok123" });
}

describe("api request layer", () => {
  beforeEach(() => {
    h.acquireTokenSilent.mockReset();
    h.getActiveAccount.mockReset();
    h.getAllAccounts.mockReset();
    h.getAllAccounts.mockReturnValue([]);
  });

  it("throws when not signed in", async () => {
    h.getActiveAccount.mockReturnValue(null);
    await expect(getMe()).rejects.toThrow("Not signed in");
  });

  it("GET sends a bearer token, no content-type, and returns the parsed body", async () => {
    signedIn();
    const body = { objectId: "oid-1", roles: ["Admin"], isStaff: true };
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 200, json: async () => body });
    vi.stubGlobal("fetch", fetchMock);

    const me = await getMe();

    expect(me).toEqual(body);
    expect(fetchMock).toHaveBeenCalledWith("http://api.test/api/me", {
      method: "GET",
      headers: { Authorization: "Bearer tok123" },
      body: undefined
    });
  });

  it("POST serializes the body and sets content-type", async () => {
    signedIn();
    const created = { id: "s1", name: "Cardiology" };
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 201, json: async () => created });
    vi.stubGlobal("fetch", fetchMock);

    const result = await createSpecialty("Cardiology");

    expect(result).toEqual(created);
    expect(fetchMock).toHaveBeenCalledWith("http://api.test/api/specialties", {
      method: "POST",
      headers: { Authorization: "Bearer tok123", "Content-Type": "application/json" },
      body: JSON.stringify({ name: "Cardiology" })
    });
  });

  it("throws an ApiError carrying the status and the server's JSON-string message", async () => {
    signedIn();
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 409,
        text: async () => JSON.stringify("A specialty named 'Cardiology' already exists.")
      })
    );

    const err = await createSpecialty("Cardiology").catch((e) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect(err.status).toBe(409);
    expect(err.message).toBe("A specialty named 'Cardiology' already exists.");
  });

  it("reads ProblemDetails.detail when the error body is an object", async () => {
    signedIn();
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        text: async () => JSON.stringify({ title: "Bad Request", detail: "Name is required." })
      })
    );

    await expect(createSpecialty("")).rejects.toThrow("Name is required.");
  });

  it("uses a plain-text error body verbatim", async () => {
    signedIn();
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({ ok: false, status: 500, text: async () => "Internal Server Error" })
    );
    await expect(createSpecialty("X")).rejects.toThrow("Internal Server Error");
  });

  it("falls back to the status when the error body is empty", async () => {
    signedIn();
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 503, text: async () => "" }));
    const err = await createSpecialty("X").catch((e) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect(err.message).toBe("Request failed (503)");
  });

  it("returns undefined for a 204 No Content (delete)", async () => {
    signedIn();
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 204 });
    vi.stubGlobal("fetch", fetchMock);

    await expect(deleteSpecialty("s1")).resolves.toBeUndefined();
    expect(fetchMock).toHaveBeenCalledWith(
      "http://api.test/api/specialties/s1",
      expect.objectContaining({ method: "DELETE" })
    );
  });
});
