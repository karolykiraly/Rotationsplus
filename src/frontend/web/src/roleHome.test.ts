import { describe, it, expect } from "vitest";
import { roleHome } from "./roleHome";

describe("roleHome", () => {
  it("routes an admin to the dashboard", () => {
    expect(roleHome(["Admin"])).toBe("/admin/dashboard");
  });

  it("routes sales to the program list", () => {
    expect(roleHome(["Sales"])).toBe("/admin/programs");
  });

  it("routes SDR to the dashboard (no scoped SDR dashboard yet)", () => {
    expect(roleHome(["SDR"])).toBe("/admin/dashboard");
  });

  it("matches roles case-insensitively", () => {
    expect(roleHome(["sales"])).toBe("/admin/programs");
  });

  it("prefers admin when a user has multiple roles", () => {
    expect(roleHome(["Sales", "Admin"])).toBe("/admin/dashboard");
  });

  it("falls back to the dashboard for unknown/empty roles", () => {
    expect(roleHome([])).toBe("/admin/dashboard");
    expect(roleHome(undefined)).toBe("/admin/dashboard");
    expect(roleHome(["Coordinator"])).toBe("/admin/dashboard");
  });
});
