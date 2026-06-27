import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";

const h = vi.hoisted(() => ({
  authed: false,
  loginRedirect: vi.fn().mockResolvedValue(undefined),
  getMe: vi.fn()
}));

vi.mock("@azure/msal-react", () => ({
  AuthenticatedTemplate: ({ children }: { children: ReactNode }) => (h.authed ? <>{children}</> : null),
  UnauthenticatedTemplate: ({ children }: { children: ReactNode }) => (h.authed ? null : <>{children}</>),
  useMsal: () => ({ instance: { loginRedirect: h.loginRedirect } })
}));

vi.mock("../api", () => ({ getMe: () => h.getMe() }));

import { StaffLoginLauncher, type StaffEntry } from "./StaffLoginLauncher";

function renderLauncher(entry: StaffEntry = "admin") {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={["/x"]}>
        <Routes>
          <Route path="/x" element={<StaffLoginLauncher entry={entry} />} />
          <Route path="/admin/dashboard" element={<div>DASH</div>} />
          <Route path="/admin/programs" element={<div>PROGRAMS</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("StaffLoginLauncher", () => {
  beforeEach(() => {
    h.loginRedirect.mockClear();
    h.getMe.mockReset().mockResolvedValue({ objectId: "o", roles: ["Admin"] });
    h.authed = false;
  });

  it("fires the workforce login redirect when unauthenticated", async () => {
    renderLauncher();
    expect(screen.getByText(/Redirecting to sign-in/)).toBeInTheDocument();
    await waitFor(() => expect(h.loginRedirect).toHaveBeenCalledTimes(1));
  });

  it("forwards an already-signed-in admin to the dashboard", async () => {
    h.authed = true;
    h.getMe.mockResolvedValue({ objectId: "o", roles: ["Admin"] });
    renderLauncher("admin");
    expect(await screen.findByText("DASH")).toBeInTheDocument();
    expect(h.loginRedirect).not.toHaveBeenCalled();
  });

  it("forwards an already-signed-in sales user to the program list", async () => {
    h.authed = true;
    h.getMe.mockResolvedValue({ objectId: "o", roles: ["Sales"] });
    renderLauncher("sales");
    expect(await screen.findByText("PROGRAMS")).toBeInTheDocument();
  });
});
