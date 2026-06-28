import { describe, it, expect, beforeEach, vi, afterEach } from "vitest";
import { getPublicPrograms } from "./publicApi";

describe("getPublicPrograms", () => {
  beforeEach(() => vi.restoreAllMocks());
  afterEach(() => vi.restoreAllMocks());

  it("returns the parsed program list on success", async () => {
    const data = [{ id: "1", specialtyName: "Internal Medicine" }];
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve(data) }));
    await expect(getPublicPrograms()).resolves.toEqual(data);
  });

  it("returns [] on a non-OK response (the landing must not hard-fail)", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, json: () => Promise.resolve([]) }));
    await expect(getPublicPrograms()).resolves.toEqual([]);
  });

  it("returns [] when the request throws", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("network")));
    await expect(getPublicPrograms()).resolves.toEqual([]);
  });
});
